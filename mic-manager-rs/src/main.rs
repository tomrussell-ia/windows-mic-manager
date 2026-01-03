//! Windows Microphone Manager - Main Entry Point
//!
//! System tray utility for managing microphone devices on Windows.

// #![windows_subsystem = "windows"]  // Commented out for debugging

use anyhow::Result;
use eframe::egui;
use mic_manager_rs::audio::capture::LevelMeter;
use mic_manager_rs::audio::enumerator::{ComGuard, DeviceEnumerator};
use mic_manager_rs::audio::notifications::{create_event_channel, DeviceNotificationClient};
use mic_manager_rs::audio::policy::PolicyConfig;
use mic_manager_rs::audio::volume::VolumeController;
use mic_manager_rs::audio::DeviceRole;
use mic_manager_rs::platform::{RegistryPreferences, WindowMode};
use mic_manager_rs::ui::flyout::{FlyoutAction, FlyoutWindow};
use mic_manager_rs::ui::theme::Theme;
use mic_manager_rs::ui::{Rect, TrayEvent, TrayManager, TrayState};
use mic_manager_rs::AppState;
use std::collections::HashMap;
use std::sync::mpsc::Receiver;
use std::time::{Duration, Instant};
use tracing::{error, info, warn};

fn main() -> Result<()> {
    // Initialize logging
    tracing_subscriber::fmt()
        .with_env_filter(
            tracing_subscriber::EnvFilter::from_default_env()
                .add_directive(tracing::Level::INFO.into()),
        )
        .init();

    info!("Starting Windows Microphone Manager");

    // Initialize COM
    let _com_guard = ComGuard::new().map_err(|e| anyhow::anyhow!("COM init failed: {}", e))?;
    info!("COM initialized");

    // Create device enumerator
    let enumerator =
        DeviceEnumerator::new().map_err(|e| anyhow::anyhow!("Enumerator failed: {}", e))?;
    info!("Device enumerator created");

    // Create event channel for device notifications
    let (event_sender, event_receiver) = create_event_channel();

    // Register notification client
    let notification_client = DeviceNotificationClient::new(event_sender);
    if let Err(e) = notification_client.register(enumerator.raw_enumerator()) {
        warn!("Failed to register notification client: {}", e);
    } else {
        info!("Notification client registered");
    }

    // Create policy config for setting default device
    let policy_config = PolicyConfig::new().ok();
    if policy_config.is_some() {
        info!("Policy config created");
    } else {
        warn!("Failed to create policy config - setting default device will not work");
    }

    // Initialize app state
    let mut app_state = AppState::new();
    if let Err(e) = app_state.initialize(&enumerator) {
        error!("Failed to initialize app state: {}", e);
    }
    info!(
        "App state initialized with {} devices",
        app_state.devices.len()
    );

    // Create volume controllers for each device
    let mut volume_controllers: HashMap<String, VolumeController> = HashMap::new();
    let mut level_meters: HashMap<String, LevelMeter> = HashMap::new();

    for device in &app_state.devices {
        // Get the raw IMMDevice to create controllers
        if let Ok(imm_device) = unsafe {
            enumerator
                .raw_enumerator()
                .GetDevice(windows::core::PCWSTR::from_raw(
                    device
                        .id
                        .encode_utf16()
                        .chain(std::iter::once(0))
                        .collect::<Vec<_>>()
                        .as_ptr(),
                ))
        } {
            if let Ok(vc) = VolumeController::new(&imm_device) {
                volume_controllers.insert(device.id.clone(), vc);
            }
            if let Ok(lm) = LevelMeter::new(&imm_device) {
                level_meters.insert(device.id.clone(), lm);
            }
        }
    }

    // Update initial mute/volume state
    for device in &mut app_state.devices {
        if let Some(vc) = volume_controllers.get(&device.id) {
            if let Ok(muted) = vc.get_mute() {
                device.is_muted = muted;
            }
            if let Ok(volume) = vc.get_volume() {
                device.volume_level = volume;
            }
        }
    }

    // Create tray manager
    let mut tray_manager = TrayManager::new();

    // Create tray icon
    let initial_state = TrayState {
        tooltip: app_state.get_tooltip(),
        muted: app_state.is_default_muted(),
    };
    tray_manager
        .create(initial_state)
        .map_err(|e| anyhow::anyhow!("Tray creation failed: {}", e))?;

    // Update startup menu checkmark
    let prefs = RegistryPreferences::new();
    if let Ok(enabled) = prefs.is_startup_enabled() {
        let _ = tray_manager.set_startup_checked(enabled);
        app_state.preferences.start_with_windows = enabled;
    }

    info!("Tray icon created");

    // Create eframe options
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([380.0, 500.0])
            .with_min_inner_size([350.0, 300.0])
            .with_decorations(true)
            .with_transparent(false)
            .with_always_on_top()
            .with_close_button(false), // Prevent accidental close - use tray menu to exit
        ..Default::default()
    };

    // Run eframe application
    eframe::run_native(
        "Microphone Manager",
        options,
        Box::new(move |cc| {
            // Apply theme
            Theme::dark().apply(&cc.egui_ctx);

            Ok(Box::new(MicManagerApp {
                app_state,
                tray_manager,
                flyout: FlyoutWindow::new(),
                enumerator,
                volume_controllers,
                level_meters,
                policy_config,
                event_receiver,
                last_icon_rect: Rect::default(),
                last_level_poll: Instant::now(),
                flyout_shown_at: None,
                focus_lost_at: None,
                initial_minimize_done: false,
            }))
        }),
    )
    .map_err(|e| anyhow::anyhow!("eframe error: {}", e))?;

    info!("Application exiting");
    Ok(())
}

/// Main application struct for eframe.
struct MicManagerApp {
    app_state: AppState,
    tray_manager: TrayManager,
    flyout: FlyoutWindow,
    enumerator: DeviceEnumerator,
    volume_controllers: HashMap<String, VolumeController>,
    level_meters: HashMap<String, LevelMeter>,
    policy_config: Option<PolicyConfig>,
    event_receiver: Receiver<mic_manager_rs::audio::DeviceEvent>,
    last_icon_rect: Rect,
    last_level_poll: Instant,
    /// Time when flyout was last shown (for focus grace period)
    flyout_shown_at: Option<Instant>,
    /// Time when focus was lost (for sustained focus loss detection)
    focus_lost_at: Option<Instant>,
    /// Whether we've done the initial minimize
    initial_minimize_done: bool,
}

impl eframe::App for MicManagerApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        // Minimize window on first frame to start hidden
        if !self.initial_minimize_done {
            self.initial_minimize_done = true;
            ctx.send_viewport_cmd(egui::ViewportCommand::Minimized(true));
            info!("Initial minimize done");
        }

        // Process tray events
        self.tray_manager.process_events();

        // Handle tray events
        while let Ok(event) = self.tray_manager.events().try_recv() {
            info!("Tray event received: {:?}", event);
            match &event {
                TrayEvent::LeftClick { rect, .. } => {
                    info!("Tray left click received, flyout_visible={}", self.app_state.flyout_visible);
                    self.last_icon_rect = *rect;
                    self.app_state.toggle_flyout();
                    info!("After toggle, flyout_visible={}", self.app_state.flyout_visible);

                    // Show/hide window using Minimized instead of Visible
                    // Visible(false) can destroy the window, Minimized just hides it
                    if self.app_state.flyout_visible {
                        info!("Showing window");
                        self.flyout_shown_at = Some(Instant::now());
                        self.focus_lost_at = None;
                        ctx.send_viewport_cmd(egui::ViewportCommand::Minimized(false));
                        ctx.send_viewport_cmd(egui::ViewportCommand::Focus);
                    } else {
                        info!("Hiding window");
                        self.flyout_shown_at = None;
                        self.focus_lost_at = None;
                        ctx.send_viewport_cmd(egui::ViewportCommand::Minimized(true));
                    }
                }
                _ => {
                    self.app_state
                        .handle_tray_event(event, &mut self.tray_manager);
                }
            }
        }

        // Handle device events
        while let Ok(event) = self.event_receiver.try_recv() {
            self.app_state
                .handle_device_event(event, &self.enumerator, &mut self.tray_manager);

            // Refresh volume controllers if devices changed
            self.refresh_controllers();
        }

        // Poll level meters (20Hz)
        if self.last_level_poll.elapsed() >= Duration::from_millis(50) {
            self.poll_levels();
            self.last_level_poll = Instant::now();

            // Decay peak holds
            self.app_state.decay_peak_holds(0.02);
        }

        // Check if should exit
        if self.app_state.should_exit {
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
            return;
        }

        // Always render the flyout UI - visibility is controlled via viewport commands
        self.flyout.show(ctx, &self.app_state);

        // Handle flyout actions
        for action in self.flyout.take_actions() {
            self.handle_flyout_action(action);
        }

        // Check for focus loss (flyout mode only - close when clicking outside)
        // Use grace period to avoid immediate close when window first appears
        // Also require sustained focus loss to avoid hiding during window operations
        if self.app_state.flyout_visible && self.app_state.window_mode == WindowMode::Flyout {
            let grace_period_elapsed = self
                .flyout_shown_at
                .map(|t| t.elapsed() > Duration::from_millis(500))
                .unwrap_or(true);

            let has_focus = ctx.input(|i| i.focused);

            if grace_period_elapsed {
                if has_focus {
                    // Window has focus, reset focus lost tracker
                    self.focus_lost_at = None;
                } else {
                    // Window lost focus - track when it happened
                    if self.focus_lost_at.is_none() {
                        info!("Focus lost - starting timer");
                        self.focus_lost_at = Some(Instant::now());
                    }

                    // Only hide if focus has been lost for more than 200ms
                    // This prevents hiding during brief focus interruptions
                    if let Some(lost_at) = self.focus_lost_at {
                        if lost_at.elapsed() > Duration::from_millis(200) {
                            info!("Hiding flyout due to sustained focus loss");
                            self.flyout_shown_at = None;
                            self.focus_lost_at = None;
                            self.app_state.hide_flyout();
                            ctx.send_viewport_cmd(egui::ViewportCommand::Minimized(true));
                        }
                    }
                }
            }
        }

        // Request repaint for level meters
        if self.app_state.flyout_visible {
            ctx.request_repaint_after(Duration::from_millis(50));
        } else {
            ctx.request_repaint_after(Duration::from_millis(100));
        }
    }
}

impl MicManagerApp {
    /// Refresh volume controllers after device changes.
    fn refresh_controllers(&mut self) {
        // Add controllers for new devices
        for device in &self.app_state.devices {
            if !self.volume_controllers.contains_key(&device.id) {
                if let Ok(imm_device) = unsafe {
                    self.enumerator
                        .raw_enumerator()
                        .GetDevice(windows::core::PCWSTR::from_raw(
                            device
                                .id
                                .encode_utf16()
                                .chain(std::iter::once(0))
                                .collect::<Vec<_>>()
                                .as_ptr(),
                        ))
                } {
                    if let Ok(vc) = VolumeController::new(&imm_device) {
                        self.volume_controllers.insert(device.id.clone(), vc);
                    }
                    if let Ok(lm) = LevelMeter::new(&imm_device) {
                        self.level_meters.insert(device.id.clone(), lm);
                    }
                }
            }
        }

        // Remove controllers for removed devices
        let device_ids: std::collections::HashSet<_> =
            self.app_state.devices.iter().map(|d| &d.id).collect();
        self.volume_controllers
            .retain(|id, _| device_ids.contains(id));
        self.level_meters.retain(|id, _| device_ids.contains(id));
    }

    /// Poll level meters and volume state for all devices.
    fn poll_levels(&mut self) {
        let mut tray_update_needed = false;
        let default_id = self.app_state.default_device_id.clone();

        for device in &mut self.app_state.devices {
            // Poll level meter
            if let Some(meter) = self.level_meters.get(&device.id) {
                if let Ok(level) = meter.get_peak_level() {
                    device.input_level = level;
                    if level > device.peak_hold {
                        device.peak_hold = level;
                    }
                }
            }

            // Poll volume and mute state (for external changes)
            if let Some(vc) = self.volume_controllers.get(&device.id) {
                if let Ok(volume) = vc.get_volume() {
                    if (device.volume_level - volume).abs() > 0.01 {
                        device.volume_level = volume;
                    }
                }
                if let Ok(muted) = vc.get_mute() {
                    if device.is_muted != muted {
                        device.is_muted = muted;
                        // Check if this is the default device
                        if Some(&device.id) == default_id.as_ref() {
                            tray_update_needed = true;
                        }
                    }
                }
            }
        }

        // Update tray icon if default device mute state changed
        if tray_update_needed {
            let _ = self
                .tray_manager
                .set_icon(self.app_state.is_default_muted());
            let _ = self.tray_manager.set_tooltip(&self.app_state.get_tooltip());
        }
    }

    /// Handle actions from the flyout UI.
    fn handle_flyout_action(&mut self, action: FlyoutAction) {
        match action {
            FlyoutAction::ToggleMute(device_id) => {
                if let Some(vc) = self.volume_controllers.get(&device_id) {
                    if let Ok(new_state) = vc.toggle_mute() {
                        self.app_state.update_device_mute(&device_id, new_state);

                        // Update tray if this is the default device
                        if Some(&device_id) == self.app_state.default_device_id.as_ref() {
                            let _ = self.tray_manager.set_icon(new_state);
                            let _ = self.tray_manager.set_tooltip(&self.app_state.get_tooltip());
                        }
                    }
                }
            }
            FlyoutAction::SetVolume(device_id, volume) => {
                if let Some(vc) = self.volume_controllers.get(&device_id) {
                    if vc.set_volume(volume).is_ok() {
                        self.app_state.update_device_volume(&device_id, volume);
                    }
                }
            }
            FlyoutAction::SetDefaultConsole(device_id) => {
                if let Some(ref policy) = self.policy_config {
                    if policy
                        .set_default_device(&device_id, DeviceRole::Console)
                        .is_ok()
                    {
                        // State will be updated via notification callback
                        info!("Set {} as default Console device", device_id);
                    }
                }
            }
            FlyoutAction::SetDefaultCommunications(device_id) => {
                if let Some(ref policy) = self.policy_config {
                    if policy
                        .set_default_device(&device_id, DeviceRole::Communications)
                        .is_ok()
                    {
                        info!("Set {} as default Communications device", device_id);
                    }
                }
            }
            FlyoutAction::SetDefaultBoth(device_id) => {
                if let Some(ref policy) = self.policy_config {
                    if policy.set_default_device_all_roles(&device_id).is_ok() {
                        info!("Set {} as default for all roles", device_id);
                    }
                }
            }
            FlyoutAction::ToggleDock => {
                self.app_state.window_mode = match self.app_state.window_mode {
                    WindowMode::Flyout => WindowMode::Docked,
                    WindowMode::Docked => WindowMode::Flyout,
                };
                self.app_state.preferences.window_mode = self.app_state.window_mode;

                // Save preference
                let prefs = RegistryPreferences::new();
                let _ = prefs.save(&self.app_state.preferences);
            }
            FlyoutAction::Close => {
                self.app_state.hide_flyout();
            }
        }
    }
}

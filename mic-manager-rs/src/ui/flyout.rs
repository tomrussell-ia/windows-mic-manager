//! Flyout window implementation using eframe/egui.
//!
//! Displays the list of microphone devices with controls.

use crate::app::AppState;
use crate::audio::MicrophoneDevice;
use crate::platform::WindowMode;
use crate::ui::Rect;
use eframe::egui::{self, SliderClamping};

/// Actions that can be triggered from the flyout UI.
#[derive(Debug, Clone)]
pub enum FlyoutAction {
    /// Set a device as the default for Console role
    SetDefaultConsole(String),
    /// Set a device as the default for Communications role
    SetDefaultCommunications(String),
    /// Set a device as default for both roles
    SetDefaultBoth(String),
    /// Toggle mute state for a device
    ToggleMute(String),
    /// Set volume for a device
    SetVolume(String, f32),
    /// Toggle dock/undock mode
    ToggleDock,
    /// Close the flyout
    Close,
}

/// Flyout window state.
pub struct FlyoutWindow {
    /// Pending actions from the UI
    pub actions: Vec<FlyoutAction>,
}

impl FlyoutWindow {
    /// Create a new FlyoutWindow.
    pub fn new() -> Self {
        Self {
            actions: Vec::new(),
        }
    }

    /// Render the flyout window content.
    pub fn show(&mut self, ctx: &egui::Context, app_state: &AppState) {
        self.actions.clear();

        egui::CentralPanel::default().show(ctx, |ui| {
            // Header with title and dock button
            ui.horizontal(|ui| {
                ui.heading("Microphones");

                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let dock_text = match app_state.window_mode {
                        WindowMode::Flyout => "📌",
                        WindowMode::Docked => "📍",
                    };
                    let dock_tooltip = match app_state.window_mode {
                        WindowMode::Flyout => "Dock window",
                        WindowMode::Docked => "Undock window",
                    };

                    if ui.button(dock_text).on_hover_text(dock_tooltip).clicked() {
                        self.actions.push(FlyoutAction::ToggleDock);
                    }
                });
            });

            ui.separator();

            // Error message if any
            if let Some(ref error) = app_state.error_message {
                ui.horizontal(|ui| {
                    ui.colored_label(egui::Color32::RED, format!("Error: {}", error));
                });
                ui.separator();
            }

            // Device list or empty state
            if app_state.devices.is_empty() {
                ui.vertical_centered(|ui| {
                    ui.add_space(20.0);
                    ui.label("No microphones detected");
                    ui.add_space(20.0);
                });
            } else {
                egui::ScrollArea::vertical()
                    .auto_shrink([false, false])
                    .show(ui, |ui| {
                        for device in &app_state.devices {
                            self.render_device_row(ui, device);
                            ui.add_space(8.0);
                        }
                    });
            }
        });
    }

    /// Render a single device row.
    fn render_device_row(&mut self, ui: &mut egui::Ui, device: &MicrophoneDevice) {
        let device_id = device.id.clone();

        egui::Frame::none()
            .fill(ui.style().visuals.widgets.noninteractive.bg_fill)
            .rounding(4.0)
            .inner_margin(8.0)
            .show(ui, |ui| {
                ui.vertical(|ui| {
                    // First row: Name and default indicators
                    ui.horizontal(|ui| {
                        // Default indicators
                        if device.is_default {
                            ui.colored_label(egui::Color32::GREEN, "●");
                            ui.label("Console");
                        }
                        if device.is_default_communication {
                            ui.colored_label(egui::Color32::BLUE, "●");
                            ui.label("Comms");
                        }

                        ui.strong(&device.name);

                        // Format tag
                        if let Some(ref format) = device.audio_format {
                            ui.with_layout(
                                egui::Layout::right_to_left(egui::Align::Center),
                                |ui| {
                                    ui.small(format.to_string());
                                },
                            );
                        }
                    });

                    ui.add_space(4.0);

                    // Second row: Mute button, volume slider, level meter
                    ui.horizontal(|ui| {
                        // Mute button
                        let mute_text = if device.is_muted { "🔇" } else { "🎤" };
                        if ui.button(mute_text).clicked() {
                            self.actions
                                .push(FlyoutAction::ToggleMute(device_id.clone()));
                        }

                        // Volume slider
                        let mut volume = device.volume_level;
                        let slider = egui::Slider::new(&mut volume, 0.0..=1.0)
                            .show_value(false)
                            .clamping(SliderClamping::Always);

                        let response = ui.add(slider);
                        if response.changed() {
                            self.actions
                                .push(FlyoutAction::SetVolume(device_id.clone(), volume));
                        }

                        // Volume percentage
                        ui.label(format!("{}%", device.volume_percent()));

                        // Level meter (simple bar)
                        let level_width = 100.0;
                        let level_height = 16.0;
                        let (rect, _response) = ui.allocate_exact_size(
                            egui::vec2(level_width, level_height),
                            egui::Sense::hover(),
                        );

                        if ui.is_rect_visible(rect) {
                            let painter = ui.painter();

                            // Background
                            painter.rect_filled(rect, 2.0, egui::Color32::DARK_GRAY);

                            // Level bar
                            let level_rect = egui::Rect::from_min_size(
                                rect.min,
                                egui::vec2(level_width * device.input_level, level_height),
                            );
                            let level_color = if device.input_level > 0.9 {
                                egui::Color32::RED
                            } else if device.input_level > 0.7 {
                                egui::Color32::YELLOW
                            } else {
                                egui::Color32::GREEN
                            };
                            painter.rect_filled(level_rect, 2.0, level_color);

                            // Peak hold indicator
                            if device.peak_hold > 0.0 {
                                let peak_x = rect.min.x + level_width * device.peak_hold;
                                painter.vline(
                                    peak_x,
                                    rect.y_range(),
                                    egui::Stroke::new(2.0, egui::Color32::WHITE),
                                );
                            }
                        }
                    });

                    ui.add_space(4.0);

                    // Third row: Set as default buttons
                    ui.horizontal(|ui| {
                        if !device.is_default {
                            if ui.small_button("Set Console Default").clicked() {
                                self.actions
                                    .push(FlyoutAction::SetDefaultConsole(device_id.clone()));
                            }
                        }
                        if !device.is_default_communication {
                            if ui.small_button("Set Comms Default").clicked() {
                                self.actions.push(FlyoutAction::SetDefaultCommunications(
                                    device_id.clone(),
                                ));
                            }
                        }
                        if !device.is_default || !device.is_default_communication {
                            if ui.small_button("Set Both").clicked() {
                                self.actions
                                    .push(FlyoutAction::SetDefaultBoth(device_id.clone()));
                            }
                        }
                    });
                });
            });
    }

    /// Take all pending actions.
    pub fn take_actions(&mut self) -> Vec<FlyoutAction> {
        std::mem::take(&mut self.actions)
    }

    /// Calculate the window position based on the tray icon rect.
    pub fn calculate_position(icon_rect: &Rect, window_size: (f32, f32)) -> (f32, f32) {
        // Position above the tray icon, centered horizontally
        let x = (icon_rect.left + icon_rect.right) as f32 / 2.0 - window_size.0 / 2.0;
        let y = icon_rect.top as f32 - window_size.1 - 10.0;

        (x.max(0.0), y.max(0.0))
    }
}

impl Default for FlyoutWindow {
    fn default() -> Self {
        Self::new()
    }
}

//! Device row UI component.
//!
//! Renders a single microphone device with all its controls.

use crate::audio::MicrophoneDevice;
use eframe::egui::{self, SliderClamping};

/// Actions that can be triggered from a device row.
#[derive(Debug, Clone)]
pub enum DeviceRowAction {
    /// Toggle mute state
    ToggleMute,
    /// Set volume level
    SetVolume(f32),
    /// Set as default Console device
    SetDefaultConsole,
    /// Set as default Communications device
    SetDefaultCommunications,
    /// Set as default for both roles
    SetDefaultBoth,
}

/// Device row component.
pub struct DeviceRow;

impl DeviceRow {
    /// Render a device row and return any actions triggered.
    pub fn show(ui: &mut egui::Ui, device: &MicrophoneDevice) -> Option<DeviceRowAction> {
        let mut action = None;

        egui::Frame::none()
            .fill(ui.style().visuals.widgets.noninteractive.bg_fill)
            .rounding(4.0)
            .inner_margin(8.0)
            .show(ui, |ui| {
                ui.vertical(|ui| {
                    // Header row
                    ui.horizontal(|ui| {
                        // Default indicators
                        if device.is_default {
                            ui.colored_label(egui::Color32::GREEN, "●");
                        }
                        if device.is_default_communication {
                            ui.colored_label(egui::Color32::LIGHT_BLUE, "●");
                        }

                        ui.strong(&device.name);

                        // Format tag
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            if let Some(ref format) = device.audio_format {
                                ui.small(format.to_string());
                            }
                        });
                    });

                    ui.add_space(4.0);

                    // Controls row
                    ui.horizontal(|ui| {
                        // Mute button
                        let mute_text = if device.is_muted { "🔇" } else { "🎤" };
                        if ui.button(mute_text).clicked() {
                            action = Some(DeviceRowAction::ToggleMute);
                        }

                        // Volume slider
                        let mut volume = device.volume_level;
                        let response = ui.add(
                            egui::Slider::new(&mut volume, 0.0..=1.0)
                                .show_value(false)
                                .clamping(SliderClamping::Always),
                        );
                        if response.changed() {
                            action = Some(DeviceRowAction::SetVolume(volume));
                        }

                        // Volume percentage
                        ui.label(format!("{}%", device.volume_percent()));
                    });

                    ui.add_space(4.0);

                    // Default buttons row
                    ui.horizontal(|ui| {
                        if !device.is_default && ui.small_button("Set Console").clicked() {
                            action = Some(DeviceRowAction::SetDefaultConsole);
                        }
                        if !device.is_default_communication
                            && ui.small_button("Set Comms").clicked()
                        {
                            action = Some(DeviceRowAction::SetDefaultCommunications);
                        }
                        if (!device.is_default || !device.is_default_communication)
                            && ui.small_button("Set Both").clicked()
                        {
                            action = Some(DeviceRowAction::SetDefaultBoth);
                        }
                    });
                });
            });

        action
    }
}

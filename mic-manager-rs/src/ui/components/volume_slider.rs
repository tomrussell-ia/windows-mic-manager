//! Volume slider UI component.
//!
//! Renders a volume slider with percentage display.

use eframe::egui::{self, SliderClamping};

/// Volume slider component.
pub struct VolumeSlider;

impl VolumeSlider {
    /// Render a volume slider. Returns the new value if changed.
    pub fn show(ui: &mut egui::Ui, volume: f32, enabled: bool) -> Option<f32> {
        let mut value = volume;
        let mut changed = false;

        ui.add_enabled_ui(enabled, |ui| {
            ui.horizontal(|ui| {
                let response = ui.add(
                    egui::Slider::new(&mut value, 0.0..=1.0)
                        .show_value(false)
                        .clamping(SliderClamping::Always),
                );

                if response.changed() {
                    changed = true;
                }

                // Volume percentage
                ui.label(format!("{}%", (value * 100.0).round() as u8));
            });
        });

        if changed {
            Some(value)
        } else {
            None
        }
    }

    /// Render a compact volume slider.
    pub fn show_compact(ui: &mut egui::Ui, volume: f32) -> Option<f32> {
        let mut value = volume;

        let response = ui.add(
            egui::Slider::new(&mut value, 0.0..=1.0)
                .show_value(false)
                .clamping(SliderClamping::Always),
        );

        if response.changed() {
            Some(value)
        } else {
            None
        }
    }
}

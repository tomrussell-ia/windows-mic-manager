//! Level meter UI component.
//!
//! Renders an audio level meter with peak hold indicator.

use eframe::egui;

/// Level meter component.
pub struct LevelMeter;

impl LevelMeter {
    /// Render a level meter.
    ///
    /// - `level`: Current input level (0.0 to 1.0)
    /// - `peak`: Peak hold level (0.0 to 1.0)
    pub fn show(ui: &mut egui::Ui, level: f32, peak: f32, width: f32, height: f32) {
        let (rect, _response) =
            ui.allocate_exact_size(egui::vec2(width, height), egui::Sense::hover());

        if ui.is_rect_visible(rect) {
            let painter = ui.painter();

            // Background
            painter.rect_filled(rect, 2.0, egui::Color32::from_gray(40));

            // Calculate colors based on level
            let level_color = Self::get_level_color(level);

            // Level bar
            let level_width = width * level.clamp(0.0, 1.0);
            let level_rect = egui::Rect::from_min_size(rect.min, egui::vec2(level_width, height));
            painter.rect_filled(level_rect, 2.0, level_color);

            // Peak hold indicator
            if peak > 0.01 {
                let peak_x = rect.min.x + width * peak.clamp(0.0, 1.0);
                painter.vline(
                    peak_x,
                    rect.y_range(),
                    egui::Stroke::new(2.0, egui::Color32::WHITE),
                );
            }

            // dB scale markers (optional)
            Self::draw_db_markers(painter, rect, width);
        }
    }

    /// Render a compact level meter (no dB markers).
    pub fn show_compact(ui: &mut egui::Ui, level: f32, peak: f32) {
        let width = 80.0;
        let height = 12.0;

        let (rect, _response) =
            ui.allocate_exact_size(egui::vec2(width, height), egui::Sense::hover());

        if ui.is_rect_visible(rect) {
            let painter = ui.painter();

            // Background
            painter.rect_filled(rect, 2.0, egui::Color32::from_gray(40));

            // Level bar
            let level_color = Self::get_level_color(level);
            let level_width = width * level.clamp(0.0, 1.0);
            let level_rect = egui::Rect::from_min_size(rect.min, egui::vec2(level_width, height));
            painter.rect_filled(level_rect, 2.0, level_color);

            // Peak hold
            if peak > 0.01 {
                let peak_x = rect.min.x + width * peak.clamp(0.0, 1.0);
                painter.vline(
                    peak_x,
                    rect.y_range(),
                    egui::Stroke::new(1.0, egui::Color32::WHITE),
                );
            }
        }
    }

    /// Get the color for a level value.
    fn get_level_color(level: f32) -> egui::Color32 {
        if level > 0.9 {
            egui::Color32::from_rgb(239, 68, 68) // Red - clipping
        } else if level > 0.7 {
            egui::Color32::from_rgb(245, 158, 11) // Yellow - high
        } else {
            egui::Color32::from_rgb(16, 185, 129) // Green - normal
        }
    }

    /// Draw dB scale markers.
    fn draw_db_markers(painter: &egui::Painter, rect: egui::Rect, width: f32) {
        // dB values to mark: -60, -40, -20, -10, -6, -3, 0
        let db_marks = [-60.0_f32, -40.0, -20.0, -10.0, -6.0, -3.0, 0.0];

        for db in &db_marks {
            // Convert dB to linear position (0-60dB range mapped to 0-1)
            let linear = (db + 60.0) / 60.0;
            let x = rect.min.x + width * linear;

            // Draw tick mark
            painter.vline(
                x,
                egui::Rangef::new(rect.max.y - 3.0, rect.max.y),
                egui::Stroke::new(1.0, egui::Color32::from_gray(80)),
            );
        }
    }
}

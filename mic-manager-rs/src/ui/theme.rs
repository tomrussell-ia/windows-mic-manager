//! Windows 11 styling and theme colors.
//!
//! Provides Windows 11 visual styling for the UI.

use eframe::egui;

/// Windows 11 theme colors.
pub struct Theme {
    /// Background color
    pub background: egui::Color32,

    /// Surface color (cards, panels)
    pub surface: egui::Color32,

    /// Primary accent color
    pub accent: egui::Color32,

    /// Text primary color
    pub text_primary: egui::Color32,

    /// Text secondary color
    pub text_secondary: egui::Color32,

    /// Success/active color (green)
    pub success: egui::Color32,

    /// Warning color (yellow)
    pub warning: egui::Color32,

    /// Error/muted color (red)
    pub error: egui::Color32,

    /// Border color
    pub border: egui::Color32,
}

impl Theme {
    /// Create a dark theme (Windows 11 dark mode).
    pub fn dark() -> Self {
        Self {
            background: egui::Color32::from_rgb(32, 32, 32),
            surface: egui::Color32::from_rgb(45, 45, 45),
            accent: egui::Color32::from_rgb(0, 120, 212),
            text_primary: egui::Color32::from_rgb(255, 255, 255),
            text_secondary: egui::Color32::from_rgb(180, 180, 180),
            success: egui::Color32::from_rgb(16, 185, 129),
            warning: egui::Color32::from_rgb(245, 158, 11),
            error: egui::Color32::from_rgb(239, 68, 68),
            border: egui::Color32::from_rgb(60, 60, 60),
        }
    }

    /// Create a light theme (Windows 11 light mode).
    pub fn light() -> Self {
        Self {
            background: egui::Color32::from_rgb(243, 243, 243),
            surface: egui::Color32::from_rgb(255, 255, 255),
            accent: egui::Color32::from_rgb(0, 120, 212),
            text_primary: egui::Color32::from_rgb(0, 0, 0),
            text_secondary: egui::Color32::from_rgb(96, 96, 96),
            success: egui::Color32::from_rgb(16, 185, 129),
            warning: egui::Color32::from_rgb(245, 158, 11),
            error: egui::Color32::from_rgb(239, 68, 68),
            border: egui::Color32::from_rgb(220, 220, 220),
        }
    }

    /// Apply the theme to an egui context.
    pub fn apply(&self, ctx: &egui::Context) {
        let mut style = (*ctx.style()).clone();

        // Panel colors
        style.visuals.panel_fill = self.background;
        style.visuals.window_fill = self.surface;

        // Widget colors
        style.visuals.widgets.noninteractive.bg_fill = self.surface;
        style.visuals.widgets.inactive.bg_fill = self.surface;
        style.visuals.widgets.hovered.bg_fill = self.border;
        style.visuals.widgets.active.bg_fill = self.accent;

        // Text colors
        style.visuals.widgets.noninteractive.fg_stroke.color = self.text_primary;
        style.visuals.widgets.inactive.fg_stroke.color = self.text_secondary;
        style.visuals.widgets.hovered.fg_stroke.color = self.text_primary;
        style.visuals.widgets.active.fg_stroke.color = self.text_primary;

        // Selection color
        style.visuals.selection.bg_fill = self.accent;
        style.visuals.selection.stroke.color = self.text_primary;

        // Hyperlink color
        style.visuals.hyperlink_color = self.accent;

        // Window rounding
        style.visuals.window_rounding = egui::Rounding::same(8.0);

        ctx.set_style(style);
    }
}

impl Default for Theme {
    fn default() -> Self {
        Self::dark()
    }
}

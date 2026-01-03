//! Icon generation and loading utilities.
//!
//! Provides functions to generate or load icons for the system tray.

use tray_icon::Icon;

/// Icon size in pixels.
pub const ICON_SIZE: u32 = 32;

/// Generate an unmuted microphone icon.
pub fn create_unmuted_icon() -> Result<Icon, String> {
    let rgba = generate_microphone_icon(false);
    Icon::from_rgba(rgba, ICON_SIZE, ICON_SIZE).map_err(|e| e.to_string())
}

/// Generate a muted microphone icon.
pub fn create_muted_icon() -> Result<Icon, String> {
    let rgba = generate_microphone_icon(true);
    Icon::from_rgba(rgba, ICON_SIZE, ICON_SIZE).map_err(|e| e.to_string())
}

/// Generate a microphone icon as RGBA data.
fn generate_microphone_icon(muted: bool) -> Vec<u8> {
    let size = ICON_SIZE as usize;
    let mut rgba = vec![0u8; size * size * 4];

    let center = size as f32 / 2.0;
    let radius = size as f32 / 2.0 - 3.0;

    // Colors
    let (r, g, b) = if muted {
        (220u8, 60u8, 60u8) // Red for muted
    } else {
        (60u8, 180u8, 60u8) // Green for unmuted
    };

    // Draw filled circle
    for y in 0..size {
        for x in 0..size {
            let idx = (y * size + x) * 4;
            let dx = x as f32 - center;
            let dy = y as f32 - center;
            let dist = (dx * dx + dy * dy).sqrt();

            if dist < radius {
                // Inside the circle
                rgba[idx] = r;
                rgba[idx + 1] = g;
                rgba[idx + 2] = b;
                rgba[idx + 3] = 255;
            } else if dist < radius + 1.0 {
                // Anti-aliased edge
                let alpha = ((radius + 1.0 - dist) * 255.0) as u8;
                rgba[idx] = r;
                rgba[idx + 1] = g;
                rgba[idx + 2] = b;
                rgba[idx + 3] = alpha;
            }
        }
    }

    // Draw microphone shape (simplified)
    draw_microphone_shape(&mut rgba, size, !muted);

    // Draw strike-through line if muted
    if muted {
        draw_strike_through(&mut rgba, size);
    }

    rgba
}

/// Draw a simplified microphone shape.
fn draw_microphone_shape(rgba: &mut [u8], size: usize, white: bool) {
    let color = if white { 255u8 } else { 40u8 };
    let center_x = size / 2;

    // Microphone body (vertical rectangle in center)
    let body_width = size / 4;
    let body_height = size / 2;
    let body_top = size / 4;

    for y in body_top..(body_top + body_height) {
        for x in (center_x - body_width / 2)..(center_x + body_width / 2) {
            if x < size && y < size {
                let idx = (y * size + x) * 4;
                if rgba[idx + 3] > 0 {
                    rgba[idx] = color;
                    rgba[idx + 1] = color;
                    rgba[idx + 2] = color;
                }
            }
        }
    }

    // Microphone stand (small line at bottom)
    let stand_y = body_top + body_height;
    if stand_y + 2 < size {
        for y in stand_y..(stand_y + 3) {
            let idx = (y * size + center_x) * 4;
            if rgba[idx + 3] > 0 {
                rgba[idx] = color;
                rgba[idx + 1] = color;
                rgba[idx + 2] = color;
            }
        }
    }
}

/// Draw a diagonal strike-through line.
fn draw_strike_through(rgba: &mut [u8], size: usize) {
    let thickness = 2;

    for i in 4..(size - 4) {
        for t in 0..thickness {
            let x = i;
            let y = i + t;

            if x < size && y < size {
                let idx = (y * size + x) * 4;
                rgba[idx] = 255;
                rgba[idx + 1] = 255;
                rgba[idx + 2] = 255;
                rgba[idx + 3] = 255;
            }
        }
    }
}

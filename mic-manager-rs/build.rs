//! Build script for Windows Microphone Manager
//!
//! Embeds the Windows application manifest for DPI awareness and
//! links required Windows libraries.

fn main() {
    // Only run on Windows
    if std::env::var("CARGO_CFG_TARGET_OS").unwrap() != "windows" {
        return;
    }

    // Embed Windows manifest for DPI awareness via .rc file
    embed_resource::compile("resources/app.rc", embed_resource::NONE);

    // Link Windows libraries
    println!("cargo:rustc-link-lib=ole32");
    println!("cargo:rustc-link-lib=user32");
    println!("cargo:rustc-link-lib=shell32");
    println!("cargo:rustc-link-lib=advapi32");

    // Re-run if resources change
    println!("cargo:rerun-if-changed=resources/app.rc");
    println!("cargo:rerun-if-changed=resources/app.manifest");
}

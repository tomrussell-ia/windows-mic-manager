//! Platform-specific module for Windows utilities.
//!
//! This module contains Windows-specific functionality including
//! registry access and icon management.

pub mod icons;
pub mod registry;

pub use registry::{PreferencesError, RegistryPreferences, UserPreferences, WindowMode};

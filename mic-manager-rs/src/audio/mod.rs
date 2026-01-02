//! Audio module for Windows Core Audio API interactions.
//!
//! This module provides access to microphone enumeration, volume control,
//! level metering, and device notifications.

pub mod capture;
pub mod device;
pub mod enumerator;
pub mod notifications;
pub mod policy;
pub mod volume;

pub use device::{AudioError, AudioFormat, DeviceEvent, DeviceRole, DeviceState, MicrophoneDevice};
pub use enumerator::DeviceEnumerator;

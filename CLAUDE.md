# Claude AI Instructions

For comprehensive project documentation, architecture patterns, and coding guidelines, see [AGENT.md](AGENT.md) in the repository root.

## Quick Reference (Auto-generated from spec-kit)

**Last updated**: 2026-01-01

### Active Technologies
- C# (.NET 8, WPF) - Current implementation in `MicrophoneManager/`
- Rust 1.75+ (stable) - Parallel rebuild tracked in `specs/001-rust-mic-manager/`

### Common Commands
```powershell
# C# Development
dotnet build MicrophoneManager/MicrophoneManager.csproj
dotnet publish MicrophoneManager/MicrophoneManager.csproj -p:PublishProfile=win-x64-singlefile

# Rust Development
cargo test; cargo clippy

# Rust FFI Library (for C# integration)
cd mic-engine-ffi
cargo build --release

# Full Build (Rust FFI + C# App)
.\scripts\build-all.ps1
.\scripts\build-rust-ffi.ps1  # Just the Rust library
```

---

**For detailed instructions, see [AGENT.md](AGENT.md)**

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

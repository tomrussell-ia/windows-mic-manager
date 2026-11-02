<!-- OPENSPEC:START -->
# OpenSpec Instructions

These instructions are for AI assistants working in this project.

Always open `@/openspec/AGENTS.md` when the request:
- Mentions planning or proposals (words like proposal, spec, change, plan)
- Introduces new capabilities, breaking changes, architecture shifts, or big performance/security work
- Sounds ambiguous and you need the authoritative spec before coding

Use `@/openspec/AGENTS.md` to learn:
- How to create and apply change proposals
- Spec format and conventions
- Project structure and guidelines

Keep this managed block so 'openspec update' can refresh the instructions.

<!-- OPENSPEC:END -->

# Claude AI Instructions

For comprehensive project documentation, architecture patterns, and coding guidelines, see [AGENTS.md](AGENTS.md) in the repository root.

## Quick Reference

**Last updated**: 2026-01-01

### Active Technologies
- C# (.NET 10, WinUI 3) - Implementation in `MicrophoneManager/`

### Common Commands
```powershell
# C# Development
dotnet build MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -p:Platform=x64
dotnet publish MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -c Release -p:PublishProfile=win-x64-folder
# Do NOT use win-x64-singlefile: it hides the WinUI .pri resources and the app crashes at startup.
```

---

**For detailed instructions, see [AGENTS.md](AGENTS.md)**

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

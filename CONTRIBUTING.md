# Contributing to Windows Microphone Manager

Thank you for your interest in contributing to this project! 

## Development Tools

This project uses AI coding assistants to accelerate development:
- **GitHub Copilot** - Code completion and inline suggestions
- **Claude** (Anthropic) - Architecture design, refactoring, and implementation

Contributors are welcome to use similar AI tools in their workflow. However, all contributions must be:
- **Reviewed and understood** by the contributor - You should be able to explain any code you submit
- **Properly tested** - Include or update unit tests as appropriate
- **Compliant with project standards** - Follow existing code patterns and conventions
- **Free of licensing issues** - Ensure AI-generated code doesn't reproduce copyrighted material

## Specification Management with OpenSpec

This project uses [OpenSpec](https://github.com/InfraredAces/openspec) for specification-driven development and AI-assisted feature planning.

### When to Create a Specification

**Create a change proposal for:**
- New features or capabilities
- Breaking changes (API, architecture)
- Significant refactoring
- Performance or security improvements that change behavior

**Skip proposals for:**
- Bug fixes (restoring intended behavior)
- Typos, formatting, comments
- Non-breaking dependency updates
- Configuration changes

### Quick Workflow

1. **Explore current state**: `openspec list` and `openspec list --specs`
2. **Create proposal**: Scaffold files in `openspec/changes/[change-id]/`
   - `proposal.md` - Why and what changes
   - `tasks.md` - Implementation checklist
   - `specs/[capability]/spec.md` - Requirement deltas (ADDED/MODIFIED/REMOVED)
3. **Validate**: `openspec validate [change-id] --strict --no-interactive`
4. **Get approval**: Wait for proposal review before implementing
5. **Implement**: Follow the tasks.md checklist
6. **Archive**: After deployment, run `openspec archive [change-id]`

See [openspec/AGENTS.md](openspec/AGENTS.md) for detailed instructions.

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Windows SDK 10.0.19041.0 or later
- Visual Studio 2022 or VS Code with C# extensions

### Building the Project

```bash
dotnet build MicrophoneManager.WinUI/MicrophoneManager.WinUI.csproj -p:Platform=x64
```

### Running Tests

```bash
dotnet test MicrophoneManager.Tests/MicrophoneManager.Tests.csproj -p:Platform=x64
```

## Code Standards

- **MVVM Pattern** - Use ViewModels with `CommunityToolkit.Mvvm` source generators
- **Service Layer** - Business logic belongs in services, not ViewModels
- **Async/Await** - Use async patterns for I/O operations
- **XML Documentation** - Document public APIs with XML comments
- **Unit Tests** - Add tests for new features using xUnit

## Submitting Changes

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes with clear messages
4. Push to your fork (`git push origin feature/amazing-feature`)
5. Open a Pull Request with:
   - Description of changes
   - Test results
   - Screenshots (if UI changes)

## Questions?

Open an issue for:
- Bug reports
- Feature requests
- Architecture questions
- Contribution guidance

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

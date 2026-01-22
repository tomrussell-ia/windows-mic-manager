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

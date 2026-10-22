# Toolchain

## `.` — C# / Godot 4 .NET

- SDK: .NET SDK `10.0.402`, pinned in `global.json` (`rollForward: latestFeature`). Projects target
  `net8.0`.
- Engine: Godot 4.7.2 .NET build (`godot-mono`; macOS: `brew install --cask godot-mono`). The SDK
  version in `OpenSettlers.csproj` (`Godot.NET.Sdk/4.7.2`) must match the installed editor.
- Game files: a Die Siedler install the user owns, passed as `--game <dir>` or the `SIEDLER_DIR`
  environment variable. The repository ships no game content.
- Commands:
  - Tests — see `docs/agents/verification.md`
  - Restore the formatter — `dotnet tool restore` (needs NuGet access the first time)
  - Format — `dotnet csharpier format .`
  - Check formatting — `dotnet csharpier check .`. The full verification command runs it first.
  - Build — `dotnet build OpenSettlers.csproj`
  - Unit tests — `dotnet test --project tests/OpenSettlers.Tests` (MSTest on Microsoft.Testing.Platform,
    selected in `global.json`)
- Formatter: CSharpier `1.3.0`, a local dotnet tool pinned in `dotnet-tools.json`;
  `.csharpierrc.json` sets `printWidth: 120` and leaves every other option at its default. CSharpier
  reads `.gitignore`, so `.godot/`, `bin/`, `obj/`, and `.superdev/` are skipped.
  `// csharpier-ignore` keeps a hand-laid-out fixture, such as a pixel grid, as written.
- Not wired: no linter or `.editorconfig`.
- Style prose: none beyond `README.md`.

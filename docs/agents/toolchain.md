# Toolchain

## `.` — C# / Godot 4 .NET

- Tools: `mise.toml` pins every development tool, and `mise install` installs them on macOS, Linux, and
  Windows. Run each command through its mise task (`mise run <task>`; `mise tasks` lists them), so the
  pinned tools and `SIEDLER_DIR` are on hand. Nothing comes from Homebrew.
- SDK: .NET SDK `10.0.402`, pinned in `global.json` (`rollForward: latestFeature`) and `mise.toml`.
  Projects target `net8.0`, so `mise.toml` also installs the .NET 8 SDK for its runtime.
- Engine: Godot 4.7.2 .NET build, from the official release through mise. The SDK version in
  `OpenSettlers.csproj` (`Godot.NET.Sdk/4.7.2`) must match it. The executable name differs per platform;
  the `GODOT` variable in `mise.toml` holds it, and the tasks call `{{ env.GODOT }}`.
- Game files: a History Edition install the user owns (the folder that holds `Siedler1.exe`), passed as
  `--game <dir>` or the `SIEDLER_DIR` environment variable. The tasks read `SIEDLER_DIR` from the
  gitignored `mise.local.toml`. The repository ships no game content.
- Commands:
  - Tests — see `docs/agents/verification.md`
  - Restore the formatter — `mise run restore` (needs NuGet access the first time)
  - Format — `mise run format`
  - Check formatting — part of `mise run check`
  - Build — `mise run build`
  - Unit tests — `mise run test` (MSTest on Microsoft.Testing.Platform, selected in `global.json`)
  - Asset check, asset browser, Godot editor — `mise run assets`, `mise run browser`, `mise run editor`
  - Ghidra dump — the `ghidra:*` tasks run the scripts in `tools/GhidraDump/`, which build a local Ghidra
    project of `Siedler1.exe` and write a searchable dump into `re-local/`. See `tools/GhidraDump/README.md`.
  - Program index — `mise run index`
- Line endings: `.gitattributes` keeps every text file LF on every platform.
- Formatter: CSharpier `1.3.0`, a local dotnet tool pinned in `dotnet-tools.json`;
  `.csharpierrc.json` sets `printWidth: 120` and leaves every other option at its default. CSharpier
  reads `.gitignore`, so `.godot/`, `bin/`, `obj/`, and `.superdev/` are skipped.
  `// csharpier-ignore` keeps a hand-laid-out fixture, such as a pixel grid, as written.
- Not wired: no linter or `.editorconfig`.
- Style prose: `docs/agents/CODING.md` and `docs/agents/CODING_PATTERNS.md`.

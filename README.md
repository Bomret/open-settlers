# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993), built on Godot 4 with C#. It ships no game content: it reads the files of a Die Siedler install the player owns.

Status: scaffold. The repository holds the Godot project, the test project, and the project documentation. The engine has no runnable mode yet.

## Requirements

- Godot 4.7 .NET build (`brew install --cask godot-mono`)
- .NET SDK 10 (pinned in `global.json`; the project targets `net8.0`)
- An install of The Settlers History Edition, as described below. Rule recovery from the game's program needs
  it as well.

## Get the History Edition install

The reference install is The Settlers History Edition (Ubisoft). Ubisoft Connect installs it and runs only on Windows. On macOS or Linux:

1. Install The Settlers History Edition through Ubisoft Connect on a Windows computer or in a Windows virtual machine.
2. Copy the game folder unchanged to your computer. The game folder holds `Siedler1.exe` and the folders `loca/` and `snd/`.
3. Pass that folder as `--game`, or set `SIEDLER_DIR` to it.

The engine does not support an install of the DOS CD release.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/roadmap.md`: the milestone plan.
- `docs/rules/`: the rules recovered from the game's program, each with its address as evidence.
- `docs/agents/`: instructions for coding agents.

## Build and test

```sh
dotnet tool restore
dotnet csharpier check .
dotnet test --project tests/OpenSettlers.Tests
dotnet build OpenSettlers.csproj
```

# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993), built on Godot 4 with C#. It ships no game content: it reads the files of a Die Siedler install the player owns.

Status: scaffold. The repository holds the Godot project, the test project, and the project documentation. The engine has no runnable mode yet.

## Requirements

- Godot 4.7 .NET build (`brew install --cask godot-mono`)
- .NET SDK 10 (pinned in `global.json`; the project targets `net8.0`)
- An installed copy of Die Siedler, as described below

## Install the game from the CD

The CD release ships the game packed: its own DOS `INSTALL.EXE` unpacks it into an install folder. DOSBox Staging runs that installer on macOS or Linux.

```sh
brew install dosbox-staging   # or your package manager on Linux
mkdir -p "/path/to/C"
dosbox -c 'mount c "/path/to/C"' -c 'imgmount d "/path/to/Die Siedler.cue" -t cdrom' -c 'd:' -c 'cd \SIEDLER' -c 'INSTALL'
```

In the installer, choose `Installieren`, accept the proposed folder with Return, and confirm with `J`. The install lands in `/path/to/C/BLUEBYTE/SIEDLER`. Pass that folder as `--game`, or set `SIEDLER_DIR` to it.

A mixed-mode CD image (data track and audio track) needs the `.cue` file, not an ISO: an ISO holds only the data track.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/agents/`: instructions for coding agents.

## Build and test

```sh
dotnet tool restore
dotnet csharpier check .
dotnet test --project tests/OpenSettlers.Tests
dotnet build OpenSettlers.csproj
```

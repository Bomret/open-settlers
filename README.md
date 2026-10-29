# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993), built on Godot 4 with C#. It ships no game content: it reads the files of a Die Siedler install the player owns.

Status: milestone M1. The engine reads the data files of an install. It has two modes, the asset check and the asset browser.

## Requirements

- Godot 4.7 .NET build (`brew install --cask godot-mono`)
- .NET SDK 10 (pinned in `global.json`; the project targets `net8.0`)
- An install of The Settlers History Edition, as described below. Rule recovery from the game's program needs
  it as well.

## Get the History Edition install

The reference install is The Settlers History Edition (Ubisoft). Ubisoft Connect installs it and runs only on Windows. On macOS or Linux:

1. Install The Settlers History Edition through Ubisoft Connect on a Windows computer or in a Windows virtual machine.
2. Copy the game folder unchanged to your computer. The game folder holds `Siedler1.exe` and the folders `loca/` and `snd/`. The engine finds these names in any letter case, so a copy whose names an archiver changed to lower or upper case also works.
3. Pass that folder as `--game`, or set `SIEDLER_DIR` to it.

The engine does not support an install of the DOS CD release.

## Run the asset check and the asset browser

Both modes need an install. Give it with `--game <install>` after the `--` that ends the Godot arguments, or set `SIEDLER_DIR` to the install folder. `--game` wins when both are present. The commands use `godot-mono`, the binary of the Homebrew cask.

Build the C# project once after a clone and again after each change to the code:

```sh
dotnet build OpenSettlers.csproj
```

Without the build Godot cannot load the C# scripts: the asset check prints `Cannot instantiate C# script` and does not exit, and the browser shows an empty window.

The asset check runs without a window:

```sh
godot-mono --headless --path . -- --check --game <install>
```

It reads every asset of every kind and prints one line per file with its entry count and kinds. Each failure prints a line with the file, the entry, and the reason. The last line gives the totals. The exit code is 0 when every entry was read. It is 1 when an entry cannot be read (a file that cannot be read or opened, or corrupt data), when a listed file is missing, or when the check crashes on an error it does not catch; the message `asset check crashed` then goes to stderr. It is 2 when there is no usable install (no `--game` value, a missing folder, or a folder without `Siedler1.exe`). It is 3 when two asset kinds share a name.

The asset browser opens a window:

```sh
godot-mono --path . -- --game <install>
```

It lists the asset kinds, the assets of the selected kind, and a preview of the selected asset. It shows a message instead when there is no usable install or two asset kinds share a name, and a notice when the selected kind lists no assets.

Neither mode writes to the install.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `src/`: the engine code. `Assets/` holds the asset kinds and the asset check, `Formats/` the file readers, `Launch/` the command-line arguments, and `Viewer/` the asset browser and its previews.
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/roadmap.md`: the milestone plan.
- `docs/rules/`: the rules recovered from the game's program, each with its address as evidence.
- `docs/agents/`: instructions for coding agents.
- `tools/GhidraDump/`: a Ghidra script that writes a local, searchable dump of the game's program.

## Build and test

```sh
dotnet tool restore
dotnet csharpier check .
dotnet test --project tests/OpenSettlers.Tests
dotnet build OpenSettlers.csproj
```

# Open Settlers

An open-source engine for Die Siedler (Blue Byte, 1993), built on Godot 4 with C#. It ships no game content: it reads the files of a Die Siedler install the player owns.

Status: milestone M2. The engine reads the data files of an install and generates the map of a seed. It has three modes, the asset check, the asset browser, and the map view.

## Requirements

- [mise](https://mise.jdx.dev). It installs the pinned tools listed in `mise.toml`: the .NET SDKs 10 and 8, the Godot 4.7.2 .NET build, and, for rule recovery, a JDK, Gradle, Python, and Ghidra. Install mise on macOS or Linux with `curl https://mise.run | sh`, or on Windows with `winget install jdx.mise`.
- Windows: the Microsoft Visual C++ Redistributable, which mise needs (`winget install Microsoft.VCRedist.2015+.x64`, or `.arm64` on ARM).
- Linux: the ICU library, which .NET loads (Debian and Ubuntu: the `libicu` package).
- A History Edition install (see below). Rule recovery from the game's program needs it too.

## Set up

```sh
mise install
```

Then create `mise.local.toml`, a gitignored file per checkout, with the path of your install:

```toml
[env]
SIEDLER_DIR = "/path/to/thesettlers"
```

Run every command below from the repository root. `mise tasks` lists the tasks.

## Get the History Edition install

The reference install is The Settlers History Edition (Ubisoft). Ubisoft Connect installs it and runs only on Windows. On Windows, use the install folder that Ubisoft Connect made. On macOS or Linux:

1. Install The Settlers History Edition through Ubisoft Connect on a Windows computer or in a Windows virtual machine.
2. Copy the install folder unchanged to your computer. The install folder holds `Siedler1.exe` and the folders `loca/` and `snd/`. The engine finds these names in any letter case, so a copy whose names an archiver changed to lower or upper case also works.
3. Set `SIEDLER_DIR` to that folder in `mise.local.toml`.

The engine does not support an install of the DOS CD release.

## Run the asset check, the asset browser, and the map view

All three modes need an install. The tasks take it from `SIEDLER_DIR`. Each task builds the C# project first, because without the build Godot cannot load the C# scripts: the asset check prints `Cannot instantiate C# script` and does not exit, and the browser shows an empty window.

The asset check runs without a window:

```sh
mise run assets
```

It reads every asset of every kind and prints one line per file with its entry count and kinds. Each failure prints a line with the file, the entry, and the reason. The last line gives the totals. Exit codes:

- 0: every entry was read.
- 1: an entry cannot be read (a file that cannot be read or opened, or corrupt data), a listed file is missing, or the check crashed on an error it does not catch (`asset check crashed` goes to stderr).
- 2: no usable install (no `--game` value or `SIEDLER_DIR`, a missing folder, or a folder without `Siedler1.exe`).
- 3: two asset kinds share a name.
- 4: a bad argument, in every mode: a flag without its value (`--game` too), an unknown flag after `--`, a bad `--seed` or `--size`, two different mode flags, `--seed-info` without `--seed`, or `--seed` or `--size` with `--check` or the browser. The message names the problem, no window opens, and the install is not opened, so 4 wins over 2.

The asset browser opens a window:

```sh
mise run browser
```

It lists the asset kinds, the assets of the selected kind, and a preview of the selected asset. Its text and lists scale with the window. It shows a message instead when there is no usable install or two asset kinds share a name, and a notice when the selected kind lists no assets.

The map view opens a window with a seed field (16 digits from 1 to 8, as the original's screen shows it) and a size picker above the terrain of the generated map:

```sh
mise run map
```

`--seed <seed>` and `--size <1|2|3>` open the view on that map (`-- --map --seed 1234567812345678 --size 2`): the seed field shows the seed, the size picker selects the size, and the next Apply keeps both. The seed takes the 16-digit form or four groups in one quoted argument (`--seed "1234 5678 1234 5678"`); unquoted groups are a bad seed. Without `--seed` the view opens on its default seed, without `--size` on size 1.

A valid seed, applied with Enter, or a new size regenerates and redraws the map. A new size keeps the last applied seed. An invalid seed shows its reason and keeps the last map. The arrow keys and a mouse drag move the view, which wraps at the map's edges. A two-finger touchpad swipe also moves the view. The mouse wheel and a touchpad pinch zoom in and out around the pointer, and the `+` and `-` keys zoom around the middle of the view. The zoom goes from 1/4 to 4 times, and a view never shows more than two maps across. Without an install it prints the asset check's message and exits with its code 2. A missing or unreadable terrain graphic shows an error in the view. Give one mode flag only: with both `--check` and `--map`, the launch reports that they are different modes and exits with code 4.

Seed info prints the digests and counts of the map of one seed and size. It is headless and needs no install and no `SIEDLER_DIR`:

```sh
mise run seed-info -- --seed 1234567812345678 --size 3
```

`--seed` is required (the same forms as the map view takes); `--size <1|2|3>` defaults to 1. The report is the same bytes on every run, in this order:

- `Seed:` and `Size:`, as given.
- `Layer digests`: the FNV-1a 64-bit digest of each map layer in `MapLayer` order (Flags, Height, Terrain, Lake, Object, Resource), 16 hex digits. The Resource line says it shares the Lake layer's bytes, so the two digests match.
- `Terrain tiles`: `<type>: <count>` for each terrain type present, ascending, counting both triangles of each tile, so the counts sum to 2 × width × height.
- `Objects`: `<code>: <count>` for each object code present, ascending.
- `Deposits`: `<type>: <n> tiles, amount <total>` for each resource type present, ascending. The type 1 total equals the generator's `Resource1Total`.

Exit codes: 0 on a printed report, 4 for a bad argument or a missing `--seed`, 1 with `ERROR no map for this seed: ...` on stderr when the seed's map cannot be generated (no land mass covers a quarter of the map).

A programming error in the browser does not close it. When the browser fails while it selects a kind or an asset, or while it draws a preview, it shows `<file or kind>: crashed: <Type>: <message>` in the preview pane and keeps the lists usable. A crash while the window opens shows that text in place of the window. Each crash also goes to stderr as `asset browser crashed`. A crash is a bug in the engine. A corrupt file is shown as a failure of that file, not as a crash.

No mode writes to the install.

To run Godot by hand, use `mise exec -- <godot>`, where `<godot>` is the name in the `GODOT` variable of `mise.toml` for your platform. Give the install with `--game <install>` after the `--` that ends the Godot arguments, or set `SIEDLER_DIR`. `--game` wins when both are present. `mise run editor` opens the project in the Godot editor.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `mise.toml`: the pinned development tools and the tasks.
- `src/`: the engine code. `Assets/` holds the asset kinds and the asset check, `Formats/` the file readers, `Launch/` the command-line arguments, `Simulation/` the random generator, the seed, and the map generator, `MapView/` the terrain and object drawing plans and the camera, and `Viewer/` the asset browser, the map view, and their shaders.
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/roadmap.md`: the milestone plan.
- `docs/rules/`: the rules recovered from the game's program, each with its address as evidence.
- `docs/agents/`: instructions for coding agents.
- `tools/GhidraDump/`: Ghidra scripts that build a local Ghidra project and write a searchable dump of the game's program, and the emulator harness that runs the original's map-start code for reference values.
- `tools/WindowsCheck/`: the procedure that confirms the emulator's values by running the original in Windows.

## Build and test

The full verification (`docs/agents/verification.md`) checks formatting, runs the unit tests, and builds the project:

```sh
mise run check
```

`mise run test` runs the unit tests alone, and `mise run format` formats the code.

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

The map view opens a window with a seed field and a size picker above the terrain of the generated map:

```sh
mise run map
```

`--seed <seed>` and `--size <1|2|3>` open the view on that map (`-- --map --seed 1234567812345678 --size 2`): the seed field shows the seed, the size picker selects the size, and the next Apply keeps both. The seed takes the 16-digit form or four groups in one quoted argument (`--seed "1234 5678 1234 5678"`); unquoted groups are a bad seed. Without `--seed` the view opens on its default seed, without `--size` on size 1.

[Controls](docs/controls.md) describes how to use the map view and the game view. Without an install it prints the asset check's message and exits with its code 2. A missing or unreadable terrain graphic shows an error in the view. An unreadable `Siedler1.exe` shows an error in the view and draws no map: the map generation reads which objects are large from it. Give one mode flag only: with both `--check` and `--map`, the launch reports that they are different modes and exits with code 4.

The game view is the map view with a game on the shown map that ticks at the original's normal speed:

```sh
mise exec -- <godot> --path . -- --game-view [--seed <seed>] [--size <1|2|3>]
```

It takes `--seed` and `--size` as the map view does, and it has the map view's exit codes.

Seed info prints the digests and counts of the map of one seed and size. It is headless and needs the install (`--game` or `SIEDLER_DIR`): the map generation reads which objects are large from its `Siedler1.exe`.

```sh
mise run seed-info -- --seed 1234567812345678 --size 3
```

`--seed` is required (the same forms as the map view takes); `--size <1|2|3>` defaults to 1. The report is the same bytes on every run, in this order:

- `Seed:` and `Size:`, as given.
- `Layer digests`: the FNV-1a 64-bit digest of each map layer in `MapLayer` order (Flags, Height, Terrain, Lake, Object, Resource), 16 hex digits. The Resource line says it shares the Lake layer's bytes, so the two digests match.
- `Terrain tiles`: `<type>: <count>` for each terrain type present, ascending, counting both triangles of each tile, so the counts sum to 2 × width × height.
- `Objects`: `<code>: <count>` for each object code present, ascending.
- `Deposits`: `<type>: <n> tiles, amount <total>` for each resource type present, ascending. The type 1 total equals the generator's `Resource1Total`.

Exit codes: 0 on a printed report, 4 for a bad argument or a missing `--seed`, 2 with `ERROR` and the problem on stderr and no map when the install is missing or its `Siedler1.exe` cannot be read, 1 with `ERROR no map for this seed: ...` on stderr when the seed's map cannot be generated (no land mass covers a quarter of the map).

### Run a play script

A play run opens the map view or the game view, runs the steps of a play script in file order on simulated time, and quits after the last step. It runs under Godot's Movie Maker, which writes a PNG per frame and one WAV and forces a fixed time step. Create the output folder first: Movie Maker writes into it from the first frame, and the run deletes nothing there.

```sh
rm -rf play-out/zoom-pan && mkdir -p play-out/zoom-pan
mise exec -- <godot> --path . --write-movie play-out/zoom-pan/frame.png --fixed-fps 30 -- --map [--seed <seed>] [--size <n>] --play play/zoom-pan.play
```

Give `--game-view` in place of `--map` to run the script on the game view.

`sh tools/play-run.sh <stem> [--seed <s>] [--size <n>] --fps <n> [--fresh]` (or `mise run play -- <stem> ...`) does the same on the map view from `play/<stem>.play` with `SIEDLER_DIR` as `--game`: it clears `play-out/<stem>/`, builds `OpenSettlers.csproj` (a failed build exits 3 and runs no Godot), stores the result under a hash of the run's inputs, reuses it while they are unchanged, and logs each call to `play-out/runs.log`. `docs/agents/verification.md` describes it.

`--play <file>` needs `--map` or `--game-view`, and Movie Maker (`--write-movie` with `--fixed-fps`); without them, or without a file, it is a bad argument and exits with code 4. The script path resolves against the project folder. `play-out/<script name>/` holds the frames and every `shot` and `dump` of the run (a result `tools/play-run.sh` reuses holds no frames; `--fresh` runs again and writes them); the script name is the script's file name without its extension. Git ignores `play-out/`. The run pins the window to 1280 x 720, so frames and the view's fit do not depend on the display. Two runs of the same script, seed, and size give equal dumps and frames. `play/zoom-pan.play` is an example that zooms and pans.

A play script has one step per line. Blank lines and lines that start with `#` are skipped. A problem in the script (an unknown step, a bad argument, an unreadable file) or a failed `expect` prints one `ERROR` that names the script and the line and exits with code 1; a failed `expect` states the expectation and the actual value. After the last step the run exits with code 0. Steps:

- `wait <seconds>` lets that much simulated time pass.
- `press <key>` presses and releases a key. A key is named as Godot names it (`Left`, `Plus`, `Escape`, `A`); a chord names its modifiers first, joined with `+` (`Shift+Equal`).
- `hold <key> <seconds>` presses a key, lets the time pass as a `wait` does, and releases it. A held arrow key moves the view for as long as it stays down.
- `scroll <up|down> [count [x y]]` turns the mouse wheel by `count` notches (default 1) at the canvas point `x y` (default the middle of the canvas).
- `drag <x1> <y1> <x2> <y2>` drags with the left button between two canvas points.
- `pinch <factor>` and `swipe <dx> <dy>` send a touchpad pinch and a two-finger swipe at the middle of the canvas.
- `click-tile <column> <row>` clicks the map tile where it shows on the canvas at that moment, as a player's click on it does. Of the tile's wrapped copies on the canvas it takes the one nearest the canvas's middle. A tile outside the map, not on the canvas, under the button bar, or drawn on the same point as another tile fails the run; the view does not move.
- `button <name>` clicks the shown game-view button whose label is exactly `name` (`Flag`, `Road`, `Woodcutter`, `Remove`); an on/off button flips. A name not shown, and every name in the map view, fails the run and lists the shown names.
- `shot <name>` writes the last drawn frame to `play-out/<script name>/<name>.png`. A step takes effect in the frame after it, so put a `wait` before the shot.
- `shot <name> <column> <row> [radius] [scale]` writes only the part of that frame around a map tile. The tile is placed where `click-tile` aims, and a tile `click-tile` cannot aim at fails the step with its message and writes no file. The crop holds every tile within `radius` steps (default 1, 0 or more) and room above them for the tallest object sprite at the current zoom, so a tree or building on the tile shows whole. A crop reaching past the map canvas is clipped to it. `scale` (default 1, a whole number of 1 or more) enlarges the crop with nearest sampling; a crop over 16384 pixels on a side once enlarged fails the step. `play/cropped-shot.play` is an example.
- `dump <name>` writes `play-out/<script name>/<name>.txt`: one `field=value` line each for `camera-x` and `camera-y` (the camera's wrapped position in whole pixels), `zoom` (four decimals), `seed`, and `size`. In the game view `ticks` (the game's tick count) and `logs` (the castle's log count, 0 before a castle stands) follow, then one `flag-goods:<column>,<row>` line for each flag that holds goods (the count of goods on it), by row and then column.
- `expect <field> <op> <value>` checks one of those fields. `flag-goods:<column>,<row>` names any node and is 0 where no goods lie on a flag there. An `expect` on `ticks`, `logs`, or a `flag-goods` field in the map view fails. The operators are `<`, `<=`, `=`, `>=`, and `>`; `seed` takes only `=`. `zoom` compares at four decimals.

Every input step reaches the view's own handlers as a Godot input event at canvas coordinates, whose origin is the canvas's top-left corner below the seed bar; nothing calls the camera or the zoom. Keyboard steps act even when the seed field has focus at the start: the run releases its focus first.

A programming error in the browser does not close it. When the browser fails while it selects a kind or an asset, or while it draws a preview, it shows `<file or kind>: crashed: <Type>: <message>` in the preview pane and keeps the lists usable. A crash while the window opens shows that text in place of the window. Each crash also goes to stderr as `asset browser crashed`. A crash is a bug in the engine. A corrupt file is shown as a failure of that file, not as a crash.

No mode writes to the install.

To run Godot by hand, use `mise exec -- <godot>`, where `<godot>` is the name in the `GODOT` variable of `mise.toml` for your platform. Give the install with `--game <install>` after the `--` that ends the Godot arguments, or set `SIEDLER_DIR`. `--game` wins when both are present. `mise run editor` opens the project in the Godot editor.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `mise.toml`: the pinned development tools and the tasks.
- `src/`: the engine code. `Assets/` holds the asset kinds and the asset check, `Formats/` the file readers, `Launch/` the command-line arguments, `Simulation/` the random generator, the seed, the map generator, and the game with its commands, roads, settlers, carriers, goods, woodcutting, and removal, `MapView/` the terrain, object, road, and settler drawing plans, the click handling, and the camera, and `Viewer/` the asset browser, the map view (`MapViewControl.cs`, which also shows the game view), the play driver, and the shaders.
- `play/`: the checked-in play scripts ("Run a play script").
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/roadmap.md`: the milestone plan.
- `docs/controls.md`: the controls of the map view and the game view.
- `docs/rules/`: the rules recovered from the game's program, each with its address as evidence.
- `docs/agents/`: instructions for coding agents.
- `tools/play-run.sh`: the play-run wrapper ("Run a play script").
- `tools/GhidraDump/`: Ghidra scripts that build a local Ghidra project and write a searchable dump of the game's program, and the emulator harness that runs the original's map-start code for reference values.
- `tools/WindowsCheck/`: the procedure that confirms the emulator's values by running the original in Windows.

## Build and test

The full verification (`docs/agents/verification.md`) checks formatting, runs the unit tests, and builds the project:

```sh
mise run check
```

`mise run test` runs the unit tests alone, and `mise run format` formats the code.

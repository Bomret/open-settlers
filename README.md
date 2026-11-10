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

`--graphics <history-edition|open-settlers>` chooses where the map view and the game view get their images, with `--map`, `--game-view`, `--layout`, or `--play` (`-- --map --graphics open-settlers`). `history-edition` is the default: the art of the install. `open-settlers` draws from the repository's own content set `content/open-settlers/`: a `manifest.json` that maps draw terms to PNG files and anchors, and the PNGs, which hold nothing derived from an install (`tools/open-settlers-art.py` writes the set). It draws flat terrain and road strips, and nothing yet for map objects, buildings, goods at flags, settlers, and the water ripple. The rules come from `Siedler1.exe` under both sources, so the launch still needs the install, and one seed, layout, and command list give the same game under both. A broken content set (malformed manifest, a term with no image, a missing or undecodable PNG, a missing or non-integer anchor, a duplicate key, an absolute path or one that leaves the set) shows an error in the view that names `manifest.json` and the key, and draws no map. A bad `--graphics` is a bad argument and exits with code 4, naming the known sources (`history-edition`, `open-settlers`): an unknown name, a missing value, a value that is another flag, a repeat, and use with the asset browser, `--check`, or `--seed-info`.

[Controls](docs/controls.md) describes how to use the map view and the game view. Without an install it prints the asset check's message and exits with its code 2. A missing or unreadable terrain graphic shows an error in the view. An unreadable `Siedler1.exe` shows an error in the view and draws no map: the map generation reads which objects are large from it. Give one mode flag only: with both `--check` and `--map`, the launch reports that they are different modes and exits with code 4.

The game view is the map view with a game on the shown map that ticks at the original's normal speed:

```sh
mise run game -- [--seed <seed>] [--size <1|2|3>]
```

The task builds the C# project first, so the game runs the current code.

It takes `--seed` and `--size` as the map view does, and it has the map view's exit codes. `--layout <file>` in place of `--seed` and `--size` starts it on a layout. "Layouts" under "Run a play script" describes layouts.

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

`sh tools/play-run.sh <stem> [--game-view] [--layout <file>] [--seed <s>] [--size <n>] [--graphics <source>] --fps <n> [--fresh]` (or `mise run play -- <stem> ...`) does the same from `play/<stem>.play` with `SIEDLER_DIR` as `--game`. It runs on the map view, on the game view with `--game-view`, or on a layout with `--layout`. `--layout` implies `--game-view` and takes no `--seed` or `--size`. `--graphics history-edition|open-settlers` passes the graphics source on; another value, a missing value, and a repeat exit 2. The wrapper clears `play-out/<stem>/` and builds `OpenSettlers.csproj`, where a failed build exits 3 and runs no Godot. It stores the result under a hash of the run's inputs, the graphics source (`history-edition` without the flag) and the files of `content/` among them, and reuses it while they are unchanged. It logs each call to `play-out/runs.log`. `docs/agents/verification.md` describes it.

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
- `fast-forward <job> state <n> max <ticks>` and `fast-forward <job> carrying max <ticks>` run the game view's ticks without drawing. They stop when some settler of the job (`woodcutter` or `carrier`) is in state `n` (decimal, or hex with `0x`) or carries a good, and then draw once. The game ticks no more until the next `shot` has been taken, so that shot shows the tick the step stopped at. After that shot the game runs on as normal. A condition that already holds runs no tick. A run that does not reach the condition within `ticks` ticks fails, naming the step and the tick count. The map view refuses the step.
- `shot <name>` writes the last drawn frame to `play-out/<script name>/<name>.png`. A step takes effect in the frame after it, so put a `wait` before the shot.
- `shot <name> <column> <row> [radius] [scale]` writes only the part of that frame around a map tile. The tile is placed where `click-tile` aims, and a tile `click-tile` cannot aim at fails the step with its message and writes no file. The crop holds every tile within `radius` steps (default 1, 0 or more) and room above them for the tallest object sprite at the current zoom, so a tree or building on the tile shows whole. A crop reaching past the map canvas is clipped to it. `scale` (default 1, a whole number of 1 or more) enlarges the crop with nearest sampling; a crop over 16384 pixels on a side once enlarged fails the step. `play/cropped-shot.play` is an example.
- `dump <name>` writes `play-out/<script name>/<name>.txt`: one `field=value` line each for `camera-x` and `camera-y` (the camera's wrapped position in whole pixels), `zoom` (four decimals), `seed`, and `size`. In the game view `ticks` (the game's tick count) and `logs` (the castle's log count, 0 before a castle stands) follow, then one `flag-goods:<column>,<row>` line for each flag that holds goods (the count of goods on it), by row and then column.
- `list <name>` writes `play-out/<script name>/<name>.things.txt`: one line for each thing on the map, `<kind> <column>,<row> <field>=<raw> ... <name>`. The map view lists map objects (kind `object`, field `code`). The game view lists every thing of the running game. It lists the map objects off the nodes of flags and buildings as `object`. A `settler` has the fields `job`, `state`, `player`, `mode`, `target`, `next`, `heading`, `frame`, `timer`, and `stamp`. A `building` has `type`, `constructing`, `status`, `flag`, and `worker`. A `flag` has `slot`. A settler, building, and flag also have `id`, the thing ID (`settler#12.3`: kind, slot, and how often the slot was filled, so no ID is handed out twice in a game). A settler waiting at or walking to a flag, and a building, have `flag-id`. A building with a worker has `worker-id`. Each road node is listed once as `road`, with `dirs`, the directions its roads leave in. Raw values are in the engine's form, a code in hex such as `code=0x0a`, and they always show. The name comes last. It shows only where the name table has evidence for it, as in `object 12,4 code=0x0a tree` and `object 13,4 code=0x31`. Lines come by kind, then row, then column. Two runs of the same script, seed, and size give equal files. A map with no objects gives an empty file. `<name>` follows the rule of `shot` and `dump` names.
- `expect <field> <op> <value>` checks one of those fields. `flag-goods:<column>,<row>` names any node and is 0 where no goods lie on a flag there. An `expect` on `ticks`, `logs`, or a `flag-goods` field in the map view fails. The operators are `<`, `<=`, `=`, `>=`, and `>`; `seed` takes only `=`. `zoom` compares at four decimals.
- `expect-at <column> <row> <what>` checks that a thing at the node matches `what`. `what` is a kind (`object`, `settler`, `building`, `flag`, `road`), a name from the name table (`tree`, `carrier`), or one raw map object code (`object:0x0a`). A name several codes share, such as `tree`, matches any of them. A settler matches its job name and its state name (`carrier`, `waits-at-flag`). A failure states what the node holds. That is `the node holds nothing`, or the lines of its things. The node must lie inside the map. The run checks that against the map's size when it reaches the step.
- `expect-count <what> <op> <n>` counts the things that match `what` and compares the count with `n`. `n` is a whole number of zero or more, and the operators are those of `expect`. A failure states the actual count. Both checks fail in the map view for a kind it cannot list. Only the game view lists `settler`, `building`, `flag`, and `road`. Both checks also fail there for a name that is only a settler job, a settler state, or a building type. Examples are `carrier` and `castle`. So `expect-count settler = 0` and `expect-count carrier = 0` fail in the map view instead of passing for the wrong reason. `flag` and `building` are kinds before they are object names. The map view reaches the objects of those names only by raw code, such as `object:0x01`. These are problems in the script, and each names the line:
  - a column or row that is no whole number
  - an unknown kind or name
  - a raw code that is malformed or over `0xff`
  - a bad count
  - a wrong number of arguments

Named things. These steps work in the game view and fail in the map view.

- `name <alias> <column> <row> <what>` saves the thing ID of the one thing at the node that matches `what` (the vocabulary and node rules of `expect-at`) under `alias`. It fails when no thing or several match, and lists what the node holds. Only a settler, building, or flag has an ID. An alias is letters, digits, `-`, and `_`, and is not a field of `expect`.
- `mark <label>` saves the game as it stands now under a label that is not a whole number.
- `expect <alias>.<field>[@<label>|@<tick>] <op> <value>` checks a field of the named thing now, at a mark, or at the end of a tick, with the operators of `expect`. Now, the fields are the thing's `list` fields, `x` and `y` included. At a mark or tick, the fields are those of the thing's state in its events: a settler's `job`, `state`, `player`, `mode`, `target`, `next`, `heading`, `frame`, `timer`, `stamp`, `x`, and `y`; a building's `type`, `constructing`, `x`, `y`, `flag`, `status`, `worker`, `stamp`, `fill`, and `progress`; a flag's `x`, `y`, `status`, `road0` to `road5`, and `good0` to `good7`. Values are decimal, `0x` hex, `true`, or `false`. The links `id`, `flag-id`, and `worker-id` take only `=` and show only now. They compare with a thing ID or with another alias. An `expect <field>` line without a dot keeps its meaning.
- `expect-event <alias> <type> [cause <cause>]` passes when the thing has an event of that type, with that cause kind when given. A failure lists the thing's events. The types are those of `EventType.All` in `src/Application/EventType.cs`. The causes are `placed` and `razed` (a player's command), `layout`, and the rule areas `settlers`, `carriers`, `woodcutting`, `walk-home`, `goods`, and `removal`.
- `expect-gone <alias>` passes only when no thing in the game has the alias's ID, which holds for a thing whose slot a new thing filled. Any other check on a gone thing (`expect` on a field, now, and `expect-event`) fails and says `gone at tick N, cause <cause>`, from the thing's last event. `events` is an output step, not a check: it writes the history a gone thing keeps.
- `events <alias> <file>` writes `play-out/<script name>/<file>.events.txt`, one line for each event of the thing: `tick=<n> type=<type> cause=<cause> [other=<id>] <field>=<value> ...`. Two runs of the same script give equal files.

An unknown alias, mark, field, event type, or cause, and an alias or mark given twice, are problems in the script and name the line. `play/named-woodcutter.play` names the woodcutter of `layouts/woodcutter.jsonc`, checks it now and at a mark, and checks an event.

#### Layouts

A layout (`layouts/*.jsonc`, [ADR 0009](docs/adr/0009-layouts-set-up-a-game-by-hand-drawn-map-and-commands.md)) is a JSONC file that sets up a game for the game view. The keys `size`, `seed`, `height`, `terrain`, and `objects` draw the map. The optional keys `terrainLower` and `lakes` refine it, where a `lakes` grid sets the lake flag by hand. The entries `castle`, `flags`, `roads`, `buildings`, `settlers`, and `goods` follow, and the game applies them in the file's order.

`--layout <file>` starts the game view on a layout, either directly (`-- --game-view --layout <file>`, also with `--play`) or through the wrapper. A file that does not load prints the file and the entry and exits with code 4. An entry the game refuses does the same. `layouts/woodcutter.jsonc` with `play/woodcutter-cut.play` shows a woodcutter at a tree he cuts. `layouts/carrier.jsonc` with `play/carrier-log.play` shows a carrier carrying a log toward the castle. Each runs in seconds with the `fast-forward` step. `layouts/head-on-pass.jsonc` with `play/head-on-pass.play` shows a carrier walking home and a woodcutter walking out passing each other head-on; it waits out about 50 seconds of game time. Reuse a layout before writing one. `docs/agents/verification.md` has the format and `docs/rules/layouts.md` the settler and good entries.

```sh
sh tools/play-run.sh woodcutter-cut --layout layouts/woodcutter.jsonc --fps 10
```

Every input step reaches the view's own handlers as a Godot input event at canvas coordinates, whose origin is the canvas's top-left corner below the seed bar; nothing calls the camera or the zoom. Keyboard steps act even when the seed field has focus at the start: the run releases its focus first.

A programming error in the browser does not close it. When the browser fails while it selects a kind or an asset, or while it draws a preview, it shows `<file or kind>: crashed: <Type>: <message>` in the preview pane and keeps the lists usable. A crash while the window opens shows that text in place of the window. Each crash also goes to stderr as `asset browser crashed`. A crash is a bug in the engine. A corrupt file is shown as a failure of that file, not as a crash.

No mode writes to the install.

To run Godot by hand, use `mise exec -- <godot>`, where `<godot>` is the name in the `GODOT` variable of `mise.toml` for your platform. Give the install with `--game <install>` after the `--` that ends the Godot arguments, or set `SIEDLER_DIR`. `--game` wins when both are present. `mise run editor` opens the project in the Godot editor.

## Layout

- `OpenSettlers.csproj`, `project.godot`: the Godot project.
- `mise.toml`: the pinned development tools and the tasks.
- `src/`: the engine code. `Domain/` holds the typed model and the random generator, `Application/` the game with its commands, roads, settlers, carriers, goods, woodcutting, and removal, and `Adapters/Simulation/` the importers and exporters of the original's bytes, the seed, and the map generator (three assemblies, ADR 0013). `Assets/` holds the asset kinds and the asset check, `Formats/` the file readers, `Launch/` the command-line arguments, `MapView/` the terrain, object, road, and settler drawing plans, the click handling, and the camera, and `Viewer/` the asset browser, the map view (`MapViewControl.cs`, which also shows the game view), the play driver, and the shaders.
- `content/`: the content sets the repository ships (ADR 0015). `content/open-settlers/` is the Open Settlers graphics source: a manifest, PNGs, and a `.gdignore`.
- `play/`: the checked-in play scripts ("Run a play script").
- `layouts/`: the checked-in layouts ("Layouts" under "Run a play script").
- `tests/OpenSettlers.Tests/`: the unit tests (MSTest). They need neither Godot nor an install.
- `docs/adr/`: decisions. `docs/invariants.md`: invariants. `CONTEXT.md`: domain terms.
- `docs/roadmap.md`: the milestone plan.
- `docs/controls.md`: the controls of the map view and the game view.
- `docs/rules/`: the rules recovered from the game's program, each with its address as evidence.
- `docs/agents/`: instructions for coding agents.
- `tools/play-run.sh`: the play-run wrapper ("Run a play script").
- `tools/open-settlers-art.py`: writes the manifest and PNGs of `content/open-settlers/`.
- `tools/GhidraDump/`: Ghidra scripts that build a local Ghidra project and write a searchable dump of the game's program, and the emulator harness that runs the original's map-start code for reference values.
- `tools/WindowsCheck/`: the procedures that run the original in Windows: the check that confirms the emulator's values, and the probe `elapsed-probe.ps1` that tallies the clock units of the main loop's passes.

## Build and test

The full verification (`docs/agents/verification.md`) checks formatting, runs the unit tests, and builds the project:

```sh
mise run check
```

`mise run test` runs the unit tests alone, and `mise run format` formats the code. The unit tests need neither the install
nor Ghidra. `mise run test:integration` runs the integration tests, which need the install (`SIEDLER_DIR`) and Ghidra.

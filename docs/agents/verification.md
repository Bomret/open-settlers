# Verification

```sh
mise run check
```

```sh
mise run test
```

The full command restores the local tools, checks formatting (CSharpier names each unformatted file.
`mise run format` fixes them), runs the unit tests, and builds the project. `mise.toml` holds the
steps.

Both commands run the unit tests: the tests that need neither the install nor Ghidra. Both skip the tests
with the MSTest category `Install` (they read `SIEDLER_DIR`, through `TestProgram.OpenInstall` or
`TestProgram.Tables`). Both also skip the category `Ghidra` (they run `analyzeHeadless`). The filter is
`TestCategory!=Install&TestCategory!=Ghidra`. Neither command needs the install, Ghidra, or Godot. The remaining tests
build their fixtures in code and pass with `SIEDLER_DIR` unset and no Ghidra on the path.

The category names are the constants of `tests/OpenSettlers.Tests/Needs.cs`. Put
`[TestCategory(Needs.Install)]` (and `Needs.Ghidra` where the test runs `analyzeHeadless`) on every test
that needs one, on the class where every test in it does. A test without the category fails or reports
inconclusive in either command without the install. `NeedsTests` pins the filters to `Needs`.

## Integration command

`mise run test:integration` runs only the tests of the categories `Install` and `Ghidra`. It needs the reference
install (`SIEDLER_DIR`) and Ghidra (`analyzeHeadless` on the path, which `mise install` provides). A test whose need is
missing reports inconclusive. Neither the full command nor the focused one runs these tests. Run this command for a
change to what they test: the install readers, the emulator tools in `tools/GhidraDump/`.

## Timing command

`mise exec -- dotnet test --project tests/OpenSettlers.Tests --filter 'TestCategory!=Install&TestCategory!=Ghidra' --output detailed`

It runs the unit tests of the full command. Microsoft.Testing.Platform prints one `passed <test> (<duration>)`
line per test, then the total duration in the run summary.

## Test paths

- `tests/`
- `*.Tests/`

## Map view Definition-of-Done lines

Verify a Definition-of-Done line about what a player sees in the map view with a play run, and judge
the result yourself. Do not ask the operator to play. Write a play script in `play/` that sets up the
situation and takes named screenshots and dumps, then run it under Movie Maker through the play-run
wrapper, from the repository root, with `SIEDLER_DIR` set:

```sh
sh tools/play-run.sh <stem> [--game-view] [--layout <file>] [--seed <seed>] [--size <n>] --fps <fps>
```

Without `--game-view` the wrapper runs the map view. `--game-view` runs the game view on `--seed` and `--size`.
`--layout <file>` runs the game view on a layout and implies `--game-view`. A layout holds its own seed and size.
The wrapper exits 2 on `--layout` with `--seed` or `--size`, on an empty value, and on a missing file.
`mise run play -- <stem> ...` runs the same wrapper. The wrapper clears `play-out/<stem>/`, builds
`OpenSettlers.csproj`, and runs the Godot command `README.md`'s "Run a play script" gives, through
`mise exec`. Read the dumps and open the screenshots in `play-out/<stem>/`. Exit 0 is a finished run, 1 a
failed `expect` or a problem in the script.

- The wrapper stores the result under a hash of the run's inputs in `play-out/.cache/<hash>/` and reuses
  it on the next call whose inputs hash the same: no Godot run, the stored dumps and screenshots copied
  back into `play-out/<stem>/` (no Movie Maker frames: the store leaves them out, and `--fresh` writes them again), the stored `WARN`/`ERROR` lines printed under a `reused` line that ends
  with the stored exit status, which the wrapper exits with. Only a run that exits 0 or 1 is stored. `--fresh` always runs and replaces the
  stored result.
- The hash covers every tracked or untracked, not ignored file under `src/` and `scenes/` (content and
  path). It also covers `project.godot`, `OpenSettlers.csproj`, `global.json`, the play script, the mode, the
  layout's path and content, the seed, the size, the frame rate, the Godot version, and the `SIEDLER_DIR` path.
  It leaves out the install's content and `.godot/`. After a change to the install, run with `--fresh`.
- A failed build prints the build output, starts no Godot, stores nothing, and exits 3. Bad arguments
  exit 2 with a message and run, store, and log nothing.
- Each call appends one tab-separated line of 12 fields to `play-out/runs.log`. The fields are the UTC time, the
  input hash, `HEAD`, `clean` or `dirty` (an untracked file counts), the stem, `mode=<map|game-view>`,
  `layout=<file>` (empty without a layout), `seed=<s>`, `size=<n>`, `fps=<n>`, `exit=<status>`, and `run`,
  `reused`, or `build` (a failed build). A line proves a run at a tip when its `HEAD` equals the tip and it reads
  `clean`.
- Calls for one stem run one at a time: a second call waits on the lock directory
  `play-out/.lock-<stem>`, says so once on stderr, then hashes its inputs and reuses what the first
  stored. Other stems run in parallel. The lock holds its owner's pid, and a call takes over the lock of
  a dead owner, and of a lock with no pid older than 5 seconds. Remove a lock by hand only when no run is live.

When a line is about one place, take a cropped shot, `shot <name> <column> <row> [radius] [scale]`, rather
than the whole frame: it keeps the tiles within `radius` steps of the tile (default 1) and the room above them
for the tallest object sprite, enlarged by the whole factor `scale` (default 1). The tile is placed as
`click-tile` places it and fails with its messages; a crop is clipped to the canvas, and one over 16384 pixels
on a side once enlarged fails the step. README.md's "Run a play script" lists every step. The named-thing steps (`name`, `mark`, `expect <alias>.<field>`, `expect-event`, `expect-gone`, `events`) check one thing's fields and events in the game view; `play/named-woodcutter.play` shows them on `layouts/woodcutter.jsonc`.

### Layouts: reach a moment without clicks

Do not click a situation together in a play script. A layout (`layouts/*.jsonc`,
[ADR 0009](../adr/0009-layouts-set-up-a-game-by-hand-drawn-map-and-commands.md)) draws the map and sets the game up.
The `fast-forward` step then runs the game to the moment under test.
**Reuse a layout before writing one.** `layouts/` holds them, and a new play script on an existing layout costs a
script alone. Add a layout only where none of them holds the situation.

- `layouts/woodcutter.jsonc` and `play/woodcutter-cut.play`: a woodcutter in his hut beside trees,
  fast-forwarded to the cut (state `0x11`).
- `play/woodcutter-figures.play` on the same layout: crops of the woodcutter at his door, on the way out, at the cut, and on the way back with the log.
- `play/woodcutter-log.play` on the same layout: crops of the woodcutter cutting, the woodcutter walking back with his log, and the road's carrier carrying that log toward the castle.
- `layouts/carrier.jsonc` and `play/carrier-log.play`: a carrier on a road with logs on its far flag,
  fast-forwarded to the carrier carrying a log.
- `layouts/head-on-pass.jsonc` and `play/head-on-pass.play`: two roads with their carriers. The script takes the
  second road down and puts a woodcutter's hut down at the first flag, so the road's carrier walking home and the
  woodcutter walking out meet head-on and pass, and checks both at the pass (tick 499).

```sh
sh tools/play-run.sh woodcutter-cut --layout layouts/woodcutter.jsonc --fps 10
sh tools/play-run.sh carrier-log --layout layouts/carrier.jsonc --fps 10
```

Each run takes a few seconds once the build is warm. `CheckedInLayoutsTests` reads every `layouts/*.jsonc` and
applies it to a game. A layout that does not load therefore fails the unit tests.

**The format.** A layout is JSONC, so it allows comments. Its keys draw the map:

- `size`: 1, 2, or 3.
- `seed`.
- `height` and `terrain`, and optionally `terrainLower`.
- Optionally `lakes`, a grid of `.` and `L` that sets Flags bit 6 and replaces the derived lake rule.
- `objects`, a list of `{"x","y","code"}`.

The entries follow, and the game applies them in the file's order:

- `castle`: `{"x","y"}`.
- `flags`.
- `roads`: `{"x","y","directions"}`, the start flag and one direction for each step.
- `buildings`: a woodcutter's hut.
- `settlers`: a woodcutter in his hut or a carrier waiting on a road.
- `goods`: `{"x","y","place","good"}` on a flag.

A refused or repeated entry fails the load with `<file>: <entry>: <reason>`. [The map form](../rules/map-form.md#a-hand-drawn-map)
holds the grid letters. [The layout rules](../rules/layouts.md) hold the entry states.

**The step.** `fast-forward <job> state <n> max <ticks>`, `fast-forward <job> carrying max <ticks>`, and
`fast-forward until <condition> max <ticks>` tick the game without drawing until the condition holds. An `until`
condition is an `expect` on a game field or `count` with an `expect-count`'s arguments. A `wait` of more than a few
seconds draws every frame; wait for the state under test with `fast-forward until` instead. The game then waits for the next `shot` (`README.md`'s "Run a play script").
Zoom in with `scroll` before the shot where the figures are small.

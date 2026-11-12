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
build their fixtures in code and pass with `SIEDLER_DIR` unset and no Ghidra on the path. They need `python3`, which
`mise install` provides: `RepositoryContentTests` runs `tools/open-settlers-art.py` into a temporary folder and fails where
`content/open-settlers/` holds a file the generator does not write the same.

The category names are the constants of `tests/OpenSettlers.Tests/Needs.cs`. Put
`[TestCategory(Needs.Install)]` (and `Needs.Ghidra` where the test runs `analyzeHeadless`) on every test
that needs one, on the class where every test in it does. A test without the category fails or reports
inconclusive in either command without the install. `NeedsTests` pins the filters to `Needs`.

No static field or static property initializer and no static constructor of a test class may reach the install
(`TestProgram.Tables()`, `TestProgram.OpenInstall()`). Without the install the `Assert.Inconclusive` they throw then fires
during type initialization, and MSTest fails every test of the class with a `TypeInitializationException`. Build the
value inside the test instead, as the cached `Map` accessor of `MapPickingTests` does, and read that accessor only from
the test. A lambda, local function, or method group that runs only when called later is allowed, such as
`new Lazy<Map>(() => Generated())`. An instance initializer or constructor is allowed: it runs when MSTest creates the
instance for a test, which then reports inconclusive.

`InstallInitializerTests` fails naming the file and line of every static initializer and static constructor that
reaches one of them. Its header comment lists the forms of reach the check catches and the ones it misses.

## Run one test class

`--filter 'ClassName=OpenSettlers.Tests.<Class>'` runs exactly one test class:

```sh
mise exec -- dotnet test --project tests/OpenSettlers.Tests --filter 'ClassName=OpenSettlers.Tests.ShotCropTests'
```

`FullyQualifiedName~<Class>` matches by substring: `FullyQualifiedName~GameTests` also selects `RoadSplitGameTests`.
Without the install, add the category clauses, which run the class's unit tests alone:

```sh
mise exec -- dotnet test --project tests/OpenSettlers.Tests --filter 'ClassName=OpenSettlers.Tests.ShotCropTests&TestCategory!=Install&TestCategory!=Ghidra'
```

The test host (MSTest on Microsoft.Testing.Platform) has no `--filter-class`: it exits 5 and lists the test project under
`Handshake failures:`. A filter that matches no test exits 8.

## Integration command

`mise run test:integration` runs only the tests of the categories `Install` and `Ghidra`. It needs the reference
install (`SIEDLER_DIR`) and Ghidra (`analyzeHeadless` on the path, which `mise install` provides). A test whose need is
missing reports inconclusive. An install whose `Siedler1.exe` holds no rule tables counts as missing. `mise` sets
`SIEDLER_DIR` from `mise.local.toml`, also in a worktree below the checkout. To run the tests without the install,
remove the variable inside `mise`:

```sh
mise exec -- env -u SIEDLER_DIR dotnet test --project tests/OpenSettlers.Tests
```

On Windows, run this with PowerShell 7 (`pwsh`). If only Windows PowerShell 5.1 is installed, write `powershell` for `pwsh`.

```powershell
mise exec -- pwsh -NoProfile -Command 'Remove-Item Env:\SIEDLER_DIR -ErrorAction SilentlyContinue; dotnet test --project tests/OpenSettlers.Tests'
```

Neither the full command nor the focused one runs these tests. Run the integration command for a change to what they
test: the install readers, the emulator tools in `tools/GhidraDump/`.

## Timing command

`mise exec -- dotnet test --project tests/OpenSettlers.Tests --filter 'TestCategory!=Install&TestCategory!=Ghidra' --output detailed`

It runs the unit tests of the full command. Microsoft.Testing.Platform prints one `passed <test> (<duration>)`
line per test, then the total duration in the run summary.

## Prepare command

`mise run restore`

It restores CSharpier, the local dotnet tool the full command's format check runs. `dotnet test` restores the NuGet
packages itself.

## Test paths

- `tests/`
- `*.Tests/`

## Map view Definition-of-Done lines

Verify a Definition-of-Done line about what a player sees in the map view with a play run, and judge
the result yourself. Do not ask the operator to play. Write a play script in `play/` that sets up the
situation and takes named screenshots and dumps, then run it under Movie Maker through the play-run
wrapper, from the repository root, with `SIEDLER_DIR` set:

```sh
sh tools/play-run.sh <stem> [--game-view] [--layout <file>] [--seed <seed>] [--size <n>] [--graphics <source>] --fps <fps>
```

Without `--game-view` the wrapper runs the map view. `--game-view` runs the game view on `--seed` and `--size`.
`--layout <file>` runs the game view on a layout and implies `--game-view`. A layout holds its own seed and size.
The wrapper exits 2 on `--layout` with `--seed` or `--size`, on an empty value, and on a missing file.
`--graphics history-edition|open-settlers` picks the graphics source of the run (the History Edition without the flag).
Another value, a missing value, and a repeat exit 2.
`mise run play -- <stem> ...` runs the same wrapper. The wrapper clears `play-out/<stem>/`, builds
`OpenSettlers.csproj`, and runs the Godot command `README.md`'s "Run a play script" gives, through
`mise exec`. Read the dumps and open the screenshots in `play-out/<stem>/`. Exit 0 is a finished run, 1 a
failed `expect` or a problem in the script.

- The wrapper stores the result under a hash of the run's inputs in `play-out/.cache/<hash>/` and reuses
  it on the next call whose inputs hash the same: no Godot run, the stored dumps and screenshots copied
  back into `play-out/<stem>/` (no Movie Maker frames: the store leaves them out, and `--fresh` writes them again), the stored `WARN`/`ERROR` lines printed under a `reused` line that ends
  with the stored exit status, which the wrapper exits with. Only a run that exits 0 or 1 is stored. `--fresh` always runs and replaces the
  stored result.
- The hash covers every tracked or untracked, not ignored file under `src/`, `scenes/`, and `content/` (content and
  path). It also covers `project.godot`, `OpenSettlers.csproj`, `global.json`, `Directory.Build.props`, the play script, the mode, the
  layout's path and content, the seed, the size, the graphics source, the frame rate, the Godot version, and the
  `SIEDLER_DIR` path.
  It leaves out the install's content and `.godot/`. After a change to the install, run with `--fresh`.
- A failed build prints the build output, starts no Godot, stores nothing, and exits 3. Bad arguments
  exit 2 with a message and run, store, and log nothing.
- Each call appends one tab-separated line of 13 fields to `play-out/runs.log`. The fields are the UTC time, the
  input hash, `HEAD`, `clean` or `dirty` (an untracked file counts), the stem, `mode=<map|game-view>`,
  `layout=<file>` (empty without a layout), `seed=<s>`, `size=<n>`, `fps=<n>`, `graphics=<source>` (`history-edition` without the flag), `exit=<status>`, and `run`,
  `reused`, or `build` (a failed build). A line proves a run at a tip when its `HEAD` equals the tip and it reads
  `clean`.
- Calls for one stem run one at a time: a second call waits on the lock directory
  `play-out/.lock-<stem>`, says so once on stderr, then hashes its inputs and reuses what the first
  stored. Other stems run in parallel. The lock holds its owner's pid, and a call takes over the lock of
  a dead owner, and of a lock with no pid older than 5 seconds. A process of another user counts as live (the
  check is `ps -p <pid> -o pid=`, which needs no locale). A pid reads as dead only when that `ps` exits 1 with no
  output and the same `ps` lists the call's own pid, so a missing `ps`, a `ps` without `-p`, and any other result make
  a call wait. The takeover happens under the mutex `play-out/.lock-<stem>.takeover`, which holds its holder's pid. The
  mutex is stale, and is then cleared, when that pid reads as dead, or when it holds no pid and is older than 5
  seconds. A call waits on a mutex of a live holder. The wait message names the lock and the
  mutex. A call removes a lock only while its own pid is in the mutex, after it judged the lock dead a second time
  under the mutex and found the pid it judged still in it, and it never moves a lock off its path; the lock's own
  call removes it on exit only while it holds that call's pid. A stale mutex is cleared in place and never moved,
  by one reaper per holder: the directory `play-out/.lock-<stem>.takeover.reap.<holder>` (`<holder>` is the pid in
  the mutex, or `nopid`) holds the reaper's pid, and its reaper judges the mutex stale again with the same holder
  in it before it removes it. A reap directory is stale by the mutex's rule, and the next call that judges a mutex
  of that holder removes it; one whose mutex is gone stays until removed by hand, and blocks nothing. A call
  removes a mutex that holds its own pid outside its critical section instead of waiting on it. Two gaps are left,
  and each needs a call that stalls or is killed. A holder that stalls more than 5 seconds between creating the
  mutex and writing its pid reads as holding none, so a reaper can remove it and a third call can take the path:
  two holders, and one can remove a lock another took. A reaper killed inside its step leaves its reap directory,
  and two calls that both judge it stale can remove it one after the other's new reaper has made it: two reapers
  of one holder, and the one that finishes second can remove a mutex a live holder took. Remove the lock, the
  mutex, and the reap directories by hand only when no run is live.
- An empty `--size` or `--seed` exits 2 with the usage message, as an empty `--fps` or `--layout` does.

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
  second road down, so its carrier walks home along the first road, and checks that it enters the castle. The pass with a
  woodcutter walking out is pinned by the golden scenario `head-on-pass-layout` (it puts a finished hut down at tick 401)
  and by `HeadOnPassTests`: a hut a layout stands has its worker inside from tick 0.
- `layouts/hut-flag.jsonc`, `layouts/hut-on-road.jsonc`, and `layouts/two-logs.jsonc` with `play/hut-flag.play`,
  `play/hut-on-road.play`, and `play/two-logs.play`: a finished hut whose flag the entry puts down, a hut whose flag splits a
  road, and a woodcutter's flag whose road no carrier works until a decoy road is taken down. `play/first-loop.play` and
  `play/settler-bodies.play` run on `layouts/woodcutter.jsonc`.

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
- `buildings`: finished-building entries `{"type","x","y"}` of woodcutter, stonecutter, forester, or sawmill (a
  woodcutter's, stonecutter's, or forester's with its worker inside).
- `settlers`: a carrier waiting on a road.
- `goods`: `{"x","y","place","good"}` on a flag.

A refused or repeated entry fails the load with `<file>: <entry>: <reason>`. [The map form](../rules/map-form.md#a-hand-drawn-map)
holds the grid letters. [The layout rules](../rules/layouts.md) hold the entry states.

**The step.** `fast-forward <job> state <n> max <ticks>`, `fast-forward <job> carrying max <ticks>`, and
`fast-forward until <condition> max <ticks>` tick the game without drawing until the condition holds. An `until`
condition is an `expect` on a game field, or `count` followed by the arguments of `expect-count`. Every `wait` draws every
frame, so wait for a state many ticks away with `fast-forward until`. The game then holds its ticks until the next `shot` (`README.md`'s "Run a play script").
Zoom in with `scroll` before the shot where the figures are small.

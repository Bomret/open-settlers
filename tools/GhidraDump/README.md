# Ghidra dump

The Ghidra scripts in this folder build a local Ghidra project of `Siedler1.exe` and write a dump of it
for searching with `grep`:

- `CreateMissingFunctions.java` creates functions for code that Ghidra's analysis leaves outside any
  function: call targets without a function, and code reached only through jumps or jump tables. Some
  of these are pieces of another routine: a function with no callers whose only references are data
  references from inside another function is a case block or jump target of that function.
- `ApplyIndexNames.java` applies the names of the "By address" table in
  `docs/rules/program-index.md` to the project: function names at code addresses, labels at data
  addresses. A row whose role starts with "Data:" gets a label even in the code section, and a
  function at its address is removed together with its instructions up to the next function or index
  row.
- `DumpProgram.java` writes the dump.
- `StateFields.java` writes the table of game-state fields.

The dump has these files:

- `functions/<address>.c`: the decompiled code of each function.
- `functions.tsv`: per function the address, name, size, callers, callees, referenced strings, and data
  references. Callers are the code that calls or jumps to the entry. Data references are the other
  references to the entry, such as tables of routine addresses. A caller that is a bare address, not a
  function entry, is a call site in code that Ghidra assigns to no function. In the game code band such
  a site can lie far past the end of the function before it, so read the code around the site to find
  its routine.
- `strings.tsv`: per string its address, value, and the functions that reference it.
- `vtables.tsv`: per class whose vtable Ghidra recovers from RTTI, the vtable address, the class, and its
  slots in order.
- `state_fields.tsv`: per offset into the game state block, the operand sizes seen, the routines that
  read it, and the routines that write it. A routine appears as `Name@address` when the index names it.
  The script follows the state block pointer through straight-line code only, so it misses accesses
  that pass through other registers or across branches.

The dump and the Ghidra project hold decompiler output of the game's program. They stay in `re-local/`
of the main checkout, which git ignores ([INV-0001](../../docs/invariants.md#inv-0001)). Record findings in
`docs/rules/program-index.md` in our own words.

## Requirements

`mise install` installs these tools (`mise.toml`):

- Ghidra 12.1.x, the release line REA accepts, so one install serves REA and the dump.
  `mise where github:NationalSecurityAgency/ghidra` prints its folder. The release ships no native
  decompiler for macOS on Apple silicon: `mise run ghidra:natives` builds it with Gradle. The build
  needs a C++ compiler (on macOS, the Xcode Command Line Tools: `xcode-select --install`).
- A JDK 25 and Gradle.
- Python, for `program_index.py`.

The import also needs the `Siedler1.exe` of the reference install (`docs/rules/AGENTS.md`, "Reference
build"), found through `SIEDLER_DIR`.

## Run

Run Ghidra headless directly. The REA tools and `rea analyze` stop after a fixed timeout that a full
analysis of `Siedler1.exe` can exceed. Ghidra headless has no timeout. The tasks run
`headless.py`, which calls Ghidra's `analyzeHeadless` on the project in `re-local/ghidra`. Each task
looks up `analyzeHeadless` (and `gradle` for `ghidra:natives`) before it creates or removes a folder, so on
a host without Ghidra a task stops with its message and leaves `re-local/` as it was.

Every git worktree of the repository shares the Ghidra project and the dump in `re-local/` of the main
checkout. The main checkout is the parent folder of git's common directory (`git rev-parse
--git-common-dir`). The tasks take the scripts and the program index from the tree they run in. Thus
`mise run ghidra:update` from a scope applies the index of that scope to the shared project. An import or
a full dump from any tree replaces the shared project or the shared dump. While a run has the project
open, Ghidra keeps the lock file `Siedler1.lock` next to `Siedler1.gpr` in `re-local/ghidra` and refuses
a second run on the project. `ghidra:import` and `ghidra:dump` stop with a message while that lock file
exists, before they create or remove a folder, so a second run leaves the running dump as it is. Ghidra
deletes the lock file when the run ends. A run that crashes can leave it behind: delete it by hand only
when no Ghidra has the project open. Outside a git repository, without git, or where git gives no single
absolute path that ends in `.git` (git before 2.31, a bare repository), the tasks use `re-local/` under the
current folder.

Build the project only when `re-local/ghidra` holds no project. The import replaces an existing project
and loses every change applied to it. The build takes about 7 minutes. It imports the program, runs
`CreateMissingFunctions.java`, and applies the index with `ApplyIndexNames.java`:

```sh
mise run ghidra:import
```

Update the project after the index changes. This runs both scripts again on the saved project, without
Ghidra's auto-analysis, and saves the project. Without `-noanalysis` Ghidra first re-analyses the whole
program before the scripts run:

```sh
mise run ghidra:update
```

Write the dump from the project, without changing it. Never edit the dump by hand, because this run
removes `re-local/dump` and writes it again. Run it only under the conditions in `docs/rules/AGENTS.md`,
"Program index":

```sh
mise run ghidra:dump
```

When only the tables must change, such as the names in `functions.tsv` and `strings.tsv`, write the
dump in index-only mode. It rewrites `functions.tsv`, `strings.tsv`, and `vtables.tsv`, does not
decompile, and leaves the files in `functions/` as they are, so their names can be stale:

```sh
mise run ghidra:dump index-only
```

`program_index.py` rebuilds the generated sections of the program index from its topic tables: the
"By address" table, which `ApplyIndexNames.java` reads, and the coverage line. It passes the dump folder
as its second argument, so the coverage line also counts the functions of the game code band in the dump.
It rewrites the index of the tree it runs in and reads the dump of the main checkout. When the dump folder
holds no `functions.tsv`, it stops with a message that names the folder and leaves the index as it was:

```sh
mise run index
```

Ghidra headless rejects a project path that is relative or that holds an element starting with a dot.
The project lies in the main checkout, so the tasks run from a worktree under a dot folder, such as a
superdev scopetree. A main checkout under a dot folder fails.

Run one analysis at a time on a machine. Two Ghidra analyses at once can overload the machine.

## Emulator harness

`EmulateMapStart.java` runs the original's map-start code in Ghidra's emulator and prints the values it
produces (ADR 0008). Tests quote these values and name the command. `emulate.py` finds `Siedler1.exe` in the
install (`--game <dir>` or `SIEDLER_DIR`), imports it into a temporary project without analysis, runs the
commands, and removes the project, so it needs no project in `re-local/` and commits no program bytes:

```sh
python3 -I tools/GhidraDump/emulate.py --game "$SIEDLER_DIR" rng:FFFF,FFFF,FFFF steps:6:1234,5678,9ABC \
  parse:1234567812345678 show:1234,5678,9ABC mask:1234,5678,9ABC
```

`--project <dir>` (before the commands) keeps the project in that folder: the first call imports, later calls run
against the import. The folder records the program's SHA-256, and a program with another SHA-256, or a folder with no
`Emu.gpr`, is imported again. The import replaces only `Emu.gpr`, `Emu.rep`, and `program.sha256`; other files in the
folder stay. The caller removes the folder.

Each output line starts with `EMU `. The header of `EmulateMapStart.java` lists the commands. Words are
hexadecimal, without spaces.

A call with commands other than `draw:` exits 0 only where every command ended: the script prints the line
`EMU command=N ended` after the command `N` (counting from 1) returns, and `emulate.py` checks these lines and leaves
them out of its output. A command ends without a line of its own where it prints none. A command does not end where it
throws, where Ghidra stops before it, where the import fails, or where Ghidra exits non-zero. A failed call prints
nothing on stdout, writes the first command that did not end (its number and its text) and the tail of Ghidra's
output to stderr, and exits non-zero.

The `gen` command runs `MapGen_Generate` up to the end of `MapGen_CountResource1` for a size value and a map
seed, for example `gen:3:1234,5678,9ABC` (the size value is decimal), and prints the generator words and the
layer digests of the canonical map form after each pass (`docs/rules/map-form.md`). A run takes from 15
seconds to a few minutes, the longer for the larger sizes. Every other command that starts from a map generates it
silently, once per size and seed in one Ghidra launch: the first run keeps the memory the generation wrote and the
registers it left, and later commands of the same launch, `draw:` batch commands among them, start from that state
instead of generating again. Each command restores a copy of that state. A launch is one `emulate.py` call, and its
commands are independent. A command prints the lines it prints in a launch of its own, whatever commands ran before it
(INV-0018). A command that fails ends a launch that runs no `draw:` batch, and the call fails, as above. A command of a
`draw:` batch that fails prints a failure line, and the batch runs on. The `gen` command always generates, because it
prints each pass. The commands `site`, `castle`, `stockrows`, `opponents`, and `peek` run the build-site evaluator, the
castle placement, the start stock table, the free-game opponent settings, and a byte dump: their forms are in the header
of `EmulateMapStart.java`, and the rules they check are in `docs/rules/build-sites.md` and `docs/rules/castle.md`. The
`flag` command runs the flag button (`0x442244`) on every tile and counts, per pair of node state and site, the tiles
that put a flag; `castle` with sites adds the same count after the castle stands. The `flagput` command places flags
with the button after a castle; `road` draws roads between those flags with the original's cursor update, click step,
and commit, and `roaddraw` runs the three strip routines that draw a road (`docs/rules/roads.md`). The `tables:x`
command prints the digest of the spiral table and the object codes of the large objects. The `supply` command runs the
settler supply of the original in a castle game at a settler rate and start stock level it takes, for example
`supply:1:1234,5678,9ABC:0:18,20:40:1200:25,0;26,0` (rate 40, no swords and shields in the castle). It prints a line for
each pass where a settler was made or the knights or the stock changed: `tests/OpenSettlers.Tests/settler-supply.txt`
holds its games, each run in a launch of its own, and `docs/rules/new-settlers.md` the rules it checks.

### The draw verb

`draw:WINDOWS:CX,CY:VERB:ARGS...` runs the settler draw path of the original's main loop. That is `0x42f4f1`, then `0x44ffe7` for
each row of the view. It runs after the passes of a `carrier:`, `wood:`, `goods:`, `home:`, `remove:`, or `construct:` game. `WINDOWS` is a list
of `FROM-TO` pass ranges, with pass numbers from 0. For each pass in `WINDOWS` it prints the settlers' bytes and what the original
blits for them. That is each settler's sprite entries, fill colour, and position, in drawing order
(`docs/rules/settlers.md`, "The draw path, emulated"). It runs `EmulateDraw.java`, a script that extends `EmulateMapStart.java`.
`emulate.py` passes the install folder to it.

Use `draw:` instead of a screenshot or a `play` run to check what a settler draws. It gives the original's exact sprites,
colours, and positions, and a test can compare them. It is the stronger check. It runs on the reference build of `Siedler1.exe`
only (the SHA-256 in `docs/rules/AGENTS.md`). It exits non-zero and prints nothing in these cases:
- The install or the program is missing.
- The program is another build.
- The run reaches no pass of a window.
- A pass of a window draws no settler.

```sh
python3 -I tools/GhidraDump/emulate.py --game "$SIEDLER_DIR" \
  "draw:95-110,230-275:17,20:goods:1:1234,5678,9ABC:0:18,20:16,20:19,21,343:16,20,2:276:0::-1:5,0,7"
```

Several `draw:` commands in one call run as a batch. A batch runs in one Ghidra launch, each command in a new script
instance, with `--project` or without. A batch on another build prints nothing and exits non-zero. Otherwise it prints the install line,
then each command's result in argument order: `EMU draw command=N ended` and its recording, or `EMU draw command=N failed`
and no lines, with `N` counting from 1. A command fails where its run does not end, and where the import fails or Ghidra
stops before it. A batch exits non-zero where any command fails. A single `draw:` command prints no `command=` line.

The settler draw turns the settlers in view every pass, so from the first window on a run differs from the verb it wraps. A recording keeps each
pass's output with the `EMU draw ` prefix removed, under a `# name command` line (`tests/OpenSettlers.Tests/settler-draws.txt`).

`site@X,Y` in place of `CX,CY` draws the building of the node `X,Y` alone in each pass of `WINDOWS` instead: the building draw `0x44e329`,
which reaches the draw of a building under construction (`0x44fa79`), or `0x44fee2` for the castle under construction. It turns no settler, so the run is the verb's. Each line lists the
building's bytes and the shadows, sprites (with the fraction a grown sprite shows), and goods the draw drew
(`tests/OpenSettlers.Tests/site-draws.txt`, `docs/rules/construction.md`).

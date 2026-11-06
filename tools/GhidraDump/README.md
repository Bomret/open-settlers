# Ghidra dump

The Ghidra scripts in this folder build a local Ghidra project of `Siedler1.exe` and write a dump of it
for searching with `grep`:

- `CreateMissingFunctions.java` creates functions for code that Ghidra's analysis leaves outside any
  function: call targets without a function, and code reached only through jumps or jump tables. Some
  of these are pieces of another routine: a function with no callers whose only references are data
  references from inside another function is a case block or jump target of that function.
- `ApplyIndexNames.java` applies the names of the "By address" table in
  `docs/rules/program-index.md` to the project: function names at code addresses, labels at data
  addresses.
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

The dump and the Ghidra project hold decompiler output of the game's program. They stay in `re-local/`,
which git ignores ([INV-0001](../../docs/invariants.md#inv-0001)). Record findings in
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
`headless.py`, which calls Ghidra's `analyzeHeadless` on the project in `re-local/ghidra`.

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
as its second argument, so the coverage line also counts the functions of the game code band in the dump:

```sh
mise run index
```

The project path must be absolute, and no element of it may start with a dot: Ghidra headless rejects
such a path.

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

Each output line starts with `EMU `. The header of `EmulateMapStart.java` lists the commands. Words are
hexadecimal, without spaces.

The `gen` command runs `MapGen_Generate` up to the end of `MapGen_CountResource1` for a size value and a map
seed, for example `gen:3:1234,5678,9ABC` (the size value is decimal), and prints the generator words and the
layer digests of the canonical map form after each pass (`docs/rules/map-form.md`). A run takes from 15
seconds to a few minutes, the longer for the larger sizes. The commands `site`, `castle`, `stockrows`,
`opponents`, and `peek` run the build-site evaluator, the castle placement, the start stock table, the free-game
opponent settings, and a byte dump: their forms are in the header of `EmulateMapStart.java`, and the rules they
check are in `docs/rules/build-sites.md` and `docs/rules/castle.md`. The `flag` command runs the flag button (`0x442244`) on every tile and counts, per pair of node state and site,
the tiles that put a flag; `castle` with sites adds the same count after the castle stands. The `flagput` command places flags with the button after a castle; `road` draws roads between those flags with the original's cursor update, click step, and commit, and `roaddraw` runs the three strip routines that draw a road (`docs/rules/roads.md`). The `tables:x` command prints the digest of the
spiral table and the object codes of the large objects.

### The draw verb

`draw:WINDOWS:CX,CY:VERB:ARGS...` runs the settler draw path of the original's main loop (`0x42f4f1`, then `0x44ffe7` for each row of the view)
after the passes of a `carrier:`, `wood:`, `goods:`, `home:`, or `remove:` game and prints, for each pass in `WINDOWS` (`FROM-TO,FROM-TO,...`, pass numbers from 0), the
settlers' bytes, and what the original blits for them: each settler's sprite entries, fill colour, and position, in drawing order
(`docs/rules/settlers.md`, "The draw path, emulated"). It runs `EmulateDraw.java`, a script that extends `EmulateMapStart.java`, and `emulate.py`
passes the install folder to it. It runs on the reference build of `Siedler1.exe` only (the SHA-256 in `docs/rules/AGENTS.md`), and exits non-zero and
prints nothing where the install or the program is missing or another build, the run reaches no pass of a window, or a pass of a window draws no settler:

```sh
python3 -I tools/GhidraDump/emulate.py --game "$SIEDLER_DIR" \
  "draw:95-110,230-275:17,20:goods:1:1234,5678,9ABC:0:18,20:16,20:19,21,343:16,20,2:276:0::-1:5,0,7"
```

The settler draw turns the settlers in view every pass, so from the first window on a run differs from the verb it wraps. A recording keeps each
pass's output with the `EMU draw ` prefix removed, under a `# name command` line (`tests/OpenSettlers.Tests/settler-draws.txt`).

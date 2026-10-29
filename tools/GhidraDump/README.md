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

- Ghidra 12.1.x, the release line REA accepts, so one install serves REA and the dump. Extract the
  official release from `github.com/NationalSecurityAgency/ghidra`. The release ships no native
  decompiler for macOS on Apple silicon: build it with `gradle buildNatives` in the release's
  `support/gradle/` folder (`brew install gradle`).
- A JDK 21 or newer (`brew install openjdk@25`).
- The `Siedler1.exe` of the reference install (`docs/rules/AGENTS.md`, "Reference build").

## Run

Run Ghidra headless directly. The REA tools and `rea analyze` stop after a fixed timeout that a full
analysis of `Siedler1.exe` can exceed. Ghidra headless has no timeout. Run every command from the
repository root.

```sh
export JAVA_HOME=/opt/homebrew/opt/openjdk@25/libexec/openjdk.jdk/Contents/Home
export PATH="$JAVA_HOME/bin:$PATH"
export HEADLESS=<Ghidra 12.1.x folder>/support/analyzeHeadless
```

Build the project only when `re-local/ghidra` holds no project. The import replaces an existing project
and loses every change applied to it. The build takes about 7 minutes:

```sh
mkdir -p "$PWD/re-local/ghidra"
"$HEADLESS" "$PWD/re-local/ghidra" Siedler1 -import "$SIEDLER_DIR/Siedler1.exe" -overwrite \
  -scriptPath "$PWD/tools/GhidraDump" -postScript CreateMissingFunctions.java \
  -postScript ApplyIndexNames.java "$PWD/docs/rules/program-index.md"
```

Update the project after the index changes. This runs both scripts again on the saved project, without
Ghidra's auto-analysis, and saves the project. Without `-noanalysis` Ghidra first re-analyses the whole
program before the scripts run:

```sh
"$HEADLESS" "$PWD/re-local/ghidra" Siedler1 -process Siedler1.exe -noanalysis \
  -scriptPath "$PWD/tools/GhidraDump" -postScript CreateMissingFunctions.java \
  -postScript ApplyIndexNames.java "$PWD/docs/rules/program-index.md"
```

Write the dump from the project, without changing it. Never edit the dump by hand, because this run
overwrites it. Run it only under the conditions in `docs/rules/AGENTS.md`, "Program index":

```sh
rm -rf "$PWD/re-local/dump" && mkdir -p "$PWD/re-local/dump"
"$HEADLESS" "$PWD/re-local/ghidra" Siedler1 -process Siedler1.exe -noanalysis -readOnly \
  -scriptPath "$PWD/tools/GhidraDump" -postScript DumpProgram.java "$PWD/re-local/dump" \
  -postScript StateFields.java "$PWD/re-local/dump"
```

When only the tables must change, such as the names in `functions.tsv` and `strings.tsv`, write the
dump in index-only mode. It rewrites `functions.tsv`, `strings.tsv`, and `vtables.tsv`, does not
decompile, and leaves the files in `functions/` as they are, so their names can be stale:

```sh
"$HEADLESS" "$PWD/re-local/ghidra" Siedler1 -process Siedler1.exe -noanalysis -readOnly \
  -scriptPath "$PWD/tools/GhidraDump" -postScript DumpProgram.java "$PWD/re-local/dump" index-only \
  -postScript StateFields.java "$PWD/re-local/dump"
```

`program_index.py` rebuilds the generated sections of the program index from its topic tables: the
"By address" table, which `ApplyIndexNames.java` reads, and the coverage line. With the dump folder as
its second argument, the coverage line also counts the functions of the game code band in the dump:

```sh
python3 -I tools/GhidraDump/program_index.py docs/rules/program-index.md re-local/dump
```

The project path must be absolute, and no element of it may start with a dot: Ghidra headless rejects
such a path.

Run one analysis at a time on a machine. Two Ghidra analyses at once can overload the machine.

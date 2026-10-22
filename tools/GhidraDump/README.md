# Ghidra dump

`DumpProgram.java` is a Ghidra script. It analyzes `Siedler1.exe` and writes a local dump for searching
with `grep`:

- `functions/<address>.c`: the decompiled code of each function.
- `functions.tsv`: per function the address, name, size, callers, callees, referenced strings, and data
  references. Callers are the code that calls or jumps to the entry. Data references are the other
  references to the entry, such as tables of routine addresses.
- `strings.tsv`: per string its address, value, and the functions that reference it.

The dump and the Ghidra project hold decompiler output of the game's program. They stay in `re-local/`,
which git ignores ([INV-0001](../../docs/invariants.md#inv-0001)). Record findings in
`docs/rules/program-index.md` in our own words.

## Requirements

- Ghidra (`brew install ghidra`) and Java 21 (`brew install openjdk@21`).
- The `Siedler1.exe` of the reference install (`docs/rules/AGENTS.md`, "Reference build").

## Run

Run Ghidra headless directly. The REA tools and `rea analyze` stop after a fixed timeout that a full
analysis of `Siedler1.exe` can exceed. Ghidra headless has no timeout.

```sh
export JAVA_HOME=/opt/homebrew/opt/openjdk@21
export PATH="$JAVA_HOME/bin:$PATH"
mkdir -p "$PWD/re-local/ghidra" "$PWD/re-local/dump"
/opt/homebrew/opt/ghidra/libexec/support/analyzeHeadless "$PWD/re-local/ghidra" Siedler1 \
  -import "$SIEDLER_DIR/Siedler1.exe" -overwrite \
  -scriptPath "$PWD/tools/GhidraDump" -postScript DumpProgram.java "$PWD/re-local/dump"
```

Run it from the repository root. The project path must be absolute, and no element of it may start with
a dot: Ghidra headless rejects such a path.

The run leaves a Ghidra project in `re-local/ghidra`. To run a script on it again without a new
analysis, replace `-import "$SIEDLER_DIR/Siedler1.exe" -overwrite` with `-process Siedler1 -noanalysis`.

Run one analysis at a time on a machine. Two Ghidra analyses at once can overload the machine.

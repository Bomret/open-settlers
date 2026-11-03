# Verification

```sh
mise run check
```

```sh
mise run test
```

The full command restores the local tools, checks formatting (CSharpier names each unformatted file;
`mise run format` fixes them), runs the unit tests, and builds the project. `mise.toml` holds the
steps.

The focused command runs the unit tests alone. They build their fixtures in code and need neither an
install nor Godot.

No timing command: the test runner's per-test durations are not wired in.

## Test paths

- `tests/`
- `*.Tests/`

## Map view Definition-of-Done lines

Verify a Definition-of-Done line about what a player sees in the map view with a play run, and judge
the result yourself. Do not ask the operator to play. Write a play script in `play/` that sets up the
situation and takes named screenshots and dumps, then run it under Movie Maker through the play-run
wrapper, from the repository root, with `SIEDLER_DIR` set:

```sh
sh tools/play-run.sh <stem> [--seed <seed>] [--size <n>] --fps <fps>
```

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
  path), `project.godot`, `OpenSettlers.csproj`, `global.json`, the play script, the seed, the size, the
  frame rate, the Godot version, and the `SIEDLER_DIR` path. It leaves out the install's content and
  `.godot/`. After a change to the install, run with `--fresh`.
- A failed build prints the build output, starts no Godot, stores nothing, and exits 3. Bad arguments
  exit 2 with a message and run, store, and log nothing.
- Each call appends one tab-separated line to `play-out/runs.log`: UTC time, input hash, `HEAD`, `clean`
  or `dirty` (an untracked file counts), stem, `seed=<s>`, `size=<n>`, `fps=<n>`, `exit=<status>`, and
  `run`, `reused`, or `build` (a failed build). A line proves a run at a tip when its `HEAD` equals the
  tip and it reads `clean`.
- Calls for one stem run one at a time: a second call waits on the lock directory
  `play-out/.lock-<stem>`, says so once on stderr, then hashes its inputs and reuses what the first
  stored. Other stems run in parallel. The lock holds its owner's pid, and a call takes over the lock of
  a dead owner, and of a lock with no pid older than 5 seconds. Remove a lock by hand only when no run is live.

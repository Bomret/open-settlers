---
name: verify
description: Start, drive, inspect, and stop this repository's app against a change.
disable-model-invocation: true
---

# Verify

The app is the Godot map view and game view. A play run starts it under Movie Maker, drives it by a play script,
writes screenshots and dumps, and exits on its own. `docs/agents/verification.md`, "Map view Definition-of-Done lines",
holds the full contract of the play-run wrapper. This file is the short path through it.

## Start

From the repository root, run the play-run wrapper in the background:

```sh
mise run play -- <stem> [--layout layouts/<file>.jsonc | --game-view --seed <seed> --size <n>] [--content-set basic] --fps 10 --fresh
```

`<stem>` names the play script `play/<stem>.play` (see `## Drive`). `--fresh` skips the result cache in
`play-out/.cache/`, so the run builds and runs the change under test. The wrapper builds `OpenSettlers.csproj`, then
runs Godot once.

Readiness is the wrapper's exit, since a play run ends by itself:

- `0`: the run finished, and every `expect` held.
- `1`: a failed `expect`, or a problem in the script. Read the output for the step.
- `2`: bad arguments. `3`: the build failed, and its output is printed. `4`: the window started at a size other
  than 1280 x 720, and nothing is stored.

A code-style violation fails the build with exit 3 (`docs/agents/verification.md`).

## Drive

Write a play script `play/<stem>.play` that sets up the situation for each acceptance item and takes a named
`shot` or dump of it. `README.md`, "Run a play script", lists every step.

- Reuse a layout from `layouts/` before you write one, and reach the moment with `fast-forward`, not clicks
  (`docs/agents/verification.md`, "Layouts: reach a moment without clicks").
- Crop a shot to the place under test: `shot <name> <column> <row> [radius] [scale]`.
- Check fields and events with `expect`, `expect-count`, `expect-event`, and `expect-gone`, so the exit status
  judges the item.
- The script is part of the change: commit it with the change, as the repository's other play scripts are.

## Inspect

- `play-out/<stem>/`: the dumps, the named shots (open the images), and the Movie Maker frames.
- The wrapper's stdout and stderr: `WARN` and `ERROR` lines, and the failing step on exit 1.
- `play-out/runs.log`: one line per call. A line proves a run at a tip when its `HEAD` field equals the tip and it
  reads `clean`.

## Stop

A play run stops by itself, so Stop only makes sure no run of this checkout is left.

1. List the locks `play-out/.lock-*` of this checkout. For each lock whose pid file names a live process
   (`ps -p <pid> -o pid=` prints it), send that process `TERM` and wait for it to exit. The wrapper removes its own
   lock on exit.
2. Leave every other file in place. The wrapper and the build write only under ignored paths (`play-out/`, `.godot/`,
   `bin/`, `obj/`), so `git status --porcelain` reads as before Start.

Stop succeeds when no live pid holds a lock of this checkout, also when no lock exists. It never acts on another
checkout's `play-out/`.

## Isolation

Each checkout and worktree has its own `play-out/`, `.godot/`, `bin/`, and `obj/`, so play runs of two checkouts
run side by side. Within one checkout, calls for one stem run one at a time through `play-out/.lock-<stem>`, and
other stems run in parallel. A play run opens no port and shares no data store.

## Untracked inputs

- `SIEDLER_DIR`: the path of a History Edition install, which `mise` reads from the gitignored `mise.local.toml`.
  A run with `--content-set basic` needs none. A checkout without the file runs only the basic set, or copies the
  file from the main checkout.
- The tools `mise install` provides: the .NET SDKs and Godot 4.7.2 .NET.

## May touch

- `play-out/` of this checkout: the run output, the result cache, the locks, and `runs.log`.
- `.godot/`, `bin/`, and `obj/` of this checkout: the build output and Godot's import cache.
- The install under `SIEDLER_DIR` is read and never written.

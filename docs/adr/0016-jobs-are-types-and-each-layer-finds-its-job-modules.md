# 16. Jobs are types, and each layer finds its job modules

**Status:** Accepted

Builds on [ADR 0013](0013-the-simulation-is-typed-and-the-original-s-bytes-stay-at-its-edge.md) and
[ADR 0014](0014-the-map-view-draws-through-a-graphics-source.md).

A job is a type: `Job` is an abstract record that holds the original's job number, and each job the engine ports
is a sealed record of its own. A number the engine does not port is an `UnportedJob`. A settler holds a job and
keeps one object for its whole life; it takes up another job without being replaced.

Each layer holds a job's code in a job module of its own: the domain the job and its data, the application the
job's rules, the adapters the job's tables read from `Siedler1.exe` and its names, and the map view the job's
look. No layer keeps a hand-written list of jobs. A source generator, run in each compilation, finds the job
modules and writes the registry of that layer, and the number-to-job lookup of the domain. The carrier, the
castle's own settler, the jobless settler, and the knights are jobs too; their rules stay the core every job
uses, wired by hand.

## Considered options

The decision weighed these options:

- A class per job that derives from `Settler`: a settler changes its job and stays the same settler, and a
  class cannot change, so each change would replace the object and every reference to it would go stale.
- A new `Settler` object with the new job on each change, the old one released: the code that changes a job
  goes on using the old object, and a refilled slot gives the settler a new thing ID and cuts its history.
- `Job` kept as a value of one number, with a constant per job: it saves the migration, but a new job still
  edits the list of constants.
- A reflection scan of each assembly at startup: small, but a malformed module fails only at run time, where
  the generator fails the build.

## Consequences

The decision has these consequences:

- A new job is added as its own files in each layer, with no edit to a shared list. The data the original holds
  about it is read by its own tables module.
- The shared states (the door, the free walk, the step out) call a job's hooks instead of asking which job it is.
- The build carries a generator project that every compilation of the engine and of the tests references.
- The order of the modules a registry holds is fixed by their type names, so a game plays the same on every
  run.

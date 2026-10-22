# 3. Rule fidelity, not proven state identity

**Status:** Accepted

The engine reproduces the rules and constants of `SIED.EXE`, each with its address as evidence. The
engine advances a deterministic simulation in fixed ticks. Where the program shows an order or a
timing, the engine follows it. Examples are which carrier moves first and how many ticks a step takes.
The engine does not prove that its game state equals the original's tick for tick. Tests check only the
random generator and the map generator bit for bit, because a map seed must give the original's map.

## Considered options

The decision weighed these options:

- Proven state identity: the strongest proof, but it needs a trace oracle as its own milestone. A trace
  oracle is a tool that compares the engine's state with memory traces that the DOSBox debugger records
  from the original.
- Rules and constants only, with orders and timings free: cheaper, but behavior that players notice
  drifts from the original, for example congestion at a flag.

## Consequences

The decision has these consequences:

- The project builds no trace oracle before the free-play release.
- Tests check the generators against short sequences of values observed in a run of the original, and
  against a digest of each generated map. They never check against a dump.
- The engine does not load save games of the DOS original. The format of those save games is unknown.
  If that format mirrors the original's memory structures, only proven state identity can fill those
  structures. The engine has its own save format.

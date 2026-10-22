# 3. Rule fidelity, not proven state identity

**Status:** Accepted

The engine reproduces the rules and constants of `SIED.EXE`, each with its address as evidence, and
advances a deterministic simulation in fixed ticks. Where the program shows an order or a timing, such as
which carrier moves first or how many ticks a step takes, the engine follows it. The engine does not prove
that its game state equals the original's tick for tick. Only the random generator and the map generator
are checked bit for bit, because a map seed must give the same map as in the original.

## Considered options

- Proven state identity, checked against memory traces of the original in the DOSBox debugger: the
  strongest proof, but it needs a trace oracle as its own milestone.
- Rules and constants only, with orders and timings free: cheaper, but behavior that players notice, such
  as congestion at a flag, drifts from the original.

## Consequences

- No DOSBox trace oracle is built before the free-play release.
- Tests check the generators against short sequences of values observed in a run of the original, and
  against a digest of each generated map, never against a dump.
- Save games of the DOS original are not loaded. Their format likely mirrors the original's memory
  structures, which only proven state identity could fill; the engine has its own save format.

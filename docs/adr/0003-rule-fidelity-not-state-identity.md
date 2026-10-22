# 3. Rule fidelity, not state identity

**Status:** Accepted

The engine reproduces the rules and constants of `SIED.EXE`, each with its address as evidence, and
advances a deterministic simulation in fixed ticks. It does not reproduce the original's game state tick
for tick. Only the random generator and the map generator are bit-exact, because a map seed must give the
same map as in the original. State identity would force the engine to copy every iteration order of the
original, such as which carrier moves first, for a proof that players cannot see.

## Considered options

- Bit-exact state everywhere, checked against memory traces of the original in the DOSBox debugger: the
  strongest proof, but it needs a trace oracle as its own milestone and replication of every iteration
  order of the original.

## Consequences

- No DOSBox trace oracle is built before the free-play release.
- Save games of the DOS original are not loaded. Their format likely mirrors the original's memory
  structures, which only state identity could fill; the engine has its own save format.

# 8. Reference values come from emulating the original

**Status:** Accepted

The values that the random generator and map generator tests compare with
([INV-0001](../invariants.md#inv-0001), [INV-0003](../invariants.md#inv-0003)) come from running the
original's own routines in an emulator on the developer's machine. A run of the original game in Windows
under a debugger confirms the emulator's result for at least one seed and map size.

Reading a routine gives the rule. It does not give an independent expected value: a value worked out
from the same reading repeats any misreading, and the test passes anyway. The emulator runs the
original's code, so its values are independent of the engine's port.

## Considered options

The decision weighed these options:

- Capture every value from the original game in Windows under a debugger. It needs a person at the
  keyboard for each capture.

## Consequences

The decision has these consequences:

- The repository holds the emulator harness, never the values it reads from the program's memory beyond
  the short sequences and digests INV-0001 allows.
- A digest covers a canonical form of the map that both the engine and the harness write, because the
  engine does not hold the original's memory layout ([ADR 0003](0003-rule-fidelity-not-state-identity.md)).
- A test names the seed, the map size, and whether its value came from the emulator or the Windows run.

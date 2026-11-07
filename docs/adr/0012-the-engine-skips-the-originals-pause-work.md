# 12. The engine skips the original's work during a pause

**Status:** Accepted

Amends [ADR 0005](0005-a-tick-is-one-pass-of-eight-clock-units.md): it corrects two statements about
the original and settles the review that ADR 0005's pause consequence calls for.

ADR 0005 says that `Sim_StepPhase` advances the phase by 1 on each pass. In a game with 49 phases, the
routines of phases 33 to 48 (`0x4293f6`, `0x42944d`, `0x4294a4`, `0x4294fb`) can advance the phase
further in the same pass, so a pass advances the phase by 1 or more
([timing](../rules/timing.md#rules-that-count-passes)).

ADR 0005 also says that whether the original's work during a pause changes the game state is not known.
Part of it does. While the original is paused, the clock step is 0, and the main loop still calls the
same routines on each pass (`0x425bce` to `0x425bf1`):

- The ambient sounds of each view draw from the random generator through `Rng_Step` at `0x466ade`
  (`0x42899a`), in every game.
- `Sim_StepPhase` advances the phase and calls the phase-table routine before it reads the clock step
  (`0x4291c2`). At phase 32, `0x42965c` calls `0x42b18d`, which writes to the player blocks, before it
  reads the clock step (`0x42965c` to `0x42966e`).
- In a game with 49 phases, the routines of phases 33 to 48 call `Rng_Step` for each player block with
  bits 7 and 6 of byte 2 set, without reading the clock step (`0x429427`). `0x46986d`, which they call
  after a low draw, returns at once while the clock step is 0 (`0x469870` to `0x46987a`).

Whether the item-table routines and the other routines of the pass change the game state during a pause
is still unknown.

The engine still runs no ticks during a pause. The random generator and the player blocks therefore
differ from the original's after a pause.

## Considered options

The decision weighed these options:

- Run the draws of phases 33 to 48 during a pause, in passes that advance no clock. The engine does not
  take the ambient-sound draws ([woodcutting](../rules/woodcutting.md)), so its random sequence already
  differs from the original's on every pass. Taking the pause draws as well would not bring the computer
  players' later choices in line with the original's. During a pause the computer players do no work
  besides the draws, because `0x46986d` returns at once.

## Consequences

The decision has these consequences:

- A pause changes no state of the engine. Under ADR 0003, the engine does not reproduce the original's
  random sequence or its player blocks across a pause.
- If the pause-time work of `0x42b18d`, of the item tables, or of the other routines of the pass turns
  out to change a rule's outcome after the pause ends, this decision needs a review.

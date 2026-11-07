# 5. A tick is one pass of 8 clock units

**Status:** Accepted

**Amended by:** [ADR 0012](0012-the-engine-skips-the-originals-pause-work.md)

The engine's tick is one pass of the original's main loop at its shortest length: 4 timer calls, 8
units of the game clock. At normal speed this is 80 milliseconds, 12.5 ticks per second. On each tick
the engine runs the work that the original runs once per pass, and it advances every rule that counts
clock units by 8. Rule timers keep the original's constants in clock units, and they keep the
original's comparisons and reloads. The game-speed switch changes how many ticks run per second. It
does not change the step. While the game is paused, the engine runs no ticks.

The original has two kinds of rules ([timing](../rules/timing.md)):

- Rules that count passes. `Sim_StepPhase` advances the phase by 1 on each pass, and the routines for
  the item tables and the per-player updates do their work by phase. These rules do not read the
  game clock.
- Rules that count clock units. They subtract the elapsed units of the pass from a countdown, or they
  compare the current clock with a stored mark.

The frame wait at `0x45d718` makes a pass take at least 4 timer calls, so the elapsed units of a pass
are at least 8. On a machine fast enough to finish a pass within 4 timer calls, every pass advances
the clock by exactly 8 units. A tick of 8 units keeps both kinds of rules at the rate they have
relative to each other on such a machine.

## Considered options

The decision weighed these options:

- A tick of one timer call, 2 clock units, as `CONTEXT.md` defined it: rules that count passes then run
  4 times as often relative to the clock as in the original. The item-table work, the per-player
  updates, and the random draws of the phase table would all run at 4 times their rate.
- A tick of one timer call, with the work that counts passes on every fourth tick: equal in effect to
  the chosen option, with a second unit that every rule must state.
- A tick with a variable step, as the original computes it: the step depends on how long a pass takes,
  which breaks INV-0002. A fixed step that equals the original's elapsed units gives the same result
  whenever the original keeps up.

## Consequences

The decision has these consequences:

- The engine reproduces the original on a machine that keeps up with the frame wait. When a pass of the
  original takes more than 4 timer calls, the original's pass-counting rules fall behind its clock. The
  engine does not reproduce this.
- A rule constant that is not a multiple of 8 keeps its value. A countdown that fires once fires on the
  first tick that takes it past its limit, as in the original. The 20-unit period at `0x428d10` and the
  1500-unit interval of `Stats_RecordHistory` keep their long-run rate, because their reloads carry the
  remainder.
- The game clock advances in steps of 8. The engine has its own save format (ADR 0003), so a game
  clock with an odd number of units from a save of the original does not arise.
- While the original is paused, its main loop still advances `Sim_Phase` and still runs the routines
  for the item tables with 0 elapsed units. Whether that work changes the game state is not known. The
  engine runs no ticks during a pause. If the item-table work turns out to change the state during a
  pause, this decision needs a review.

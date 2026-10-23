# Timing

The program has a fixed-rate timer and a game clock. The simulation does not advance in fixed steps: each
pass of the main loop advances the rules by the number of clock units that elapsed since the last pass.

## The timer

**Status:** confirmed. **Platform layer.**

A timer thread at `0x406c90` calls the game code's timer entry point at `0x479cb2` at a fixed rate. It takes
the rate in calls per second from the value at `0x67d608`, which holds 50 when the program starts. At
normal speed, the timer handler therefore runs every 20 milliseconds.

The rate varies with the History Edition's game-speed switch. The routine at `0x405fe0` stores the speed
factor (1, 2, 4, or 8) at `0x674048` and sets the rate to 50, 100, 200, or 400 calls per second to
match. Two further values of the speed factor, set by the code at `0x4168c7` and `0x4168d5`, make the timer thread
run without waiting. The DOS original's timer rate is not confirmed by this program.

The changed mouse handling at `0x47a4c3` multiplies its timing thresholds by the speed factor.
**Inferred:** this keeps mouse timing the same in real time at every speed.

## The game clock

**Status:** confirmed.

The entry point at `0x479cb2` calls the timer handler at `0x479cc0`. The timer handler does three things
on each call:

- It counts the call in a 16-bit interrupt counter.
- It adds the clock step to the game clock. The game clock is a 32-bit fixed-point number with 16
  fraction bits, so its upper 16 bits count whole clock units.
- It runs the input handling for both players' pointers (`0x47a4c3`, called twice; inferred from what
  that routine updates).

The clock step is 2.0 while a game runs: the routine at `0x41e710` sets it when a game starts. Pause sets
the step to 0 (`0x45772e`) and restores it afterwards (`0x457751`). The main loop at `0x426adf` calls the
pause when the current key code equals the code of `P` (inferred: the pause key).

The clock step holds only 2.0 or 0. The three writes above are its only writers, and the timer handler is
the only code that adds it to the game clock. The whole clock units therefore advance in steps of 2, one
step per timer call.

**Inferred:** at normal speed the game clock advances 100 units per second, 2 units every 20
milliseconds. This follows from 50 timer calls per second and a step of 2.0 per call.

The save code at `0x45fbf8` writes the game clock into the save game, and the load code at `0x4606bc`
reads it back.

## How the main loop advances the simulation

**Status:** confirmed.

The main loop starts at `0x425a10`. On each pass it calls the routine at `0x426dfc`. That routine takes the
whole clock units of the game clock, subtracts the value it saw on the previous pass, and keeps the
difference as the elapsed units of this pass. The rules then advance by that difference: for example,
the pass adds it to a running total and subtracts it from a countdown (`0x426e8b`). The main loop sets
that countdown to 6000 units when a game starts (`0x425a44`). **Inferred:** 6000 units are 60 seconds
at normal speed.

Because the whole clock units advance in steps of 2, the elapsed units of a pass are always even.

## The frame wait

**Status:** confirmed.

Each pass of the main loop calls the routine at `0x45d718` before it presents the frame. That routine
waits until the interrupt counter is at least 4 above the value it stored at the previous frame, then
stores the new value. A pass therefore takes at least 4 timer calls: at least 80 milliseconds at normal
speed. While the clock runs, the elapsed units of a pass are at least 8. The wait itself lies in the
game code; it waits through a platform routine at `0x406c60` that hands control between the threads.

A routine at `0x425c9c` resets the interrupt counter and waits until the next timer call has happened.
Its callers are unknown: the program holds no direct call to it and no reference to its address as a
value. Whether any code reaches this routine is unknown.

## Rules that count passes

**Status:** confirmed.

While a game runs, each pass of the main loop calls these routines once, in this order: `0x4286d8`,
`Sim_StepPhase` at `0x4291c2`, then the routines for the item tables at `0x46414d`, `0x42cb2d`, and
`0x42f3d9`. `Sim_StepPhase` adds 1 to the phase counter and wraps it at the phase count. The routine at
`0x42055e` sets the phase count when a game starts: 33, or 49 when a player's start parameter at
`+0x1d6` is from 1 to 11.

The phase selects the work of a pass:

- Phases 0 to 31: the per-player update for player `phase & 3`, and a slice of each item table.
  `Sim_StepPhase` skips the per-player update while the clock step is 0.
- Phases from 32: one routine from the phase table at `0x4292ee`. The routine at `0x42a74a`, reached
  from that table, copies the running total of the elapsed units and resets it.

Which work runs counts passes, not clock units: each entry of an item table gets its turn once per
phase cycle, however many clock units the cycle takes. The work on a turn can itself count clock units,
as the countdowns of `Table18` do (see the next section).

While the clock step is 0, the main loop still advances the phase and still calls the routines for the
item tables, with 0 elapsed units. Whether that work changes the game state is unknown.

## Rules that count clock units

**Status:** confirmed.

`Main_ComputeElapsed` at `0x426dfc` stores the whole units of the current pass at `+0x206`. Rules read
either those units or the elapsed units at `+0x284`. They count in three ways:

- A countdown that fires once. The rule subtracts the elapsed units and fires when the countdown goes
  below zero. The two notice countdowns at `+0x17e` and `+0x182` post a message then. The timed
  messages of a player (`0x44cb8f`) work the same way. `Game_Countdown` stops at 0 instead. A countdown
  in each view record fires when it reaches 0 or goes below it (`0x426f91`).
- A countdown per entry. Each entry of `Table18` that `0x42cb2d` processes keeps the clock units of its
  last turn. On its turn, the rule subtracts the units since then from the entry's countdown and fires
  when the countdown goes below zero.
- A period with a remainder. The rule fires when the clock has passed the period, and it reloads by
  adding the period to the old value, so the remainder carries into the next period. A rule that fires
  by a countdown repeats until the countdown is no longer below zero: `0x428d10` with a period of 20
  units, and a timer of each player block in `0x428a75`. A rule that compares the clock with a stored
  mark fires at most once per pass and moves the mark by the period: `Stats_RecordHistory` every 1500
  units, and `0x42639a` every 6000 units.

The constants that start these counts include 6000, 180000, and 360000 units. The 20-unit period and
the 1500-unit interval are not multiples of 8.

Only the readers of the clock fields that the dump lists for the game state block are checked
(`re-local/dump/state_fields.tsv`). Code that reaches these fields through another pointer is not
covered.

## Consequence for the engine

The engine's tick is one pass of 8 clock units
([ADR 0005](../adr/0005-a-tick-is-one-pass-of-eight-clock-units.md)). On a machine that finishes a pass
within 4 timer calls, every pass of the original advances the clock by exactly 8 units, so the tick
keeps the rate of the pass-counting rules relative to the clock-counting rules.

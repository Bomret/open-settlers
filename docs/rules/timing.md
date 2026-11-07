# Timing

The program has a fixed-rate timer and a game clock. The simulation does not advance in fixed steps: each
pass of the main loop advances the rules by the number of clock units that elapsed since the last pass.

## The timer

**Status:** confirmed. **Platform layer.**

The timer thread `Timer_ThreadMain` at `0x406c90` calls the game code's timer entry point `Timer_GameEntry` at `0x479cb2` at a fixed rate. It takes
the rate in calls per second from the value at `0x67d608`, which holds 50 when the program starts. At
normal speed, the timer handler therefore runs every 20 milliseconds.

The rate varies with the History Edition's game-speed switch. `Speed_SetFactor` at `0x405fe0` stores the speed
factor (1, 2, 4, or 8) at `0x674048` and sets the rate to 50, 100, 200, or 400 calls per second to
match. Two further values of the speed factor, set by the code at `0x4168c7` and `0x4168d5`, make the timer thread
run without waiting. The DOS original's timer rate is not confirmed by this program.

The changed mouse handling in `Input_UpdatePointer` at `0x47a4c3` multiplies its timing thresholds by the speed factor.
**Inferred:** this keeps mouse timing the same in real time at every speed.

## The game clock

**Status:** confirmed.

`Timer_GameEntry` calls the timer handler `Timer_Handler` at `0x479cc0`. The timer handler does three things
on each call:

- It counts the call in a 16-bit interrupt counter.
- It adds the clock step to the game clock. The game clock is a 32-bit fixed-point number with 16
  fraction bits, so its upper 16 bits count whole clock units.
- It runs the input handling for both players' pointers (`Input_UpdatePointer`, called twice; inferred from what
  that routine updates).

The clock step is 2.0 while a game runs: `Game_StartClock` at `0x41e710` sets it when a game starts. Pause sets
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

The main loop `Main_Loop` starts at `0x425a10`. On each pass it calls `Main_ComputeElapsed` at `0x426dfc`. That routine takes the
whole clock units of the game clock, subtracts the value it saw on the previous pass, and keeps the
difference as the elapsed units of this pass. The rules then advance by that difference: for example,
the pass adds it to a running total and subtracts it from a countdown (`0x426e8b`). The main loop sets
that countdown to 6000 units when a game starts (`0x425a44`). **Inferred:** 6000 units are 60 seconds
at normal speed.

## The frame wait

**Status:** confirmed.

Each pass of the main loop calls `Frame_WaitAndPresent` at `0x45d718` before it presents the frame. That routine
waits until the interrupt counter is at least 4 above the value it stored at the previous frame, then
stores the new value. A pass therefore takes at least 4 timer calls: at least 80 milliseconds at normal
speed. While the clock runs, the elapsed units of a pass are at least 8, and always even because the clock advances in steps of 2. The wait itself lies in the
game code; it waits through the platform routine `Frame_Handshake` at `0x406c60` that hands control between the threads.

A measurement on the History Edition agrees. The probe `tools/WindowsCheck/elapsed-probe.ps1` polled the
game state block of a running game. It tallied `Clock_Elapsed` only for the passes that advanced
`Sim_Phase` by exactly 1 with the clock running, which leaves out paused passes, passes that ran several
phases (see "Rules that count passes"), and passes the poller missed. All 5921 tallied passes had 8
elapsed units: 1884 at speed 1 and 4037 at speed 8. The totals that `0x42a74a` copied were 264 to 296
units per phase cycle of 49 phases, multiples of 8 that match 33 to 37 passes of 8 units each.

`Timer_WaitNextCall` at `0x425c9c` resets the interrupt counter and waits until the next timer call has happened.
Its callers are unknown: the program holds no direct call to it and no reference to its address as a
value. Whether any code reaches this routine is unknown.

## Rules that count passes

**Status:** confirmed.

While a game runs, each pass of the main loop calls these routines once, in this order: `0x4286d8`,
`Sim_StepPhase` at `0x4291c2`, then the routines for the item tables at `0x46414d`, `0x42cb2d`, and
`0x42f3d9`. `Sim_StepPhase` adds 1 to `Sim_Phase`, wraps it at the phase count, and calls the
routine for the new phase from one of two tables: the table at `0x42923c` for phases 0 to 31, and the
table at `0x4292ee` for phase 32 and up. The routine at `0x42055e` sets the phase count when a game
starts: 33, or 49 when a player's start parameter at `+0x1d6` is from 1 to 11.

The phase selects the work of a pass:

- Phases 0 to 31: the per-player update for player `phase & 3`, and a slice of each item table.
  `Sim_StepPhase` skips the per-player update while the clock step is 0 (`0x429200` to `0x42920d`), and
  while `Table70_Count` (`+0x25e`) is 50 or more (`0x42920e` to `0x429221`).
- Phase 32: the routine `0x42965c`. It calls `0x42b18d` first. While the clock runs, it then calls
  `0x42a74a`, which copies the running total of the elapsed units and resets it, and further routines.
- Phases 33 to 48: the routines at `0x4293f6`, `0x42944d`, `0x4294a4`, and `0x4294fb`, one per player
  block in turn. Each tests bits 7 and 6 of byte 2 of its player block (inferred: the block marks an
  active computer player). When both are set, the routine draws from the random generator through
  `Rng_Step` at `0x466ade`. When the draw is below the player block's `+0x1ae`, it calls `0x46986d`
  and returns: the pass runs no further phase, and the item-table routines of the pass still run.
  Otherwise the routine adds 1 to `Sim_Phase` itself and goes on to the next player's routine in the
  same pass. `0x4294fb` goes back to `0x4293f6` while `Sim_Phase` is below the phase count
  (`0x42956c`), and sets `Sim_Phase` to 0 when it reaches the count. The routines therefore go round
  the player blocks until a draw leads to the call of `0x46986d` or the phase reaches the phase count.

A pass therefore runs one phase from 0 to 32, or one or more phases from 33 to 48. A phase cycle takes
33 passes when the phase count is 33, and from 33 to 49 passes when it is 49. When the routines of
phases 33 to 48 set `Sim_Phase` to 0, the item-table routines of that pass process the slices of phase
0, and the next pass starts at phase 1. The per-player update of phase 0 then does not run in that
cycle. When a draw at phase 48 leads to the call of `0x46986d`, `Sim_Phase` stays at 48,
`Sim_StepPhase` wraps it to 0 on the next pass, and the per-player update of phase 0 runs.

Which work runs counts passes, not clock units: each entry of an item table gets its turn once per
phase cycle, however many clock units the cycle takes. The work on a turn can itself count clock units,
as the countdowns of `Table18` do (see the next section).

While the clock step is 0, the main loop still calls the same routines on each pass, with 0 elapsed
units (`0x425bce` to `0x425bf1`): `0x427062`, `0x4286d8`, `Sim_StepPhase`, the routines for the item
tables at `0x46414d`, `0x42cb2d`, and `0x42f3d9`, then `0x42f4f1` and `0x425cb9`. This work changes the
game state:

- The ambient sounds of each view draw from the random generator through `Rng_Step` (`0x428985`, the
  call at `0x42899a`), in every game, for each view whose byte 0 has bit 0 clear; `0x428985` returns
  without a draw when that bit is set (`0x42898d` to `0x428999`).
- `Sim_StepPhase` advances the phase. At phase 32, `0x42965c` calls `0x42b18d` before it reads the
  clock step (`0x42965c` to `0x42966e`); `0x42b18d` writes `+0x188` and `+0x184` of each player block
  through `0x42b1ce`. Phase 32 then returns before it calls `0x42a74a`.
- In a game with 49 phases, the routines of phases 33 to 48 draw through `Rng_Step` without reading
  the clock step (`0x429427`). `0x46986d` returns at once while the clock step is 0 (`0x469870` to
  `0x46987a`).

Whether the item-table routines and the other routines of the pass change the game state during a
pause is unknown. The engine skips all of this work
([ADR 0012](../adr/0012-the-engine-skips-the-originals-pause-work.md)).

## Rules that count clock units

**Status:** confirmed.

`Main_ComputeElapsed` stores the whole units of the current pass at `+0x206`. Rules read
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

## In the engine

The engine's tick is one pass of 8 clock units
([ADR 0005](../adr/0005-a-tick-is-one-pass-of-eight-clock-units.md)): 4 timer calls, 12.5 ticks per
second at normal speed. `Game.Tick` in `src/Simulation/Game.cs` applies the commands due at the tick,
then advances the tick count; the view calls it 12.5 times per second of frame time and carries the
fraction. Each tick then runs one pass of the item tables' routines: the retry scan, the phase step, the slice of the flag
list (the carriers' requests only: [Carriers](carriers.md#the-flag-pass)), and the slices of the building and settler lists
([Settlers](settlers.md#the-pass)), then, with `Game.EveryPassTurns`, one more turn for every settler in a state the view draws in motion; the clock starts at 0 and gains 8 a pass, and
the phase at 0 goes to 1 on the first pass. The players' timers are not in the engine yet.

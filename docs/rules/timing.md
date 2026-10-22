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
the pass adds it to a running total and subtracts it from a countdown that starts at 6000 units
(`0x425a44`). **Inferred:** 6000 units are 60 seconds at normal speed.

Because the whole clock units advance in steps of 2, the elapsed units of a pass are 0, 2, 4, and so on,
never an odd number.

A routine at `0x425c9c` resets the interrupt counter and waits until the next timer call has happened.
Its callers are unknown: the program holds no direct call to it and no reference to its address as a
value. **Inferred:** a pass uses it to wait for the timer.

**Consequence for the engine (inferred):** the rules see time in whole timer calls, so the original
advances them in steps of 2 clock units. The number of steps per pass depends on how long a pass takes.
The engine advances its simulation one timer call per tick, that is, 2 clock units per tick. Rule timers
keep counting in clock units, and each tick advances them by 2. This gives the same result as the
original whenever a pass of the original takes less than one timer call. One possible exception: the game
clock that the load code at `0x4606bc` restores from a save game could hold an odd number of whole units.

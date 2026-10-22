# Game code

## The game code band

**Status:** inferred.

The game code of `Siedler1.exe` lies in the address band `0x41da10` to `0x47b739`. The band starts at
the procedure at `0x41da10` and ends with the procedure at `0x47b4f9`.

The band was found by comparison with the DOS program `SIED.EXE`. Short windows of code were taken
across the whole code section of `Siedler1.exe`. In each window, the operands that hold an absolute
address or a call or jump distance were ignored, and the rest was searched for in `SIED.EXE`. Windows
inside the band match exactly one place in `SIED.EXE`. Windows outside the band match nothing. Some
windows inside the band also match nothing. These are routines that the History Edition changed, or
windows that the comparison could not mask. One changed routine is the mouse handling at `0x47a4c3`,
which reads the platform layer's speed factor (see [Timing](timing.md)).

`SIED.EXE` is not evidence ([ADR 0004](../adr/0004-history-edition-is-the-reference-install.md)). The
comparison only locates the band. The comparison tool is not part of the repository.

The game code keeps the calling style of the original assembly: a routine receives a pointer to the
register and state block in `EDI` and builds no stack frame. The routines at `0x45f755`, `0x479cc0`,
and `0x47a4c3` show this style.

## Outside the band

**Status:** confirmed by the imports that each part calls.

The rest of the code section is the platform layer and its libraries:

- The Windows platform layer: graphics output through DirectDraw, sound output through DirectSound, the
  timer thread, input, and the game-speed switch. For example, the timer thread is at `0x406c90`.
- The Uplay client.
- libcurl and OpenSSL, which the Uplay client uses.

# 4. The History Edition is the reference install

**Status:** Accepted

Amends [ADR 0003](0003-rule-fidelity-not-state-identity.md): the program whose rules the engine
reproduces is `Siedler1.exe`, not `SIED.EXE`.

The reference install is The Settlers History Edition (Ubisoft, 2018). Its program `Siedler1.exe` is a
32-bit Windows (PE32) executable, and the analysis tool REA opens it directly. Its game code is the game
code of the DOS program `SIED.EXE`, rebuilt for Windows: with address operands masked, routines in the
band `0x41f000`–`0x479000` match `SIED.EXE` byte for byte. The rest of the file is a new platform layer
for graphics, sound, timing, and input, together with the Uplay client, libcurl, and OpenSSL. The game's
graphics file `SPAD.PA` is byte-identical in both releases. The project recovers every rule from
`Siedler1.exe` and cites its address there. `SIED.EXE` is not evidence.

## Considered options

The decision weighed these options:

- Keep `SIED.EXE` and write an LE-to-PE converter so that REA can open it: loader work before any rule
  question, for game code that the History Edition already holds in a format REA reads.
- Read the History Edition, keep `SIED.EXE` as the authority, and cite both addresses through a byte
  matcher: double evidence work for every rule, and every contributor needs both releases.
- Make the History Edition the reference, but check the tick rate and the generators once against
  `SIED.EXE`: a matcher kept for two rules. The project accepts the risk of a changed routine instead.
- Dump the loaded LE image from a DOSBox debugger build: the dump still needs a loader.

## Consequences

The decision has these consequences:

- The platform layer is new code. A rule note marks a finding that lives only there, such as the timer
  that drives the game loop.
- Where the rebuild changed a routine of the original, the engine reproduces the History Edition's
  version. Nothing detects such a change.
- Contributors who recover rules need the History Edition. Whether the engine also reads the assets of
  a CD install stays open until the asset milestone.
- The engine loads the save games of neither release, for the reason ADR 0003 gives: both releases
  share the game code, so their save games share its unknown format.
- The releases differ in their sound and music files: the History Edition has `snd/NN.SND` and MIDI
  files, and the CD release has `SOUNDS.PA` and XMI files.

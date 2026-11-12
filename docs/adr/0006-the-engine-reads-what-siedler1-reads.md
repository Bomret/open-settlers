# 6. The engine reads the files that Siedler1.exe reads

**Status:** Accepted

**Amended by:** [ADR 0015](0015-the-repository-ships-content-sets.md)

Amends [ADR 0004](0004-history-edition-is-the-reference-install.md): it settles the question that ADR
0004 left open until the asset milestone.

The engine reads the files of a History Edition install that `Siedler1.exe` reads: the graphics
archives `loca/SPAD.PA`, `loca/SPAE.PA`, and `loca/SPAF.PA`, the text files `loca/eng.txt`,
`loca/ger.txt`, and `loca/fre.txt`, the sound files `snd/NN.SND`, and the music file
`snd/music_1.midi`. The engine reads no other file of the install, and it does not read a DOS CD
install. Every format the engine reads must be recoverable from `Siedler1.exe`, the only program that is
evidence (ADR 0004). The program names no other data file.

## Considered options

The decision weighed these options:

- Also read a DOS CD install: its own formats, `SOUNDS.PA` and XMI music, are not read by
  `Siedler1.exe`, so no address could confirm them.
- Read every data file of the History Edition install, including `BLOCK1.PA`, `BLOCK2.PA`, `COOKIE.PA`,
  `music_2.midi`, `ADLINTRO.midi`, and `MPUINTRO.midi`: `Siedler1.exe` names none of them. The first
  three are named only by the DOS intro program `INTRO.EXE`, which the History Edition replaces with
  `intro.wmv`. Reading them needs `INTRO.EXE` as evidence, for data that no milestone uses.

## Consequences

The decision has these consequences:

- The engine does not support a DOS CD install.
- A file of the install that `Siedler1.exe` does not read is no asset of the engine, even when its
  format is known.

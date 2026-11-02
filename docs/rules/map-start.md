# Map start

The program generates each map from a seed. This note records where the seed comes from and where it enters
the map generation.

## The random generator

**Status:** confirmed.

The game code has one random generator. Its state is three 16-bit words in the game state, and the save
game carries them (save code at `0x45fc29`, load code at `0x4606ee`). `Rng_Step` at `0x466ade` advances
the generator by one step. The same step also appears inline in other routines, for example at
`0x4425a1` and `0x47a374`.

Not all steps come from the main loop. The inline step at `0x47a374` lies in the sound code
`Sound_Play` at `0x47a32d`, which the timer handler runs on the timer thread (`0x479e54`). It steps the generator
whenever a sound starts while the clock runs and bit 5 of the game flags at state `+0x1c8` is set.
**Inferred:** the sequence that the main loop draws therefore depends on when sounds play.

One step works on the three state words, here `a`, `b`, and `c` in the order they lie in the game state
(`+0x212`, `+0x214`, `+0x216`). All arithmetic is on unsigned 16-bit words and wraps. The step computes
`t = (a + b) xor c`, then `c = c + b`, then `b = b xor c` (with the new `c`), rotates `b` and `c` right by
one bit, and stores `t` in `a`. The flags of the step follow `t`; the step keeps every register.
**Emulated:** the harness in `tools/GhidraDump/` ran the step on the reference `Siedler1.exe`. The all-zero
state stays all-zero, the all-ones state gives `0001 8000 7FFF`, and the state `1234 5678 9ABC` gives `F210
53A6 789A` after one step and `6D98 7060 4561` after six. The engine's `RandomGenerator` keeps the same three
words and its tests pin these values.

## The free-game seed

**Status:** confirmed, except where marked.

The free-game seed is 48 bits, held as three 16-bit words. Every 48-bit value is a seed (inferred: no
check of the value was found).

- `FreeGame_NewSeed` at `0x41ee3b` draws three values from the random generator and stores them as the seed. It
  runs once, at program start: its only entry is the jump at `0x41e701` at the end of the start-up
  sequence, and no table holds its address. The generator state it draws from is set during start-up
  (`0x424fca`).
- Opening the free-game screen (`FreeGame_OpenScreen` at `0x4692aa`) draws no new seed. It seeds the generator from the free-game
  seed, combined with fixed masks, and draws the settings of the computer players (`0x4692d4`). Whether
  another action draws a new seed is unknown.
- The screen shows the seed as four groups of 12 bits (`0x467831`). **Inferred:** the player reads and
  enters the seed in this form.
- The code at `0x467ee1` writes edited groups back into the three seed words. **Inferred:** this is where
  the player's input reaches the seed.

**The screen form.** **Emulated:** the four groups are four characters each, and each character is a digit
`1` to `8` that stands for three bits (the digit minus `1`). A group is therefore 12 bits, `1111` is `000`,
and `8888` is `FFF`. No other character is accepted: the typing code (`0x467e12`) takes the error exit at
`0x467f49` for a character below `1` or above `8`.

**The typed form to the words.** The typing code reads 16 digits as two numbers of 24 bits, the first digit
highest, and stores each as three bytes, highest byte first, at the seed's six bytes (`+0x364` to
`+0x369`). The seed words read these bytes lowest byte first. Digits `1234 5678 1234 5678` give the words
`3905 0577 7739`. The original takes the 16 digits as typed, with no separator. The engine accepts those
16 digits, or four groups of four digits separated by single spaces as the screen shows them. It refuses
any other text, with its reason: empty, a length other than 16 characters (after the single spaces of the
grouped form), or a character outside `1` to `8`.

**The words to the screen.** The display code (`0x467831`) does not undo that. **Static:** it reads the
seed's six bytes as words, lowest byte first, and cuts four groups that overlap: the first is the word at
`+0x364` shifted right by 4, the second the double word at `+0x364` shifted right by 8 and masked to 12
bits, the third the double word at `+0x366` shifted right by 12 and masked, and the fourth the word at
`+0x368` masked. The reads and shifts are in the decompiled `0x467547`, and the typing code's byte stores
(highest byte at `+0x364`) are in the decompiled `0x467c03`. The harness runs the same instructions and
agrees. Each group is shown as four digits (`0x4678c7`, bits 11 to 9, 8 to 6, 5 to 3, 2 to 0, each plus
`1`), highest three bits first. The words `1234 5678 9ABC` show as `1554 5133 6816 6385`, and the words
`3905 0577 7739` that the digits `1234 5678 1234 5678` parse to show as `2731 4582 2731 4582`. Bits 0 to 3 of
word 0 never reach the screen. **Inferred:** the typed groups and the shown groups of one seed therefore
differ, apart from groups of all zero bits or all one bits. Whether the History Edition screen shows the
same text is not checked against the running game: the operator can confirm it by typing
`1234567812345678` and reading the screen.

## From the seed to the map

**Status:** confirmed.

When a free game starts, the code at `0x468b3d` combines each of the three seed words with a fixed
16-bit mask and stores the results as the map seed. **Emulated:** the masks are `5A5A`, `A5A5`, and `C3C3`,
combined by exclusive or: the seed `1234 5678 9ABC` gives the map seed `486E F3DD 597F`. It also hands on
the map size value, the free-game size value at state `+0x362` (`Game_SetStartParams` at `0x468717`, which
is the routine's return value); the sizes that value gives are in [Map form](map-form.md#map-sizes).

A mission does not use the free-game seed. The code at `0x4688f4` takes the map seed from the mission's
record instead.

The map generator is `MapGen_Generate` at `0x4216e9`, called from the game start at `0x41eeb9`. It loads the
three words of the map seed into the random generator's state, draws two values, and then runs a fixed
sequence of generation passes. The passes draw their random values from that generator. The order of
the passes, and which of them draw, are in the [Program index](program-index.md#map-generation). The
height and terrain passes, with their draw counts, are in [Map form](map-form.md#the-passes).

The sound code cannot step the generator during map generation: the map generator clears bit 5 of the
game flags at its start and sets it at its end, and the sound code runs only while that bit is set.
**Inferred:** the same map seed and map size therefore give the same map.

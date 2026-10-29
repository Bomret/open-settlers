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

## From the seed to the map

**Status:** confirmed.

When a free game starts, the code at `0x468b3d` combines each of the three seed words with a fixed
16-bit mask and stores the results as the map seed. It also hands on a value that is, inferred from
the start screen's map-size setting, the map size.

A mission does not use the free-game seed. The code at `0x4688f4` takes the map seed from the mission's
record instead.

The map generator is `MapGen_Generate` at `0x4216e9`, called from the game start at `0x41eeb9`. It loads the
three words of the map seed into the random generator's state, draws two values, and then runs a fixed
sequence of generation passes. The passes draw their random values from that generator. The order of
the passes, and which of them draw, are in the [Program index](program-index.md#map-generation).

The sound code cannot step the generator during map generation: the map generator clears bit 5 of the
game flags at its start and sets it at its end, and the sound code runs only while that bit is set.
**Inferred:** the same map seed and map size therefore give the same map.

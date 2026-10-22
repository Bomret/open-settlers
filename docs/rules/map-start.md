# Map start

The program generates each map from a seed. M2 recovers how the random generator and the map generator
work; this note records only where the seed comes from and where it enters the map generation.

## The random generator

**Status:** confirmed.

The game code has one random generator. Its state is three 16-bit words in the game state, and the save
game carries them (save code at `0x45fc29`, load code at `0x4606ee`). The routine at `0x466ade` advances
the generator by one step. The same step also appears inline in other routines, for example at
`0x4425a1` and `0x47a374`.

## The free-game seed

**Status:** confirmed, except where marked.

The free-game seed is 48 bits, held as three 16-bit words. Every 48-bit value is a seed (inferred: no
check of the value was found).

- When the free-game screen opens, the code at `0x41ee3b` draws three values from the random generator
  and stores them as the seed. Each visit to the screen therefore proposes a new seed.
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

The map generator is the routine at `0x4216e9`, called from the game start at `0x41eeb9`. It loads the
three words of the map seed into the random generator's state, draws two values, and then runs a fixed
sequence of generation passes. **Inferred:** the passes draw their random values from that generator,
so the same map seed and map size give the same map.

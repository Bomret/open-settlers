# M7 Military and territory: scope

Goal and done condition: [roadmap, M7](../roadmap.md#m7-military-and-territory). Marks:
[Leads](AGENTS.md#leads).

## Territory

- Owner bits: `0x444e68`, `0x44bd72`, and `0x44a0aa` compare the top three bits of tile byte 1 with a
  player number. The program index calls byte 1 the height, so either the bits hold the owner or the
  survey misread the layer. guess
- `0x444e68`: counts the tiles of one owner in a ring and maps the count to a level through
  `0x4454e7`. guess
- `0x4454e7`: maps a count to steps of 16 at thresholds from 4 to 24. guess
- `0x41e726` and `0x41f6a5`: build the spiral tables at `Map_SpiralTable` (known) and `+0xc4`, which
  bound the search radii. likely

## Attack and loss of a building

- `0x4615cf`: branches on player `+0x100`, plays a sound, and checks the `Table18` items of three types
  near the player position. An attack on or the loss of a castle. guess
- `0x462d88`: a sibling of `0x4615cf`. guess
- `0x462153`: tears down a `Table70` item; also calls `0x4454e7`. guess

## Open questions

- Where are the military buildings, the knight levels, the gold, the weaponsmith, and combat? No lead
  names them.

# M4 Construction: scope

Goal and done condition: [roadmap, M4](../roadmap.md#m4-construction). Marks: [Leads](AGENTS.md#leads).

## Placement

- `0x44b6e6`: computes what a player can build at the cursor and stores it at player `+0x100` and
  `+0x101`. It has more than ten callers. likely
- `0x44ba52`, `0x44bd72`, `0x44be72`: its checks on neighbor tiles; they read the owner bits of tile
  byte 1 and an offset list at `0x674a30`. likely
- `0x45e375`: runs five checks on the tiles around a position: `0x45080d`, `0x45e44b`, `0x45e4ca`,
  `0x45eb4c`, and `0x45f154`. guess
- `0x45f402`: another neighborhood check. guess
- `0x449a63`, `0x449f58`, `0x44a0aa`: update the tiles around the cursor after a placement change.
  guess
- `0x4662e2`, `0x466719`: read the terrain class of a tile and search a bit set; placement or
  territory. guess
- `0x41f6a5`: fills the spiral table of map offsets at `+0xc4` from `Map_SpiralTable`; placement and
  search code reads it. likely

## Construction

- `0x45ed5f`: creates or connects objects after a check passes. guess
- `0x42cf9d`: when a `Table18` timer runs out, clears the tile's object and reruns the placement check.
  A building that burns or is removed, or a temporary object. guess

## Wood and stone

- `0x424175` to `0x4246f4`: 15 routines, each setting the scatter parameters of one kind of map object
  for `MapGen_ScatterObjects` (known). Their order lists the map object kinds, such as trees and stones.
  likely
- `0x428d10`: known. Changes tiles at a random cursor every 20 clock units; tree growth is one reading.

## Open questions

- Where are the builder, the digger, the forester, the sawmill, and the stonecutter? No lead names them.
  Start from the job types in `0x4312ed` (see [M3](m3-first-loop.md)) and the object dispatch `0x42fc81`.

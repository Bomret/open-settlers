# M4 Construction: scope

Goal and done condition: [roadmap, M4](../roadmap.md#m4-construction). Marks: [Leads](AGENTS.md#leads).

## Placement

- `0x44b6e6`: computes what a player can build at the cursor and stores it at player `+0x100` and
  `+0x101`. It has more than ten callers. known ([Build sites](../rules/build-sites.md))
- `0x44ba52`, `0x44bd72`, `0x44be72`: its checks on neighbor tiles; they read the owner bits of tile
  byte 1 and an offset list at `0x674a30`. known ([Build sites](../rules/build-sites.md))
- `0x45e375`: runs five checks on the tiles around a position: `0x45080d`, `0x45e44b`, `0x45e4ca`,
  `0x45eb4c`, and `0x45f154`. guess
- `0x45f402`: another neighborhood check. guess
- `0x449a63`, `0x449f58`, `0x44a0aa`: `Building_Place` writes a building site (its type, needs, target
  height, progress, object code) and its flag; `Flag_MoveDeposit` and `Deposit_MergeInto` move the deposit of
  a node that takes an object. known ([Build sites](../rules/build-sites.md#placing-a-building))
- `0x42d307`, `0x449f18`: the size table and the needs table of the building types. known
  ([Build sites](../rules/build-sites.md#the-four-types))
- `0x4662e2`, `0x466719`: split the road under a node that just took a flag into two roads, and
  link one half to its far flag ([Roads](../rules/roads.md#a-flag-on-a-road)). known
- `0x41f6a5`: fills the spiral table of map offsets at `+0xc4` from `Map_SpiralTable`; placement and
  search code reads it. likely

## Construction

- `0x45ed5f`: creates or connects objects after a check passes. guess
- `0x42cf9d`: `Building_Free`, the end of a burning building: clears the tile's object, frees the slot, and
  judges the node again; where the node state is 5, 6, or 7 and the site fits the type from the size table
  `0x42d307`, it places a building through `0x449a63`. known ([program index](../rules/program-index.md))
- `0x42d55f`: the handler a building under construction goes to, instead of its type's handler. guess
  (the construction slices read it)

## Wood and stone

- `0x424175` to `0x4246f4`: 15 routines, each setting the scatter parameters of one kind of map object
  for `MapGen_ScatterObjects` (known). Their order lists the map object kinds, such as trees and stones.
  likely
- `0x428d10`: known. Changes tiles at a random cursor every 20 clock units; tree growth is one reading.

## Open questions

- The stonecutter, the forester, and the sawmill are the building types 4, 9, and 0x11 (the handlers
  `0x42d987`, `0x42d9c9`, `0x42da0b` of the jump table `0x42cd1d`; names inferred, confirmed by the slices that
  build each type's work). Where are the builder and the digger? Start from the jump tables `0x42cd1d`
  (building types), `0x42fca1` (settler states), and `0x43ca54` (job start routines), and the construction
  handler `0x42d55f`. `0x4312ed` is `Flag_ReleaseGoods`, no table of jobs.

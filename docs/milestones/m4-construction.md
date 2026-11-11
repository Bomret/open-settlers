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
- `0x42d55f`: the handler a building under construction goes to, instead of its type's handler: it keeps the
  rank of the site's requests for planks and stones, and asks for the builder (job 3, request at `0x42d5fc`). known
  ([Materials](../rules/construction-materials.md#the-building-passs-turn-of-a-site),
  [the builder](../rules/construction.md#the-request-for-a-builder))
- `0x42be63`: `Building_RequestWorker`: a building asks the castle for a settler of a job; where none of the job waits, a
  settler without a job takes the job and the tool of the request from the stock (the second way). known
  ([the worker request](../rules/construction.md#the-worker-request))
- `0x43ce20`, `0x43ec82`, `0x43ef20`, `0x43e008`: the builder's start routine (job 3), state 9 (the materials and the blows), the end
  of the building, and state `0x4a` (he steps onto the flag and goes home). known
  ([the builder's turn](../rules/construction.md#the-builders-turn))
- `0x43f0ba`, `0x43f0fa`: the order of the materials (a bit set for a stone) and the step of a blow, per type. known
  ([the builder's turn](../rules/construction.md#the-builders-turn))
- `0x44fa79`: the draw routine's common part for a building under construction: the base, the type's scaffold and the
  finished building grown by the progress word; the object tables `0x44fcfe` and `0x44fd16`. known from the code, no draw run
  ([drawing a site](../rules/construction.md#drawing-a-site))
- `0x43faf4`: the builder's figure handler (the adds `0x500` and `0x580`). known from the code
  ([drawing a site](../rules/construction.md#drawing-a-site))
- `0x42d328`: the handler of the large types under construction: it waits until the ground around the site is levelled (and
  asks for the digger, job 2 with the tool good 16, at `0x42d52a`), then goes on at `0x42d55f`. known
  ([the digger](../rules/digging.md#the-large-site-waits-for-the-ground))
- `0x43cd9d`, `0x43e45e`: the digger's start routine (job 2) and state 8: sixteen passes over the site's node and its six neighbours,
  each changing by one the nodes that stand at the height the pass names, then the site starts. known
  ([the digger](../rules/digging.md#state-8-0x43e45e))
- `0x43fa87`: the digger's figure handler (the adds `0x300` and `0x380`). known from the code
  ([the digger](../rules/digging.md#the-diggers-figure))
- `0x44c151` to `0x44c204`, in `0x44bd72`: the evaluator's third-ring term for a large site that waits for its digger. known
  ([the digger](../rules/digging.md#a-site-that-waits-in-the-build-site-evaluator))
- `0x4617fd`, `0x4312ed`: the site's removal. A site goes through the same take-down as a hut: its worker at work (worker bit) goes home,
  one on the way goes home through the release of the goods at its flag, the fills and the planks needed are cleared, the word at 14 takes the clock,
  and the goods for the site lose their destination. known ([Removal](../rules/removal.md#taking-a-building-site-down))
- `0x464539`: the direction a good takes at once when some road holds a carrier and no storage was reached. known for one road with a
  carrier, the choice among several not recovered ([Goods at a flag](../rules/goods.md))
- `0x42965c`, `0x42a313`, `0x42a35d`, `0x42a3a7`: the phase routine and its three orders of steps, by which the castle hands its
  goods out to the flags whose requests rank highest; the castle's first settler carries each good out and lays it on the castle's
  flag. known for the routine and its order, the carrying out is read from the emulator's runs only
  ([Materials](../rules/construction-materials.md#the-castles-hand-out))
- `0x464117`, `0x4644ab` to `0x464a3f`: the classes of the goods and the request search of a flag's turn. known
  ([Materials](../rules/construction-materials.md#the-request-search))

## Wood and stone

- `0x424175` to `0x4246f4`: 15 routines, each setting the scatter parameters of one kind of map object
  for `MapGen_ScatterObjects` (known). Their order lists the map object kinds, such as trees and stones.
  likely
- `0x428d10`: known. Changes tiles at a random cursor every 20 clock units; tree growth is one reading.

## Open questions

- The stonecutter, the forester, and the sawmill are the building types 4, 9, and 0x11 (the handlers
  `0x42d987`, `0x42d9c9`, `0x42da0b` of the jump table `0x42cd1d`; the stonecutter's name is known: his work cuts
  the stone heaps and lays the stone on the flag, [Stonecutting](../rules/stonecutting.md); the other names are
  inferred, confirmed by the slices that build each type's work). The builder is job 3 (known, [the builder](../rules/construction.md))
  and the digger is job 2 (known, [the digger](../rules/digging.md)). `0x4312ed` is `Flag_ReleaseGoods`, no table of jobs.

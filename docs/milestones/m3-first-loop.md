# M3 First loop: scope

Goal and done condition: [roadmap, M3](../roadmap.md#m3-first-loop). Marks: [Leads](AGENTS.md#leads).

## Item tables

The four item tables, their item sizes, their allocator and release pairs, and how a pass walks them are
settled: [Item tables](../rules/item-tables.md). What is left open:

- `Table70`: flags. Six links to other `Table70` items, one per road direction, and one link to a
  building. It is the node set of the route search below. guess
- `Table18`: buildings, with a timer and a link to a `Table120` item or a flag. guess
- `Table120`: storage inventories; player `+0x108` indexes it. guess
- `Table16`: settlers, with a job byte at item `+0xa`, a state byte at `+0xb`, and a link at `+0xc`.
  guess

## Tick

- `0x4286d8` and its callees `0x428728`, `0x428d10`, `0x428a75`: known (Simulation); the tick loop is in
  [Timing](../rules/timing.md#in-the-engine).
- `0x46414d`: known. The phase's slice of `Table70`. Its per-item worker is `0x464c36`, with the helpers
  `0x464bb2` and `0x45d3ea`. likely
- `0x42cb2d`, `0x42f3d9`: known. The slices of `Table18` and `Table16`.

## Flags, roads, and routes

- `0x4422c5`: places a flag on the tile under the cursor. likely
- `0x442002`: sets up the road-drawing state with six direction slots in the view record. guess
- `0x4617fd`: creates and connects objects, such as flags, roads, and building sites; it uses the table
  allocators. guess
- `0x462153`: tears down a `Table70` item and branches on its type. guess
- `0x462e1d` with `0x463204`, `0x46385b`, `0x463a40`, `0x463ef9`: walks a road or path from a map
  position. guess
- `0x42b4bc` (entry variant `0x42b455`): route search over the `Table70` graph with a work queue at
  `+0xb4` and `+0xb8`. likely
- `0x42ca7a`: advances the search generation at `+0x26e` and clears the mark of every `Table70` item when
  it wraps. likely
- `0x4312ed`: releases the settlers tied to a link value, by job type, when a flag or road goes away.
  likely

## Castle and start

- `0x41eeeb`: known. Calls `0x41f1f4` and `0x41f2d2` for each player.
- `0x41f1f4`: copies a player's start position to player `+0xfc` and `+0xfe` and centers the view.
  likely
- `0x41f2d2`: places the start objects of a player: the castle and its contents. likely
- `0x442704` with `0x442f0c` and `0x44333d`: places a player's first building and adds to player
  `+0x116`. guess
- `0x42055e`: known. Fills the player blocks of a new game.
- `0x42abed`: watches the counts of the player's `Table120` item and posts message 10 when they run out.
  guess

## Build interface

- `0x440b01`: routes a view's clicks to the start screen or the game handlers. likely
- `0x440c24`: in-game click handler; tests map against panel. likely
- `0x440d23`: dispatches road, flag, and build actions and plays the error sounds. likely
- `0x44c768`, `0x44c8cf`: write a button list into view `+0x60`, perhaps the build menu. guess
- `0x442244`, `0x442630`: click actions that reevaluate the build marker. guess

## Open questions

- Which `Table16` job byte is the carrier, and where does a carrier hand a good over at a flag?
- Where is the woodcutter's work cycle? The object dispatch `0x42fc81` and the jump at `0x43f742`
  enumerate the object types.

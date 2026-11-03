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
- `0x41f1f4`, `0x442704`, `0x442630`: known. The castle's placement, its start stock, and the click that
  places it in a free game: [Castle](../rules/castle.md).
- `0x442f0c` with `0x44333d`: known. The castle's 20 first settlers and the tools they take from the stock; the
  engine does not create them yet: [Castle](../rules/castle.md#what-the-placement-writes).
- `0x45e375` with `0x45f402`: likely. The territory claim and the border bits of the castle's flag; the engine
  holds the result of the claim, not the routines.
- `0x41f2d2`: places a group of 3, 6, or 12 settlers of one job on the start node of a mission start. guess
- `0x42055e`: known. Fills the player blocks of a new game.
- The castle's draw position (the node plus 16 east and 8 south, `docs/rules/map-objects.md`): open lead, inferred.
  The castle is a building drawn through `0x44db26` from a `Table70`-style record, and the emulator verbs run no view
  record, drawing surface, or building record, so no verb reaches the blit call. Lead: build the record in the
  emulator (or run the original under a debugger in Windows) and read the position `0x44db26` hands the blit.
- `0x42abed`: watches the counts of the player's `Table120` item and posts message 10 when they run out.
  guess

## Build interface

- `0x440b01`: routes a view's clicks to the start screen or the game handlers. likely
- `0x440c24`: in-game click handler; tests map against panel. likely
- `0x440d23`: dispatches road, flag, and build actions and plays the error sounds. likely
- `0x44c768`, `0x44c8cf`: write a button list into view `+0x60`, perhaps the build menu. guess
- `0x44b6e6`: known. The build-site evaluator behind every click: [Build sites](../rules/build-sites.md).
- `0x442244`: the flag button. It places a flag where the evaluator's site is not 0 and its node state is 4, 6,
  or 7. likely

## Open questions

- Which `Table16` job byte is the carrier, and where does a carrier hand a good over at a flag?
- Where is the woodcutter's work cycle? The object dispatch `0x42fc81` and the jump at `0x43f742`
  enumerate the object types.

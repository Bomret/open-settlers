# M3 First loop: scope

Goal and done condition: [roadmap, M3](../roadmap.md#m3-first-loop). Marks: [Leads](AGENTS.md#leads).

## Item tables

The four item tables, their item sizes, their allocator and release pairs, and how a pass walks them are
settled: [Item tables](../rules/item-tables.md). The fields of a settler, a building, and the storage's list of
waiting settlers are settled as far as the first walk reads them: [Settlers](../rules/settlers.md#the-items). What is
left open:

- `Table70`: the flag items' own fields: the goods at `+0xc` to `+0x13`, their destination words at `+0x14`, the direction bytes
  at `+0x3c` to `+0x41`, and bit 7 of byte 4 are settled: [Goods at a flag](../rules/goods.md#the-fields). The request words at
  `+0x42` to `+0x45` are open (no building asks for a good yet). The status at `+5` and the road bytes at `+6` to `+0xb` are
  settled as far as the carriers read them: [Carriers](../rules/carriers.md#the-flag-pass). likely
- `Table120`: the stock words and the counters of the storage item besides `+0x42` and `+0x4a`; player `+0x108`
  indexes it. guess
- `Table16`: the states other than 1 to 6, 10, 12, 15, and 0x42, and the fields they use (state 3 and 0x42 are settled for a
  carrier with no goods: [Carriers](../rules/carriers.md#the-carriers-walk)). guess

## Tick

- `0x4286d8` and its callees `0x428d10` and `0x428a75`: known (Simulation); the tick loop is in
  [Timing](../rules/timing.md#in-the-engine). `0x428728` is settled: [Settlers](../rules/settlers.md#the-retry-scan).
- `0x46414d`: known. The phase's slice of `Table70`. Its road loop asks for carriers
  ([Carriers](../rules/carriers.md#the-flag-pass)); its goods part (destination by `0x45d3ea`, direction by `0x464aa8`,
  `0x464bb2`, `0x464c36`) is in [Goods at a flag](../rules/goods.md), confirmed against the emulator for a log at a flag with one
  storage. Open leads: the request words of a building, the second carrier of a busy road (`0x462acd`, read, not ported), and the
  priority table of the player's block. The harness verb `wood:` leaves flag slot 0 to the castle's flag, where no good ever
  leaves; `goods:` takes the dummy slot as the original's game start does.
- `0x42cb2d`, `0x42f3d9`: known. The slices of `Table18` and `Table16`: [Settlers](../rules/settlers.md#the-pass).
- `0x42f4f1`: runs the turn of each settler in view once more; the engine does not (the simulation must not depend on the
  view). likely

## Flags, roads, and routes

- `0x4422c5`: known. Places a flag on the node the evaluator judged; with `0x45d7ad`, `0x449f58`, and `0x44a0aa`:
  [Flags](../rules/flags.md). Its second caller `0x440c24` (the click handler) is not recovered: open lead.
- The flag table's slot limit (state `+0x258`, set by `0x41dbea` from a start-up value): open lead, inferred. The
  engine holds 256 flags.
- The flag's colour bits in the object byte (`0x44db26`): open lead, inferred. The engine draws entry 1378 always.
- `0x442002`, `0x44b6ce`, the click step `0x443f62` to `0x44477f` of `0x440c24`, the commit `0x444e68`, and the road
  strips `0x428008`, `0x428105`, and `0x4281cc`: known. Drawing a road, what it writes, and how it is drawn:
  [Roads](../rules/roads.md). Open leads there: view `+1` bit 3 (a click on a road node places a flag), state `+0x37e`
  bit 6 (the cursor update walks the road under a node), a flag put on a road node (does the road split?), and what
  the clear `+4` bit of a water road's items means. The items' bytes at `+5` and the lengths at `+6` are written by the
  commit; no slice reads them yet.
- `0x4617fd`: takes a building or flag item down, not a placement: clears the bits on its node and neighbor, releases
  its slots, and counts the goods back. likely
- `0x462153`: tears down a `Table70` item and branches on its type. guess
- `0x462e1d` with `0x463204`, `0x46385b`, `0x463a40`, `0x463ef9`: walks a road from a map node along its bits to
  both of its flags, called by the code that takes a road or a flag down (`0x45ed5f`, `0x462d88`); the drawing does not
  use it. likely
- `0x42be63`, `0x42ca7a`, and the search in the settler's walk: known. The route search and the request for a worker:
  [Settlers](../rules/settlers.md#the-route-search). `0x42b455` is the same search for a carrier, started at both ends of
  a land road: known, with the second way of the request (a jobless settler, no tool):
  [Carriers](../rules/carriers.md#the-request). Open lead: `0x42b4bc`, the boatman of a water road, and the second way of
  the request for a worker with a tool. likely
- `0x4312ed`: releases the settlers tied to a link value, by job type, when a flag or road goes away.
  likely

## Castle and start

- `0x41eeeb`: known. Calls `0x41f1f4` and `0x41f2d2` for each player.
- `0x41f1f4`, `0x442704`, `0x442630`: known. The castle's placement, its start stock, and the click that
  places it in a free game: [Castle](../rules/castle.md).
- `0x442f0c` with `0x44333d`: known. The castle's 20 first settlers and the tools they take from the stock; the
  engine creates them: [Castle](../rules/castle.md#what-the-placement-writes).
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
- `0x440c24`: in-game click handler; tests map against panel; its road step is known ([Roads](../rules/roads.md)). likely
- `0x440d23`: dispatches road, flag, and build actions and plays the error sounds. likely
- `0x44c768`, `0x44c8cf`: write a button list into view `+0x60`, perhaps the build menu. guess
- `0x44b6e6`: known. The build-site evaluator behind every click: [Build sites](../rules/build-sites.md).
- `0x442244`: known. The flag button. It places a flag where the evaluator's site is not 0 and its node state is
  4, 6, or 7: [Flags](../rules/flags.md).

## The first walk (open leads, inferred values)

- The woodcutter's hut is the building type 2 and its worker job 5 (known: [Woodcutting](../rules/woodcutting.md#the-hut-and-the-job)),
  its sprite entry 1418 (known: the draw stub `0x44e63b`). Its object code (2), its site rule (node state 5, site 3 or 4), and the
  marks on its node (flag bits `0x42`, and `0x10` on its flag's node) are inferred; no run places a building. Lead: the
  building placement routine (the click handler `0x440c24` reaches it) and a Windows run that places a hut. The type 1 and job 11 that
  the first walk was read with are another hut's and worker's (the work at `0x433f71`: a node with no object by the water, likely a
  fisherman's).
- The settler's drawing is written from the figure tables at `0x4501a1` and `0x4503c5` and the walk sequences of entry 2 of the
  graphics archive, but no emulator or debugger run of the draw (`0x43f537`, `0x43fed8`, `0x44fff3`, `0x406610`) confirms the
  details listed as inferred in [Settlers](../rules/settlers.md#drawing-the-walk-leads): the sequence offsets, the triple index,
  the position, the second figure, the colour overlay, and the variants from figure byte 0x80. Lead: run `0x43f537` in the
  emulator with a settler record and read the draw record it writes.
- The castle's own handler (building type 0x18, `0x42e7e0`) and the settlers the castle makes later (`0x428a75`), and the ambient
  sounds of each view (`0x428964`): they may draw from the random generator; the port draws nothing for them. The knights' idle
  turns (jobs 22 to 26) are known and ported ([Woodcutting](../rules/woodcutting.md#the-random-generator)).
- The woodcutter sent home (mode `0xfe`), the free walk's passing of settlers and its check of a node marked solid, the way back with
  a negative count (`0x4385ab`), and the handlers of the object codes from `0x67` (`0x428eb8`): likely, not ported
  ([Woodcutting](../rules/woodcutting.md#open-leads)).
- The drawing of the goods on a flag (`0x44db26`, `0x44de9a`): the place offsets and the sprite entry 320 plus the good (a log is
  entry 327) are read from the decompile and **inferred**; no emulator or debugger run of the draw confirms them. Lead: run `0x44db26`
  in the emulator with a flag that holds a log and read the position and entry `0x44de9a` hands the blit.
- The hut's node and flag marks (`0x42`, `0x10`) that the `wood` verb writes by hand are **inferred** (above); the expected walk of
  the woodcutter in `wood-walks.txt` depends on them.
- The phase count of a game with one player and no computer opponent (33, inferred from `0x42055e`).
- The passing of two settlers going opposite ways (the original swaps them; no run of the games here swaps) and the walk
  home (mode 254). A blocked step is settled: the settler tries it again ([Settlers](../rules/settlers.md#the-walk)).
- The carrier's idle sprites (**inferred**: frames `0x6e` to `0x73`, the timer it holds) and the figure row of job 0 (base 0);
  no run of the draw confirms them. Lead: the same as for the walk's draw.
- A road that already holds two carriers (the original counts the carrier's turns at the road's end and may send it on; the
  port leaves it where it is), the settlers' search level count (`+0x10a` of the player block, which only matters with several storages), and
  the storage's settler count (`+0x40`), which the request lowers and the engine does not keep.

## Open questions

- Where does a carrier hand a good over at a flag? (The carrier's job is settled: job 0, from a settler of job 21:
  [Carriers](../rules/carriers.md#the-job).)
- Where is the woodcutter's work cycle? Known: [Woodcutting](../rules/woodcutting.md). The jump at `0x43f742` follows the settler
  drawing `0x43f537` (likely its job table), not the work: the work is the state routines `0x438b8c`, `0x436c38`, and `0x438a9e` that the job's
  start `0x43cf15` reaches.

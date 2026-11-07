# Roads

This note records how the program lets the player draw a road between two flags, what the road writes, and how a
road is drawn. Addresses and routine names are in the [Program index](program-index.md#roads); the flags the road
joins are in [Flags](flags.md); the table the flags live in is in [Item tables](item-tables.md); the judgment of a
node for a flag is in [Build sites](build-sites.md).

## The road's bits in the map

**Status:** confirmed.

The flag byte of a node (byte 0 of record A, the `Flags` layer of the canonical form) holds in its low 6 bits one bit
for each of the six directions along which a road leaves the node. The directions, as the program's tables give them
(`0x442002`, `0x444e68`, `0x443f62`):

| Direction | Step in column and row | Node reached |
| --- | --- | --- |
| 0 | +1, 0 | east |
| 1 | +1, +1 | south-east |
| 2 | 0, +1 | south |
| 3 | -1, 0 | west |
| 4 | -1, -1 | north-west |
| 5 | 0, -1 | north |

A road step in direction `d` from node A to node B sets bit `d` at A and bit `(d + 3) mod 6` at B. A node in the
middle of a road holds two bits, a flag holds one for each road that leaves it, and the building a flag serves holds
one towards its flag (the castle holds bit 1 and its flag bit 4, [Castle](castle.md)). The evaluator reads these bits
as the flag bits of [Build sites](build-sites.md): a node whose bits are exactly 2 or exactly 0x10 is a building's or
a flag's, and a node with other bits is on a road (node state 4).

## Drawing a road

**Status:** confirmed for the steps below; the option at the end is an open lead.

The player presses the road button (`0x440d23`, action `0x18`) with the pointer on a flag: the evaluator
(`0x44b6e6`) must answer 1 or 2 for the node, which it did for every flag of the player's the emulated roads began at
and for no other node. The view enters road mode
(bit 7 of the view's flag byte at `+1`), and the road's length (`+0xce`) starts at 0. The player then clicks, one
node at a time, the nodes next to the end of the road; the road ends where the click reaches a flag. Pressing the road
button again, or a failed end, leaves road mode and takes the drawn road off the map (`0x442002`), one bit pair for
each of the steps counted. The drawing changes the map as it goes: the bits of the road so far stand in the flag
bytes while it is drawn.

**The allowed directions.** The cursor update (`0x44b6ce`) runs for the road's end and leaves a mask of the six
directions at `+0xd0` of the view. A direction is allowed when the node it leads to:

1. belongs to the player (the top 3 bits of its height byte are the player's number plus 4); a node of nobody's or
   of another player's is refused;
2. is the node the road came from: a road begun marks the way back by the bit of that direction at the end, and the
   way back is allowed (the cursor shows another sprite for it). At the start flag no direction marks a way back,
   whatever roads leave the flag;
3. otherwise holds no object of class 2 or 4 (trees and stones, [Build sites](build-sites.md)), and then is allowed
   if it holds a flag (class 3), or holds no road bits, or holds road bits and the evaluator would put a flag on it
   (its site is not 0: no flag next to it, and it is not a building's or flag's node).

**The click on a node.** The click step (`0x443f62` to `0x44477f`) finds the direction from the end of the road to
the clicked node. A node that is not a neighbor of the end, or is the end itself, changes nothing and makes no sound.
For a neighbor the step goes on only if the direction is in the mask; otherwise it sounds the error (4) and changes
nothing. Then:

- if the end already holds the bit of that direction, the click takes the last step back: the length falls by one and
  the two bits are cleared;
- else, if the clicked node holds a flag (object code 1), the road ends there: the commit below runs;
- else, if the clicked node holds road bits of any road, the step is refused with the error sound;
- else the length rises by one, the two bits are set, and the end moves to the node.

**The commit** (`0x444e68`) walks the drawn road from the flag it ended at back to the flag it started at, along the
bits. It fails, and the road is taken off the map, when a node on the way is not the player's, or when the two
triangles beside the steps are not all land or all water. The triangles beside a step depend on its direction and
are read from the node the walk leaves: for direction 0 the lower triangle of the node and the upper triangle of the
node north of it; for 1 both triangles of the node; for 2 the upper triangle of the node and the lower triangle of
the node west of it; for 3 the lower triangle of the west node and the upper triangle of the node north of that; for
4 both triangles of the north-west node; for 5 the lower triangle of the north-west node and the upper triangle of
the node north of the node. A step is water when every triangle it reads has a terrain nibble below 4, and land when
any does not. A road of land steps is the usual road; a road of water steps only is accepted too, with one bit less
in the items below. A road that mixes the two is refused. On success the commit joins the two flags: for each end,
in the flag table's item (70 bytes, [Item tables](item-tables.md)) of the flag, the bit of the end's direction is set
in the bytes at `+3` and (for a land road) `+4`, the length class (`0x4454e7`: 0 up to 3 steps, 1 for 4 and 5, 2 for
6, 3 for 7 to 9, 4 for 10 to 12, 5 for 13 to 17, 6 for 18 to 23, 7 from 24, shifted left by 4) is stored at
`+6 + direction`, the direction of the other end goes into bits 3 to 5 of the byte at `+0x3c + direction`, and the
pointer to the other flag's item goes to `+0x24 + 4 x direction`. The road leaves the map as drawn, with the last
step's two bits set after the commit succeeded. A road from a flag back to itself is accepted and joins the one item
to itself.

**An error of the program.** A road whose first step goes back along a road that already leaves the start flag is let
through when the node it leads to is a flag (a road of one step between two flags): the step finds the start's bit,
takes it for a step back, clears the old road's two bits, and counts the length to -1. The rest of the drawing goes
on with a wrong length, and the commit then joins the wrong items. The engine refuses such a step.

**Open lead (inferred, not run):** bit 6 of the state's byte at `+0x37e` makes the cursor update also walk the road
under a node that holds one (`0x444b29`). The harness holds it clear; the engine does not walk.

The two other leads this section carried, bit 3 of the view's flag byte and a flag put on a node of a road, are known
now: [A flag on a road](#a-flag-on-a-road).

## A flag on a road

A flag put on a node that holds a road splits the road into two. The program does it in the flag placement itself, so
every path that places a flag does it: the flag button, a click in road mode, and a building whose flag node holds a
road ([Build sites](build-sites.md#placing-a-building)). The notes below cover the placement, the split, the carriers,
and the pointer button that lets a road click place a flag.

### Where the split runs

**Status:** confirmed, emulated.

`Flag_Place` (`0x4422c5`) does its usual writes ([Flags](flags.md#what-the-placement-writes)). Where the evaluator's
node state at `+0x100` of the player block is 4 (the node has road bits, [Build sites](build-sites.md#the-node-state)),
it then calls `Road_SplitAtFlag` (`0x4662e2`) on the node. A node state of 6 or 7 places a flag and splits nothing. The
flag button (`0x442244`) allows the node states 4, 6, and 7, so a flag goes on a road node wherever the site is not 0.
For node state 4 that is a node with no flag on its six neighbors and at least one land triangle around it
([Build sites](build-sites.md#the-site)): a road needs 4 steps at least to take a flag.

### What the split writes

**Status:** confirmed, emulated (`road-splits.txt`, sections `split-*`).

The node holds two road bits; call the lower direction A and the higher B (a second bit of 4 with bit 5 also set is
taken for 5, as in [Removal](removal.md#taking-a-road-down)). The split:

1. Walks the road from the node along each direction to the flag at its end (`Road_GatherSettlers`, `0x463a40`): the
   steps, the settlers on the half, and the slot of the end flag with the direction by which the road leaves it. The
   two counts of steps sum to the old road's length.
2. **A carrier on its way to the road.** Where the road byte of the end flag reached along B has bit 7 (a carrier is on
   its way, [Carriers](carriers.md#the-request)), the split searches the settler table, from slot 0 up, for the first
   settler in state 1, 2, `0xf`, or 5 or 7 with next state 2, whose target and mode name one of the two end flags and
   the direction by which the road leaves it. The end flag that is not the carrier's target loses bit 7 of its road
   byte. A carrier on its way therefore gets the half at the end it walks to, and the other half starts with no
   mark. The search takes only the first match. **Inferred (read, not run):** where no settler matches, the end
   reached along B loses the mark.
3. Joins the node's flag to each end flag in turn through `Road_LinkHalf` (`0x466719`). For the new flag `N`, the end
   flag `F`, direction `d` at `N` and `e` at `F`:
   - `N`'s bit `d` is set in the road bits at `+3`. Its bit `d` in the land bits at `+4` is copied from `F`'s bit `e`,
     so a land road stays land and a water road stays water ([Water](#water-roads)). `F`'s own bits stay.
   - Bit `d` of `N`'s status byte at `+5` and bit `e` of `F`'s are cleared (set again below where carriers remain).
   - `N`'s road byte at `+6 + d` is cleared, and takes bit 7 where `F`'s road byte had it; `F`'s road byte keeps its
     bit 7 only. The length class of the half (`0x4454e7`, from the steps of the walk in step 1) is then or'd into
     both bytes.
   - `N`'s bits 3 to 5 of the byte at `+0x3c + d` take `e`, and `F`'s at `+0x3c + e` take `d`; the pointers at
     `+0x24 + 4 x d` of `N` and `+0x24 + 4 x e` of `F` name each other. Each end keeps the goods bits of its byte at
     `+0x3c`.
4. **Carrier count of a half.** The settlers on a half count against the number its class holds
   (`Road_CarrierCapacities`, `0x462acd`), less one where the half carries the on-the-way mark. Up to that number, the
   status bit is set at both ends and the number goes into the low nibble of both road bytes. The settlers beyond
   the number are sent away, taken from the start of the list, the ones nearest the new flag: a waiting carrier
   (state `0x45`) takes state `0x44`, any other settler takes next state `0xff` and the good it carries is counted
   lost (`Good_CountLost`). This is the code of the flag take-down ([Removal](removal.md#taking-a-flag-down)) run the other
   way. **Inferred:** no emulated game reaches it, because a road holds one carrier unless goods keep it busy and both
   halves of the emulated road hold a carrier each.

The split asks for no carrier and moves no good. The end flags keep the goods that lie on their places.

### The carrier of each half

**Status:** confirmed, emulated, except where marked.

The old road's carrier (with its settlers gathered into the two halves by step 1) stays on the half where it is. The
other half has no carrier: both its road bytes hold the class alone, so the next flag pass of either end flag asks the
castle for one (the request in [Carriers](carriers.md#the-request); a flag pass comes once for each phase cycle, so
the request comes within 33 passes in the emulated games). The sent carrier targets the end flag of the half that the
route from the castle reaches first, with the direction by which the half leaves it.

| The carrier at the click | What it does | Emulated |
| --- | --- | --- |
| On its way out of the castle (state 5, or state 2 at the castle's flag, mode 0 to 5) | Keeps the half at the end flag it targets (step 2): it takes that road there and waits next to the new flag. The far half has no mark. | `split-onway-40`, `split-onway-75`, `split-west-onway-40` |
| Walking the road (state 3), carrying nothing, on a node of one half | Keeps that half, counted in its road bytes and status bits, and walks on to the end of the half: next to the new flag where the half's end is the new flag. | `split-walking-100`, `split-walking-175`, `split-west-walking-100` |
| Walking the road in state 3 and standing on the new flag's node | It joins the half behind it (the one whose direction equals its heading) and, at its next turn, steps from the new flag back along that half, to wait next to the old end flag it came from. | `split-node-140`, `split-west-node-140` |
| Walking the road with a good (state 3, the good in its mode byte), short of the new flag | Keeps its half, reaches the new flag and sets the good down on the new flag's first free place (the flag pass sends it on). It then turns back, as the carrier on the node. | `split-good-610` |
| Walking with a good, standing on the new flag's node | Sets the good down on the new flag at its next turn and turns back. | `split-good-660` |
| Walking with a good, past the new flag | Keeps its half and delivers the good to the old end flag it walks to. | `split-good-700` |
| Waiting next to an old end flag (state `0x42`) | Takes state `0x45` at the split (the walk's rule for a waiting carrier), then state `0x43` and 3, walks the half and waits next to the same flag again, 28 passes after the click in the emulated game. It keeps its half. | `split-waiting-240` |
| The other half (no carrier) | Gets a new carrier through the flag pass as above. | all |

A road with more than one carrier, and a settler in state `0xe` at an end flag, take the rules of steps 1 and 4
(**inferred**, not run).

**No free settler.** The split requests nothing itself: no routine it calls reaches a request. The half without a carrier
is a road whose byte holds the class alone, so each flag pass of its flags asks again, as for any road
([Carriers](carriers.md#the-flag-pass)). Where the castle has no spare settler, the request fails, the flag sets bit 7 of
its status, and the retry scan clears the bit one pass later; the pair repeats at every flag pass until a settler is free.
**Emulated:** `split-nofree-1000` (five roads carry the five spare settlers; the castle-side half asks at passes 1022,
1055, 1088, and on, and finds none).

### Water roads

**Status:** confirmed by reading; not run, because the emulated maps hold no water road beside a flag.

The split carries the kind of the road to both halves (step 3: the land bit of the new flag's side is copied from the end
flag's). The take-down of a flag between two roads writes no land bit at the far flags ([Removal](removal.md#taking-a-flag-down)):
each far flag keeps the bit it had for its own road, so a flag between two water roads leaves a water road, and a flag
between a land road and a water road leaves a road that is land at one end and water at the other.

### The pointer button that places a flag on a road

**Status:** confirmed for the branch and for what sets the bit; **inferred** that the bit is the right pointer button
(the platform layer's button order).

Bit 3 of the view's flag byte at `+1` is not a mode of road drawing. The pointer update (`0x47a4c3`) sets it while the
second of the two pointer button words is down and clears it when the button is up, and `Input_BufferKey` (`0x426b57`)
clears it on a key press. No other routine in the game band writes it, and the road button (`0x440d23`, action `0x18`),
the click step, and the road cancel (`0x442002`) never do. Road mode does not set it.

With the bit set, the click step (`Road_ClickStep`, `0x443f62`) acts as follows:

- **A node that holds road bits and no flag.** After the usual mask check, the step checks the node with the evaluator
  (`0x44b6e6`) and the road walk (`0x444b29`, which turns the placement away where the node's road leads to no flag: the
  road being drawn), and puts a flag with `Flag_Place` (`0x44426c`), where the site is not 0. The road then ends at the
  new flag (the commit runs). **Emulated:** `road-click-bit3`: the second road of two steps ends at the flag put on the
  first road's node, which splits the first road.
- **An empty node.** The step puts a flag there (`0x444491`) when the site is not 0 and ends the road at it. **Emulated:**
  `road-click-bit3-empty`. A node next to a flag has site 0, and the road steps on.
- Without the bit the first case is refused with the error sound (4) and the second steps on. **Emulated:**
  `road-click-bit0`.

With the bit set, the road, flag, and building buttons do something else: the click runs `0x4417db`, which toggles bit 6
of the view's first flag byte where bit 1 is set and road mode is off, and places nothing. The meaning of that bit is not
recovered.

### Emulated

The harness verbs: `remove` with the clicks `p@x,y` (the flag button) and `b@x,y` (the building placement `0x449a63`)
and `road` with the fourth field `2` (bit 3 held), in `tools/GhidraDump/EmulateMapStart.java`. `road-splits.txt`
(`tests/OpenSettlers.Tests/`) holds the lines of 18 runs: the split with the carrier in each state, with a good, on the
west road, with no free settler, the refused nodes, the building with node state 6, and the three road clicks. The lines
are expected values for the engine's split; `RoadSplitTests` replays the `split-*` runs.

## How a road is drawn

**Status:** confirmed for the mask and the texture; **inferred** for the pixel alignment.

The roads are drawn with the terrain, in the pass `0x427c09` that also draws the borders of territories, not with the
objects. For each node, three directions are drawn from the node, the three others from the node at the other end:

| Direction | Routine | Mask entry | Strip |
| --- | --- | --- | --- |
| 0, east | `0x428008` (called by `0x427d3e`) | 234 plus the height difference | 32 pixels wide, as high as the higher of the two nodes |
| 1, south-east | `0x428105` (`0x427e3a`) | 243 plus the height difference | 16 pixels wide, from the node's own height |
| 2, south | `0x4281cc` (`0x427f1f`) | 252 plus the height difference | 16 pixels wide, from the node's own height |

The height difference is the node's height less the other end's (5 bits each). The archive holds the masks for a
difference of 4 either way (entries 230 to 256, rows 5 to 41 high); a strip beyond that has no mask. The routine takes
2 pixels above the node's line and 38 more that belong to the program's own layout.

The texture is 300, 301, or 302 by a slope sum: for east, three times the negated difference plus the height of the
node north of the node less that of the node south-east of it; for south-east, twice the height of the east node
less that of the south node; for south, three times the difference less the height of the west node plus that of the
south-east node. A sum of 5 or more takes 300, from -5 up to 4 takes 301, and below -5 takes 302. The terrain then
changes it: the largest of the two nibbles the routine reads (east: the lower of the node and the upper of the north
node; south-east: both of the node; south: the upper of the node and the lower of the west node) below 4 gives 309 (a
road in water); above 7, 300 to 302 are raised by 3 (up to 13) or by 6. A road whose bit is set for a direction the
passes do not draw (3, 4, 5) is drawn from the node at the other end.

Where no road lies, a pair of neighbors with different owners takes the routines `0x428389`, `0x42846d`, and
`0x428528`: the borders of the territories (inferred; not drawn). This replaces the reading of
[Graphics images](graphics-images.md) that the masks 230 to 256 and the textures 300 to 309 are the edges between
terrains: they are the roads.

**Inferred:** the position. The strips are placed from the geometry the masks have: an east strip is 32 pixels wide and
starts at the node's column of pixels; the south-east strip is 16 wide and starts there; the south strip is 16 wide
and ends there. The vertical placement is the one the routines compute. No run of the program's drawing confirms the
alignment of the masks with the engine's terrain.

## Emulated

The harness verb `road:N:S0,S1,S2:L:cx,cy:x,y;...:sx,sy,D;...` places the castle and the flags with the original's code,
and then runs for each road the road button's check, and for each direction the cursor update (`0x44b6ce`) and the
click step (`0x443f62` to `0x44477f`), with the sound requests skipped and noted. It prints the mask, the sound, the
length, the cursor, the map bytes the step changed, and for a finished road the bytes it changed in the flag table.
`RoadBuildingTests` replays 588 lines of three maps (`road-builds.txt`): 28 built roads (among them roads over both
edges of the map, loops, and water roads), 35 roads that did not end on a flag and were cancelled, 119 refused steps,
one road refused at its end, 6 starts the original refuses, and 6 of the error above (the engine refuses those steps; the
harness restores the state before the road). Every step's verdict, every map byte, and every item bit and direction
matches. The verb `roaddraw:SEED:COUNT` runs the three strip routines on random heights and terrain bytes;
`RoadDrawsTests` replays 120 cases and matches the mask, the texture, and the height of the strip above the node.

## In the engine

`CommandKind.BuildRoad` takes the start flag in `Command.X` and `Command.Y` and the step of each direction in
`Command.Directions`. `RoadBuilding.Walk` (`src/Simulation/RoadBuilding.cs`) draws the road into the map as the
original does and takes it back unless the road is accepted; `Game.CheckRoad` asks the same without changing the game,
and `Game.Roads` lists the built roads (`Road`: the slots and directions of the two ends, the steps, and whether it is
a water road). The digest holds the roads. A refused command, and a road that does not end on a flag, changes nothing.
The stand-in road button (`src/Viewer/MapViewControl.cs` with `src/MapView/RoadDrawing.cs`) draws a road outside the
game, a node at a time, and sends one command where the road reaches a flag; Escape, a right click, or the button
again cancels. `RoadDraws`, `RoadGraphics`, and the control draw the strips over the terrain in every copy of the map;
the road being drawn is a white line. Departures: the length class, the second bit byte at `+5`, and the items'
pointers are not kept (a later slice reads the road from the map and from `Game.Roads`); the `+0x37e` option, the
second-button click that puts a flag on a road ([A flag on a road](#a-flag-on-a-road)); the step back at the start;
directions after the road reached a flag are refused; the sounds; and the pixel alignment of the strips.

**A road ending on a road.** The Road button holds no view flag bit 3 and the stand-in offers no second pointer button, so
a road whose step lands on a node of another road, a node with road bits and no flag, is refused as the original refuses it
(`road-click-bit0`; [the evidence](#the-pointer-button-that-places-a-flag-on-a-road)). The same holds for a step onto the
road's own earlier node and for a road across another. The other road, the flags, and the map stay as they were
(`RoadOnRoadTests`). The branch of `0x44426c` that puts a flag on the node and splits that road is not built: no control
sets the bit.

`Game.PlaceFlag` on a road node (node state 4) splits the road (`src/Simulation/Game.Split.cs`): `Game.Roads` holds the two
halves in place of the old road, the water kind goes to both, the settlers on the road are gathered as the take-down of a flag
gathers them (`JoinRoads` is the inverse), and the half without a carrier asks for one at the next flag pass. The events are
`split-roads`, with the cause of the flag's placement. `RoadSplitTests` replays the `split-*` runs of `road-splits.txt`.
A road holds one kind in the engine: the join of a water road and a land road is a land road.

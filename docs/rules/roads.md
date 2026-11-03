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

**Status:** confirmed for the steps below; the three options at the end are open leads.

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

**Open leads (inferred, not run):**

- Bit 3 of the view's flag byte at `+1` is set and cleared by the pointer update (`0x47a4c3`); its meaning is not
  recovered. With it set, a click on a node that holds a road and no flag places a flag there (`0x4422c5`) and goes on
  instead of refusing. The harness holds the bit clear; the engine refuses the step.
- Bit 6 of the state's byte at `+0x37e` makes the cursor update also walk the road under a node that holds one
  (`0x444b29`). The harness holds it clear; the engine does not walk.
- A flag put on a node of a road (the evaluator allows it, node state 4) splits the road in the original's tables.
  The harness runs no such placement over a road, and the engine's flag placement leaves the road's items alone.

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
pointers are not kept (a later slice reads the road from the map and from `Game.Roads`); the bit-3 and `+0x37e` options
and the flag over a road above; the step back at the start; directions after the road reached a flag are refused;
the sounds; and the pixel alignment of the strips.

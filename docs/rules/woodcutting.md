# Woodcutting

How the woodcutter works: which building and which job he belongs to, how he picks a tree, how he walks to it, cuts it,
carries the log to his hut's flag, and starts again, and how a felled tree leaves the map. Addresses and routine names are in
the [Program index](program-index.md#simulation); the settlers and their walk to the hut are in [Settlers](settlers.md); the flag
items are in [Flags](flags.md) and [Item tables](item-tables.md); the carriers that take the log on from the flag are in
[Carriers](carriers.md).

**Emulated.** The harness verb `wood:` (`tools/GhidraDump/emulate.py`) starts a game as the `worker` verb does (castle, flags,
roads, a finished building of type 2 at a flag) and runs the program's own routines for each pass: `0x428728`, the object pass
`0x428d10`, the phase step, `0x46414d`, `0x42cb2d`, and `0x42f3d9`. The engine runs the same commands and matches the worker's
item and the hut's after every state or node change, every object byte that changes, the goods on the hut's flag, and the
generator's words, in two games (`tests/OpenSettlers.Tests/wood-walks.txt`, run with the verb `goods:`, so that the logs the
woodcutter lays are carried to the castle, [Goods at a flag](goods.md)): a hut among trees for 4700 passes (three cuts, the
first felled tree gone from the map) and a hut with no tree in its reach for 2500 passes.

## The hut and the job

**Status:** confirmed.

The handler of the building type 2 (`0x42d945`, entry 2 of the table at `0x42cd1d`) asks for a settler of job 5, as the handler of
the type 1 (`0x42e0b8`) asks for job 11 ([Settlers](settlers.md#the-request)). Job 5's work is the one below: it looks for objects
from 8 to 0x17, the animated ones the map generation calls trees (class 1 in the class table), so the type 2 is the woodcutter's hut
and job 5 the woodcutter. The castle's 20 first settlers hold one of job 5, in slot 10. The type 1 is the hut of the settler of job 11,
whose work (`0x433f71`, state 0x1f) looks for a node with no object and no road by the water: **likely** a fisherman's. An earlier
reading of [Settlers](settlers.md) took type 1 and job 11 for the woodcutter's; no run had named the hut. The hut's sprite is the
object 0xa8, entry 1418 (`0x44e63b`); the type 1's is 0xa7, entry 1417 (`0x44e62b`).

## The start at the hut

**Status:** confirmed.

At the hut's door the worker is in state 4 ([Settlers](settlers.md#the-walk)). When its timer has run down to the time the door
leaves it (word 12), and the building's status bit 5 is clear, it runs the routine of its job through the table at `0x43ca54`: for
job 5, `0x43cf15`. Where the settler is not sent home (byte 11, the mode, is not `0xfe`) the routine empties the hut's node of the
settler's slot and takes state 0x12. A settler sent home goes back into the castle's list of waiting settlers: not ported.

## Choosing a tree

**Status:** confirmed; **emulated**.

State 0x12 (`0x438b8c`): when the timer has run out, the worker takes a draw of the game's generator (`0x44257a`, the same step as
`Rng_Step`, the new first word) and from it an entry of the spiral table: `((draw & 0x1fc) + 4) / 4`, an entry from 1 to 128, so a
node within the first 128 of the spiral around the hut. If an object code from 8 to 0x17 stands on it the worker has its tree. If
not, 400 is added to the timer and the worker draws again, until that sum wraps the timer, which ends the turn. The timer holds a
debt after a turn that found no tree (the wrapped value), so each later turn tries only once or twice: the search costs about 163
tries when the timer is 0 and fewer as the debt goes, and the worker waits, turn after turn, while no tree stands in reach.

On a hit the worker's vector (bytes 11 and 12 of its item) is the tree's offset from the hut less one each way, because the first step
takes it from the hut onto its flag, south-east; bytes 13 and 14 hold the negated vector, the way back. Byte 15 holds 0x10, the state
after the step. The step itself is state 7 (`0x43e036`): the settler goes south-east when its node and the flag's node hold no other
settler, with the time of the step times the door's speed `xor 0x1f` over 32, and waits it out in state 5.

## The walk

**Status:** confirmed for the steps with no obstacle; **emulated**.

State 0x10 (`0x436c38`) walks the vector. Each step, with byte 15 clear, takes a heading class from the signs and sizes of the
vector (east, south): 12 plus a part for a vector that points west (2 more when the north part is less than the west part, 1 more
when the lesser part is under twice the greater) and so on up to 23. The class picks a row of the table at `0x4388ff`, 8 bytes a row
(read from the install, `RuleTables.WalkDirection`), and the first direction of the row is the step: 4 east, 8 south-east, 12 south,
16 west, 20 north-west, 24 north. The step is made when the node is not marked 0x40 (flag bit 6: a building, the hut's node among
them) and holds no settler. The vector takes the step, byte 15 becomes 8 when it is 0, the node changes, and the timer adds the time
of the shape `4 + 9 * direction + (height reached - height left)`; the walk goes on while the sum does not wrap the timer, as the road
walk's step does.

**Not ported:** the other directions of a row when the first is taken, the heading memory the original keeps in the low bits of byte
15, and the passing of settlers on the free walk; a step that cannot be made waits 0x7f units with the frame 0x52.

## The cut

**Status:** confirmed; **emulated**.

With byte 15 equal to 8 the walk has arrived (`0x437ddd`, which jumps by the job; job 5 goes to `0x437f53`). The woodcutter's branch,
on the tree's node: it takes the way back as its vector, and if an object from 8 to 0x17 still stands there, byte 13 is 0xff for the
codes 8 to 15 and 0, byte 14 is 0, the frame 0x74, the timer 0x3ff, and the state 0x11. Where the tree is gone, byte 13 is 0x80 and the
settler walks back with no log.

State 0x11 (`0x438a9e`): each time the timer runs out, byte 14 counts up and the object byte of the node (bit 7 kept) takes
`0x5c` plus the count, plus 5 for the codes 8 to 15 (so `0x5d` to `0x61`, or `0x62` to `0x66`); with the count 5 the cut is over, else the
frame is `0x74` plus the count and the timer adds the table word of that shape (`0x43f248`), and the loop goes on while it does not wrap.
When it is over: byte 13 is 0x80, byte 14 is 1, byte 15 is 0, the state 0x10, the timer 0: the settler walks back the vector it took.

## The log

**Status:** confirmed; **emulated**.

Back at the flag (the vector 0, byte 13 = 0x80) the branch at `0x437ffd` lays a log (good 7) on the flag when byte 14 is more than 0:
`0x436b46` writes it to the first empty one of the flag item's 8 places (bytes `0xc` to `0x13`), clears the place's word at `0x14` plus
twice the place, sets bit 7 of byte 4 of the flag item, and counts the good up in the player block. A flag with no empty place takes
nothing and the settler goes on: no check. Then `0x43c7c2` takes the settler from the flag into the hut (the building north-west of
it) as the walk to the hut does ([Settlers](settlers.md#the-walk)), with state 6 while the hut's node is busy; byte 11 is 0, so the
start routine of state 4 sends it to look for the next tree. The log gets its destination and its direction at the flag's next turns,
and the waiting carrier is told to fetch it: [Goods at a flag](goods.md).

## The object pass

**Status:** confirmed for the codes 0x53 to 0x66 and the lake detail; the handlers of the codes from 0x67 are **not ported**.

`0x428d10`, called by the main loop's `0x4286d8` after `0x428728` and before the phase step, each pass: every 20 clock units (a
countdown that wraps as a debt, so a late pass catches up) it takes as many nodes as the map's size gives, `(columns / 32) * (rows /
32)` (state `+0x21c`), moving a cursor 23 columns east and a row south where it wraps the row. At each node:

- A node whose object byte has bit 7 (a water triangle), whose flag byte has bit 6, and whose resource byte (the lake detail) is not 0
  takes a draw; where the detail is below 10 it grows by one when the draw's bits 8 to 13 are 0, and one moves to the neighbour that
  the draw's bits 2 and 3 pick (south-east, south, north-west, north) when that is a lake node too.
- An object code from 0x53 goes through the table at `0x428eb8`: 0x53 vanishes (bit 7 kept) when a draw's low two bits are 0; the cut
  tree's codes `0x5d` to `0x66` become `0x53`; 0x54 to 0x5c do nothing; the codes from 0x67 grow (saplings to trees) or decay and are
  not ported, because the generation places none and there is no planting yet.

So a felled tree stands as a cut tree until the cursor passes its node, then as the object 0x53, and vanishes at a later pass of the
cursor with the chance 1 in 4, so a stump stays 1 to 2 cursor rounds and more on a 32 by 32 map (a round is about 2560 passes). The
generation's own 0x53 objects vanish the same way. There is no regrowth: the original's growth (0x67 and up) needs a forester.

## The random generator

**Status:** confirmed; **emulated**.

The game's generator continues from the map's generation (the verb's words at the first pass are those of the pass `CountResource1`
in `tests/OpenSettlers.Tests/map-digests.txt`: the castle's placement draws nothing). Besides the woodcutter's search, the draws that
reach the words in the first 5000 passes are those of the object pass above and of the knights waiting in the castle: a settler of
job 22 to 25 draws when its timer has run out until a draw is below 4000, 2000, 1000, or 500, which promotes it to the next job (and
moves its entry in the castle's list), adding 6000 to its timer for each draw above (`0x43910b`, `0x4391eb`, `0x4392cb`, `0x4393ab`);
job 26 only waits. The engine takes these draws too, so the woodcutter's choice follows the original's. The forest scenario of
`wood-walks.txt` holds one promotion (the knight of slot 7 at pass 1507, job 22 to 23), and its 16 bytes are checked against the verb. **Departures:** the original
draws also for the ambient sounds of each view (`0x428964`, `0x428985`) and in `0x428a75` (the castle's later settlers), which the
simulation must not depend on or does not port, so a real game's tree choices differ from the verb's by those draws.

## Drawing

**Status:** the trees and the stumps are drawn from the map like any object (the codes 0x5d to 0x66 and 0x53 as the still sprites
`1250 + code - 8`); the woodcutter is drawn as the walking settlers are ([Settlers](settlers.md#drawing-the-walk-leads)) with the
figure rows of job 5. The engine draws him in the states 2, 3, 4, 5, 0xe, 0x10, 0x11, and 0x19, the full list in
[Settlers](settlers.md#drawing-the-walk-leads). The rows cover the walk, the way back with the log (byte 13 0x80, byte 14 1,
state 0x10), and the felling at the cut (figure bytes from 0x80, frames `0x74` to `0x78`).

The frames `0x74` to `0x78` are **confirmed** by the draw run of the `woodcutter` recording
([Settlers](settlers.md#the-draw-path-emulated), `tests/OpenSettlers.Tests/settler-draws.txt`, every pass of the cut). A screenshot
of `play/woodcutter-figures.play` confirms the frame `0x74`. The frames `0x74`, `0x76`, and `0x77` draw the felling figure bytes.
Those are from `0x80`, with two sprites and no second figure. The frames `0x75` and `0x78` draw walking bytes with the figure, its
overlay, and a second figure. The figure bytes of the walk, the way back with the log, and the door (state 4) are the rows of the
handler's three adds.

The woodcutter's hat is the colour overlay of his figure (entry 2500 plus the figure). It is filled with the palette index `0x40`
plus 4 for the player, as every settler's overlay is. The draw path holds no colour of his own, so the hat takes the player's
fill. That is the index `0x40` for player 0, the same as the carrier's overlay. The recordings fix the index. The palette gives
the RGB. The goods on a flag are drawn by `0x44db26` (read from the decompile): for each of the 8 places
of the flag item (bytes `0xc` to `0x13`) holding a good, `0x44de9a` draws the sprite 320 plus the good's low 5 bits at the flag's draw
position moved by (6, -4), (10, -2), (-4, -4) for the places 0 to 2 (before the flag's sprite) and (10, 2), (-8, -2), (6, 4), (-8, 2),
(-4, 4) for the places 3 to 7 (after it), from the flag's draw at its node. A log (good 7) is the sprite entry 327, drawn in
every wrap copy. **Inferred:** no emulator or debugger run of the draw confirms the offsets or the entry (the sprite of a good is
read from the registers the decompile shows); an open lead.

## Open leads

- The woodcutter's walk to the storage (mode 0xfe) is ported with the walk home ([Settlers](settlers.md#walking-home)). The free
  walk's other directions and the passing of settlers,
  and the handlers of the codes 0x67 and up. likely
- The tool for a second woodcutter (the request's second way, [Settlers](settlers.md#the-request)): the castle holds one worker of
  job 5. guess
- The player block's counters of goods on flags (`0x436b46` counts the good up at `+good - 0x6f` of the player block). guess
- The hut's marks: the engine sets the flag bits `0x42` at the hut's node and `0x10` at its flag's node as the castle's placement
  does, and the verb sets them the same way; the original's placement of a hut is not recovered. **Inferred.**

## In the engine

`src/Adapters/Simulation/Game.Woodcutting.cs`: `Door`, `SearchTree`, `StepOut`, `FreeWalk`, `Arrive`, `Cut`, `PutGood`, `EnterHut`, and
`ObjectPass`; `Game.FlagGoods(flag)` reads the 8 places of a flag; `Settler.WoodcutterJob` is job 5, `Building.WoodcutterType` the type
2; `RuleTables.WalkRows` holds the rows of `0x4388ff`; `Map.Generator` is the generator the generation leaves, and the game's
generator (`Game.Random`, internal) continues from it. `Game.Settlers.cs` runs the knights' idle turns (`KnightIdle`). `Game.Digest`
holds the generator, the object pass's clock, countdown, and cursor, and the goods on the flags.

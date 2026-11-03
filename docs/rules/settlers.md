# Settlers and the first walk

How the program lets a building ask for a worker, searches the roads for the way, and walks the worker from the
castle to the building. Addresses and routine names are in the [Program index](program-index.md#simulation); the
tables the items live in are in [Item tables](item-tables.md); the clock is in [Timing](timing.md); the flags and
roads are in [Flags](flags.md) and [Roads](roads.md); the castle's first settlers are in [Castle](castle.md).

**Emulated.** The harness verb `worker:` (`tools/GhidraDump/emulate.py`) starts a game as the `road` verb does
(castle, flags, roads), places a finished building on the node north-west of a flag, and runs the program's own
routines for each pass: `0x428728`, the phase step, `0x46414d`, `0x42cb2d`, and `0x42f3d9`. The engine runs the same
commands and matches the bytes of the worker's item and of the hut's item after every pass until the worker's first turn in
state 4, in three games (`tests/OpenSettlers.Tests/worker-walks.txt`): a hut one road from the castle, a hut three
flags away over four roads, and a hut that gets a road only at pass 100. The flag pass sends a carrier to each road
([Carriers](carriers.md)), so the worker walks among carriers and waits where one stands on its way.

## The items

**Status:** confirmed for the fields below; the rest of the items are not read.

A settler is a 16-byte item of `Table16`: byte 0 holds the player in bits 0 and 1 and the job in bits 2 to 6; byte 1
the frame of the walk; the word at 2 the timer; the dword at 4 the node (the node's index in the map's records, times
4); the word at 8 the clock at the settler's last turn; byte 10 the state; byte 11 the mode; the word at 12 the target;
byte 14 the storage slot while the settler waits in one and the direction back along the road while it walks; byte 15
the state that follows. The castle's 20 first settlers hold the jobs of [Castle](castle.md#what-the-placement-writes)
in the order of their slots (the woodcutter is slot 15, job 11).

A building is an 18-byte item of `Table18`: the dword at 0 the node; byte 4 the type in bits 2 to 7 (bit 7 while the
building is built: the castle starts as `0xe0`) and the player in bits 0 and 1; byte 5 the status (bit 2 a request
found no worker, bit 6 the worker is inside, bit 7 the worker is on the way); the word at 6 the slot of its flag; the
word at 10 the worker's slot; the word at 12 the castle's build progress. A flag's item holds a pointer to its
building at `+0x34` and marks it in bit 6 of byte 4. A node's second record holds the slot of the settler on it in its
word at 2 (0 for none; the original cannot tell settler 0 from none).

The storage item (`Table120`) holds, from `+0x42`, a word for each job: the slot of the settler of that job that waits
in the storage, 0 for none (`+0x42 + 2 * job`); and at `+0x4a` the number of settlers sent out that have not left.

## The pass

**Status:** confirmed.

The main loop calls, in this order for each pass: `0x428728` (the retry scan, below), the phase step `0x4291c2` (one
more, back to 0 at the phase count), the flag pass `0x46414d`, the building pass `0x42cb2d`, and the settler pass
`0x42f3d9`. The engine runs one pass for each tick (8 clock units) and runs the flag pass for the carriers only
([Carriers](carriers.md#the-flag-pass)).

- The building pass handles the buildings whose slot divided by 32, modulo 32, is the phase, so a building is
  handled once for each cycle of 33 passes. A finished building goes to the handler of its type (the table at
  `0x42cd1d`, 8 bytes an entry); the castle's own handler (`0x42e7e0`, knights and defence) is not ported.
- The settler pass handles the settlers whose slot modulo 16 is the phase modulo 16, so a settler is handled at two
  phases of each cycle, 16 or 17 passes apart. Each goes through the state table at `0x42fca1`.
- **Departure from the engine:** the program runs the turn of each settler in view once more every pass
  (`0x42f4f1`), which makes the motion on the screen smooth. The simulation must not depend on the view, so the
  engine runs the pass alone.

## The retry scan

**Status:** confirmed.

`0x428728` clears bit 2 of the status of the buildings in a window, one cursor across the table: it takes the window
size (`+0x52`, which `0x42195c` takes from the table at `0x421d28` by the map size: 16, 30, or 55 buildings for the sizes
1, 2, 3), starts at the cursor (`+0x54`, which goes back to 0 when it is at the end), clears the bit, and stops
after 10 buildings that had it set. A request that found no worker sets the bit, and a building whose bit is set asks
for no worker; so the request is made again at the building's next turn, once the scan has been past it. The scan also
clears bit 7 of the status of a window of flags the same way, which the carriers' requests use
([Carriers](carriers.md#the-flag-pass)).

## The request

**Status:** confirmed for the first way; **not ported** for the second.

A building of the woodcutter's type asks, at its turn when bits 2, 6, and 7 of its status are clear (`0xc4`), for a
settler of job 11 (`0x42be63`). The search starts at the building's flag and runs over the roads breadth first (below).
At a storage's flag it looks at the storage's list of waiting settlers:

1. A settler of the job waits: the storage clears its entry, the settler takes state 15, mode 255, and the building's
   flag as its target, the building's status gets bit 7, and the storage counts the settler as sent out.
2. None waits: a jobless settler (job 21) and the tool of the job from the stock become one (not ported: the castle
   holds one woodcutter, so a second hut gets no worker).

If the search reaches no storage, or the storage has neither, the request fails and the building sets bit 2.

The type of the woodcutter's hut is **inferred**: type 1 asks for job 11 and the tool of good 17, and the castle holds
one settler of job 11 (whose tool the castle took from the stock when it made him: good 17); no run names the hut. The engine's stand-in command
`CommandKind.PlaceBuilding` places a hut of that type finished at once, on a node whose south-east neighbor holds a
flag and whose site is small or large (**inferred**: node state 5, site 3 or 4; no run places a building). The hut is
a **stand-in** for the construction slice (M4), not a rule of the original. It marks the map as the castle does: the
building's node gets flag bits `0x42`, the flag's node bit `0x10`, the object code 2 (**inferred** for a small
building), and the building's slot in the node's link byte.

## The route search

**Status:** confirmed for the search in the walk and in `0x42be63`; `0x42b455` (the carriers' request) is the same search
from both ends of a road ([Carriers](carriers.md#the-request)). The flags are the nodes, the roads are the links; a flag has one
link for each of the six directions (east 0 to north 5), and a building at its north-west.

The search takes a generation number (`0x42ca7a`), marks the flag it starts from, and seeds a queue with the flag at
the other end of each road that leaves it, trying the directions from 5 down to 0 and giving each the direction it
was reached by. It then takes the flags off the queue in order, tries their directions from 5 down to 0, and for each
flag not yet marked marks it, gives it the first direction of the flag it came from, and queues it. A flag therefore
keeps the first direction of the first route that reached it: of several routes with the same number of flags, the one
whose first flag came first in the order of the directions. The search ends when the flag it looks for is marked, or
the queue is empty (no route). The engine's `Game.Route(from, to)` returns the first direction, or -1.

The marks of a generation are 16-bit words in the flag items; when the generation counter wraps, the program clears
all marks (`0x42ca7a`). The queue holds 994 flags a level; the engine's search has no limit.

## The walk

**Status:** confirmed for the states below; the others are **not ported**.

All the walk's arithmetic is in 16 bits, and a timer that runs out wraps into a debt that the next step pays.

- **State 15, leaving** (`0x43dd4a`): the settler steps south-east onto the building's flag, if that node, the node it
  stands on, and (for mode 255) the target building's node hold no settler; else it stays in state 15 with frame
  `0x52`. The step's shape is `13 + (height of the flag's node - height of the node it leaves)`; the step time is the
  table word at `0x43f248` for that shape, times the building's door speed xor `0x1f` (the table at `0x43c92b`, by the
  type, where the castle under construction has the type 0x38 and, once it stands, 0x18), divided by 32. The settler
  keeps the stamp of the clock and waits in state 5. The time of the step depends on whether the castle stands: the
  engine matches both (the scenarios start before and after the castle's completion).
- **State 5, waiting** (`0x43dceb`): the units since the last turn come off the timer; when they were more than the
  time left, the timer is 0 and the state is the settler's next state.
- **State 2, walking** (`0x4398ea` with `0x43a64c` and its five siblings): when the timer has run out, the settler looks
  where it stands. On a node of a road (no flag bit 7) the direction is the road's other bit, besides the direction
  it came from; on a flag it is the first direction of the route to the flag it walks to. It steps (the node's occupant
  must be free), takes the direction back as byte 14, and adds the step time to the timer: the word at `0x43f248` for
  the shape `4 + 9 * direction + (height reached - height left)`. If the sum wraps (the time of the step was longer than
  the debt), the settler waits out the rest and keeps this step's shape as its frame; if it does not, the step was over
  already and the settler goes on at once. The step shapes for the six directions are 9 apart; the table repeats with
  that period, with the walking time 255 on flat ground.
- **Entering** (in `0x4398ea`): at the flag it walks to, with mode 255, the settler goes on to the building north-west
  of the flag: the building's status gets bit 6, its bit 7 goes out and the worker's slot goes into the building,
  the settler steps onto the building's node with the shape `0x28 + height difference`, takes that step's time as its
  timer and the part of it that the door's speed leaves (time times the door speed, divided by 32) as its target, and
  takes state 4. The door's node being busy keeps the settler on the flag in state 6 (not ported).
- **State 4, through the door** (`0x43c96b`): waits out the step and runs the job's routine (`0x43ca54`): the woodcutter's
  work starts here (state 31). Not ported: the next slice's. The test files end with the first turn in state 4.
- **State 1, waiting in the castle** (`0x438eeb`): the settler enters its slot in the storage's list of waiting settlers
  every turn (jobs 22 to 26 and 10 take other branches: not ported).
- **State 10, the castle's builder** (`0x43f17a`): the settler with the job 4, slot 0, adds the elapsed units times 128 to
  the castle's build progress each turn; when that wraps (about 64 passes after the castle is placed) the castle
  stands: bit 7 of its type goes out, which changes the door speed index from 0x38 to 0x18, and the settler takes state 12.
- **A blocked step:** where the next node holds a settler, the timer is 0x7f, the frame `0x51 + direction`, and the heading
  the direction less 6; at the next turn after the timer the settler tries the same direction again.
- **Not ported:** the passing of two settlers going opposite ways (the original swaps them; the port waits), the walk
  home when no route is left (mode 254), the knights (jobs 22 to 26 and the castle's handler, which draw from the random
  generator), and the settlers the castle makes later (`0x428a75`).

## Drawing the walk: leads

**Status:** inferred (below). The engine draws the hut (entry 1417 of the archive, **inferred** as the type's sprite, drawn like the
castle) and the walking settlers. What the program does, read from `0x42f512`, `0x43f537`, the job's figure routine (`0x43fed8` for
job 11), `0x44fff3`, and `0x406610`: the draw record of a settler takes the frame (byte 1) to pick a sequence from
a table the program loads from entry 2 of the graphics archive at start-up (`0x41e8e0`): 200 big-endian offsets, each to a run of
triples (figure byte, dx, dy) that is indexed by the timer shifted right 3, so the animation follows the step's
remaining time in ticks and the offsets carry the settler along the step. The figure word is the job times
1024 plus the figure byte; the tables at `0x4501a1` (8 words for each job: the base entry of the figure in the entries from
1850 and of a second figure from 3150) and `0x4503c5` (two words for each figure byte) turn it into the sprite entries,
and the player's colour overlay is the entry plus 2500. The frame's index is the shape of the step: `4 + 9 * direction`
plus the height difference, so the facing is the direction of the step.

**In the engine** (`SettlerFigures`, `SettlerGraphics`, `src/MapView`): a settler in state 2, 3, 5, or 0x42 is drawn with the sprites its
frame's sequence names for the time it has left, read from the install's archive at run time and the tables from the program
(`ProgramTables.FigureBases`, `FigureOffsets`, matched by digest with the emulator's `peek`). The time left is the
timer less the clock units since the settler's turn, so the animation moves with the game's ticks. The view draws
the settlers of the jobs 0 (carriers, [Carriers](carriers.md#drawing)) and 11 in every wrap copy, rebuilt after each tick. **Inferred, with no emulator run of the draw:** that the
sequence offsets count from the byte after the entry's first dword, that a sequence runs to the next one's offset, that
the triple index is the time left shifted right 3, the figure's position (the node plus 16 and 8, lifted by its height,
plus the triple's dx and dy, as an object), that the second figure takes no offset, the colour overlay filled with the
palette index 0x40 plus 4 for the player, and the job's variants for figure bytes from 0x80 (not read). The original draws a
settler in view from the turn it acts every pass; the engine takes the time left from the clock instead.

## Open leads

- The second way of the request for a worker (a jobless settler and a tool); the carriers' request has it without the tool
  ([Carriers](carriers.md#the-request)).
- The settler's state 4 and the woodcutter's work (the next slice), the states 6, 12, 25, and the walk home (mode 254).
- The knights and the castle's handler of type 0x18, the settler creation of `0x428a75`, and the goods part of the flag pass `0x46414d`.
- The passing of two settlers going opposite ways.
- That the phase count is 33 for the game of one player with no computer opponent (`0x42055e`).
- The building type of the woodcutter's hut, the hut's object code, and its site rule (no run places a building).
- The game's random generator continues from the map's generator in the original, and the knights' idle turns draw
  from it; the port does not draw yet.

## In the engine

`Game.Buildings` and `Game.Settlers` hold the items (`src/Simulation/Building.cs`, `Settler.cs`); the pass, the
request, the route search, and the walk are in `src/Simulation/Game.Settlers.cs`. `ProgramTables` reads the step
times, the door speeds, and the scan windows from the install's program at run time (`StepTimes`, `DoorSpeeds`,
`ScanWindows`). `Game.Digest` holds the buildings, the settlers, the clock, the phase, the scan cursor, the castle's
waiting list, and the settlers' places. The carriers' part is in `src/Simulation/Game.Carriers.cs`
([Carriers](carriers.md#in-the-engine)).

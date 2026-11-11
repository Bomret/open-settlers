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
in the order of their slots (the woodcutter is slot 10, job 5: [Woodcutting](woodcutting.md#the-hut-and-the-job)).

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

The main loop calls, in this order for each pass: `0x428728` (the retry scan, below), the phase step `0x4291c2` (one or
more phases: [Timing](timing.md#rules-that-count-passes)), the flag pass `0x46414d`, the building pass `0x42cb2d`, and the settler pass
`0x42f3d9`. The engine runs one pass for each tick (8 clock units) and runs the flag pass for the carriers only
([Carriers](carriers.md#the-flag-pass)).

- The building pass handles the buildings whose slot divided by 32, modulo 32, is the phase, so a building is
  handled once for each phase cycle. A cycle takes 33 passes in a game with 33 phases and 33 to 49 passes in a
  game with 49 phases, where one pass can run several of the phases 33 to 48
  ([Timing](timing.md#rules-that-count-passes)). The building pass returns at phase 32 and up (`0x42cb3a`).
  A finished building goes to the handler of its type (the table at
  `0x42cd1d`, 8 bytes an entry); the castle's own handler (`0x42e7e0`, knights and defence) is not ported.
- The settler pass handles the settlers whose slot modulo 16 is the phase modulo 16, so a settler is handled at two
  phases of each cycle, 16 passes apart. The next cycle follows after 17 passes in a game with 33 phases and after
  17 to 33 passes in a game with 49 phases. The settler pass returns at phase 32 and up (`0x42f3e6`). Each goes
  through the state table at `0x42fca1`.
- **Departure from the engine:** the program runs the turn of each settler in view once more every pass
  (`0x42f4f1`), which makes the motion on the screen smooth. The simulation must not depend on the view, so with
  `Game.EveryPassTurns` the engine gives that turn to every settler in a state the view draws in motion (2, 3, 4,
  5, 0xe, 0x10, 0x11, 0x19), in view or not, after the slices of every pass (phase 32 included). The other states, the
  knights' among them, keep the slice turns alone, so their events and random draws come at the slice's rate. The game view sets it; without it a settler takes a turn only in its slice, as in the
  original's passes without a view, which is what the emulator recordings of the walks hold.

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

A building of the woodcutter's type (2) asks, at its turn when bits 2, 6, and 7 of its status are clear (`0xc4`), for a
settler of job 5 (`0x42be63`). The search starts at the building's flag and runs over the roads breadth first (below).
At a storage's flag it looks at the storage's list of waiting settlers:

1. A settler of the job waits: the storage clears its entry, the settler takes state 15, mode 255, and the building's
   flag as its target, the building's status gets bit 7, and the storage counts the settler as sent out.
2. None waits: a jobless settler (job 21) and the tool of the job from the stock become one (not ported: the castle
   holds one woodcutter, so a second hut gets no worker).

If the search reaches no storage, or the storage has neither, the request fails and the building sets bit 2.

The type of the woodcutter's hut is 2: its handler (`0x42d945`) asks for job 5, whose work is cutting trees, and the castle holds
one settler of job 5 ([Woodcutting](woodcutting.md#the-hut-and-the-job)). The first walk of this note was read with the type 1 and
job 11 as the woodcutter's, which are another hut's and worker's; the walk is the same for either. A layout's finished-building
entry ([Layouts](layouts.md#finished-buildings)) stands a finished hut of that type on a node whose south-east neighbor holds a
flag or takes one, and whose site is small or large. The command `CommandKind.PlaceBuilding` places a building site, which
asks for no worker ([Build sites](build-sites.md#placing-a-building)). The placement marks the map as the original's
`Building_Place` does: the building's node gets flag bits `0x42`, the flag's node bit `0x10`, the object code 2 (3 for a large
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
the queue is empty (no route). The engine's `RoadNetwork.Route(flagCount, from, to)` (`Game.Network`) returns the first direction, or -1.

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
  work starts here ([Woodcutting](woodcutting.md#the-start-at-the-hut)). The test files of the walk end with the first turn in state 4.
- **State 1, waiting in the castle** (`0x438eeb`): the settler enters its slot in the storage's list of waiting settlers
  every turn (job 10 takes another branch: not ported; the knights, jobs 22 to 26, draw too:
  [Woodcutting](woodcutting.md#the-random-generator)).
- **State 10, the castle's builder** (`0x43f17a`): the settler with the job 4, slot 0, adds the elapsed units times 128 to
  the castle's build progress each turn; when that wraps (about 64 passes after the castle is placed) the castle
  stands: bit 7 of its type goes out, which changes the door speed index from 0x38 to 0x18, and the settler takes state 12.
- **A blocked step:** where the next node holds a settler, the timer is 0x7f, the frame `0x51 + direction`, and the heading
  the direction less 6; at the next turn after the timer the settler tries the same direction again.
- **Passing:** see [Walking home](#walking-home).
- **Not ported:** the chain of blocked settlers that ends in a step against the chain ([Walking home](#walking-home)), the
  castle's own handler (`0x42e7e0`), and the settlers the castle makes later (`0x428a75`).

## Drawing the walk: leads

**Status:** confirmed for the carrier, the woodcutter, and the settler of the type 1 hut by the emulator verb `draw:`
([Emulated](#the-draw-path-emulated)). The recordings `tests/OpenSettlers.Tests/settler-draws.txt` hold what the original draws,
and the engine matches them pass for pass. The figure word and the two tables are also confirmed by the program evidence below
and by the screenshots of `play/woodcutter-figures.play`. Inferred parts are marked. The jobs and states that no recording holds
a settler of are inferred.

The engine draws the hut and the walking settlers. The hut is entry 1418 of the archive, the object of the type 2's draw stub,
drawn like the castle.

What the program does is read from `0x42f512`, `0x43f537`, the job's figure handler, `0x44fff3`, and `0x406610`. The draw record
of a settler takes the frame (byte 1) to pick a sequence from a table. The program loads that table from entry 2 of the graphics
archive at start-up (`0x41e8e0`). It holds 200 big-endian offsets, each to a run of triples (figure byte, dx, dy). The timer
shifted right 3 indexes the run. So the animation follows the step's remaining time in ticks, and the offsets carry the settler
along the step. The frame's index is the shape of the step: `4 + 9 * direction` plus the height difference. So the facing is
the direction of the step.

Terms used below:
- The **figure byte** is the first value of the triple the sequence yields for the settler's frame and time left.
- The **figure word** is a 16-bit value stored with the draw record. Its low byte is the figure byte plus the job's constant.
  Its high byte picks a row of the figure bases.
- The **figure bases** are the first table below, one pair of sprite bases for each high byte of the figure word.
- The **stamp** is the clock value the settler stored at its last turn.

**The figure word** (confirmed: `0x43f537` jumps by byte 0 bits 2 to 6, the job, through the stubs at `0x43f744` plus 8 for each
job). Each stub is a `jmp rel32` to the job's handler. The handler adds a constant to the figure byte and stores the word with the
draw record. `0x44fff3` reads it. The job does not index the tables. The constant does.
- Job 0, the carrier (`0x43f8ae`): no constant, so the word is the figure byte. The carried good adds its own word, the 16-bit word
  at `0x43f4b7` plus 2 * good (`0x43f904`). Its high byte is the row of the figure bases. The log adds `0x1400`
  ([Drawing a carrier with a good](goods.md#drawing-a-carrier-with-a-good)).
- Job 5, the woodcutter (stub `0x43f76c`, handler `0x43fb6b`): a figure byte below 0x80 gets `0x0b00` (`0x43fc16`, the walk).
  It gets `0x1000` (`0x43fc0b`) where byte 13 is 0x80, byte 14 is 1, and the state is 0x10. That is the way back with the log
  ([Woodcutting](woodcutting.md#the-cut)). A figure byte from 0x80 gets `0x0e80` (`0x43fbe5`), so its word is `0x0f00` plus the
  byte less 0x80. That is the felling. The bytes 0x85 and 0x86 also play a sound (`0x44fe13`), which the engine leaves out.
- Job 11, the settler of the type 1 hut (stub `0x43f79c`, handler `0x43fed8`): a figure byte below 0x80 gets `0x2c00`
  (`0x43ff4b`, the walk). It gets `0x2f00` (`0x43ff40`) under the same test as the woodcutter's way back (byte 13 0x80,
  byte 14 1, state 0x10). A figure byte from 0x80 gets `0x2c80` (`0x43ff0f`). It gets `0x2d80` (`0x43ff1a`) where byte 14 is 1.
  The bytes 0x80, 0x87, 0x88, and 0x8f skip the sound request that the others make. The `job11` recording confirms the walk
  (states 5, 2, and 4). The other adds are read from the code only.

**The tables** (confirmed: `0x44fff3` loads `0x4501a1` at `0x4500fb` and `0x4503c5` at `0x450128` with `lea esi, imm32`, and reads
16-bit words from them with `mov ax, [ebx+esi]`). Both addresses are odd, so the words are read unaligned. Read from the even
address before them, the bytes are no words. Read from the odd one, the bases run 0,0 / 48,6 / 96,-1 / 48,24 and the offsets
0,0 / 1,0 / 2,0 and on. The element is a pair of words (4 bytes).
- The figure bases at `0x4501a1`: the pair at 4 times the high byte of the word (`(word & 0xff00) >> 6` at `0x44fff3`) holds the
  base of the figure, from the entry 1850, and the base of the second figure, from the entry 3150. A negative second base means
  no second figure. The table holds 128 pairs in the engine's read (`RuleTables.FigureBases`). The rows the woodcutter takes
  are 0x0b (48, 54), 0x0f (272, none), and 0x10 (48, 60).
- The figure offsets at `0x4503c5`: the pair at 4 times the low byte of the word is what the figure and the second figure add
  (`RuleTables.FigureOffsets`, 256 pairs). The code does not bound the low byte.

The figure is the entry 1850 plus the base plus the first offset. The second figure, where there is one, is 3150 plus its base
plus the second offset. The overlay in the player's colour of the figure 1850 + i is the entry 2500 + i (`0x406610`).

The engine reads both tables and the three constants of the woodcutter's handler from the install (`ProgramTablesImporter.Import`). It
refuses an install whose draw routine does not load those addresses. It also refuses an install whose stub of job 5 does not jump
to `0x43fb6b`, or whose handler has no `add word [edi+8], imm16` at the three addresses.

Screenshot evidence (`play/woodcutter-figures.play` on `layouts/woodcutter.jsonc`): the woodcutter walks out, steps through his
door, fells, and walks back with a hat, an axe, and a log.

**In the engine** (`SettlerFigures`, `StepOffsets`, `SettlerFigureTables`, `SettlerSprites`, `src/MapView`): a settler of the job 0,
5, or 11 in state 2, 3, 4, 5, 0xe, 0x10, 0x11, or 0x19 is drawn. `SettlerFigures` places it and orders it under every graphics
source. It emits three draw terms for it: the figure, the overlay, and the second figure.

A term names the settler's job, activity, heading, frame, animation step (the time left), player, and carried good. It holds no
entry and no colour index (INV-0010).

`SettlerFigures` reads the dx and dy of each step from the typed `StepOffsets` table. That table is entry 2 of the archive and the
only data the code reads from the graphics archive. The rows of the frames come from the program's tables.

The History Edition source (`HistoryEditionSource`, `SettlerFigureTables`, `SettlerSprites`) picks the sprites. It takes the ones
the frame's sequence names for the time left, by the program's figure tables. It reads them from the install's archive when first
asked and fills the overlay with the player's colour. It anchors the second figure by the header of its figure and draws the shadow
before each figure. A source draws nothing for a term it has no image for.

A settler in any other state has no record:
- a carrier that waits next to a flag (0x42),
- a carrier told to go home (0x44, 0x45),
- a settler that claims its node on its way home (0x1c),
- a settler at work (the woodcutter's 0x12, the type 1 hut's 0x1f).

The tile pass draws the carriers in 0x42 to 0x45 instead ([Carriers](carriers.md#drawing)).

The time left is the timer less the clock units since the settler's turn, so the animation moves with the game's ticks. The view
draws the settlers of the jobs 0 (carriers, [Carriers](carriers.md#drawing)), 5, and 11 in every wrap copy. It rebuilds them
after each tick. A term the source has no image for gives no quad, and the viewer neither warns nor throws. The tests fail on a
term the figure tables pick a sprite for that the archive has no image for (`HistoryEditionSettlersTests`). The engine does not draw a settler of another
job. That is a limit of the port. The program draws settlers of every job.

The draw runs ([The draw path, emulated](#the-draw-path-emulated)) confirm the rules below. They cover the carrier in states 2,
3, 5, 0xe, 0x10, 0x19, 0x42, 0x44, and 0x45, with and without each good the recordings carry. They cover the woodcutter in states
2, 4, 5, 0x10, 0x11, 0x12, 0x19, and 0x1c, and the settler of the type 1 hut in states 2, 4, 5, and 0x1f. The engine's draws equal
the original's in every recorded pass.
- The sequence offsets count from the byte after the entry's first dword, as `0x41e8e0` keeps its table. A sequence runs to the
  next one's offset.
- The triple index is the time left shifted right 3.
- The time left is the timer less the clock units since the stamp. The view pass gives a settler in view a turn every pass. That
  turn runs the timer down by the clock units since the stamp and sets the stamp to the clock. The recordings hold each
  settler's timer and stamp before that turn. The engine's draw from them equals the original's.
- The figure's position is the node (32 x - 16 y, 20 y), less 4 times its height byte, less 2 more, plus the triple's dx and dy
  (signed bytes). `0x44ffe7` adds 0x10 and 8 before the blit. The engine's positions are the record's, before that addition.
- The overlay of the figure 1850 + i is the entry 2500 + i, filled with the palette index 0x40 for player 0. The woodcutter's
  hat is that overlay. The carrier's overlay is the same. The draw path has no other colour for either, so the fill is the
  player's.
- The second figure moves by the header bytes 0 and 1 of the figure 1850 + i, read as signed bytes (`0x4066d2`, `0x4066e4`). The
  figure and the overlay stay where they are. A settler draws its figure, then its overlay, then its second figure. A figure
  entry that is absent or no sprite, an overlay with no drawn pixel, or a job with no second figure draws what is left.
- The record joins the list of the view's row of the settler plus the signed byte of the frame in the table at `0x43f402`
  (181 bytes, `RuleTables.FrameRows`). The frames 9 to 26, 63 to 80, 121 and 122, and 125 to 128 hold -1 and move a record up
  one row. Every other frame holds 0. The lists are drawn from the top row to the bottom, so a settler drawn up a row is drawn
  before the settlers of the row it stands in. Records of one row come in the order of the original's scan (below).
- The frames 0x74 to 0x78 are the cut. `0x438a9e` sets the frame ([Woodcutting](woodcutting.md#the-cut)). The frames 0x74,
  0x76, and 0x77 hold the figure bytes 0x80 and up. That is the felling, with the row 0x0f and two sprites. The frames 0x75
  and 0x78 hold walking bytes only. The frames 0x74, 0x76, and 0x77 also hold walking bytes for part of their time left.

**Confirmed** the same way: the order of the records of one row. `0x42f512` scans the nodes of the view row by row, two rows a
step. It appends a record as `0x43f537` builds it. So a list holds its records by the node's row, then the column from the
view's west edge. The recordings hold pairs of neighbours in one list row, from different node rows and from one node row. Two
of them are the carrier next to the woodcutter and the type 1 hut's settler next to a carrier. Both orders of their slots occur.
The engine's order by row, node row, column equals the original's in every one.

**Inferred:** the draws of every job other than 0, 5, and 11, and of every state that no recording holds a settler of. The
engine does not port them.

The player's colour index is `0x40` plus 4 times the player byte with 1 and 2 exchanged. `0x44ffe7` turns a player bits value of
1 into 2 and 2 into 1 before the blit. This is read from the code. No recording holds another player than 0. The engine does not
exchange them.

### The draw path, emulated

The harness verb `draw:` (`tools/GhidraDump/emulate.py`, `EmulateDraw.java`; [the harness](../../tools/GhidraDump/README.md#emulator-harness))
runs the settler draw of the main loop (`0x425a10`). It runs after the passes of a `carrier:`, `wood:`, `goods:`, `home:`, or
`remove:` game, on the reference install only.

Use `draw:` instead of a screenshot or a `play` run to check what a settler draws. A screenshot shows a picture that a reader
judges by eye. `draw:` gives the original's sprite entries, fill colours, and positions for each pass, and the tests compare the
engine's draws with them exactly. It is the stronger check. It runs only on the reference build of `Siedler1.exe`. Use a
screenshot or a `play` run where no reference install is at hand.

After the simulation's passes the verb runs the view pass `0x42f4f1`. For each view, `0x42f512` does the following:
- It clears the row lists (`0x4409d5`).
- It scans the nodes of the view for settlers.
- It gives each settler a turn through `0x42fc81`.
- It builds the settler's record through `0x43f537`.
- It ends the lists with `0x440a53`.

Then, for each row of the view, the row draw `0x44ffe7` draws each record of the row through `0x406610`. Its blits are `0x405590`
(a sprite) and `0x405880` (a colour fill). The harness skips the shadow of each settler (`0x4061a0`, the mask entry 4); the engine differs here and draws it ([Map objects](map-objects.md#the-drawing-routines)). It also
skips the sound request of the felling bytes 0x85 and 0x86 (`0x44fe13`).

For each pass of its windows, in drawing order, the harness records each settler's record: the slot and the sprites blitted. Each
sprite is an entry, a fill colour (-1 for none), and a position. The view is a stand-in that the harness builds. It is the
640 by 480 view of `0x4200b6` (38 columns, 28 rows), looking at a node. Its screen origin is chosen so that a position is the
node's map pixel. The graphics archive is read from the install. The rule data of its entry 2 is prepared as `0x41e8e0` does. The
passes before the first window run without the view, so a game's first settlers walk as in the other recordings. From the first
window on, each settler in view takes a turn every pass.

**Recordings** (`tests/OpenSettlers.Tests/settler-draws.txt`, from the History Edition's `Siedler1.exe`, SHA-256 in `docs/rules/AGENTS.md`):
- `carrier`: a hut of the woodcutter with a road to the castle and a log laid on the hut's flag at pass 5 (`goods:`), the carrier walking
  its road (state 3), waiting next to a flag (0x42: no record), and walking with the log (state 3, mode 7);
- `woodcutter`: the same game without the log, the woodcutter walking to his door (state 2, 5), stepping through it (4), walking free
  (0x10), felling (0x11, the frames 0x74 to 0x78), and walking back with the log (0x10, byte 13 0x80, byte 14 1) and through his door again;
- `job11`: the same game with a hut of the type 1 (`goods:`, the building type 1): the settler of job 11 walking to his door (states 5, 2) and
  stepping through it (4) in the rows of the handler `0x43fed8`; `job11-work`: the same with a log laid at pass 215, the settler at work (0x1f: no
  record) while a carrier walks, with and without the log;
- `walk-home`: the woodcutter sent home as the teardown of a hut sends its settlers (`home:`, pass 68): claiming its node (0x1c: no record, a carrier
  walking in the same passes), searching for a flag (0x19), walking home free (0x10), on the road (2), and through the castle's door (4);
- `rejoined`, `dismissed`, `dismissed-with-log` (`remove:`): carriers told to go home after a road was joined (0x45: no record), taken down (0x44:
  no record), and taken down while carrying a log, which then search for a flag (0x19), walk free (0x10), and walk on the road (2);
- `goods`: one good laid on the hut's flag every 223 passes, goods 1 to 25 (`goods:`), and the carrier walking its road (state 3) and handing the
  good over at the castle (0xe) with each good it took: 2 to 13, 15 to 17, 19 to 22, 24, 25, and the log; `goods-alone`: the goods 1, 14, 18, 23, and 26,
  which the first game did not take, laid one at a time at the type 1 hut's flag, where the woodcutter lays no log. Together they hold the carrier with every good
  of the table at `0x43f4b7` (the goods 1 to 26), in both states.

`SettlerDrawTests` gives `SettlerFigures.Draws` each pass's settlers, the heights of their nodes, and the clock, and compares the sprites the figure tables pick for its terms, their colours, and
positions with the original's, in order; it names the first pass and settler that differ. The recordings were made on the reference install
only, the tests skip where none is present, and a `draw:` run that reaches no pass of its windows, or whose pass draws no settler, fails and prints no recording.

## Walking home

A settler whose work is taken away (the teardown of a building sends the settlers inside it, `0x461b77`: it sets the state
byte to `0x1c` and nothing else) walks to the nearest own flag with a road and from there to a storage. The state table's
stubs lie at `0x42fca1` plus 8 times the state, so `0x1c` is `0x434946` and `0x19` is `0x4353ef`.

**Emulated.** The harness verb `home:` runs the passes of `wood:` and, before one pass, sends a chosen castle settler home
(optionally moved to another node first, or put into another state and mode byte); the engine matches the settler's 16 bytes
and the generator's words after every pass, and the word of its job in the castle's list of waiting settlers, until the
settler waits in the castle in state 1, in six games (`tests/OpenSettlers.Tests/home-walks.txt`): settlers of the jobs 7, 2, and
5 (the woodcutter) from the castle, a settler 11 nodes away whose search finds no flag and walks by random nodes (about 700
passes), a settler in state `0x19` with mode 1 that meets the road's carrier and passes it, and a settler on a flag no road
leads from to the castle, which takes state `0x19` with mode 1. The game takes the building table's dummy first, as a real
game's start does (`0x45da8f` at the end of `0x421530`), so the castle is building slot 1: a settler that enters a building
whose link is 0 takes state `0x19`, so with the castle in slot 0 no settler would ever stay in it. The engine's castle is
slot 0 and has no dummy; the engine treats every building on the node as a building.

- **State `0x1c`.** When no settler stands on the node (the node's settler word is 0) the settler writes its slot there, takes
  the frame `0x52`, timer 0, mode 0, the stamp of the clock, and state `0x19`; else it waits.
- **State `0x19`.** After its timer ran out it searches the spiral table (entries 1 to 258) from the near end, or from the
  far end where the mode byte is not 0, for the first node that holds a flag (node bit 7), whose flag item's byte 4 has a
  road bit (low six bits) and whose owner bits (the top three bits of the height byte) are the settler's player plus 4. The
  table's offset becomes the vector of the free walk (bytes 11 and 12), byte 13 is `0x80`, byte 14 `0xff`, byte 15 0, the timer 0,
  and the state `0x10` (`0x35` for the jobs 22 to 26). The mode of a settler that failed to find its storage is 1, so it
  walks to the farthest such flag.
- **No flag found.** The settler draws random offsets: ten tries with the generator's low and high byte masked with 15 and
  less 8, twenty with 31 and 16, twenty with 63 and 32, then without end with 15 and 8. A node with no object and a height
  not 0 is taken where it carries the settler's owner bits (always in the last phase). The offsets become the vector as above.
- **Arrival (`0x4385ab`, from `0x437ddd`).** The free walk ends at a vector of 0. The jobs 0 to 4, 6, 9, 10, 12, 13, 15 to 19,
  and 21 take `0x4385ab` at once; the jobs 5, 7, 8, 11, 14, and 20 take it where byte 13 is `0x80` and byte 14 negative (the
  knights, 22 to 26, where byte 13 is `0x80`). On an own flag with a road the settler takes state 2 with mode `0xfe`, bytes 12
  to 15 and the timer 0; anywhere else it takes state `0x19` with mode 0 and timer 0, and searches again.

- **The walk to the storage (state 2 with mode `0xfe`, `0x4398ea`).** A byte of 0x80 or more in the mode (the mode `0xff` of a
  worker to its hut is one) enters the building north-west of the flag it walks to; the mode `0xfe` has no target yet (word 12
  is 0), so at the first flag the settler picks its storage (`0x45d09b`): a breadth-first search over the flags, six
  directions from north to east, for the nearest flag whose item has bit 7 in byte `0x42` (the castle's flag does from the
  start of the game); the storage's flag is the building's word at 6. The castle is the only storage, so the pick is the
  castle's flag where a road leads to it, or where the settler stands on it. A pick that fails sends the settler to state
  `0x19` with mode 1 and timer 0, as does a route search that finds no road on (`0x43a24e`: for the modes `0x80` to
  `0xfe`). The walk is the walk of a worker to its hut: it steps along the route, at the castle's flag enters the castle
  (`0x43c7c2`), and takes state 4.
- **Passing (`0x43a6c1`).** A step east, south-east, or south onto a node that a walking settler (state 2 or 3) holds, and
  that settler is blocked on the node of this one (its byte 14 is the opposite direction less 6), swaps the two: each takes
  the node of the other, the stepping settler the shape `4 + 9 * (direction + 6) + rise` and the byte 14 `(direction + 3) mod
  6`, the other the shape `4 + 9 * ((direction + 3) mod 6) - rise`, byte 14 the direction, and as timer that shape's time.
  Every other occupied node blocks (timer `0x7f`, frame `0x51 + direction`, byte 14 the direction less 6); the steps west,
  north-west, and north have no swap. A carrier's steps (`0x43b788` and its siblings) are the same. **Emulated:** the settler
  meets the road's carrier, which waits for it north-west of the flag in the second game above, and passes it.
- **Blocked steps (`0x4398ea`).** A settler whose byte 14 is negative counts its turns in byte 15; at the tenth on a flag, and
  the fiftieth elsewhere, the original follows the chain of settlers that wait for one another (100 hops at most; no draw
  is made) and counts from 0 again; a chain that comes back to the settler makes it step against the chain and so pass.
  **Inferred:** the count (no run blocks a settler for 10 turns; the scenarios never hold a settler for a second turn), and
  **not ported:** the chain's step against the chain.
- **State 4, entering a storage (`0x43c96b`, then `0x43cd06`).** At the castle's door the settler does what the job's routine
  of `0x43ca54` does; every routine but those of the jobs 4 and 21 begins with a test of the mode `0xfe` and for that
  mode goes on at `0x43cd06`: the node's settler word is cleared, the settler takes the building's storage as byte 14 and
  15 (the castle: 0), word 12 0, mode 0, and state 1. The turn of state 1 enters it in the castle's list. **Emulated:**
  the jobs 7, 2, and 5 reach state 1 and the list holds their slot.
- **A good carried home.** The settler on the walk home holds no good: its mode byte is `0xfe` where a carrier holds the
  good code. A good that a settler carried is not carried into the castle; `0x462c8d` (called by the teardown routines
  `0x4617fd`, `0x462153`, `0x46385b`) counts it back: for the goods 14 and 15 the player's counter at `+0x4c` goes down,
  and where the good's class byte (the table at `0x462d6d`) is not negative the building at the flag it was destined for
  (its slot is the good's destination word) loses one in byte 8 (class 0) or byte 9 (other) of its item, unless that
  building is the castle (type `0x18`) or of the type `0xa`. **Emulated** for a log lying on a flag that is taken down with the
  castle as its destination (the castle that is still built is not of the type `0x18`, so its byte 9 falls); the other cases are read
  from the code (**inferred**). The teardown's setters are in [Removal](removal.md): `0x4617fd` sends the worker home (state `0x19` or `0x1c`),
  `0x46385b` a settler on a road that goes away (state `0x19`), and a carrier that waited next to a flag takes state `0x44` and then `0x19`.
  **Emulated:** a carrier and a worker go home from the states `0x44` and `0x19` and `0x1c`.

**Not ported:** the commands that tear down, the boatman's state `0x1a` (`0x4350f8`; water roads are not ported), state `0x35`
of the knights, the arrivals of the jobs 8, 11, 14, and 20 where they do not walk home, the entry of the jobs 4 and 21 into
a storage (`0x43ceea`, and `0x43cc5f`, which also counts the settler in the word `0x40` of the storage item), the chain of
blocked settlers, and a storage other than the castle. **Confirmed** by the `walk-home`, `dismissed`, and `dismissed-with-log` recordings: the
state `0x19` is drawn (the frame `0x52` picks the sequence) and the state `0x1c` is not. **Inferred:** the second and third rings of the random search and its last phase without end (no run
reaches them: the scenarios find a node in the first ring), the ring masks of those phases are read from the code only.

## Open leads

- The second way of the request for a worker (a jobless settler and a tool); the carriers' request has it without the tool
  ([Carriers](carriers.md#the-request)).
- The states 12 and 25; the states 4, 6, 7, 0x10, 0x11, and 0x12 are in [Woodcutting](woodcutting.md).
- The castle's handler of type 0x18, the settler creation of `0x428a75`, and the goods part of the flag pass `0x46414d`.
- The count of blocked turns (10 on a flag, 50 elsewhere) and the step against a chain of blocked settlers (`0x439985`): no
  run reaches them.
- A worker that is outside a hut that is taken down, and the settlers of the castle, the military buildings, and the storage that the
  teardown `0x4617fd` handles in branches of its own: [Removal](removal.md#taking-a-building-down).
- That the phase count is 33 for the game of one player with no computer opponent (`0x42055e`).
- The hut's object code and its site rule (no run places a building).
- The knights' idle turns draw from the game's generator, which continues from the map's generator: ported
  ([Woodcutting](woodcutting.md#the-random-generator)).

## In the engine

`Game.Buildings` (a `BuildingTable` of `Building`s, each with a `BuildingType`) and `Game.Settlers` (a `SettlerTable` of
`Settler`s) hold the items (`src/Domain/Building.cs`, `Settler.cs`). A `Settler` is one object for its whole life. It has an
identity and body (node, player, timer, stamp, frame). It has a `Job` that can change: a settler without a job becomes a carrier
when a road takes it, and stays the same settler with the same thing ID. It has one `Activity` record that holds only that
activity's data (`src/Domain/Activity.cs`).

Each activity type keeps the code of the original's state it stands for (`Activity.Code`). `src/Domain/Activity.cs` lists the
types and their codes. A state code with no activity type is kept as it is in `RawState`, whose turn changes nothing. The
original's branch for mode `0xfd` to state `0x49` and its branch for state `0x4a` are not ported, because no rule writes
those states.

The original shares bytes 11 to 15 of the item between its states. An activity names the values it uses: an `Errand` for the
walk's mode and target, a `Load` for the good a carrier carries and where it goes, a `Heading`, and a free walk's `Trip`. The
bytes it leaves unused ride along in its `Residue`, so that a change of activity keeps them as the original does. No rule reads
or writes the residue. A search for a flag home (`SearchesHome`) holds its mode byte itself (`FarEndMark`). A heading gives the
idle frame of a carrier (`Heading.IdleFrame`), and a job says whether its settler arrives home generically
(`Job.ArrivesHomeGenerically`).

`SettlerEncoder` (`src/Adapters/Simulation/SettlerEncoder.cs`) writes the state, mode, target, heading, next, and stamp of the
original's item from the typed settler. The digest, the event snapshots, and the thing readouts use it. The readouts show the
original's encoding as read-only display, and no rule reads it. `BuildingEncoder` does the same for the building's status,
worker, fill, and type index.

The table pass is `TablePass` (`src/Application`). It runs the flag, building, and settler slices of a phase in the original's
order and hands each settler to the behavior registered for its activity type in `ActivityRegistry`. `GameFactory` registers
the behaviors (`Game.Behaviours`), one for each activity type. The pass is `MainPass` (`src/Application/MainPass.cs`). The
castle's settlers and the request for a worker are `CastleRoster`. The walk to an errand is `WalkBehavior` with the step in
`RoadStep`, and the route search is `RoadNetwork.Route`. `ProgramTablesImporter` reads the step times, the door speeds, and the
scan windows from the install's program at run time (`StepTimes`, `DoorSpeeds`, `ScanWindows`). `Game.Digest` (`GameDigest` in
the adapters) holds the buildings, the settlers, the clock, the phase, the scan cursor, the castle's waiting list, and the
settlers' places.

The carriers' part is in `src/Application/CarrierBehavior.cs` ([Carriers](carriers.md#in-the-engine)). The woodcutter's part and
the map's object pass are in `src/Application/WoodcutterBehavior.cs` ([Woodcutting](woodcutting.md#in-the-engine)). The walk home
is in `src/Application/WalkHomeBehavior.cs`. Both build on `SettlerMovement` (the settlers' nodes, the step, and the timer).

# Forestry

How the forester works: which building and which job he belongs to, how he picks a node to plant on, how he walks to it, waits,
and puts a sapling down, and how he goes home, and how the saplings grow into trees the woodcutter fells. He works as the
woodcutter does ([Woodcutting](woodcutting.md)) with his own search, an arrival that plants, and no load. Addresses and routine
names are in the [Program index](program-index.md#simulation); the settlers and their walk to the building are in
[Settlers](settlers.md); the worker's request is in [Construction](construction.md#the-finished-building-asks-for-its-worker).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do, with a
finished building of type 9 at a flag, and runs the original's own routines for each pass. The engine runs the same commands and
matches the forester's item after every change of his state, every object byte that changes, the building's bytes where they
change, and the generator's words, in two games (`tests/OpenSettlers.Tests/forester-walks.txt`): a building in the forest at the
generated map's seed for 6000 passes (six saplings planted, three grown, one stump gone), and the same game with 30 saplings set
by hand on the map before pass 2 (the verb's SET field), for 2500 passes (seven grow, at the passes the object pass gives). The
removal of his building matches the original in two games (`removals.txt`: `forester-inside`, `forester-out`), and the engine draws
him as the original does in the passes of one recording (`settler-draws.txt`, `forester`).

## The building and the job

**Status:** confirmed.

The handler of the building type 9 (`0x42d9c9`, entry 9 of the table at `0x42cd1d`) asks for a settler of job 8 with no tool
([Construction](construction.md#the-finished-building-asks-for-its-worker)). Job 8's work is the one below: it puts the objects
`0x67` and `0x68` on a node. So the type 9 is the forester's building and job 8 the forester. The castle starts with no settler of
job 8, so the request takes its second way: a settler without a job becomes the forester and no tool leaves the stock
([Settlers](settlers.md#the-request)). The building's sprite is the object `0xaa`, entry 1420. An earlier note named the type and
the job from the handler alone ([Build sites](build-sites.md)).

## The start at the building

**Status:** confirmed; **emulated**.

At the door the worker is in state 4 and runs the routine of his job through the table at `0x43ca54` as the woodcutter does: for
job 8, `0x43d009`. Where the settler is not sent home (byte 11, the mode, is not `0xfe`) the routine empties the building's node of
the settler's slot and takes state `0x13`; a settler sent home goes on at `0x43cd06` ([Settlers](settlers.md#walking-home)).

## Choosing a node

**Status:** confirmed; **emulated**.

State `0x13` (`0x438c9f`): when the timer has run out, the worker takes a draw of the game's generator (`0x44257a`) and from it an
entry of the spiral table, `((draw & 0x1fc) + 4) / 4`, an entry from 1 to 128, as the woodcutter does. The node of that entry,
counted from the node he stands on (the building's), is his goal where all of these hold:

- the flag byte of the node, without its bit 7, is 0: no road leaves the node and the node is no lake's middle or large object;
- the object byte of the node, without its bit 7, is 0: no object stands there, no flag or building either;
- the terrain byte of the node's tile is `0x55`: both triangles are the lowland code 5 ([Map form](map-form.md)); and
- the terrain byte of the tile north-west of the node (the offset at `+0x14` of the state block, as the stonecutter's search reads
  it) is `0x55` too.

Where one fails, `0x2bc` (700) is added to the timer (the woodcutter's miss adds 400) and he draws again, until that sum wraps the
timer, which ends the turn. **No other check:** a node where another settler stands, or a node the forester does not own, is a goal
as well.

On a hit the vector (bytes 11 and 12) is the goal's offset from the building less one each way, bytes 13 and 14 hold the negated
vector, the way back, and byte 15 holds `0x10`, the state of the free walk, which comes after the step from the building onto its
flag (state 7, `0x43e02e`, as the woodcutter's).

## The walk out

**Status:** confirmed; **emulated**.

State `0x10` is the free walk ([Woodcutting](woodcutting.md#the-walk)) with no check of its own. At its end the arrival of job 8
(`0x437ddd` jumps by the job, entry 8 goes to `0x438194`) takes over.

## Waiting and planting

**Status:** confirmed; **emulated**.

The arrival at the goal, with byte 13 not `0x80`: the way back (bytes 13 and 14) becomes the vector (bytes 11 and 12). Where the
object byte of the node he stands on, without its bit 7, is 0, byte 14 is cleared (the sapling is not down), the frame is `0x79`, the
timer is `0xbf`, and he takes state `0x14`. Else byte 13 is set to `0x80` (the way-back mark), bytes 14 and 15 are cleared, the timer is 0, and he walks back at once, still in
state `0x10` (`0x4381f4`; a goal that took an object between the search and the arrival, scenario `goal-taken`).

State `0x14` (`0x4389bf`), when the timer has run out and byte 14 is 0:

1. The frame is `0x7a`. A draw of the generator gives the sapling: `0x67`, or `0x68` where the draw's low bit is set. Where no object
   stands on the node (without bit 7) and no road leaves it (flag bits 0 to 5), the object byte keeps its bit 7 and takes the sapling's code
   (the planting does not look at the flag byte's bit 6).
2. Byte 14 is complemented (it is `0xff` now), and `0x80` is added to the timer. Where that does not wrap the timer, the turn goes on
   at once; where it wraps, the turn ends, and the next turn after the timer has run out is the next part.
3. With byte 14 not 0 he walks back: byte 13 `0x80`, byte 14 0, byte 15 0, state `0x10`, timer 0.

The draw is taken whether the node holds an object or not, so the generator is the same.

## The walk home

**Status:** confirmed; **emulated**.

The way back is the free walk of state `0x10` with the vector of bytes 11 and 12 and the mark `0x80` in byte 13. At its end the arrival
of job 8 reads byte 13 as `0x80` (`0x438217`): with byte 14 negative he goes home as any settler (`0x4385ab`,
[Settlers](settlers.md#walking-home)); else byte 11 is 0 and `0x43c7c2` takes him into the building, from where he searches for the next
node. He carries nothing and lays nothing on the flag.

## Growth

**Status:** confirmed; **emulated**.

The object pass (`0x428d10`, [Woodcutting](woodcutting.md#the-object-pass)) sends an object code from `0x53` through the table at `0x428eb8`,
8 bytes an entry. The entries of the two saplings:

- `0x67` (`0x4290d5`) and `0x68` (`0x42910f`) take one draw. Where the draw's bits 8 and 9 are clear (one chance in four) the node's
  object byte keeps its bit 7 and takes a tree: `0x10` plus the draw's low three bits for `0x67`, `8` plus the draw's low three bits
  for `0x68`. Else nothing changes.

So a sapling is a tree at once, with no stage between: the trees `0x10` to `0x17` and `8` to `0xf` are the codes the woodcutter's search
reads (`0x438c11`, [Woodcutting](woodcutting.md#choosing-a-tree)), and the trees of the codes 8 to 15 take the second cutting variant. The
cursor visits a node once in a round of about 2560 passes on a map of 32 by 32, so a sapling stands several rounds before it grows.

The same table sends the codes `0x69` to `0x6d` and `0x79` and up to an entry that adds one to the code, `0x6e` to one that sets `0x79`, `0x6f`
to one that clears the object, and `0x70` to `0x78` to one that clears the object where the word at `+0x28c` of the state block is 0. The
generation places none of these codes and no building's work puts them down: **not ported**.

## Drawing

**Status:** the figure words are **confirmed** by the draw run of the `forester` recording (`settler-draws.txt`: the walk out, the arrival
and the wait, the planting, the first steps back); the saplings are drawn as every map object is.

The figure handler of job 8 (`0x43fd88`, reached by the stub at `0x43f784`) adds `0x0e00` to the figure byte for a byte below 0x80, which
covers the walk and the frames `0x79` and `0x7a` of the wait and the planting, and `0x1080` for a byte from 0x80, where it also toggles
the settler's sound bit and asks for the sound `0x30` (no recorded pass reaches that byte). The engine reads the two words from the program's
instructions (`RuleTables.ForesterFigureAdds`). The state `0x14` is drawn; the state `0x13`, in the building, is not.

The sapling stages are the objects `0x67` and `0x68`, drawn from the map like any object (entries 1345 and 1346 of the graphics archive,
the entry being 1250 plus the code less 8). The `open-settlers` content set draws them as a thin stem with a small crown (`object/103`,
`object/104`) and the forester as a green figure in each pose (`settler/forester/*`); the planting is drawn in the pose of work.

## Removal

**Status:** confirmed; **emulated**.

A forester's building is taken down as the woodcutter's hut is ([Removal](removal.md)): his worker walks home. A worker in the building
takes state `0x1c` and walks to the castle. A worker out at his goal is sent home where he stands: he takes state `0x19`, searches for the
nearest flag of his player, and walks from there to the castle (`removals.txt`, `forester-inside` at pass 800 and `forester-out` at pass
1345, in the state of the wait).

## Open leads

- The figure byte from `0x80` of the forester's handler and its sound: no pass of the recorded runs reaches it. likely
- The handlers of the codes from `0x69` of the object pass (above). guess
- A node a settler stands on is a goal and a planting node: the original has no check, the engine has none, and no emulator run puts a
  settler there. likely

## In the engine

`src/Application/WoodcutterBehavior.Forester.cs` (the forester's part of `WoodcutterBehavior`): `StartForester`, `SearchSpot`,
`ArriveForester`, `Plant`, and `GrowSapling` (the object pass's handler, called from `ObjectPass`). The activities are `LooksForSpot`
(state `0x13`) and `Plants` (state `0x14`, with the way back, whether the sapling is down, and the walk's memory), the walk to the node is
a `WalksFree` whose purpose is `ToSpot`. `Job.Forester` is job 8, `MapObjectKind.Sapling` the codes `0x67` and `0x68`, and
`MapObject.Grown` the tree a draw makes of one. `CastleRoster` asks for his worker for a building of type 9 through the request's second
way, and a layout's `forester` entry stands a settler without a job in the building as the forester (`WalkBehavior.StandWorkerInHut`).

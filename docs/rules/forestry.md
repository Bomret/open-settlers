# Forestry

This note covers how the forester works. It states which building and which job he belongs to, how he picks a node to plant on, and how he walks to it. It states how he waits, puts a sapling down, and goes home. It also states how the saplings grow into trees that the woodcutter fells. He works as the woodcutter does ([Woodcutting](woodcutting.md)), with three differences: his own search, an arrival that plants, and no load. Addresses and routine names are in the [Program index](program-index.md#simulation). The settlers and their walk to the building are in [Settlers](settlers.md). The worker's request is in [Construction](construction.md#the-finished-building-asks-for-its-worker).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do. The game has a finished building of type 9 at a flag. The verb runs the original's own routines for each pass.

The engine runs the same commands. It matches these values in two games (`tests/OpenSettlers.Tests/forester-walks.txt`):

- the forester's item after every change of his state;
- every object byte that changes;
- the building's bytes where they change;
- the generator's words.

The first game has a building in the forest at the generated map's seed. It runs 6000 passes. Six saplings are planted, three grow, and one stump is gone. The second game is the same game with 30 saplings set by hand on the map before pass 2. The verb's SET field sets them. It runs 2500 passes, and seven saplings grow, at the passes the object pass gives.

The removal of his building matches the original in two games (`removals.txt`: `forester-inside`, `forester-out`). The engine draws him as the original does in the passes of one recording (`settler-draws.txt`, `forester`).

## The building and the job

**Status:** confirmed.

The handler of building type 9 is at `0x42d9c9`, entry 9 of the table at `0x42cd1d`. It asks for a settler of job 8 with no tool ([Construction](construction.md#the-finished-building-asks-for-its-worker)). Job 8's work, described below, puts the objects `0x67` and `0x68` on a node. So type 9 is the forester's building and job 8 is the forester.

The castle starts with no settler of job 8, so the request takes its second way. A settler without a job becomes the forester, and no tool leaves the stock ([Settlers](settlers.md#the-request)). The building's sprite is the object `0xaa`, entry 1420.

## The start at the building

**Status:** confirmed; **emulated**.

At the door the worker is in state 4. He runs the routine of his job through the table at `0x43ca54`, as the woodcutter does. For job 8 that routine is `0x43d009`.

Where the settler is not sent home, byte 11, the mode, is not `0xfe`. Then the routine empties the building's node of the settler's slot and takes state `0x13`. A settler sent home goes on at `0x43cd06` ([Settlers](settlers.md#walking-home)).

## Choosing a node

**Status:** confirmed; **emulated**.

State `0x13` is at `0x438c9f`. When the timer has run out, the worker takes a draw of the game's generator (`0x44257a`). From the draw he takes an entry of the spiral table, `((draw & 0x1fc) + 4) / 4`. That entry is from 1 to 128, as for the woodcutter.

The node of that entry is his goal where all of these hold. The entry is counted from the node he stands on, which is the building's.

- The flag byte of the node, without its bit 7, is 0. That means no road leaves the node, and the node is no lake's middle or large object.
- The object byte of the node, without its bit 7, is 0. That means no object stands there, no flag, and no building.
- The terrain byte of the node's tile is `0x55`. Both triangles are then the lowland code 5 ([Map form](map-form.md)).
- The terrain byte of the tile north-west of the node is also `0x55`. The offset is at `+0x14` of the state block, and the stonecutter's search reads it the same way.

Where one check fails, `0x2bc` (700) is added to the timer, and he draws again. The woodcutter's miss adds 400. The draws go on until that sum wraps the timer, which ends the turn.

**No other check:** a node where another settler stands is a goal as well. So is a node the forester does not own.

On a hit, the vector in bytes 11 and 12 is the goal's offset from the building less one each way. Bytes 13 and 14 hold the negated vector, which is the way back. Byte 15 holds `0x10`, the state of the free walk. The free walk comes after the step from the building onto its flag, which is state 7 at `0x43e02e`, as the woodcutter's.

## The walk out

**Status:** confirmed; **emulated**.

State `0x10` is the free walk ([Woodcutting](woodcutting.md#the-walk)), with no check of its own. At its end the arrival of job 8 takes over. The routine at `0x437ddd` jumps by the job, and entry 8 goes to `0x438194`.

## Waiting and planting

**Status:** confirmed; **emulated**.

The arrival at the goal has byte 13 not at `0x80`. Then the way back in bytes 13 and 14 becomes the vector in bytes 11 and 12. What follows depends on the object byte of the node he stands on, without its bit 7.

- Where that byte is 0, byte 14 is cleared, because the sapling is not down. The frame is `0x79` and the timer is `0xbf`. He takes state `0x14`.
- Otherwise byte 13 is set to `0x80`, the way-back mark. Bytes 14 and 15 are cleared and the timer is 0. He walks back at once, still in state `0x10` (`0x4381f4`). This happens when a goal took an object between the search and the arrival, as in scenario `goal-taken`.

State `0x14` is at `0x4389bf`. It acts when the timer has run out and byte 14 is 0. It has three parts:

1. The frame is `0x7a`. A draw of the generator gives the sapling. It is `0x67`, or `0x68` where the draw's low bit is set. Where no object stands on the node, without bit 7, and no road leaves it, flag bits 0 to 5, the object byte keeps its bit 7 and takes the sapling's code. The planting does not look at the flag byte's bit 6.
2. Byte 14 is complemented, so it is `0xff` now. `0x80` is added to the timer. Where that does not wrap the timer, the turn goes on at once. Where it wraps, the turn ends. The next turn after the timer has run out is the next part.
3. With byte 14 not 0, he walks back. Byte 13 is `0x80`, bytes 14 and 15 are 0, the state is `0x10`, and the timer is 0.

The draw is taken whether the node holds an object or not, so the generator is the same.

## The walk home

**Status:** confirmed; **emulated**.

The way back is the free walk of state `0x10`. It uses the vector of bytes 11 and 12 and the mark `0x80` in byte 13. At its end the arrival of job 8 reads byte 13 as `0x80` (`0x438217`). Then one of two things happens:

- With byte 14 negative, he goes home as any settler (`0x4385ab`, [Settlers](settlers.md#walking-home)).
- Otherwise byte 11 is 0 and `0x43c7c2` takes him into the building. From there he searches for the next node.

He carries nothing and lays nothing on the flag.

## Growth

**Status:** confirmed; **emulated**.

The object pass (`0x428d10`, [Woodcutting](woodcutting.md#the-object-pass)) sends an object code from `0x53` through the table at `0x428eb8`. An entry has 8 bytes. The entries of the two saplings, `0x67` (`0x4290d5`) and `0x68` (`0x42910f`), take one draw. Where the draw's bits 8 and 9 are clear, the node's object byte keeps its bit 7 and takes a tree. That is one chance in four. The tree is `0x10` plus the draw's low three bits for `0x67`, and `8` plus the draw's low three bits for `0x68`. Otherwise nothing changes.

So a sapling is a tree at once, with no stage between. The trees `0x10` to `0x17` and `8` to `0xf` are the codes the woodcutter's search reads (`0x438c11`, [Woodcutting](woodcutting.md#choosing-a-tree)). The trees of the codes 8 to 15 take the second cutting variant. The cursor visits a node once in a round of about 2560 passes on a map of 32 by 32. So a sapling stands several rounds before it grows.

The same table sends other codes to entries as follows:

- The codes `0x69` to `0x6d` and `0x79` and up go to an entry that adds one to the code.
- The code `0x6e` goes to an entry that sets `0x79`.
- The code `0x6f` goes to an entry that clears the object.
- The codes `0x70` to `0x78` go to an entry that clears the object where the word at `+0x28c` of the state block is 0.

The generation places none of these codes, and no building's work puts them down. They are **not ported**.

## Drawing

**Status:** the figure words are **confirmed** by the draw run of the `forester` recording (`settler-draws.txt`). The run covers the walk out, the arrival and the wait, the planting, and the first steps back. The saplings are drawn as every map object is.

The figure handler of job 8 is at `0x43fd88`, reached by the stub at `0x43f784`. It adds two values to the figure byte:

- `0x0e00` for a byte below 0x80. That covers the walk and the frames `0x79` and `0x7a` of the wait and the planting.
- `0x1080` for a byte from 0x80. There it also toggles the settler's sound bit and asks for the sound `0x30`. No recorded pass reaches that byte.

The engine reads the two words from the program's instructions (`RuleTables.Get<ForesterFigureAdds>(new Forester())`). The engine draws state `0x14`. It does not draw state `0x13`, in the building.

The sapling stages are the objects `0x67` and `0x68`. The engine draws them from the map like any object. They are entries 1345 and 1346 of the graphics archive, where the entry is 1250 plus the code less 8. The `open-settlers` content set draws them as a thin stem with a small crown (`object/103`, `object/104`). It draws the forester as a green figure in each pose (`settler/forester/*`). The planting has the pose of work.

## Removal

**Status:** confirmed; **emulated**.

A forester's building is taken down as the woodcutter's hut is ([Removal](removal.md)), and his worker walks home. A worker in the building takes state `0x1c` and walks to the castle.

A worker out at his goal is sent home where he stands. He takes state `0x19`, searches for the nearest flag of his player, and walks from there to the castle. The runs are `forester-inside` at pass 800 and `forester-out` at pass 1345, in the state of the wait (`removals.txt`).

## Open leads

- The figure byte from `0x80` of the forester's handler and its sound: no pass of the recorded runs reaches it. likely
- The handlers of the codes from `0x69` of the object pass (above). guess
- A node a settler stands on is a goal and a planting node. The original has no check, the engine has none, and no emulator run puts a settler there. likely

## In the engine

The file `src/Application/ForesterRules.cs` holds `ForesterRules`, the rules module of the forester (`JobRules<Forester>`). It holds `Start`, `SearchSpot`, `Arrive`, and `Plant`. `HutWork.ObjectPass` calls `GrowSapling` as the object pass's handler.

The activities are `LooksForSpot` (state `0x13`) and `Plants`. `Plants` is state `0x14`, with the way back, whether the sapling is down, and the walk's memory. The walk to the node is a `WalksFree` whose purpose is `ToSpot`.

`Forester` is job 8. `MapObjectKind.Sapling` holds the codes `0x67` and `0x68`. `MapObject.Grown` is the tree a draw makes of one.

`CastleRoster` asks for his worker for a building of type 9 through the request's second way. A layout's `forester` entry stands a settler without a job in the building as the forester (`WalkBehavior.StandWorkerInHut`).

# Stonecutting

This note covers how the stonecutter works. It states which building and which job he belongs to, how he picks a stone heap, and how he walks to it. It states how he cuts it stage by stage, carries each stone to his building's flag, and starts again. It also states how a heap leaves the map. He works as the woodcutter does ([Woodcutting](woodcutting.md)), with three differences: his own search, a walk that stops beside a heap, and a cut that steps onto the heap.

Addresses and routine names are in the [Program index](program-index.md#simulation). The settlers and their walk to the building are in [Settlers](settlers.md). The goods on a flag are in [Goods at a flag](goods.md). The stones that reach a building site or the castle are in [Materials for a building site](construction-materials.md).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do. The game has a finished building of type 4 at a flag. The verb runs the original's own routines for each pass.

The engine runs the same commands. It matches these values in three games (`tests/OpenSettlers.Tests/stone-walks.txt`):

- the stonecutter's item after every change of its node, its state, or the part of its cut;
- every object byte that changes;
- the goods on the building's flag;
- the generator's words.

The three games are:

- A building among heaps for 3500 passes. It makes four cuts, and three heaps reach other stages.
- The same game with a heap set to its last stone. The verb's SET field writes the object byte by hand.
- The same game with the flag full of goods when he comes back.

The removal of his building matches the original in two games (`removals.txt`: `stonecutter-inside`, `stonecutter-out`). The engine draws him as the original does in the passes of one recording (`settler-draws.txt`, `stonecutter`).

## The building and the job

**Status:** confirmed.

The handler of building type 4 is at `0x42d987`, entry 4 of the table at `0x42cd1d`. It asks for a settler of job 7 with the tool of stock word `0x2e`, which is good 23 ([Construction](construction.md)). The handler of type 2 asks for job 5 in the same way ([Woodcutting](woodcutting.md#the-hut-and-the-job)).

Job 7's work, described below, cuts the objects `0x48` to `0x4f`. These are class 2, the grey stone heaps that the map generation places. The work lays good 10, a stone ([Goods](goods.md)), on the flag with the put `0x436b46` at `0x438182`. So type 4 is the stonecutter's building and job 7 is the stonecutter.

The castle's first settlers include one of job 7, in slot 12. The building's sprite is the object `0xa9`, entry 1419.

## The start at the building

**Status:** confirmed; **emulated**.

At the door the worker is in state 4. He runs the routine of his job through the table at `0x43ca54`, as the woodcutter does. For job 7 that routine is `0x43cfdd`.

Where the settler is not sent home, byte 11, the mode, is not `0xfe`. Then the routine empties the building's node of the settler's slot and takes state `0x15`. A settler sent home goes on at `0x43cd06` ([Settlers](settlers.md#walking-home)).

## Choosing a heap

**Status:** confirmed; **emulated**.

State `0x15` is at `0x435b17`. When the timer has run out, the worker takes a draw of the game's generator (`0x44257a`). From the draw he takes an entry of the spiral table, `((draw & 0x1fc) + 4) / 4`. That entry is from 1 to 128, as for the woodcutter.

The node of that entry is his goal where both of these hold:

- Its flag byte has bit 6 clear. Bit 6 is the mark of a lake's middle, a large object, or a heap.
- An object code from `0x48` to `0x4f` stands on the node north-west of it.

So the heap lies north-west of the node he walks to. Where the check fails, 100 is added to the timer, and he draws again. The woodcutter's miss adds 400. The draws go on until that sum wraps the timer, which ends the turn.

On a hit, the vector in bytes 11 and 12 is the goal's offset from the building less one each way. Bytes 13 and 14 hold the negated vector, which is the way back. Byte 15 holds `0x16`, the state after the step from the building onto its flag. That step is state 7 at `0x43e02e`, as the woodcutter's.

## The walk out

**Status:** confirmed; **emulated**.

State `0x16` (`0x436a84`) is the free walk of state `0x10` ([Woodcutting](woodcutting.md#the-walk)) with one check at each turn once the timer has run out. The check asks whether no settler stands on the node north-west of the worker and a heap stands there. Where both are true, the walk ends at once.

What is left of the vector joins the way back, so bytes 13 and 14 take bytes 11 and 12 added. The vector becomes 0, and byte 15 becomes 8. The free walk reads that as arrived. Without a heap beside him, the walk goes on as the free walk does.

**Not ported:** the same check on the way back after a cut that failed. The worker is still in state `0x16` then, with the way back as his vector.

## The cut

**Status:** confirmed; **emulated**.

The arrival of job 7 is reached when `0x437ddd` jumps by the job. Entry 7 goes to `0x438025`. Beside the heap, with byte 13 not `0x80`, the way back becomes the vector again in bytes 11 and 12.

Where no settler stands on the node north-west and a heap still stands there, three things happen. The worker leaves his node and takes that node. He takes state `0x17`. The frame is `0x28` plus the height reached less the height left. The timer is the time of that step in the table of step times (`0x43f248`), not scaled by the door's speed. A quarter of that time is kept in the word at 14.

Otherwise bytes 13 and 14 become `0x80` and 0. He walks back with no stone, still in state `0x16`.

State `0x17` (`0x435c5c`) has three parts, which byte 13 selects:

1. **0, the step onto the heap's node.** The timer is waited out until it has fallen to the quarter. Then the frame is `0x7b` and the timer takes away the quarter plus 1. The part is 1 with `0x600` added to the timer.
2. **1, the stroke.** It acts when the timer has run out and no settler stands on the node south-east, the node he came by. The heap's object code goes up by one. The code `0x4f` is taken off the node with its flag bit 6 (`0x435d4f`). **Emulated:** that bit is the one flag byte that changes in the 1100 passes of `last-stone` in `stone-walks.txt`, at the last cut. He steps onto the south-east node. The frame is `0x0d` plus the height reached less the height left. The stamp is the clock, the part is 2, and the timer takes three quarters of that step's time. He waits there while a settler stands on that node, with timer 0.
3. **2, the step back.** When the timer has run out, he walks back. Byte 13 is `0x80`, byte 14 is 1 because a stone is carried, byte 15 is 0, the state is `0x10`, and the timer is 0.

A heap with the code `0x48` has eight stones to give. It is cut eight times. Its code goes up with each cut to `0x4f`, and the cut of `0x4f` is the last. A heap's code range is the stone heap's stages (`MapObjectKind.StoneHeap`).

## The stone on the flag

**Status:** confirmed; **emulated**.

Back at the flag, the vector is 0 and byte 13 is `0x80`. The arrival at `0x438025` branches on byte 14:

- Negative: he goes home as any settler (`0x4385ab`, [Settlers](settlers.md#walking-home)).
- 0: no stone.
- More than 0: the stone. Good 10 goes through `0x436b46` onto the first empty place of the flag, as the woodcutter's log does ([Woodcutting](woodcutting.md#the-log)).

Then byte 11 is 0 and `0x43c7c2` takes him into the building. From there he searches for the next heap.

**A flag with no empty place takes nothing, and he goes on: no check.** The emulator shows no wait. With eight goods on the flag, the stone is not laid, he enters the building, and the stone is lost (scenario `full-flag` of `stone-walks.txt`). The engine does the same.

The stone gets its destination at the flag's next turn. The destination is a building site that asks for stones, or else the castle. The castle's stone count rises when the carrier delivers it ([Materials](construction-materials.md#the-request-search), [Goods at a flag](goods.md)).

## Drawing

**Status:** the figure words are **confirmed** by the draw run of the `stonecutter` recording (`settler-draws.txt`). The run covers the passes of a whole cut: the walk out, the step onto the heap, the stroke, the step back, and the way back with the stone. New rules do not draw the heap stages and the stone good.

The figure handler of job 7 is at `0x43fcd8`, reached by the stub at `0x43f77c`. It adds three values to the figure byte:

- `0x0d00` for a byte below 0x80, the walk.
- `0x1200` on the way back with the stone and in the step back off the heap. The way back has byte 13 at `0x80`, byte 14 at 1, and state `0x10`. The step back has state `0x17` and byte 13 at 2.
- `0x1280` for a byte from 0x80, the stroke.

The engine reads the three words from the program's instructions (`RuleTables.Get<FigureAdds>(new Stonecutter())`). The engine draws states `0x16` and `0x17`. It does not draw state `0x15`, in the building.

The heap stages are the objects `0x48` to `0x4f`, drawn from the map like any object. A stone on a flag is the sprite `320` plus the good. The engine draws it as every good on a flag is ([Woodcutting](woodcutting.md#drawing)).

The `open-settlers` content set draws the eight stages as a grey pile that shrinks (`object/72` to `object/79`). It draws the stone as a grey stone (`good/10`). It draws the stonecutter as a grey figure in each pose (`settler/stonecutter/*`). The way back with the stone has the pose of a carried load.

## Removal

**Status:** confirmed; **emulated**.

A stonecutter's building is taken down as the woodcutter's hut is ([Removal](removal.md)), and his worker walks home. A worker in the building takes state `0x1c` and walks to the castle.

A worker out at a heap is sent home where he stands. He takes state `0x19`, searches for the nearest flag of his player, and walks from there to the castle. The runs are `stonecutter-inside` and `stonecutter-out` in `removals.txt`. The second run removes the building in the stroke.

## Open leads

- The check of state `0x16` on the way back after a failed cut. likely
- A second stonecutter: the request's second way, a new settler made of a jobless one with a pick (good `0x2e`) from the stock ([Settlers](settlers.md#the-request)). The castle holds one worker of job 7. guess

## In the engine

The file `src/Application/StonecutterRules.cs` holds `StonecutterRules`, the rules module of the stonecutter (`JobRules<Stonecutter>`). It holds `Start`, `SearchStone`, `Arrive`, `CutStone`, and `WalkTurn`. The free walk runs `WalkTurn` at each turn, and `StoneHeaps.Eye` is the check of his walk out. The step out, the free walk, and the entry into the hut are `HutWork`'s.

The activities are:

- `LooksForStone`, state `0x15`.
- `WalksFree` with `EyesHeaps` set, state `0x16`. It is a trip whose purpose is `ToHeap`.
- `CutsStone`, state `0x17`, with the part of the cut and the quarter.

`Stonecutter` is job 7. `MapObjectKind.StoneHeap` holds the codes `0x48` to `0x4f`. `StonecutterRules.GoodStone` is the stone. `CastleRoster` asks for his worker for a building of type 4. A layout's `stonecutter` entry stands his worker in the building (`WalkBehavior.StandWorkerInHut`), because the module declares `CanStandInHut`.

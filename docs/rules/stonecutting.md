# Stonecutting

How the stonecutter works: which building and which job he belongs to, how he picks a stone heap, how he walks to it, cuts it
stage by stage, carries each stone to his building's flag, and starts again, and how a heap leaves the map. He works as the
woodcutter does ([Woodcutting](woodcutting.md)) with his own search, a walk that stops beside a heap, and a cut that steps onto
the heap. Addresses and routine names are in the [Program index](program-index.md#simulation); the settlers and their walk to the
building are in [Settlers](settlers.md); the goods on a flag are in [Goods at a flag](goods.md); the stones that reach a building
site or the castle are in [Materials for a building site](construction-materials.md).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do, with a
finished building of type 4 at a flag, and runs the original's own routines for each pass. The engine runs the same commands and
matches the stonecutter's item after every change of its node, its state, or the part of its cut, every object byte that changes,
the goods on the building's flag, and the generator's words, in three games (`tests/OpenSettlers.Tests/stone-walks.txt`): a
building among heaps for 3500 passes (four cuts, three heaps at other stages), the same game with a heap set to its last stone
(the verb's SET field writes the object byte by hand), and the same game with the flag full of goods when he comes back. The
removal of his building matches the original in two games (`removals.txt`: `stonecutter-inside`, `stonecutter-out`), and the engine
draws him as the original does in the passes of one recording (`settler-draws.txt`, `stonecutter`).

## The building and the job

**Status:** confirmed.

The handler of the building type 4 (`0x42d987`, entry 4 of the table at `0x42cd1d`) asks for a settler of job 7 with the tool of
the stock word `0x2e` (good 23, [Construction](construction.md)), as the handler of the type 2 asks for job 5 ([Woodcutting](woodcutting.md#the-hut-and-the-job)). Job 7's work is the
one below: it cuts the objects `0x48` to `0x4f` (class 2, the grey stone heaps the map generation places) and lays the good 10
(a stone, [Goods](goods.md)) on the flag with the put `0x436b46` at `0x438182`. So the type 4 is the stonecutter's building and
job 7 the stonecutter. The castle's 20 first settlers hold one of job 7, in slot 12. The building's sprite is the object `0xa9`,
entry 1419. An earlier note named the type and the job from the handler alone ([Build sites](build-sites.md)).

## The start at the building

**Status:** confirmed; **emulated**.

At the door the worker is in state 4 and runs the routine of his job through the table at `0x43ca54` as the woodcutter does: for
job 7, `0x43cfdd`. Where the settler is not sent home (byte 11, the mode, is not `0xfe`) the routine empties the building's node of
the settler's slot and takes state `0x15`; a settler sent home goes on at `0x43cd06` ([Settlers](settlers.md#walking-home)).

## Choosing a heap

**Status:** confirmed; **emulated**.

State `0x15` (`0x435b17`): when the timer has run out, the worker takes a draw of the game's generator (`0x44257a`) and from it an
entry of the spiral table, `((draw & 0x1fc) + 4) / 4`, an entry from 1 to 128, as the woodcutter does. The node of that entry
is his goal where its flag byte has bit 6 clear (the mark of a lake's middle, a large object, or a heap) and an object code from
`0x48` to `0x4f` stands on the node north-west of it, so the heap lies north-west of the node he walks to. Where that fails, 100 is
added to the timer (the woodcutter's miss adds 400) and he draws again, until that sum wraps the timer, which ends the turn.

On a hit the vector (bytes 11 and 12) is the goal's offset from the building less one each way, bytes 13 and 14 hold the negated
vector, the way back, and byte 15 holds `0x16`, the state after the step from the building onto its flag (state 7, `0x43e02e`,
as the woodcutter's).

## The walk out

**Status:** confirmed; **emulated**.

State `0x16` (`0x436a84`) is the free walk of state `0x10` ([Woodcutting](woodcutting.md#the-walk)) with one check at each turn
once the timer has run out: where no settler stands on the node north-west of the worker and a heap stands there, the walk ends
at once. What is left of the vector joins the way back (byte 13 and 14 take bytes 11 and 12 added), the vector becomes 0, and byte
15 becomes 8, which the free walk reads as arrived. Without a heap beside him the walk goes on as the free walk does.

**Not ported:** the same check on the way back after a cut that failed (the worker is still in state `0x16` then, with the way back
as his vector).

## The cut

**Status:** confirmed; **emulated**.

The arrival of job 7 (`0x437ddd` jumps by the job, entry 7 goes to `0x438025`). Beside the heap, with byte 13 not `0x80`: the way
back becomes the vector again (bytes 11 and 12). Where no settler stands on the node north-west and a heap still stands there,
the worker leaves his node, takes that node, and takes state `0x17` with the frame `0x28` plus the height reached less the height
left, the time of that step in the table of step times (`0x43f248`, not scaled by the door's speed) as timer, and a quarter of it
(the word at 14) kept. Else bytes 13 and 14 are `0x80` and 0 and he walks back with no stone, still in state `0x16`.

State `0x17` (`0x435c5c`) has three parts, in byte 13:

1. **0, the step onto the heap's node.** The timer is waited out until it has fallen to the quarter. Then the frame is `0x7b`, the
   timer takes away the quarter plus 1, and the part is 1 with 0x600 added to the timer.
2. **1, the stroke.** When the timer has run out and no settler stands on the node south-east (the node he came by), the heap's
   object code goes up by one; the code `0x4f` is taken off the node with its flag bit 6 (`0x435d4f`; **emulated:** the bit is the one
   flag byte the 1100 passes of `last-stone` in `stone-walks.txt` change, at the last cut). He steps onto the south-east node
   (frame `0x0d` plus the height reached less the height left), the stamp is the clock, the part is 2, and the timer takes three
   quarters of that step's time. He waits there while a settler stands on that node (timer 0).
3. **2, the step back.** When the timer has run out he walks back: byte 13 `0x80`, byte 14 1 (a stone is carried), byte 15 0, state
   `0x10`, timer 0.

A heap with the code `0x48` (eight stones to give) is cut eight times: its code goes up with each cut to `0x4f`, and the cut of
`0x4f` is the last. A heap's code range is the stone heap's stages (`MapObjectKind.StoneHeap`).

## The stone on the flag

**Status:** confirmed; **emulated**.

Back at the flag (the vector 0, byte 13 `0x80`) the arrival at `0x438025` branches on byte 14: negative, he goes home as any
settler (`0x4385ab`, [Settlers](settlers.md#walking-home)); 0, no stone; more than 0, the stone: the good 10 goes through
`0x436b46` onto the first empty place of the flag, as the woodcutter's log does ([Woodcutting](woodcutting.md#the-log)). Then
byte 11 is 0 and `0x43c7c2` takes him into the building, from where he searches for the next heap.

**A flag with no empty place takes nothing and he goes on: no check.** The plan for the slice said he waits until a place frees.
The emulator shows no wait: with eight goods on the flag the stone is not laid, he enters the building, and the stone is lost (the
scenario `full-flag` of `stone-walks.txt`). The engine does the same.

The stone gets its destination at the flag's next turn: a building site that asks for stones, else the castle, whose stone count
rises when the carrier delivers it ([Materials](construction-materials.md#the-request-search), [Goods at a flag](goods.md)).

## Drawing

**Status:** the figure words are **confirmed** by the draw run of the `stonecutter` recording (`settler-draws.txt`, the passes of
a whole cut: the walk out, the step onto the heap, the stroke, the step back, the way back with the stone); the heap stages and the
stone good are not drawn by new rules.

The figure handler of job 7 (`0x43fcd8`, reached by the stub at `0x43f77c`) adds `0x0d00` to the figure byte for a byte below
0x80 (the walk), `0x1200` on the way back with the stone (byte 13 `0x80`, byte 14 1, state `0x10`) and in the step back off the
heap (state `0x17`, byte 13 2), and `0x1280` for a byte from 0x80 (the stroke). The engine reads the three words from the
program's instructions (`RuleTables.StonecutterFigureAdds`). The states `0x16` and `0x17` are drawn; the state `0x15`, in the
building, is not.

The heap stages are the objects `0x48` to `0x4f`, drawn from the map like any object; a stone on a flag is the sprite `320` plus the
good, drawn as every good on a flag is ([Woodcutting](woodcutting.md#drawing)). The `open-settlers` content set draws the eight stages
as a grey pile that shrinks (`object/72` to `object/79`), the stone as a grey stone (`good/10`), and the stonecutter as a grey figure
in each pose (`settler/stonecutter/*`); the way back with the stone is drawn in the pose of a carried load.

## Removal

**Status:** confirmed; **emulated**.

A stonecutter's building is taken down as the woodcutter's hut is ([Removal](removal.md)): his worker walks home. A worker in the
building takes state `0x1c` and walks to the castle. A worker out at a heap is sent home where he stands: he takes state `0x19`,
searches for the nearest flag of his player, and walks from there to the castle (`removals.txt`, `stonecutter-inside` and
`stonecutter-out`; the second removes the building in the stroke).

## Open leads

- The check of state `0x16` on the way back after a failed cut. likely
- A second stonecutter: the request's second way, a new settler made of a jobless one with a pick (good `0x2e`) from the stock
  ([Settlers](settlers.md#the-request)); the castle holds one worker of job 7. guess

## In the engine

`src/Application/WoodcutterBehavior.Stonecutter.cs` (the stonecutter's part of `WoodcutterBehavior`): `SearchStone`, `EyeHeaps`,
`ArriveStonecutter`, and `CutStone`. The activities are `LooksForStone` (state `0x15`), `WalksFree` with `EyesHeaps` set (state
`0x16`, a trip whose purpose is `ToHeap`), and `CutsStone` (state `0x17`, with the part of the cut and the quarter). `Job.Stonecutter`
is job 7, `MapObjectKind.StoneHeap` the codes `0x48` to `0x4f`, and `WoodcutterBehavior.GoodStone` the stone. `CastleRoster` asks for his
worker for a building of type 4, and a layout's `stonecutter` entry stands his worker in the building
(`WalkBehavior.StandWorkerInHut`).

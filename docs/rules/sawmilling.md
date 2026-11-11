# Sawmilling

How the sawmill and its sawmiller work: which building and which job, the request for logs the building's flag makes and the rank it
keeps, the sawmiller's wait for a log, his sawing, the step out with the plank, the plank laid on the flag, and how a sawmill is taken
down. The logs come to the sawmill and the planks leave it by the carriers' goods rules ([Goods at a flag](goods.md), [Materials for a
building site](construction-materials.md)). Addresses and routine names are in the [Program index](program-index.md#simulation); the
settlers and their walk to the building are in [Settlers](settlers.md); the work of the woodcutter, who brings the logs, is in
[Woodcutting](woodcutting.md).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do, with a finished
building of type 17 (a large building, object code 3) at a flag. The verb sets the pointer a built site leaves at `+0xe` of the
building's item by hand (`0x43ef82`), because the building's handler reads its flag through it. The engine runs the same commands and
matches the sawmiller after every change of his node, his state, or the part of his work, the building's bytes, the goods on its flag,
the request words of its flag, the castle's log and plank counts, and the generator's words, in two games (`tests/OpenSettlers.Tests/saw-walks.txt`):
two logs laid on the castle's flag (the whole run, 2200 passes: the walk in, the wait, two sawings, two planks to the castle), and
eight logs laid on a flag beside the castle followed by a ninth that no request takes (the castle's log count rises). The removal of
the building matches in three games (`saw-removals.txt`: the worker waiting, sawing, and out with the plank), and the engine draws him as
the original does in the passes of one recording (`settler-draws.txt`, `sawmiller`).

## The building and the job

**Status:** confirmed.

The handler of the building type 0x11 (`0x42da0b`, entry 0x11 of the table at `0x42cd1d`) asks for a settler of job 6 with the tool of
the stock word `0x2c` (good 22), as the handler of the type 2 asks for job 5 ([Woodcutting](woodcutting.md#the-hut-and-the-job)). Job 6's
work takes the logs the building holds (a log is the good 7, [Goods](goods.md)) and puts the good 8 on the flag with the put `0x43c6aa`
(state `0xd`): a plank, the good a building site's request asks for in the first request word ([Materials](construction-materials.md#the-request-words)).
So the type 0x11 is the sawmill and job 6 the sawmiller. The castle's 20 first settlers hold one of job 6, in slot 11. The building is large
(site 4, [Build sites](build-sites.md#the-four-types)) and its sprite is the object `0xa0`. An earlier note named the type and the job from
the handler alone.

## The start and the request for logs

**Status:** confirmed; **emulated**.

At the door the worker is in state 4 and runs the routine of his job through the table at `0x43ca54` as the woodcutter does: for job 6,
`0x43cf41`. Where he is not sent home (byte 11, the mode, is not `0xfe`) the routine empties the building's node of the settler's slot. Where
byte 11 is not 0 (it is `0xff` after the walk to the building, and 0 after he laid a plank down) it also writes the flag's request words:
the class byte at `0x42` is 0, the one at `0x44` is `0x20` (class 5, which the good class table `0x464117` gives the log, in the second
request word) and the rank at `0x45` is 0; the rank at `0x43` stays. The settler takes state `0x18` with byte 11 0.

## The rank of the request for logs

**Status:** confirmed; **emulated**.

The building's handler (`0x42da0b`) runs in the building pass. After its request for a worker, which it makes where the status bits 2, 6, and 7
are clear, it keeps the rank of the request for logs while the worker is inside (status bit 6): the fill byte at 9 of the building's item
holds the logs on site in its high nibble and the logs on the way in its low nibble (the carrier's handover adds `0x0f`, a request that
serves the flag counts one more on the way, [Goods at a flag](goods.md)); with their sum below 8 the rank at the flag's `0x45` is `0xff` shifted
right by the sum, else 0. The pointer to the flag is the word at `+0xe` of the building's item, which the end of the building's construction
writes (`0x43ef82`).

A request of rank 0 asks for nothing, and a flag that asks is found by the request search ([Materials](construction-materials.md#the-request-search)):
the woodcutter's logs go to the sawmill ahead of the castle while its sum of logs is below 8, and each log served shifts the rank on
(`0xff`, `0x7f`, `0x3f`, and on), so that eight logs laid at once all go to the sawmill and a ninth goes to the castle (`store-full`).

## State 0x18

**Status:** confirmed; **emulated**.

State `0x18` (`0x4359fe`) has two parts, in byte 11:

1. **0, no log taken.** The building of the node the sawmiller stands on is read through the node's word. With no log on site (the high nibble of byte 9
   is 0) nothing changes: not the timer and not the stamp, so he waits through every turn. With one, byte 9 falls by `0x10`, byte 11
   becomes `0xff`, the frame is `0x7c`, the timer `0x93f`, the stamp the clock, and the node holds his slot.
2. **`0xff`, sawing.** The time since the stamp comes off the timer, as in every waiting state. Once it has run out the node's word is 0, byte 11 is 8
   (the plank), the word at 12 is 0, byte 15 is `0xd`, and he goes on at `0x43e1d0` in the same turn.

## The step out and the plank

**Status:** confirmed; **emulated**.

`0x43e1d0` counts the good of byte 11 in the player's block (the goods made: **not ported**), takes state `0xb`, and runs its body (`0x43e228`),
the step of state 7 from the building's node onto the flag south-east of it ([Woodcutting](woodcutting.md#the-start-at-the-hut)) with one more
condition: the flag item's eight places (bytes `0xc` to `0x13`) must not all hold goods. A settler on either node or a full flag gives
the frame `0x52` and the timer 0, and he tries again at his next turn (in the first emulated game he waits about 100 passes for a carrier to
leave the flag's node). Else he takes the flag's node, the frame is `0x0d` plus the height reached less the height left, the timer is the step's time scaled
by the door's speed, and he waits it out in state 5, which goes on in state `0xd` (byte 15).

State `0xd` (`0x43c6aa`) puts the good of byte 11 on the first empty one of the first seven places of the flag, or, where those hold
goods, on the last place with no check, writes 0 as the place's destination and sets bit 7 of the flag's byte 4: the plank gets its destination at the
flag's next turn ([Goods at a flag](goods.md)), a site that asks for planks or else the castle. Byte 11 becomes 0 and he goes on as state 6
(`0x43c7ca`) into the building ([Woodcutting](woodcutting.md#the-start-at-the-hut)); the start routine then takes state `0x18`, and with byte 11 0 it
leaves the flag's request words alone.

A log takes `0x93f` clock units, about 300 passes, and the walk out and in about 40 more.

## Drawing

**Status:** confirmed by the draw run of the `sawmiller` recording (`settler-draws.txt`: the walk to the door, the sawing, the step out with
the plank, the plank laid down).

The figure handler of job 6 (`0x43fc21`, reached by the stub at `0x43f774`) adds `0x0c00` to the figure byte for a byte below 0x80 (the
walk), `0x1700` in state 5 with byte 15 `0xd` (the step out with the plank), and `0x1580` for a byte from 0x80 (the saw), with a sound
request at some of the saw's frames (**not ported**) and a move of 4 of the record's word at `+0x10` for them, which the recording shows
no visible effect of. The engine reads the three words from the program's instructions (`RuleTables.SawmillerFigureAdds`). The states 4, 5,
`0x18` while he saws, and `0xd` are drawn; in state `0x18` with no log (byte 11 0) the recording draws nothing, as it draws nothing for the
stonecutter in the building. **Inferred:** the state `0xb`, in which he waits at the door for the node or a place on the flag, is not drawn,
as the state 7 is not.

The `open-settlers` content set draws him as a yellow-brown figure in each pose (`settler/sawmiller/*`): the plank he carries out in the
pose of a carried load, the saw in the pose of work.

## Removal

**Status:** confirmed; **emulated**.

A sawmill is taken down as every finished building is ([Removal](removal.md)): his worker walks home. A worker in the building takes
state `0x1c` and walks to the castle, from the node of the building (`saw-removals.txt`, `sawmill-inside` and `sawmill-sawing`; a worker who holds the
node while he saws takes state `0x19`). A worker out on the flag with the plank is sent home from where he stands, and the plank is lost
(`sawmill-out`). A log that waited in the building is lost with it (its fill bytes are cleared).

## Open leads

- A second sawmill: the request's second way, a new settler made of a jobless one with a saw (good 22) from the stock
  ([Settlers](settlers.md#the-request)); the castle holds one worker of job 6. guess
- The state `0xb` with a settler waiting at the door: no draw run covers it. guess
- The record move at `0x43fc2c` and the saw's sound (`0x43fc85`). guess

## In the engine

`src/Application/SawmillBehavior.cs`: `Start` (job 6, registered in `GameFactory` with `WoodcutterBehavior.RegisterStart`), `Work` (state `0x18`),
`StepOut` (state `0xb`), `PutDown` (state `0xd`), and `KeepRank`, which `MainPass` runs for a finished sawmill after `CastleRoster.HandleBuilding`.
The activities are `Saws` (state `0x18`, with a flag for the part), `StepsOutWithGood` (state `0xb`), and `PutsDownGood` (state `0xd`);
`Job.Sawmiller` is job 6. The plank is laid through the same `GoodsTransport.LayGood` the woodcutter's log is. Logs and planks go
by the request search and the storage search of the goods, which need no rule of their own.

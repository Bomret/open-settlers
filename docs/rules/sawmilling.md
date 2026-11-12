# Sawmilling

This note covers the sawmill and its sawmiller. It states which building and which job, the request for logs that the flag makes, and the rank the request keeps. It then states the sawmiller's wait for a log, his sawing, his step out with the plank, the plank laid on the flag, and the removal of the sawmill. The carriers' goods rules move the logs to the sawmill and the planks away from it ([Goods at a flag](goods.md), [Materials for a building site](construction-materials.md)). Addresses and routine names are in the [Program index](program-index.md#simulation). The settlers and their walk to the building are in [Settlers](settlers.md). The woodcutter brings the logs, and his work is in [Woodcutting](woodcutting.md).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) starts a game as the woodcutter's scenarios do. The game has a finished building of type 17 at a flag. Type 17 is a large building with object code 3. A built site leaves a pointer at `+0xe` of the building's item (`0x43ef82`), and the building's handler reads its flag through it. The verb sets that pointer by hand.

The engine runs the same commands as the harness. It matches the sawmiller after every change of his node, his state, or the part of his work. It also matches the building's bytes, the goods on its flag, and the request words of its flag. It matches the castle's log and plank counts and the generator's words. Two games (`tests/OpenSettlers.Tests/saw-walks.txt`) hold these checkpoints.

- The first game lays two logs on the castle's flag. The whole run lasts 2200 passes: the walk in, the wait, two sawings, and two planks to the castle.
- The second game lays eight logs on a flag beside the castle. A ninth log follows that no request takes, so the castle's log count rises.

The removal of the building matches in three games (`saw-removals.txt`): the worker waiting, sawing, and out with the plank. The engine draws the worker as the original does in the passes of one recording (`settler-draws.txt`, `sawmiller`).

## The building and the job

**Status:** confirmed.

The handler of building type 0x11 is at `0x42da0b`, entry 0x11 of the table at `0x42cd1d`. It asks for a settler of job 6 with the tool of stock word `0x2c`, which is good 22. The handler of type 2 asks for job 5 in the same way ([Woodcutting](woodcutting.md#the-hut-and-the-job)).

Job 6 takes the logs the building holds. A log is good 7 ([Goods](goods.md)). The job puts good 8 on the flag with the put at `0x43c6aa`, in state `0xd`. Good 8 is a plank, the good that a building site asks for in its first request word ([Materials](construction-materials.md#the-request-words)). So type 0x11 is the sawmill and job 6 is the sawmiller.

The castle's first settlers include one of job 6, in slot 11. The building is large, with site 4 ([Build sites](build-sites.md#the-building-types)). Its sprite is the object `0xa0`.

## The start and the request for logs

**Status:** confirmed; **emulated**.

At the door the worker is in state 4. He runs the routine of his job through the table at `0x43ca54`, as the woodcutter does. For job 6 that routine is `0x43cf41`.

Where the mode in byte 11 is not `0xfe`, the worker is not sent home. Then the routine empties the building's node of the settler's slot. Where byte 11 is not 0, it also writes the flag's request words. Byte 11 is `0xff` after the walk to the building and 0 after he laid a plank down.

The request words take these values:

- The class byte at `0x42` is 0.
- The class byte at `0x44` is `0x20`. That is class 5, which the good class table at `0x464117` gives the log, in the second request word.
- The rank at `0x45` is 0.
- The rank at `0x43` stays.

The settler takes state `0x18` with byte 11 at 0.

## The rank of the request for logs

**Status:** confirmed; **emulated**.

The building's handler (`0x42da0b`) runs in the building pass. It first requests a worker, where status bits 2, 6, and 7 are clear. While the worker is inside, status bit 6 is set, and the handler keeps the rank of the request for logs.

The fill byte at 9 of the building's item holds two counts. Its high nibble counts the logs on site, and its low nibble counts the logs on the way in. The carrier's handover adds `0x0f`. A request that serves the flag counts one more log on the way ([Goods at a flag](goods.md)). The rank at the flag's `0x45` is `0xff` shifted right by the sum of both counts where the sum is below 8. Otherwise the rank is 0.

The pointer to the flag is the word at `+0xe` of the building's item. The end of the building's construction writes it (`0x43ef82`).

A request of rank 0 asks for nothing. The request search finds a flag that asks ([Materials](construction-materials.md#the-request-search)). The woodcutter's logs go to the sawmill ahead of the castle while the sum of logs is below 8. Each log served shifts the rank on to `0xff`, `0x7f`, `0x3f`, and so on. Eight logs laid at once all go to the sawmill, and a ninth goes to the castle (`store-full`).

## State 0x18

**Status:** confirmed; **emulated**.

State `0x18` (`0x4359fe`) has two parts, which byte 11 selects:

1. **0, no log taken.** The routine reads the building of the node the sawmiller stands on through the node's word. With no log on site, the high nibble of byte 9 is 0 and nothing changes. The timer and the stamp stay, so he waits through every turn. With a log on site, byte 9 falls by `0x10` and byte 11 becomes `0xff`. The frame is `0x7c`, the timer is `0x93f`, and the stamp is the clock. The node holds his slot.
2. **`0xff`, sawing.** The time since the stamp comes off the timer, as in every waiting state. Once the timer has run out, the node's word is 0, byte 11 is 8 (the plank), the word at 12 is 0, and byte 15 is `0xd`. He goes on at `0x43e1d0` in the same turn.

## The step out and the plank

**Status:** confirmed; **emulated**.

The routine at `0x43e1d0` counts the good of byte 11 in the player's block. That count of goods made is **not ported**. The routine takes state `0xb` and runs its body at `0x43e228`. The body is the step of state 7 from the building's node onto the flag south-east of it ([Woodcutting](woodcutting.md#the-start-at-the-hut)). It adds one condition: the flag item's eight places, bytes `0xc` to `0x13`, must not all hold goods.

A settler on either node, or a full flag, gives the frame `0x52` and the timer 0. He tries again at his next turn. In the first emulated game he waits about 100 passes for a carrier to leave the flag's node. In `flag-full` of `saw-walks.txt` he waits about 240 passes while eight goods lie on the flag.

Otherwise he takes the flag's node. The frame is `0x0d` plus the height reached less the height left. The timer is the step's time scaled by the door's speed. He waits it out in state 5, which goes on in state `0xd` through byte 15.

State `0xd` (`0x43c6aa`) puts the good of byte 11 on the first empty one of the flag's first seven places. Where those hold goods, it puts the good on the last place with no check. It writes 0 as the place's destination and sets bit 7 of the flag's byte 4. The plank gets its destination at the flag's next turn ([Goods at a flag](goods.md)). The destination is a site that asks for planks, or else the castle.

Byte 11 becomes 0. The sawmiller goes on as state 6 (`0x43c7ca`) into the building ([Woodcutting](woodcutting.md#the-start-at-the-hut)). The start routine then takes state `0x18`. With byte 11 at 0 it leaves the flag's request words alone.

A log takes `0x93f` clock units, about 300 passes. The walk out and in takes about 40 more.

## Drawing

**Status:** confirmed by the draw run of the `sawmiller` recording (`settler-draws.txt`). The run covers the walk to the door, the sawing, the step out with the plank, and the plank laid down.

The figure handler of job 6 is at `0x43fc21`, reached by the stub at `0x43f774`. It adds three values to the figure byte:

- `0x0c00` for a byte below 0x80, the walk.
- `0x1700` in state 5 with byte 15 at `0xd`, the step out with the plank.
- `0x1580` for a byte from 0x80, the saw.

The handler requests a sound at some of the saw's frames, which is **not ported**. It also moves the record's word at `+0x10` by 4 for those frames. The recording shows no visible effect of that move. The engine reads the three words from the program's instructions (`RuleTables.SawmillerFigureAdds`).

The engine draws states 4, 5, `0x18` while he saws, and `0xd`. In state `0x18` with no log, byte 11 is 0 and the recording draws nothing. It also draws nothing for the stonecutter in the building. **Inferred:** state `0xb` is not drawn, as state 7 is not. In state `0xb` he waits at the door for the node or a place on the flag.

The `open-settlers` content set draws him as a yellow-brown figure in each pose (`settler/sawmiller/*`). The plank he carries out has the pose of a carried load. The saw has the pose of work.

## Removal

**Status:** confirmed; **emulated**.

A sawmill is taken down as every finished building is ([Removal](removal.md)), and its worker walks home. A worker in the building takes state `0x1c` and walks to the castle from the node of the building (`saw-removals.txt`, `sawmill-inside` and `sawmill-sawing`). A worker who holds the node while he saws takes state `0x19`. A worker out on the flag with the plank is sent home from where he stands, and the plank is lost (`sawmill-out`). A log that waited in the building is lost with it, because its fill bytes are cleared.

## Open leads

- A second sawmill: the request's second way, a new settler made of a jobless one with a saw (good 22) from the stock
  ([Settlers](settlers.md#the-request)); the castle holds one worker of job 6. guess
- The state `0xb` with a settler waiting at the door: no draw run covers it. guess
- The record move at `0x43fc2c` and the saw's sound (`0x43fc85`). guess

## In the engine

The file `src/Application/SawmillerRules.cs` holds these routines. `SawmillerRules` is the rules module of the sawmiller (`JobRules<Sawmiller>`):

- `Start` is job 6. `GameFactory` registers each module of the generated `JobModules` registry with `WoodcutterBehavior.RegisterModule`.
- `Work` is state `0x18`.
- `StepOut` is state `0xb`.
- `PutDown` is state `0xd`.
- `Turn` is the module's building hook. `MainPass` calls it for each building after `CastleRoster.HandleBuilding`, and it keeps the rank of a finished sawmill.

The activities are `Saws` (state `0x18`, with a flag for the part), `StepsOutWithGood` (state `0xb`), and `PutsDownGood` (state `0xd`). `Job.Sawmiller` is job 6. The plank is laid through `GoodsTransport.LayGood`, as the woodcutter's log is. Logs and planks go by the request search and the storage search of the goods, which need no rule of their own.

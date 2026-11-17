# Baking

This note covers the bakery and its baker: the building and the job, the request for flour that the flag makes and the rank it keeps, the
baker's wait for flour, his baking in two stretches, his step out with the bread, the castle's hand-out of flour, and the removal of the
bakery. The bakery follows the mill's pattern ([Milling](milling.md)): the carriers' goods rules move the flour to the bakery and the bread away
from it ([Goods at a flag](goods.md)), and the castle's hand-out is in
[Materials for a building site](construction-materials.md#the-castles-hand-out). Addresses and routine names are in the
[Program index](program-index.md#simulation). The names of the type, the job, and the goods are **inferred**
([Food buildings](food-buildings.md#which-chain-is-which)).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) puts a finished building of type 16 at a flag as it does for the mill; the baker's
building is large (object code 3), and its item holds the pointer to its flag at `+0xe` that a built site leaves (`0x43ef82`), which the handler reads.
The engine matches the baker after every change of his node, state, or stretch, the building's bytes, the flag's goods and request words, and the castle's
flour and bread counts in two games (`tests/OpenSettlers.Tests/baker-walks.txt`): one flour, and nine flours laid at once (the bakery takes eight, the ninth goes to the
castle). The removal matches in four games (`baker-removals.txt`: waiting, baking, out with the bread, flour on its way), the first hand-out in one
(`baker-materials.txt`, verb `construct:`), and the draw in one recording (`settler-draws.txt`, `baker`).
The wait on a full flag has no emulator game for the baker: only the engine's own test (`BakeryTests`, the flag-is-full case) covers it. The baker's
step out is the shared `HutWork.StepOutWithGood`, which the mill's full-flag game checks up to the pass 1588, where the engine's carriers free the flag's node
earlier than the original's (triage #344, [Milling](milling.md)); a baker game on a full flag would meet the same difference.

## The building and the job

**Status:** confirmed for the handler, **inferred** for the names.

The handler of building type 0x10 is at `0x42e37e`, entry 0x10 of `0x42cd1d`. It asks at `0x42e3a4` for a settler of job 16 with no tool, as `Building_RequestWorker`
(`0x42be63`) takes it ([Food buildings](food-buildings.md#the-six-types)). The castle holds no baker, so the request's second way makes one of a settler
without a job. The job takes flour in and puts bread (good 6) on the flag with the put at `0x43384f`.

## The start and the request for flour

**Status:** confirmed.

At the door the baker runs the start routine of job 16, `0x43d3d9`, entry 0x10 of `0x43ca54`. It empties the building's node of the settler's slot. Where byte 11 is
not 0 (`0xff` after the walk in, 0 after he laid bread down) it also writes the flag's request words:

- The class byte at `0x42` is `0x20`, class 5, which the good class table at `0x464117` gives flour, in the first word.
- The class byte at `0x44` is 0, and the rank at `0x43` is 0. The rank at `0x45` stays.

The settler takes state `0x24` with byte 11 at 0.

## The rank of the request for flour

**Status:** confirmed; **emulated**.

The handler keeps the rank at `0x43` with the worker inside (status bit 6), and after its request for a worker found none: `0xff` shifted right by the goods that the building's
fill byte 8 counts, the low nibble (on the way) and the high nibble (on site) together, and 0 where they are 8 or more. The start rank is a constant of the handler; the mill's handler reads
a player word instead. Each flour that a request serves halves the served rank where its bit 0 is set, and the handler writes the rank again from the fill. The rank is 1 at seven flours held, the serve of the
eighth makes it 0, so a full store is eight flours and the ninth goes to the castle (`store-full` in `baker-walks.txt`).

## State 0x24

**Status:** confirmed; **emulated**.

State `0x24` (`0x433694`) is the work. Byte 11 counts its stretches.

1. **0, waiting.** With no flour on site (the high nibble of byte 8 is 0) nothing changes. With one, byte 8 falls by `0x10`, byte 11 becomes 1, the frame is `0x8a`, the timer is `0x2ff`,
   the stamp is the clock, and the node holds his slot. The status of the building is not marked here, unlike the mill's.
2. **1, baking.** The time since the stamp comes off the timer. Once it has run out, byte 11 becomes 2: bit 4 of the building's status is set, the node's word is 0, and `0x5dc` is added to
   the timer.
3. **2, pausing.** When the timer runs out again, byte 11 becomes 3: bit 4 is cleared, byte 11 is 6 (bread), the word at 12 is 0, byte 15 is `0xd`, and he goes on at `0x43e1d0` in the same turn.

One flour makes one bread, in about 280 passes (`0x2ff` and `0x5dc` clock units, 8 units to a pass), and he is drawn only while the node holds his slot. The loop that adds the pause
carries on at once where the sum does not overflow the 16-bit timer, as the mill's does.

## The step out and the bread

The step out (state `0xb`, `0x43e228`) and the put (state `0xd`, `0x43c6aa`) are the sawmiller's ([the step out](sawmilling.md#the-step-out-and-the-plank)); the bread goes on the flag's
first empty place and then to a flag that asks for it or to the castle. Only the mines ask for bread (class 0, M6), so the bread goes to the castle, whose bread count rises by one
(`bread=1` at the pass 1687 of the one-flour game). The count of goods made (`0x43e1d0`) is **not ported**.

## The castle's hand-out

The orders of `0x42a313` hold a step for flour (class 5, first word, good 5) as they hold the grain's. The castle hands flour out to the asking bakery, which counts one flour on its way in the low byte
(`baker-materials.txt`: the fill byte 1, the rank 0 and then `0x7f`). With no flour in the stock the bakery waits and the game runs on.

## Removal

**Status:** confirmed; **emulated**.

A bakery is taken down as every finished building is ([Removal](removal.md)). The baker walks home from where he stands: waiting or pausing (state `0x1c`), baking with the node held (state `0x19`), or out on the flag with the
bread (the bread is lost). The fill byte is cleared with the building, so flour held on site and flour on its way are lost, and the flag's request words lose their class; no good stays counted as on its way
(`baker-removals.txt`).

## Drawing

**Status:** confirmed by the draw run of the `baker` recording (`settler-draws.txt`).

The figure handler of job 16 is at `0x440112`, reached by the stub at `0x43f7c4`. It adds `0x4800` to the figure byte for a byte below `0x80`, `0x4a00` in state 5 with byte 15 at `0xd`, and `0x4880` for a byte from `0x80`,
with no sound. The engine reads them through `RuleTables.Get<FoodHutFigureAdds>(new Baker())`. He is drawn while he bakes with the node held, on the step out with the bread, on the put, and at the door. The `open-settlers`
content set draws him as a tan figure (`settler/baker/*`); the bread has the loaf of the goods.

## In the engine

`src/Application/BakerRules.cs` is the rules module of the baker (`JobRules<Baker>`): `Start`, `Work` (state `0x24`), and `Turn` (the handler's rank). It stands the baker in a layout (`CanStandInHut`, [Settlers and goods in a layout](layouts.md)).
The activity is `Bakes` (with the stretch in `Phase`). The step out and the put are `HutWork.StepOutWithGood` and `HutWork.PutDownGood`, shared with the sawmiller and the miller. `Building.AtWork` is status bit 4, which the baker's state routine `0x433694` sets in his pause (`0x4337d1`) and clears with the bread (`0x43383e`); the miller's sets it too ([Milling](milling.md)).
`layouts/baker.jsonc` with `play/baker.play` shows a bakery working.

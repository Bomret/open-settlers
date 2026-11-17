# Pig farming

This note covers the pig farm and its pig farmer: the building and the job, the request for grain that the flag makes and the rank it keeps, the pig farmer's wait for grain,
his work in stretches while the pigs the farm holds grow, the pig he carries out, the castle's hand-out of grain, and the removal of the pig farm. The carriers' goods rules move
the grain to the pig farm and the pig away from it ([Goods at a flag](goods.md)), and the castle's hand-out is in
[Materials for a building site](construction-materials.md#the-castles-hand-out). The mill is the pattern the pig farm follows ([Milling](milling.md)). Addresses and routine names are in the
[Program index](program-index.md#simulation). The names of the type, the job, the goods, and the building's byte 9 are **inferred** ([Food buildings](food-buildings.md#which-chain-is-which)).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) puts a finished building of type 14 at a flag, as it does for the mill. The type is large (object code 3), and its
item holds the pointer to its flag at `+0xe` that a built site leaves (`0x43ef82`), which the handler reads. The verb also runs `0x44579c` with the player setup, so the rank starts from the
original's word. The engine matches the pig farmer after every change of his node, state, or stretch, the building's bytes, the flag's goods and request words, the castle's grain and pig counts,
and the generator's words in three games (`tests/OpenSettlers.Tests/pig-walks.txt`): no grain in 3000 passes, one grain to the castle's first pig (pass 12709), and eight grains laid at once
with a ninth later. The removal matches in six games (`pig-removals.txt`), the first hand-out in one (`pig-materials.txt`, verb `construct:`), and the draw in one recording
(`settler-draws.txt`, `pig-farmer`). The part `castle-off@300` of those commands is explained in [The rank](#the-rank-of-the-request-for-grain).

## The building and the job

**Status:** confirmed for the handler, **inferred** for the names.

The handler of building type 0xe is at `0x42e0fa`, entry 0xe of `0x42cd1d`. It asks for a settler of job 12 with no tool, as `Building_RequestWorker` (`0x42be63`) takes it
([Food buildings](food-buildings.md#the-six-types)). The castle holds no pig farmer, so the request's second way makes one of a settler without a job. The job takes grain in and puts a pig
(good 2) on the flag with the put at `0x433577`.

## The start and the request for grain

**Status:** confirmed.

At the door the pig farmer runs the start routine of job 12, `0x43d179`, entry 0xc of `0x43ca54`. It empties the building's node of the settler's slot. Where byte 11 is not 0 (`0xff` after the
walk in, 0 after he laid a pig down) it also sets the building's byte 9 to 1 and writes the flag's request words:

- The class byte at `0x42` is `0x10`, class 4, which the good class table at `0x464117` gives grain, in the first word.
- The class byte at `0x44` is 0, and the rank at `0x43` is 0. The rank at `0x45` stays.

The settler takes state `0x25` with byte 11 at 0. Where byte 11 was 0 he takes state `0x25` with byte 11 at 6 and his timer at 0, which runs the end of a round at once (below).

## The rank of the request for grain

**Status:** confirmed; **emulated**; the pointer is a **stand-in**.

The handler keeps the rank at `0x43` with the worker inside (status bit 6), and after a request for a worker that found none: the high byte of the player word at `+0x158`, shifted right by the
goods that the building's fill byte 8 counts, the low nibble (on the way) and the high nibble (on site) together, and 0 where they are 8 or more. `Player_SetDefaults3` (`0x44579c`) stores `0xffdc`
there, so the rank is `0xff`, `0x7f`, and so on, down to 1 at seven grains and 0 at eight. The engine reads the byte through `PigFarmRates.RankBase`. Each grain that a request serves halves the served
rank where its bit 0 is set, and the handler writes the rank again from the fill: the rank `0xff` shifted by any count below 8 keeps bit 0, so the pig farm takes eight grains, one more than the mill's seven.

The mill's handler loads the pointer to the player block before it reads the word. The pig farm's handler has no such step: it reads the pointer that the code before it left in a scratch word of the
item block (`[EDI+0x30]`), which the pass (`Table18_Pass`, `0x42cb2d`) leaves alone between buildings. Within a group of 32 slots the handlers run in the order of the slots. The castle's handler (type
0x18, `0x42e7e0`, **not ported**) sets that word to a map-table address on every turn, at its common tail from `0x42edb9`, and on some turns to a slot address after that; it never leaves the player
block there. So a pig farm whose handler runs after the castle's in the same group, with no handler between them that loads the player block, reads its rank from unrelated memory: the emulator's pig
farm then writes the rank 0, and its grain is never served. The castle holds the first slot, and a pig farm in the slots up to 31 shares its group. Of the handlers that can run in between, the mill's
(`0x42e2a0`) loads the player block in its preamble and so restores the word; the others' preambles are an open lead (the dump holds only the castle's and the type 1 handler's functions), as is
which slot orders occur in a game.

**Stand-in.** The engine always reads the player word. That departs from the original in the case above, where the original's value is unrelated memory that the engine does not model (ADR 0003);
no deterministic original value is recovered. The harness part `castle-off@PASS` makes the emulator take the player word, so the rest of the rule can be compared: it sets the castle's type to 0 for
the building slice of each pass from that pass on, and the type is back for the settler slice, which reads the door speed of the type. The pig farm's turns before the worker is inside are the
exception: the games show the rank 0 at the turn that finds a worker (the pass 98), which the engine writes for every turn while the worker is on the way (`PigFarmerRules.Turn`). Nothing reads the
rank then, since the request has no class before the start routine runs.

## State 0x25

**Status:** confirmed; **emulated**.

State `0x25` (`0x4333c1`) is the work. Byte 11 counts its stretches.

1. **0, waiting.** With no grain on site (the high nibble of byte 8 is 0) nothing changes. With one, byte 8 falls by `0x10`, byte 11 becomes 1, the frame is `0x8b`, the timer is `0x17f`, the stamp is
   the clock, and the node holds his slot. The building's status bit 4 is not set (the mill sets it).
2. **1 to 6.** The time since the stamp comes off the timer. Once it has run out, byte 11 counts up. At 3 and 5 the node holds his slot, the frame is `0x8b`, and the timer is `0x17f`: a work stretch.
   At 2, 4, and 6 the node's word is 0 and the pause follows: with fewer than eight pigs held (byte 9 of the building), one draw of the generator is made, and where it is below the entry
   of the table at `0x433684` for the pigs held (index 0 to 7) the building holds one more; then `0x800` is added to the timer, and the next stretch follows at once where that leaves the timer behind.
3. **7, the end of a round.** The farm gives a pig where it holds 8 pigs, never where it holds fewer than 4, and in between where a draw (the 16-bit word times 20, shifted down by 16) is below the pigs
   held. A pig takes one from the building's byte 9, sets byte 11 to 2 (pig), the word at 12 to 0, byte 15 to `0xd`, and goes on at `0x43e1d0` in the same turn. Where no pig is given a second draw
   decides: where its low four bits are 0, byte 11 is 0 and he waits for grain; else the work starts at stretch 1 again (a work stretch, the stamp the clock) with no new grain.

The table at `0x433684` holds 0, 6000, 8000, 10000, 11000, 12000, 13000, and 14000 (words, for 0 to 7 pigs held): a pause lets a pig grow with odds from 6000 in 65536 (one held) to 14000 in 65536 (seven held),
and none where none is held. The start routine's byte 9 of 1 is the only way the count leaves 0 in a game, so a farm that gave all its pigs would never grow another (it gives none below 4).

**The rate.** One grain does not make one pig. A grain starts a round of three work stretches and three pauses, about 912 passes. A round that gives no pig starts the next one without grain, with the odds of 15
in 16, so a grain lasts for 16 rounds on average; a pig that is given leaves the farm with one pig fewer, and the end of the round runs again at once (the start routine's stretch 6), so a farm that holds more
than 4 may give several pigs at a time. The first pig of the game of `pig-walks.txt` (one grain, byte 9 at 1) comes at the pass 12261, after twelve rounds in which the farm grew from one pig to five; the castle
counts it at the pass 12709.

## The step out and the pig

The step out (state `0xb`, `0x43e228`) and the put (state `0xd`, `0x43c6aa`) are the sawmiller's ([the step out](sawmilling.md#the-step-out-and-the-plank)); the pig goes on the flag's first empty place and
then to a flag that asks for it or to the castle. No flag asks for pigs yet: the butcher is a later slice's. The count of goods made (`0x43e1d0`) is **not ported**.

## The castle's hand-out

The three orders of `0x42a313` hold a step for grain (class 4, first word, good 4) as they hold the plank's. The castle hands grain out to the asking pig farm as it hands it out to the mill: the three
grains of the stock went to the pig farm at once in the games (`pig-materials.txt`: the stock 3 to 0, fill byte 3, rank `0x1f` after the third). With only the pig farm asking, all grain goes to it; the
split between the mill and the pig farm is the ranks' in the request search ([Grain split](grain-split.md)).

## Removal

**Status:** confirmed; **emulated**.

A pig farm is taken down as every finished building is ([Removal](removal.md)). The pig farmer walks home from where he stands: waiting or pausing (state `0x1c`), working with the node held (state `0x19`), or out
on the flag with the pig (the pig is lost). The fill byte is cleared with the building, so grain held on site and grain on its way are lost, and the flag's request words lose their class; no good stays
counted as on its way (`pig-removals.txt`: inside, working, pausing, out with a pig, grain on its way, grain held).

## Drawing

**Status:** confirmed by the draw run of the `pig-farmer` recording (`settler-draws.txt`) for the walk in and the work stretches 1 and 3. The step out with the pig has the shape of the
miller's step out, which the `miller` recording confirms; the draw run's own turns of the settlers in view move the pig farmer's timeline, so a window at the pass of the first pig was not reached
(**inferred** for that step; the add is the program's, read from `0x43ff7a`). The play run of `play/pig-farm.play` shows the step out with the pig drawn.

The figure handler of job 12 is at `0x43ff56`, reached by the stub at `0x43f7a4`. It adds `0x3200` to the figure byte for a byte below `0x80`, `0x3400` in state 5 with byte 15 at `0xd`, and `0x3280` for a
byte from `0x80`, with no sound. The engine reads them through `RuleTables.Get<PigFarmerFigureAdds>(new PigFarmer())`. He is drawn while he works with the node held (the stretches 1, 3, and 5), on the step out with
the pig, on the put, and at the door. The `open-settlers` content set draws him as a pink figure (`settler/pig-farmer/*`); the pig has its own image among the goods.

## In the engine

`src/Application/PigFarmerRules.cs` is the rules module of the pig farmer (`JobRules<PigFarmer>`): `Start`, `Work` (state `0x25`), and `Turn` (the handler's rank). It stands the pig farmer in a layout (`CanStandInHut`,
[Settlers and goods in a layout](layouts.md)). The activity is `TendsPigs` (with the stretch in `Phase`). The step out and the put are `HutWork.StepOutWithGood` and `HutWork.PutDownGood`, shared with the sawmiller
and the miller. The building's byte 9 is `Building.FillHigh`. `PigFarmRates` holds the rank base and the growth table the importer reads (`PigFarmerTables`). `layouts/pig-farm.jsonc` with `play/pig-farm.play`
shows a pig farm working.

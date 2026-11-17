# Milling

This note covers the mill and its miller: the building and the job, the request for grain that the flag makes and the rank it keeps, the
miller's wait for grain, his grinding in four stretches, his step out with the flour, the castle's hand-out of grain, and the removal of the
mill. The carriers' goods rules move the grain to the mill and the flour away from it ([Goods at a flag](goods.md)), and the castle's hand-out is
in [Materials for a building site](construction-materials.md#the-castles-hand-out). The sawmill is the pattern the mill follows
([Sawmilling](sawmilling.md)). Addresses and routine names are in the [Program index](program-index.md#simulation). The names of the type, the job,
and the goods are **inferred** ([Food buildings](food-buildings.md#which-chain-is-which)).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) puts a finished building of type 15 at a flag, as it does for the sawmill. The type is
small (object code 2), and its item holds the pointer to its flag at `+0xe` that a built site leaves (`0x43ef82`), which the handler reads. The verb also runs
`0x44579c` with the player setup, so the rank starts from the original's word. The engine matches the miller after every change of his node, state, or stretch,
the building's bytes, the flag's goods and request words, the castle's grain and flour counts, and the generator's words in three games
(`tests/OpenSettlers.Tests/mill-walks.txt`): one grain, eight grains laid at once with a ninth later (the mill takes seven, the eighth and ninth go to the castle),
and a full flag. The full flag game ends at the pass 1588, the first place the carriers free: after it the engine's carriers leave the flag's node earlier than
the original's, so the miller steps out earlier (**open lead**, the carriers' hand-over, not the mill's). The removal matches in six games (`mill-removals.txt`), the
first hand-out in one (`mill-materials.txt`, verb `construct:`), and the draw in one recording (`settler-draws.txt`, `miller`).

## The building and the job

**Status:** confirmed for the handler, **inferred** for the names.

The handler of building type 0xf is at `0x42e2a0`, entry 0xf of `0x42cd1d`. It asks at `0x42e2d2` for a settler of job 15 with no tool, as `Building_RequestWorker`
(`0x42be63`) takes it ([Food buildings](food-buildings.md#the-six-types)). The castle holds no miller, so the request's second way makes one of a settler
without a job. The job takes grain in and puts flour (good 5) on the flag with the put at `0x433a1e`.

## The start and the request for grain

**Status:** confirmed.

At the door the miller runs the start routine of job 15, `0x43d33d`, entry 0xf of `0x43ca54`. It empties the building's node of the settler's slot. Where byte 11 is
not 0 (`0xff` after the walk in, 0 after he laid flour down) it also writes the flag's request words:

- The class byte at `0x42` is `0x10`, class 4, which the good class table at `0x464117` gives grain, in the first word.
- The class byte at `0x44` is 0, and the rank at `0x43` is 0. The rank at `0x45` stays.

The settler takes state `0x23` with byte 11 at 0.

## The rank of the request for grain

**Status:** confirmed; **emulated**.

The handler keeps the rank at `0x43` with the worker inside (status bit 6): the high byte of the player word at `+0x15a`, shifted right by the goods that the building's
fill byte 8 counts, the low nibble (on the way) and the high nibble (on site) together, and 0 where they are 8 or more. `Player_SetDefaults3` (`0x44579c`) stores `0x7fee`
there, so the rank is `0x7f`, `0x3f`, and so on. The engine reads the byte through `RuleTables.MillRankBase`. After the handler asks for a worker and the request finds none,
it also writes the rank (the failed request leaves `[edi]` with bit 6 set, which the handler tests next): the emulated games show `0x7f` at the first turn of the building. **Inferred** departure: the engine reads the building's request-failed bit, which stays set until the retry scan clears it, so it keeps the rank on every turn while the bit holds, where the program writes it only on the turn whose request failed. It is unobservable: the written value equals the stored one while the fill is unchanged, and no grain class is asked before the worker's first start.

Each grain that a request serves halves the served rank where its bit 0 is set, and the handler writes the rank again from the fill. The rank is 0 at seven grains held, so a full store is seven grains
and the eighth goes to the castle (`store-full`).

## State 0x23

**Status:** confirmed; **emulated**.

State `0x23` (`0x43386b`) is the work. Byte 11 counts its stretches.

1. **0, waiting.** With no grain on site (the high nibble of byte 8 is 0) nothing changes. With one, byte 8 falls by `0x10`, bit 4 of the building's status (byte 5) is set, byte 11
   becomes 1, the frame is `0x89`, the timer is `0x17f`, the stamp is the clock, and the node holds his slot.
2. **1 to 4.** The time since the stamp comes off the timer. Once it has run out, byte 11 counts up: at 2 and 4 the node's word is 0 and `0x5dc` is added to the timer, and the
   next stretch follows at once where that leaves the timer behind; at 3 the frame is `0x89`, the node holds his slot, and the timer is `0x17f`.
3. **5.** Bit 4 of the status is cleared, byte 11 is 5 (flour), the word at 12 is 0, byte 15 is `0xd`, and he goes on at `0x43e1d0` in the same turn.

So a grain takes two grindings of `0x17f` and two pauses of `0x5dc`, about 470 passes, and he is drawn only while the node holds his slot.

## The step out and the flour

The step out (state `0xb`, `0x43e228`) and the put (state `0xd`, `0x43c6aa`) are the sawmiller's ([the step out](sawmilling.md#the-step-out-and-the-plank)); the flour goes on the flag's
first empty place and then to a flag that asks for it or to the castle. The baker's flag asks for flour ([Baking](baking.md)). The count of goods made (`0x43e1d0`) is **not ported**.

## The castle's hand-out

The three orders of `0x42a313` hold a step for grain (class 4, first word, good 4) as they hold the plank's. The castle hands grain out to the asking mill as it hands out planks, and the
mill counts one grain on its way in the low byte (`mill-materials.txt`: stock 3 to 2, fill byte 1, rank 0, then `0x3f`). The step for the food class 0 (fish, meat, bread; the mines ask them) finds
no asker yet and stays inert (M6). With no grain in the stock the mill waits and the game runs on.

## The fill byte of a food good

The table `0x462d6d` gives each good its counter: the food goods 1 to 6 count in byte 8, the low byte. The engine reads it in one place (`RuleTables.FillCounterOf`) for the count on the way (the request
search and the hand-out), the count back where a good is lost (`0x462ca4`, the removal's), and the delivery. For every good with a class it is the byte of the request word that asks for it.

## Removal

**Status:** confirmed; **emulated**.

A mill is taken down as every finished building is ([Removal](removal.md)). The miller walks home from where he stands: waiting or pausing (state `0x1c`), grinding with the node held (state `0x19`), or out on the flag with the flour (the flour is lost). The fill byte is cleared with the building,
so grain held on site and grain on its way are lost, and the flag's request words lose their class; no good stays counted as on its way (`mill-removals.txt`).

## Drawing

**Status:** confirmed by the draw run of the `miller` recording (`settler-draws.txt`).

The figure handler of job 15 is at `0x4400d3`, reached by the stub at `0x43f7bc`. It adds `0x4300` to the figure byte for a byte below `0x80`, `0x4500` in state 5 with byte 15 at `0xd`, and `0x4380` for a byte from `0x80`,
with no sound. The engine reads them through `RuleTables.Get<FoodHutFigureAdds>(new Miller())`. He is drawn while he grinds with the node held, on the step out with the flour, on the put, and at the door. The `open-settlers` content set draws him as a pale figure
(`settler/miller/*`); the flour has the sack of the goods.

## In the engine

`src/Application/MillerRules.cs` is the rules module of the miller (`JobRules<Miller>`): `Start`, `Work` (state `0x23`), and `Turn` (the handler's rank). It stands the miller in a layout (`CanStandInHut`, [Settlers and goods in a layout](layouts.md)).
The activity is `Grinds` (with the stretch in `Phase`). The step out and the put are `HutWork.StepOutWithGood` and `HutWork.PutDownGood`, shared with the sawmiller. `Building.AtWork` is status bit 4 (the miller's state routine `0x43386b` sets it at `0x4338d0` and clears it at `0x433a0d`; the baker's sets it too, [Baking](baking.md)). `layouts/mill.jsonc` with `play/mill.play` shows a mill working.

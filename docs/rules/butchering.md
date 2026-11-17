# Butchering

This note covers the butcher's shop and its butcher: the building and the job, the request for pigs that the flag makes and the rank it keeps, the
butcher's wait for a pig, his one stretch of work, his step out with the meat, the castle's hand-out of pigs, and the removal of the shop. The
carriers' goods rules move the pigs to the shop and the meat away from it ([Goods at a flag](goods.md)), and the castle's hand-out is in
[Materials for a building site](construction-materials.md#the-castles-hand-out). The mill is the pattern the shop follows ([Milling](milling.md)).
Addresses and routine names are in the [Program index](program-index.md#simulation). The names of the type, the job, and the goods are **inferred**
([Food buildings](food-buildings.md#the-evidence-for-the-chains)).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) puts a finished building of type 13 at a flag, as it does for the mill. The type is
large (object code 3), and its item holds the pointer to its flag at `+0xe` that a built site leaves (`0x43ef82`), which the handler reads. The game starts
at the start stock level 30, whose castle holds the butcher's knife, so the castle makes its jobless settler the butcher (the request's second way,
[Settlers](settlers.md#the-request)). The verb also runs `0x44579c` with the player setup. The engine matches the butcher after every change of his node,
state, or work, the building's bytes, the flag's goods and request words, the castle's pig and meat counts, and the generator's words in three games
(`tests/OpenSettlers.Tests/butcher-walks.txt`): one pig, eight pigs laid at once with a ninth later (the shop takes eight, the ninth goes to the castle), and a
full flag. The full flag game ends at the pass 1241, the first place the carriers free: after it the engine's carriers leave the flag's node earlier than the
original's, so the butcher steps out earlier (the open lead of the carriers' hand-over that [Milling](milling.md) names, not the shop's). The removal matches in
five games (`butcher-removals.txt`), the first hand-out in one (`butcher-materials.txt`, verb `construct:`), and the figure's adds are read from the program
and checked against the install (`ProgramTablesTests`).

## The building and the job

**Status:** confirmed for the handler, **inferred** for the names.

The handler of building type 0xd is at `0x42e1b3`, entry 0xd of `0x42cd1d`. It asks for a settler of job 13 with the tool of the stock word `0x26`, good 19, the
butcher's knife, as `Building_RequestWorker` (`0x42be63`) takes it ([Food buildings](food-buildings.md#the-six-types)). The castle holds no butcher, so the
request's second way makes one of a settler without a job and a knife from the castle's stock, as for the farmer ([Farming](farming.md)). The job takes pigs in
and puts meat (good 3) on the flag with the put at `0x4333a5`.

## The start and the request for pigs

**Status:** confirmed.

At the door the butcher runs the start routine of job 13, `0x43d275`, entry 0xd of `0x43ca54`. It empties the building's node of the settler's slot. Where byte 11 is
not 0 (`0xff` after the walk in, 0 after he laid meat down) it also writes the flag's request words:

- The class byte at `0x42` is 8, class 3, which the good class table at `0x464117` gives the pig, in the first word.
- The class byte at `0x44` is 0, and the rank at `0x43` is 0. The rank at `0x45` stays.

So the start writes the first word only and clears the second word's class, as the mill's does. The settler takes state `0x26` with byte 11 at 0.

## The rank of the request for pigs

**Status:** confirmed; **emulated**.

The handler keeps the rank at `0x43` with the worker inside (status bit 6): `0xff` shifted right by the goods that the building's fill byte 8 counts, the low
nibble (on the way) and the high nibble (on site) together, and 0 where they are 8 or more. The constant is in the handler (`0x42e1de`), where the mill's is a
word of the player block. After the handler asks for a worker and the request finds none, it also writes the rank (the failed request leaves `[edi]` with bit 6
set, which the handler tests next), as the mill's does. The emulated games show `0xff` at the first turn of the shop. **Inferred** departure: the engine reads the building's request-failed bit, which stays set until the retry scan clears it, so it keeps the rank on every turn while the bit holds, where the program writes it only on the turn whose request failed. It is unobservable: the written value equals the stored one while the fill is unchanged, and no pig class is asked before the worker's first start.

Each pig that a request serves halves the served rank where its bit 0 is set, and the handler writes the rank again from the fill. The rank is 0 at eight pigs held, so a
full store is eight pigs and the ninth goes to the castle (`store-full`).

## State 0x26

**Status:** confirmed; **emulated**.

State `0x26` (`0x4332a8`) is the work. Byte 11 is 0 while he waits and `0xff` while he works.

1. **0, waiting.** With no pig on site (the high nibble of byte 8 is 0) nothing changes. With one, byte 8 falls by `0x10`, byte 11 becomes `0xff` (the byte inverted), the
   frame is `0x8c`, the timer is `0x5ff`, the stamp is the clock, and the node holds his slot. The shop's status byte is left alone: the butcher sets no bit of it, where the
   miller sets bit 4.
2. **`0xff`, working.** The time since the stamp comes off the timer. Once it has run out, the node's word is 0, byte 11 is 3 (meat), the word at 12 is 0, byte 15 is `0xd`, and he
   goes on at `0x43e1d0` in the same turn.

So one pig takes one stretch of `0x5ff` clock units, about 190 passes, and one pig makes one meat. He is drawn only while the node holds his slot.

## The step out and the meat

The step out (state `0xb`, `0x43e228`) and the put (state `0xd`, `0x43c6aa`) are the sawmiller's ([the step out](sawmilling.md#the-step-out-and-the-plank)); the meat goes on the flag's
first empty place and then to a flag that asks for it or to the castle. No flag asks for meat yet: the mines are M6's. The count of goods made (`0x43e1d0`) is **not ported**.

## The castle's hand-out

The three orders of `0x42a313` hold a step for pigs (class 3, first word, good 2) as they hold the plank's. The castle hands pigs out to the asking shop as it hands out
planks, and the shop counts the pig on its way in the low byte (`butcher-materials.txt`: stock 9 to 8, fill byte 1, then the rank `0x7f`). The castle's four start pigs at level 30
go to the shop at once, so the game empties the stock first (the click `k@2,0`). With no pig in the stock the shop waits and the game runs on.

## Removal

**Status:** confirmed; **emulated**.

A butcher's shop is taken down as every finished building is ([Removal](removal.md)). The butcher walks home from where he stands: waiting (state `0x1c`), at work with the node held
(state `0x19`), or out on the flag with the meat (the meat is lost). The fill byte is cleared with the building, so pigs held on site and pigs on their way are lost, and the flag's
request words lose their class; no good stays counted as on its way (`butcher-removals.txt`).

## Drawing

**Status:** confirmed by the program's instructions; the draw run of the `butcher` recording (`settler-draws.txt`) checks the sprites.

The figure handler of job 13 is at `0x43ff90`, reached by the stub at `0x43f7ac`. It adds `0x3700` to the figure byte for a byte below `0x80` (the immediate at `0x440020`), `0x3a00` in
state 5 with byte 15 at `0xd` (`0x440015`), and `0x3780` for a byte from `0x80` (`0x43fff8`). For the bytes `0xb2`, `0xba`, `0xc2`, and `0xca` it also requests a sound (`0x44fe13`) and moves the
word at `+0x10` by 4, which is **not ported**; the work's frame `0x8c` is none of them. The engine reads the three adds through `RuleTables.Get<FoodHutFigureAdds>(new Butcher())`. He is drawn while he works with the
node held, on the step out with the meat, on the put, and at the door. The `basic` content set draws him as a red figure (`settler/butcher/*`); the meat has the sack of the goods.

## In the engine

`src/Application/ButcherRules.cs` is the rules module of the butcher (`JobRules<Butcher>`): `Start`, `Work` (state `0x26`), and `Turn` (the handler's rank). It stands the butcher in a layout
(`CanStandInHut`, [Settlers and goods in a layout](layouts.md)), with the knife from the castle's stock. The activity is `Butchers` (with `Working`). The step out and the put are
`HutWork.StepOutWithGood` and `HutWork.PutDownGood`, shared with the sawmiller and the miller. `layouts/butcher.jsonc` with `play/butcher.play` shows a shop working.

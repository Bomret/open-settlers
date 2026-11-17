# Farming

How the farmer works: which building and job he belongs to, how he picks a node to sow or a ripe field to harvest, how he waits and
tends it, how he carries the grain to the farm's flag, and how the fields grow and decay in the map's object pass. Addresses and routine
names are in the [Program index](program-index.md#simulation). The farm, its job, its tool, and the grain good are in
[Food buildings](food-buildings.md). The settlers and the walks are in [Settlers](settlers.md) and [Woodcutting](woodcutting.md); the
flag items are in [Flags](flags.md); the carrier that takes the grain on is in [Carriers](carriers.md).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) with a finished farm (type 12, a large building: the verb gives
its node the object code 3) at a flag runs the original's passes, the object pass included. `tests/OpenSettlers.Tests/farm-walks.txt`
holds four scenarios of the verb, and `FarmerTests` runs the engine through the same commands: the farm among free meadow (7000 passes,
the farmer sows three times), the fields of every stage set by hand (9000 passes), ripe and grown fields in the farmer's reach (4500
passes, the castle's grain rises from 20 to 23), and a tree on every node of his reach where the original would sow (3000 passes, he
waits). The engine matches the farmer's 16 bytes at each state change and every 250 passes, the farm's bytes, the grain on the farm's
flag, the castle's grain count, the generator, and every object byte that changes in every pass. Removal is in
[Removal](removal.md#the-farm).

## The job

**Status:** confirmed; **emulated**.

The handler of the building type 0xc (`0x42e25e`) asks for a settler of job 14 with the tool of stock word `0x28`, the good 20, the
scythe. The castle holds no farmer, so a jobless settler and a scythe become one, as for the other tool jobs ([Settlers](settlers.md#the-request)).
At the door the start routine (`0x43d311`) empties the farm's node of the settler's slot and takes state `0x21`. A settler sent home
(byte 11 equal `0xfe`) goes back to the castle's list instead: not ported, as for the woodcutter. The name of the job is **inferred**
([Food buildings](food-buildings.md#the-evidence-for-the-chains)).

## Choosing a node

**Status:** confirmed; **emulated**.

State `0x21` (`0x433b35`): when the timer has run out, the farmer takes a draw and from it an entry of the spiral table:
`((draw & 0x7c) + 0x1c) / 4`, an entry from 7 to 38, the nodes of the second and third ring around the farm and the first of the fourth.
He takes the node when
a field he may harvest stands on it (the codes `0x6e` and `0x79` to `0x7e`), or when he may sow it. Each node he does not take adds `0x1f4` to
the timer, and the turn ends when that wraps the timer, so he tries the nodes a few at a time while none fits, and the game goes on.

He may sow a node when all of these hold:

- no object stands on it (the object byte's low 7 bits are 0) and no road leaves it (the flag byte's low 6 bits are 0);
- both triangles of its tile are lowland (the terrain byte is `0x55`);
- none of its six neighbors holds the object code 3 or 4, a large building or the castle;
- the lower triangle of the tile west of it, both triangles of the tile north-west of it, and the upper triangle of the tile north of it
  are lowland.

So he sows nowhere on a road, a flag (code 1), a building or site (codes 2 to 4), a tree or a stone, and not beside a large building.
The vector to the node is the node's offset from the farm less one each way, as for the woodcutter: the first step takes him from the
farm onto its flag. The way back is that vector turned round.

## Sowing and harvesting

**Status:** confirmed; **emulated**.

The arrival at the end of the free walk (`0x438391`, entry 0xe of `0x437dfb`) takes the way back as the vector. On the node:

- where a field he may harvest stands, the frame is `0x88`, byte 13 is 1, and the timer is `0x7bf`;
- else where the node holds no object and no road, the frame is `0x87`, byte 13 is 0, and the timer is `0x5bf`;
- else he turns back at once with no grain.

State `0x22` (`0x433a3a`), when the timer has run out. A harvest sets byte 14 to 1 (he carries grain) and takes the field one stage on:
the object code goes up by one, where `0x6f` results it is `0x79`, and where `0x70` or `0x7f` results it is `0x6f`. So the grown field `0x6e`
becomes the first ripe stage, each ripe stage the next, and the last ripe stage the harvested field `0x6f`. A sowing writes the code `0x69`
where the node still holds no object and no road. Both end with byte 13 `0x80`, byte 15 0, state `0x10`, and the timer 0: he walks back the
vector.

At the flag (byte 13 `0x80`), a byte 14 above 0 lays one grain (good 4) on the flag with `Flag_PutGood` (`0x436b46`, call `0x43847d`), and
he goes into the farm through `0x43c7c2`. The goods transport takes the grain on as any good ([Goods at a flag](goods.md)): to a building
that asks for grain, else to the castle, whose grain count rises by one.

## The fields in the object pass

**Status:** confirmed; **emulated**.

The object pass (`0x428d10`, [Woodcutting](woodcutting.md#the-object-pass)) sends an object code from `0x53` through the table at `0x428eb8`
(entry = code less `0x53`). The fields are the entries 22 to 28 and 38 to 43:

| Codes | Entry's routine | Effect at each visit of the cursor |
| --- | --- | --- |
| `0x69` to `0x6d` | `0x429146` | the code goes up by one |
| `0x6e` | `0x429151` | the code becomes `0x79` (the object byte's bit 7 kept) |
| `0x6f` | `0x429184` | the object is cleared (bit 7 kept) |
| `0x70` to `0x78` | `0x42918f` | the object is cleared where the decay counter is 0 |
| `0x79` to `0x7d` | `0x429165` | the code goes up by one |
| `0x7e` | `0x429170` | the code becomes `0x6f` |

The decay counter is the word at `+0x28c` of the game state. The pass takes one off it for each node it visits (`0x428da0`), and where that
runs below 0 it starts again at `0x10`; it starts at 0 (`0x421566`). So the codes `0x70` to `0x78`, the signs of the object table, vanish
in the visit that finds the counter at 0, one visit in 17. No work puts them down; the scenario of the fields of every stage sets them by hand.

A field grows from sown to ripe in 6 visits of its node, and a ripe field decays in 6 more to the harvested field, which the next visit
clears: a node is visited once in a round of about 2560 passes on a map of 32 by 32, so a field stands about 13 rounds and the farmer has about 6 rounds to
harvest it. The cursor and the countdown are the woodcutter's. The decay counter is part of the engine's digest.

## What a field blocks

**Status:** confirmed for the classes (read from the install); inferred for the refusals, which rest on the existing class reads and no
emulator run places a field.

The class table (`0x41e640`, [Map object codes](map-object-codes.md)) gives the growing and ripe codes (`0x69` to `0x6e`, `0x79` to `0x7e`)
class 2, the class of a stone: the road cursor refuses a node of class 2 ([Roads](roads.md)), and the build-site evaluator reads it as it
reads a stone ([Build sites](build-sites.md)). The harvested field `0x6f` and the signs `0x70` to `0x78` have class 0 and block nothing.
`FieldBlockingTests` pins the install's class of the field codes and shows the engine refusing a road step, a flag, and a building on a
growing or ripe field and allowing them on a harvested one, with the install's tables; the harness has no verb that places a field, so
these refusals are the engine's own reads of the classes, not emulator verdicts.

## Drawing

**Status:** confirmed for the figure adds; the farmer's frames `0x87` and `0x88` are the engine's reading of the figure bytes.

The figure handler of job 14 (`0x44002b`, stub 14 of `0x43f744`) adds `0x3d80` to a figure byte from `0x80` while byte 13 is 0 (sowing), and
`0x3e80` while it is not (harvesting; it also asks for the sound `0x34` at the figure bytes `0x83` and `0x84`, which the engine's frames do
not reach). Below `0x80` it adds `0x4000` on the way back with the grain (byte 13 `0x80`, byte 14 1, state `0x10`) and `0x3d00` otherwise.
The engine reads the four constants from the install (`FarmerTables`). The field stages are drawn from the map like any object (the entries
of the codes `0x69` to `0x7e`). The `open-settlers` set draws the farmer with the figure of its other workers in the farmer's colour, a field
of each stage in its own image, and the grain on a flag as the other goods.

## Open leads

- The sound request at the harvest's figure bytes `0x83` and `0x84` and the program's use of byte 15 `0x10` as the state after the step are read as the forester's are.
- A farmer sent home by byte 11 equal `0xfe` and the passing of settlers on the free walk are not ported ([Woodcutting](woodcutting.md#the-walk)).

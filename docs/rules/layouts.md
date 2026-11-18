# Settlers and goods in a layout

A layout ([ADR 0009](../adr/0009-layouts-set-up-a-game-by-hand-drawn-map-and-commands.md)) may start settlers and put
goods on flags. The original has no such start. Every settler and good comes about in a run of its routines.

The engine therefore starts a settler only in a state that one of the original's routines writes. Such a state is an
**entry state** ([INV-0005](../invariants.md#inv-0005)). The engine puts a good as the original's put does. Addresses are
in the [Program index](program-index.md#simulation) and refer to the [reference build](AGENTS.md#reference-build). The
entries apply at tick 0 in the order the file lists them, after the castle, flags, roads, and buildings that the file
lists before them.

**Status:** the engine's own form, a stand-in for the layout's file keys. The fields each entry state sets are the
original's. The emulated worker walks ([Settlers](settlers.md#the-walk)) and carrier walks
([Carriers](carriers.md#the-carriers-walk)) confirm them.

## Where a settler comes from

A placed settler is one of the castle's settlers. He is one of the 20 first settlers of the [castle](castle.md), or one the
castle names ([The castle's settlers](#the-castles-settlers)). He waits in the castle in state 1 with the job the entry names.
The [walk](settlers.md#the-walk) lists the states. [Settlers](settlers.md#the-items) lists the
jobs.

The request that sends a worker (`0x42be63`) and the request that sends a carrier (`0x42b455`) empty the castle's entry
in its list of waiting settlers ([the request](settlers.md#the-request)). A placed settler empties it the same way
where it names him. The castle's counter of settlers sent out rises with the request and falls with the departure from
the castle (`0x43dd4a`). A placed settler therefore leaves the counter as it was.

A castle that holds no such settler refuses the entry. A second woodcutter or stonecutter is one the castle does not hold.

The original sends the settler its list names, the one that last took its turn in state 1. The engine takes that
settler where the list names one. **Stand-in:** where the list names none, the engine takes the lowest slot of the
job. This is the case at tick 0, because no turn has run. For a carrier the engine takes a settler of job 0. Else it
takes one of job 21, which is a jobless settler, and makes him a carrier, as `0x42b455` does.

## Entry states

| Entry | Key | Routine that writes it |
| --- | --- | --- |
| Woodcutter, stonecutter, forester, miller, fisher, farmer, butcher, baker, or pig farmer in his building | the `buildings` entry of the type (below), the building's node | the entering of `Settler_Walk` (`0x4398ea`), which hands over to `Settler_PassDoor` (`0x43c96b`, then `0x43cf15`) |
| Carrier waiting on his road | `{"job": "carrier", "state": "waiting-on-road", "x", "y", "direction"}`, a flag of the road and the direction the road leaves it | `Carrier_WaitAtEnd` (`0x43bc52`), after the road's entering in `Settler_Walk` (`0x4398ea`) |

The miller is a settler without a job that the castle makes the miller (the request names no tool), walked in as the forester is; his start routine `0x43d33d` then asks for grain ([Milling](milling.md)). The baker is made the same way, and his start routine `0x43d3d9` asks for flour ([Baking](baking.md)). The pig farmer is made and walked in the same way; his start routine `0x43d179` asks for grain too and sets the pigs the farm holds to one ([Pig farming](pig-farming.md)). The woodcutter is in state 4 at the hut's node, with the step through the door just taken. The timer holds the step's
time. The target holds the part of it that the door's speed leaves. The stamp holds the clock, and the mode is 255.

The carrier is in state `0x42` on the node of the road next to the flag at the road's far end. The far end is the end
that the castle is not reached from first.

**The woodcutter and the stonecutter.** The mission start (`0x41f2d2`) creates a building's settlers inside the finished building, so a
woodcutter's or a stonecutter's finished-building entry stands his worker in the building and the `settlers` list has no entry for
either. The stonecutter's goes on at `0x43cfdd` into the search for a heap, state `0x15` ([Stonecutting](stonecutting.md)), and the
forester's, a settler without a job made the forester, at `0x43d009` into the search for a node, state `0x13` ([Forestry](forestry.md)). The
farmer's, a settler without a job made the farmer with a scythe, goes on at `0x43d311` into the search for a field, state `0x21` ([Farming](farming.md)). The
entry takes the settler through the entering of `0x4398ea`. The hut's status gets bit 6, which
means it has its worker, and loses bit 7, which means a worker is on the way. The hut's worker is the settler. The
settler stands on the hut's node, and the node's settler word names him. The hut asks for no second worker, because
`0x42d945` tests bits 6 and 7 of the status. The engine refuses the entry where a settler stands on the hut's door or on its flag.

At his next turn the door routine (`0x43c96b`) lets him out into the search for a tree
([Woodcutting](woodcutting.md#the-start-at-the-hut)), which is state `0x12`. The walk would leave some fields that no
later routine reads before it sets them, such as the heading. Those fields hold the castle's values.

**The carrier.** The entry names the road by one of its flags and the direction. The road is a land road with no
carrier or carrier on the way, and land roads join it to the castle. The settler takes these fields
([Carriers](carriers.md#the-carriers-walk)):

- Job 0, state `0x42`, and the node next to the far flag.
- The timer `0x3f` and the frame `0x6e` plus the direction the road leaves that flag.
- The low byte of the stamp, which is that direction less 6.
- The mode, which is that direction, and the target, which is that flag's slot.
- No settler word on the node.

Both ends of the road count one carrier in the low nibble of their road bytes, and bit 7 is clear (`0x4398ea`). A good
at either end of the road then sends him on at his next turn (`0x42ff81`).

## The castle's goods

**Stand-in.** The `castle` entry takes an optional `stock` key. It is an object of good names and counts, such as
`{"scythe": 1, "butcher-knife": 1, "grain": 1}`. The castle then holds exactly those counts and 0 of every other good.

The start stock of the seed's level or of `--stock` plays no part. Nothing is taken from the named counts for the castle's
construction or for the tools of the first settlers. Later deliveries and takings of any good count as for a castle placed with
its start stock. An empty object leaves the castle with no goods. Without the key the castle holds its
[start stock](castle.md#the-start-stock). The original has no such start.

The names are the good names of the name table, the goods 1 to 26 (`NameTable.GoodName`). They are the same on the install and
on the basic content set. A name that only an object, a job, or a building type has is not a good. `stone` names both an object
and a good, and under `stock` it is the good. The castle keeps its 20 first settlers. The key is taken on the castle entry only.
A flag entry that names it fails as an unknown key.

The load fails with a problem that names the file and the castle entry, and no game starts. These cases fail it:

- a name that is no good's;
- a name given twice;
- a value that is not an object;
- a count that is not a whole number from 0 to 50000, the most a count of the castle's stock holds.

## The castle's settlers

**Stand-in.** The `castle` entry takes an optional `settlers` key. It is an object of job names and counts, such as
`{"jobless": 1}`. The castle then holds its castle settler and then exactly the named settlers. The castle settler is job 4 in
slot 0, and he builds the castle. The named settlers follow in the order the file names them, each job's together. They wait
in the castle in state 1, as the 20 first settlers do.

Without the key the castle holds the 20 first settlers. With `settlers` and no `stock`, the castle keeps the start stock as the
original leaves it after equipping its 20 first settlers. An empty object leaves the castle settler alone, and the castle is
still built. The original has no such start.

The names are the job names of the name table (`NameTable.JobName`). They are the same on the install and on the basic content
set. These jobs have a name:

- `carrier`, `digger`, `builder`, and `woodcutter`;
- `sawmiller`, `stonecutter`, `forester`, and `fisher`;
- `pig-farmer`, `butcher`, `farmer`, and `miller`;
- `baker` and `jobless`.

A building type's name is not a job's. `mill` and `sawmill` are no job's names, and `woodcutter` names the job. The jobs 1, 9, 10,
17 to 20, and 22 to 31 have no name, so no layout names them. The knights are the jobs 22 to 26. The castle settler,
`castle-settler`, cannot be named. The key is taken on the castle entry only. A flag entry that names it fails as an unknown key.

Requests take a named settler as they take a first settler ([Where a settler comes from](#where-a-settler-comes-from)). A
building takes a settler of its job first. Else it takes a settler without a job and the job's tool from the castle's goods. A
road takes a named carrier before a settler without a job.

A finished-building entry can name a worker that the named settlers and goods cannot give. The load then fails with the
problem "the castle holds no ... for the building".

`layouts/castle-contents.jsonc` and `play/castle-contents.play` show a castle of two carriers and one settler without a job.
The layout has two sawmills. The first takes the settler without a job, and the second finds no settler.

The settler table of a map holds a count of places that depends on its size (`SettlerTable.SizeOf`). The game keeps one place
free, so the table is full one place early (`SettlerTable.IsFull`). The castle settler and the named settlers together fit in
the places left.

The load fails with a problem that names the file and the castle entry, and no game starts. These cases fail it:

- a name that is no job's;
- `castle-settler`;
- a name given twice;
- a value that is not an object;
- a count that is not a whole number from 0;
- counts that, with the castle settler, are more than the places left in the settler table.

## Goods

`{"x", "y", "place": 0 to 7, "good": 1 to 25}` puts the good on the place of the flag at the node, as `Flag_PutGood`
(`0x436b46`) does. The place's destination is cleared and the flag's goods bit is set. The next turn of the flag's
goods then gives the good a destination and a direction ([Goods at a flag](goods.md#the-turn-of-a-flags-goods)).

The original's put takes the first empty place and checks nothing. The layout names the place. The engine refuses a
node with no flag, a place outside the flag's 8 places, and a place that holds a good. The engine does not port the
player block's counter of goods on flags (`0x436b46`).

## Finished buildings

`{"type": "woodcutter" | "stonecutter" | "forester" | "sawmill" | "fisher" | "farm" | "baker" | "mill" | "pig-farm" | "butcher", "x", "y"}` in the `buildings` list stands a finished building
of that type on the node, with its flag south-east of it. The entry state is the building the original's mission start
leaves: `0x41f2d2` places it through `Building_Place` (`0x449a63`, at `0x41f2f6`) and finishes it ([Build
sites](build-sites.md#the-building-types)). The placement and its refusals are those of the building site, since the evaluator
judges the node the same way, and a node that holds no site for the type's size, or no room for the flag, fails the load with
the entry named. A type the placement does not build (the storehouse, the castle, any other name; the food types are in [Food buildings](food-buildings.md), and a mill's miller, a fisher's fisher, a farm's farmer, a baker's baker, and a pig farm's pig farmer are placed as below, the other food buildings' workers not yet) and an entry with no type fail the
read, naming the types. **Stand-ins:** the finished building has no status bit 4 and no pointer in its item, and its progress
word is 0, so the digests of the M3 layouts stay (`Game.PlaceFinishedBuilding`). Its object code is 2 for the three small
types, following the size-table caller `0x42cf9d` of `Building_Place`, where the mission start writes 2 only for type `0xb` and
3 for every other type. The woodcutter's worker is the castle's woodcutter, and the stonecutter's the castle's stonecutter, walked in as above, where the mission
start creates a new settler. The forester's worker is a settler without a job that the castle makes the forester (the request's second way, no tool,
[Forestry](forestry.md)), walked in the same way, the fisher's worker is the castle's settler of job 11, walked in as the woodcutter's is ([Fishing](fishing.md)), and the farmer's a settler without a job and a scythe from the castle's stock (the request's second way with its tool; a castle whose
stock holds no scythe refuses the entry, so a layout's seed must draw a start stock level that holds one: `layouts/farm.jsonc`'s draws 38). The butcher's is made the same way with the butcher's knife (good 19), walked in as the farmer is, and his start routine `0x43d275` then asks for pigs ([Butchering](butchering.md)); `layouts/butcher.jsonc` uses the seed of the farm layout. The sawmill stands with no worker inside: its worker comes through the building's worker request as the game runs
(`CastleRoster.HandleBuilding`, [Sawmilling](sawmilling.md#the-building-and-the-job)). A player's command places a building
site and never a finished building.

## Not supported

Some settlers have no entry state here. These are every other state of a settler and every other job: mid-walk, mid-cut, and in a building other than the woodcutter's hut, the stonecutter's, the forester's, the fisher's, the farm, or the butcher's shop, and a woodcutter, a stonecutter, a forester, a fisher, a farmer, or a butcher apart from his finished-building entry. The file reader refuses the entry.

## In the engine

`Layout.Placement.cs` reads the `buildings`, `settlers`, and `goods` keys. It reads the castle entry's `stock` and `settlers` too. They travel on the castle's command (`Command.Stock` and `Command.Settlers`) to `PlaceCastleHandler` and `CastleRoster.CreateCastleSettlers`. `Game.PlaceFinishedBuilding` stands the finished building
(`BuildingPlacement.PlaceFinished`). For a building of a job whose rules module declares `CanStandInHut` (a woodcutter, a stonecutter, a forester, a miller, a fisher, a farmer, a butcher, a baker, or a pig farmer; `WalkBehavior.StandsWorker`) the entry then calls `WalkBehavior.StandWorkerInHut` (`Game.Walks`), the entry state of
the settler in his hut.
`CarrierBehavior.PlaceCarrierOnRoad` and `GoodsTransport.PlaceGood` are the carrier's and the good's. A settler for a road that the layout has not built before it fails the
load with the entry named. A good on a flag that does not stand fails it the same way.

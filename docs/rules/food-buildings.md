# Food buildings and food goods

This note names the building types of the food chain, the jobs and tools they ask for, and the goods 1 to 26. It covers what the
placement, the builder, and the worker request need of them. The work cycle of each job has its own note: [Fishing](fishing.md),
[Farming](farming.md), [Milling](milling.md), [Baking](baking.md), [Pig farming](pig-farming.md), and [Butchering](butchering.md). Addresses and routine names are in the
[Program index](program-index.md#simulation). The placement, the materials, and the builder are in
[Build sites](build-sites.md), [Materials](construction-materials.md), and [Construction](construction.md).

**Emulated.** The emulator's `peek` of four tables gives the values below. The tables are the size table (`0x42d307`), the needs table
(`0x449f18`), and the two draw tables (`0x44fcfe` and `0x44fd16`).

Two files hold the placement evidence. `tests/OpenSettlers.Tests/food-sites.txt` holds the emulator's placement of a site of each
food type (verb `remove`, click `b@x,y,T`). `BuildingSitePlacementTests` matches the engine's bytes to it. The emulator's click
places whatever the evaluator judged and refuses nothing, because the program's click on a panel's building is not recovered
([Build sites](build-sites.md), "The node states"). The file therefore holds the evaluator's site of eight nodes of the same game, the
scenario `food-evaluated`. The test sets a food type's size against each node. A large type is placed on a site of 4. A small one is placed on a
site of 3 or 4. Either is refused on a smaller site, and the game stays unchanged. That size rule is a **stand-in** for the build panel and
no recorded verdict of the program.

## The six types

**Status:** confirmed for the table values and the handlers' requests. The names are **inferred** (INV-0006); the next section
gives the evidence.

The handler of a type is entry *type* of the jump table `Building_TypeJumpTable` (`0x42cd1d`). Each handler loads the job and the
tool stock word for `Building_RequestWorker` (`0x42be63`), as [Construction](construction.md#the-worker-request) describes. The tool
good is the stock word halved.

| Type | Name | Size | Planks, stones | Handler | Job, tool | Order word, steps | Finished object, scaffold object |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | fisher | small | 2, 0 | `0x42e0b8` | job 11, good 18 | `0`, `0x1000` and `0x1000` | 167, 186 |
| 0xc | farm | large | 4, 1 | `0x42e25e` | job 14, good 20 | `8`, `0x800` and `0x556` | 154, 177 |
| 0xd | butcher | large | 2, 1 | `0x42e1b3` | job 13, good 19 | `2`, `0x1000` and `0x800` | 156, 184 |
| 0xe | pig farm | large | 4, 1 | `0x42e0fa` | job 12, no tool | `8`, `0x800` and `0x556` | 155, 177 |
| 0xf | mill | small | 3, 1 | `0x42e2a0` | job 15, no tool | `4`, `0x800` and `0x800` | 188, 187 |
| 0x10 | baker | large | 2, 1 | `0x42e37e` | job 16, no tool | `4`, `0x1000` and `0x800` | 162, 183 |

The order word is the material order of `Builder_MaterialOrder` (`0x43f0ba`). Its set bits are the turns that take a stone. The
farm and the pig farm take their stone fourth, the butcher takes its stone second, and the mill and the baker take theirs third. The
fisher takes planks only. The steps are the two words of `Builder_BlowSteps` (`0x43f0fa`). The sizes come from the size table. The
fisher and the mill are small, and the other four food types are large. The door speeds (`0x43c92b`) are one byte for each type, and the
program reads all of them.

The handler of the mill reads the building's bytes before it asks, so its request lies at `0x42e2d2`; the other five ask at the
start of the handler (13 bytes in).

**Not food types.** The same table holds the rest of the types 0 to 0x17:

- Types 2, 4, 9, and 0x11 are the huts and the sawmill ([Build sites](build-sites.md#the-building-types)).
- Types 5 to 8 are mines (size 0, job 9). Type 0xa is the storehouse.
- Types 3, 0x13, and 0x14 ask for the jobs 17, 18, and 19. Types 0x12 and 0x17 ask for job 10.
- Types 0xb, 0x15, and 0x16 ask for no worker.

Every handler asks for at most one job, with one tool, except types 0x13 and 0x14. Those load a second tool word (stock words `0x2c`
and `0x30` beside `0x22`). The second tool word of `Building_RequestWorker` is therefore not zero in every handler, as
[Construction](construction.md#the-worker-request) first stated for the types of M4. No food handler uses it.

### The evidence for the chains

**Status:** the names of the types 0xd to 0x10, of the jobs 12, 13, 15, and 16, and of the goods 2, 3, 5, and 6 are **inferred**. No text of
the install names them, because the `loca/` files hold no building or good name. The code fixes two chains and leaves the names open.
The jobs 12 and 15 each take grain in. Each puts one good that the next job of its chain takes in and turns into a third. Job 13
follows job 12, and job 16 follows job 15. Three kinds of evidence tell the chains apart:

1. **The tool.** Only the handler of the type 0xd asks for a tool (good 19, stock word `0x26`); the types 0xe, 0xf, and 0x10 ask for none.
   The install's manual (`THE SETTLERS - Manual-EN.pdf`, chapter 3, the list of tools) gives the butcher a butcher's knife and the baker
   none. That makes the job 13 the butcher's and the chain 12, 13 the pig and meat chain, and it leaves
   the chain 15, 16 the flour and bread chain. The manual is install documentation, not the program.
2. **The art the program draws.** A play run of `play/food-sites.play` on the History Edition source draws each finished type from the
   objects of the table at `0x44fd16`, on the layout `layouts/food.jsonc`. A good on a flag draws entry 320 plus its number. The
   object 188 of the type 0xf is a small, tall windmill. The object 162 of the type 0x10 is a house with an oven door. The types 0xf
   and 0x10 are therefore the mill and the baker. The object 155 of the type 0xe is the farm's twin, a stone house with a barn. The
   object 156 of the type 0xd is a wooden house. The goods 2, 5, and 6 draw a pig, a sack, and loaves of bread (entries 322, 325, 326).
3. **The tables.** The types 0xc (the farm) and 0xe share the size, the needs, the order word, the steps, and the scaffold object 177.
   The type 0xe is therefore the farm's sibling in the pig farm's place. The type 0xf is the one small grain-fed type, which fits a mill
   that only grinds.

The manual also lists the chains in prose. The farmer's wheat goes to the miller, who makes flour sacks for the baker's bread. It also
goes to the pig farmer, whose pig the butcher turns into meat.

## The well

**Status:** inferred (a negative finding). The original has no well. No handler of the 24 types asks for a job that
produces a good from nothing, and no good takes water. The goods 1 to 6 form a closed set of fish, grain, pig, meat, flour, and
bread (below), and each of the six handlers above is accounted for. M5's food buildings are therefore the six above. The roadmap and
the [M5 scope](../milestones/m5-food.md) name them.

## The jobs

**Status:** confirmed for the start routines, **inferred** for the pairing of job and building name (above).

`Settler_JobStartTable` (`0x43ca54`) runs the start routine of the job where the settler has stepped through the door. The start
routines of jobs 12 and 13 write the class of the good the building takes in into the first request word of the building's flag. That
word is the class byte at `+0x42`, with a set bit per class. The routines clear the class of the second word (`+0x44`), as the miller's and
the baker's do ([Milling](milling.md#the-start-and-the-request-for-grain), [Baking](baking.md#the-start-and-the-request-for-flour)).
The handler then keeps the rank of the first word. This is **confirmed** for jobs 12, 13, 15, and 16, whose notes read their routines.
Each writes the first request word only ([Pig farming](pig-farming.md#the-start-and-the-request-for-grain),
[Butchering](butchering.md#the-start-and-the-request-for-pigs)). The routines of jobs 11 and 14 write none, so the fisher's and the
farm's buildings ask for no good.

| Job | Start | Takes in (class) | Goes on to state | Puts (the state's byte 11) |
| --- | --- | --- | --- | --- |
| 11 fisher | `Fisher_Start` (`0x43d14d`) | nothing | `0x1f` | fish, by the put at `0x43837f` (the arrival `0x438232`) |
| 12 pig farmer | `PigFarmer_Start` (`0x43d179`) | grain (class 4, first word) | `0x25` | pig, `0x433577` |
| 13 butcher | `Butcher_Start` (`0x43d275`) | pig (class 3, first word; the start clears the second word's class, [Butchering](butchering.md#the-start-and-the-request-for-pigs)) | `0x26` | meat, `0x4333a5` |
| 14 farm | `Farmer_Start` (`0x43d311`) | nothing | `0x21` | grain, by the put at `0x43847d` (the arrival `0x438391`) |
| 15 miller | `MillWorker_Start` (`0x43d33d`) | grain (class 4, first word) | `0x23` | flour, `0x433a1e` |
| 16 baker | `BakerWorker_Start` (`0x43d3d9`) | flour (class 5, first word) | `0x24` | bread, `0x43384f` |

Only two jobs put a good with `Flag_PutGood` (`0x436b46`) from a free walk: job 11 (good 1) and job 14 (good 4). The jobs inside a
building set byte 11 and go on to state `0xd` (`Settler_PutDownGood`, `0x43c6aa`), the put of [Sawmilling](sawmilling.md). The
type 1 building's settler is the fisher. Job 11 is the job the engine ports as `Fisher`, and [Fishing](fishing.md) describes his walk
and cast.

## The goods

**Status:** confirmed for the classes, inferred for the names. The names are those the chains, the tool list, and the art give (INV-0006);
no text of the install names a good. Every good 1 to 26 is an entry of the content catalog under its stable name (`GoodTables` holds the evidence for each good's name).

`Good_DestinationClasses` (`0x464117`) gives each good a class and a request word; a good of class `0xff` goes to storage at once. The
goods 1 to 26 are the 26 steps of `Player_SetTransportPriorities` (`0x445570`).

| Good | Name | Class, word | Evidence |
| --- | --- | --- | --- |
| 1 | fish | 0, first (`0x42`) | put by job 11 (`0x43837f`); shares class 0 with goods 3 and 6, the three goods of a request that takes any of them |
| 2 | pig | 3, first | put by job 12 (`0x433577`); asked by job 13 (class 3); drawn as a pig (entry 322) |
| 3 | meat | 0, first | put by job 13 (`0x4333a5`) |
| 4 | grain | 4, first | put by job 14 (`0x43847d`); asked by jobs 12 and 15 (class 4) |
| 5 | flour | 5, first | put by job 15 (`0x433a1e`); asked by job 16 (class 5); drawn as a sack (entry 325) |
| 6 | bread | 0, first | put by job 16 (`0x43384f`); drawn as loaves (entry 326) |
| 7 | log | 5, second (`0x44`) | the woodcutter's put (`0x438013`); asked by the sawmiller's start ([Sawmilling](sawmilling.md)) |
| 8 | plank | 1, first | the sawmiller's put (`0x43c6aa`); a site's first request ([Materials](construction-materials.md)) |
| 9 | boat | none (storage) | `0x464117`, row 9; no routine read puts or asks it; drawn as a boat hull (entry 329); the manual's boat builder makes boats of planks, and a boat leaves the castle for a waterway. **Inferred** |
| 10 | stone | 4, second | the stonecutter's put (`0x438182`); a site's second request |
| 11 | iron ore | 1, second | asked by the start of job 10 in the type 0x12, the foundry; `0x464117`, row 11. **Inferred** from the manual's foundry chain |
| 12 | iron | 2, second | put by the work of job 10 (`0x4342df`); asked by the types 0x13 and 0x14, the two smiths; `0x464117`, row 12. **Inferred** |
| 13 | coal | 2, first | asked by the types 0x12, 0x14, and 0x17, the two foundries and the blacksmith; `0x464117`, row 13. **Inferred** |
| 14 | gold ore | 0, second | asked by the type 0x17, the other foundry; `0x464117`, row 14; counts against resource 1 with good 15 (`0x462c8d`). **Inferred** |
| 15 | gold | 3, second | put by the work of job 10 (`0x4342f1`); `0x464117`, row 15. **Inferred** |
| 16 | shovel | none (storage) | the tool of the digger's request, job 2 (`0x42d328`, stock word `0x20`); [Digging](digging.md). **Inferred** from the manual's list |
| 17 | hammer | none (storage) | the tool of the builder's request (`0x42d5fc`, stock word `0x22`) and the first tool of the types 0x13 and 0x14 |
| 18 | fishing rod | none (storage) | the tool of the type 1 handler (`0x42e0b8`, stock word `0x24`) |
| 19 | butcher's knife | none (storage) | the tool of the type 0xd handler (`0x42e1b3`, stock word `0x26`) |
| 20 | scythe | none (storage) | the tool of the type 0xc handler (`0x42e25e`, stock word `0x28`) |
| 21 | hatchet | none (storage) | the tool of the type 2 handler (`0x42d945`, stock word `0x2a`) |
| 22 | saw | none (storage) | the tool of the type 0x11 handler (`0x42da0b`, stock word `0x2c`), the second tool of the type 0x13 |
| 23 | pickaxe | none (storage) | the tool of the type 4 handler (`0x42d987`, stock word `0x2e`) |
| 24 | pliers | none (storage) | the second tool of the type 0x14 (stock word `0x30`) |
| 25 | sword | none (storage) | `0x464117`, row 25; no routine read puts or asks it; drawn as a blade with a yellow hilt (entry 345). **Inferred**: the manual's weapon smith makes swords and shields |
| 26 | shield | none (storage) | `0x464117`, row 26; no routine read puts or asks it; no sprite observed (a layout cannot lay good 26 on a flag). **Inferred** by elimination: the second weapon of the manual's weapon smith. The order of 25 and 26 is **open** |

The nine tools are the nine of the manual's list: hammer, saw, hatchet, shovel, scythe, pickaxe, fishing pole, pliers, and butcher's
knife. The two workers with two tools are the manual's iron worker and blacksmith. The iron worker (type 0x13) has a hammer and a saw. The
blacksmith (type 0x14) has a hammer and pliers. The names of the goods 9 and 11 to 26 rest on those chains, the manual, and the sprites.
The program names none of them.

A good counts into the byte of a building's fill that the table `0x462d6d` names (`RuleTables.FillCounterOf`,
[Milling](milling.md#the-fill-byte-of-a-food-good)). The food goods 1 to 6 therefore count into byte 8. The pig farm's and the mill's
handlers read its high nibble as the goods held and its low nibble as the goods on the way.

### Request class of each food good

Goods 1, 3, and 6 are class 0 of the first request word; the pig is class 3, grain class 4, and flour class 5, all of the first word.
The mines, which later slices read, take class 0; the food buildings ask by the class bits their jobs' start routines write.

## Drawing

**Status:** confirmed. The draw routine `0x44e329` picks a stub by the type (the jump table `0x44e3aa`); the stub of each food type
stores the object number of the finished building: 167 (fisher), 154 (farm), 156 (butcher), 155 (pig farm), 188 (mill), and 162
(baker), each plus the 1250 of `0x44da4d`. The table at `0x44fd16` holds the same numbers for the site's last stage, and the table
at `0x44fcfe` the scaffold's: 186, 177, 184, 177, 187, and 183. A good on a flag draws the entry 320 plus its number.

## In the engine

- `src/Domain/BuildingType.cs` (`Fisher`, `Farm`, `Butcher`, `PigFarm`, `Mill`, `Baker`) and `src/Domain/Good.cs` (`Fish`, `Pig`, `Meat`,
  `Grain`, `Flour`, `Bread`, `Foods`). A new building type goes into each of these lists: `BuildingPlacement.Types`, `Layout.Placement`'s
  `BuildingNames` and its refusal text, `ProgramTablesImporter.WorkerRequestAddresses`, `NameTable`, the build panel in
  `MapViewControl`, `SiteStages.ScaffoldIsLarge`, `ObjectSprites.Objects` (History Edition), and `OpenSettlersSource.FinishedKeys` with
  the art of `tools/basic-art.py`. The other goods 7 to 26 are catalog entries only.
- `CastleStock.FoodCounts`, `PlayFields.Food`, and `CastleReadout.Text` give the counts to the play dump, `expect`, and the castle bar. `CastleReadout.AddedGoods` adds each good that the content set's start stock does not list and the castle holds, by stable name.
  The `list` readout names the goods on a flag (`goods`) and the good a settler carries (`carrying`) by their stable names (`good.Name`), with no raw value beside them.
- The workers of the fisher, farm, mill, pig farm, butcher, and baker run their work cycles ([Fishing](fishing.md), [Farming](farming.md), [Milling](milling.md), [Pig farming](pig-farming.md), [Butchering](butchering.md), [Baking](baking.md)).

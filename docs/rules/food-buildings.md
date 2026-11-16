# Food buildings and food goods

This note names the building types of the food chain, the jobs and tools they ask for, and the goods 1 to 26. It covers what the
placement, the builder, and the worker request need of them. The work cycle of each job (the fisher's catch, the farm's fields, the
mill, the baker, the pig farm, and the butcher) is read by the later slices of M5. Addresses and routine names are in the
[Program index](program-index.md#simulation). The placement, the materials, and the builder are in
[Build sites](build-sites.md), [Materials](construction-materials.md), and [Construction](construction.md).

**Emulated.** The emulator's `peek` of the size table (`0x42d307`), the needs table (`0x449f18`), and the two draw tables (`0x44fcfe`
and `0x44fd16`) gives the values below. `tests/OpenSettlers.Tests/food-sites.txt` holds the emulator's placement of a site of each
food type (verb `remove`, click `b@x,y,T`), and `BuildingSitePlacementTests` matches the engine's bytes to it.

## The six types

**Status:** confirmed for the table values and the handlers' requests. The names are **inferred** (INV-0006) from the chain below:
what each job takes in and puts out.

The handler of a type is entry *type* of the jump table `Building_TypeJumpTable` (`0x42cd1d`). Each handler loads the job and the
tool stock word for `Building_RequestWorker` (`0x42be63`), as [Construction](construction.md#the-worker-request) describes. The tool
good is the stock word halved.

| Type | Name | Size | Planks, stones | Handler | Job, tool | Order word, steps | Finished object, scaffold object |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | fisher | small | 2, 0 | `0x42e0b8` | job 11, good 18 | `0`, `0x1000` and `0x1000` | 167, 186 |
| 0xc | farm | large | 4, 1 | `0x42e25e` | job 14, good 20 | `8`, `0x800` and `0x556` | 154, 177 |
| 0xd | baker | large | 2, 1 | `0x42e1b3` | job 13, good 19 | `2`, `0x1000` and `0x800` | 156, 184 |
| 0xe | mill | large | 4, 1 | `0x42e0fa` | job 12, no tool | `8`, `0x800` and `0x556` | 155, 177 |
| 0xf | pig farm | small | 3, 1 | `0x42e2a0` | job 15, no tool | `4`, `0x800` and `0x800` | 188, 187 |
| 0x10 | butcher | large | 2, 1 | `0x42e37e` | job 16, no tool | `4`, `0x1000` and `0x800` | 162, 183 |

The order word is the material order of `Builder_MaterialOrder` (`0x43f0ba`): the set bits are the turns that take a stone, so the
farm and the mill take their stone fourth, the baker its stone second, the pig farm and the butcher theirs third, and the fisher
takes planks only. The steps are the two words of `Builder_BlowSteps` (`0x43f0fa`). The sizes are the size table: the pig
farm is a small building and the other large food types are large. The door speeds (`0x43c92b`) are one byte for each type, read for
all of them.

The handler of the pig farm reads the building's bytes before it asks, so its request lies at `0x42e2d2`; the other five ask at the
start of the handler (13 bytes in).

**Not food types.** The same table holds the rest of the types 0 to 0x17. Types 2, 4, 9, and 0x11 are the huts and the sawmill
([Build sites](build-sites.md#the-building-types)); 5 to 8 are mines (size 0, job 9); 0xa is the storehouse; 3, 0x13, and 0x14 ask for the
jobs 17, 18, and 19, and 0x12 and 0x17 ask for job 10; 0xb, 0x15, and 0x16 ask for no worker. Every handler asks for at most one job, with
one tool, except types 0x13 and 0x14: those load a second tool word (stock words `0x2c` and `0x30` beside `0x22`). The second tool
word of `Building_RequestWorker` is therefore not zero in every handler, as [Construction](construction.md#the-worker-request) first
stated for the types of M4; no food handler uses it.

### The well

**Status:** inferred (a negative finding). The original has no well. No handler of the 24 types asks for a job that
produces a good from nothing and no good takes water: the goods 1 to 6 form a closed chain of fish, grain, flour, bread, pig, and
meat (below), and each of the six handlers above is accounted for. M5's food buildings are therefore the six above. The roadmap and
the [M5 scope](../milestones/m5-food.md) name them.

## The jobs

**Status:** confirmed for the start routines, **inferred** for the pairing of job and building name.

`Settler_JobStartTable` (`0x43ca54`) runs the start routine of the job where the settler has stepped through the door. The start
routines of jobs 12, 13, 15, and 16 write the class of the good the building takes in into both request words of the building's
flag (the class bytes at `+0x42` and `+0x44`, a set bit per class); the handler then keeps the rank of the first word. The routines of
jobs 11 and 14 write none, so the fisher's and the farm's buildings ask for no good.

| Job | Start | Takes in (class) | Goes on to state | Puts (the state's byte 11) |
| --- | --- | --- | --- | --- |
| 11 fisher | `Fisher_Start` (`0x43d14d`) | nothing | `0x1f` | fish, by the put at `0x43837f` (the arrival `0x438232`) |
| 12 mill | `MillWorker_Start` (`0x43d179`) | grain (class 4, first word) | `0x25` | flour, `0x433577` |
| 13 baker | `BakerWorker_Start` (`0x43d275`) | flour (class 3, first word) | `0x26` | bread, `0x4333a5` |
| 14 farm | `Farmer_Start` (`0x43d311`) | nothing | `0x21` | grain, by the put at `0x43847d` (the arrival `0x438391`) |
| 15 pig farm | `PigFarmer_Start` (`0x43d33d`) | grain (class 4, first word) | `0x23` | pig, `0x433a1e` |
| 16 butcher | `Butcher_Start` (`0x43d3d9`) | pig (class 5, first word) | `0x24` | meat, `0x43384f` |

Only two jobs put a good with `Flag_PutGood` (`0x436b46`) from a free walk: job 11 (good 1) and job 14 (good 4). The jobs inside a
building set byte 11 and go on to state `0xd` (`Settler_PutDownGood`, `0x43c6aa`), the put of [Sawmilling](sawmilling.md). The
type 1 hut's settler is the fisher: job 11 is the job the engine ports as `TypeOneHut`, and its walk (a node by the water) is the
fisher's. The rename of the job is the next slice's.

## The goods

**Status:** confirmed for the classes, inferred for the names. The names are those the chain above gives (INV-0006); no text of the
install names a good.

`Good_DestinationClasses` (`0x464117`) gives each good a class and a request word; a good of class `0xff` goes to storage at once.

| Good | Name | Class, word | Evidence |
| --- | --- | --- | --- |
| 1 | fish | 0, first (`0x42`) | put by job 11 (`0x43837f`); shares class 0 with goods 3 and 6, the three goods of a request that takes any of them |
| 2 | flour | 3, first | put by job 12 (`0x433577`); asked by job 13 (class 3) |
| 3 | bread | 0, first | put by job 13 (`0x4333a5`) |
| 4 | grain | 4, first | put by job 14 (`0x43847d`); asked by jobs 12 and 15 (class 4) |
| 5 | pig | 5, first | put by job 15 (`0x433a1e`); asked by job 16 (class 5) |
| 6 | meat | 0, first | put by job 16 (`0x43384f`) |
| 7 | log | 5, second (`0x44`) | the woodcutter's put (`0x438013`); asked by the sawmiller's start ([Sawmilling](sawmilling.md)) |
| 8 | plank | 1, first | the sawmiller's put (`0x43c6aa`); a site's first request ([Materials](construction-materials.md)) |
| 9 | not named | none (storage) | no routine read puts or asks it |
| 10 | stone | 4, second | the stonecutter's put (`0x438182`); a site's second request |
| 11 | not named | 1, second | asked by the start of job 10 in the type 0x12; put by none read |
| 12 | not named | 2, second | asked by the types 0x13 and 0x14; put by the work of job 10 (`0x4342df`) |
| 13 | not named | 2, first | asked by the types 0x12, 0x14, and 0x17 |
| 14 | not named | 0, second | asked by the type 0x17 |
| 15 | not named | 3, second | put by the work of job 10 (`0x4342f1`) |
| 16 to 26 | not named | none (storage) | the tool goods: 16 is the digger's tool (the handler of type 0 asks for it), 17 the builder's, 18 the fisher's, 19 the baker's, 20 the farm's, 21 the woodcutter's, 22 the sawmiller's, 23 the stonecutter's and the miners', 24 the tool of the type 0x14; 25 and 26 no routine read names |

A good counts into the low byte of a building's fill (byte 8) unless it is 7 or from 9 on but not 13 (`Good.FillsHigh`), so the
food goods 1 to 6 count into byte 8, the byte the baker's and the mill's handlers read as held (high nibble) and on the way (low
nibble).

### Request class of each food good

Goods 1, 3, and 6 are class 0 of the first request word; flour is class 3, grain class 4, and the pig class 5, all of the first word.
The mines, which later slices read, take class 0; the food buildings ask by the class bits their jobs' start routines write.

## Drawing

**Status:** confirmed. The draw routine `0x44e329` picks a stub by the type (the jump table `0x44e3aa`); the stub of each food type
stores the object number of the finished building: 167 (fisher), 154 (farm), 156 (baker), 155 (mill), 188 (pig farm), and 162
(butcher), each plus the 1250 of `0x44da4d`. The table at `0x44fd16` holds the same numbers for the site's last stage, and the table
at `0x44fcfe` the scaffold's: 186, 177, 184, 177, 187, and 183. A good on a flag draws the entry 320 plus its number.

## In the engine

- `src/Domain/BuildingType.cs` (`Fisher`, `Farm`, `Baker`, `Mill`, `PigFarm`, `Butcher`) and `src/Domain/Good.cs` (`Fish`, `Flour`, `Bread`,
  `Grain`, `Pig`, `Meat`, `Foods`). A building type is added to every list by: `BuildingPlacement.Types`, `Layout.Placement`'s
  `BuildingNames` and its refusal text, `ProgramTablesImporter.WorkerRequestAddresses`, `NameTable`, the build panel in
  `MapViewControl`, `SiteStages.ScaffoldIsLarge`, `ObjectSprites.Objects` (History Edition), and `OpenSettlersSource.FinishedKeys` with
  the art of `tools/open-settlers-art.py`.
- `CastleStock.FoodCounts`, `PlayFields.Food`, and `CastleReadout.Text` give the counts to the play dump, `expect`, and the castle bar.
- **Stand-in:** the six types' workers run no work cycle yet: a settler who takes a food job enters the building and stands. The job
  slices add the cycles.

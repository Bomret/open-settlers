# The builder and the worker request

This note covers how a building site asks for its builder and how the builder walks out and builds in state 9. It also covers how the finished building asks for its own worker and how a site is drawn as it grows. Addresses and routine names are in the [Program index](program-index.md#castle-and-build-sites). The materials the builder works from are in [Materials for a building site](construction-materials.md). The site's placement is in [Build sites](build-sites.md#placing-a-building).

**Emulated.** The file `tests/OpenSettlers.Tests/construction-builder.txt` holds checkpoints of two games of the harness verb `construct:` (`tools/GhidraDump/README.md`).

- In the first game, the castle's builder walks out to a forester's site. He takes the two planks as they reach it, builds it in 16 blows, and goes home. The finished forester asks for its worker, and a settler without a job walks out to it.
- The second game places a woodcutter's site beside it. A settler without a job and a tool from the stock builds it.

`ConstructionBuilderTests` runs the engine through the same commands. At each checkpoint these values match:

- the builder's and the worker's 16 bytes;
- the site's 18 bytes;
- the flag's request bytes;
- the generator.

That tests the number of blows, their draws, and their ticks.

The goods reach the site at the passes the original delivers them. The fixture lists those passes, and the test adds the goods to the site's fill. The engine's hand-out lays a good on the castle's flag at once ([Materials](construction-materials.md#stand-ins-and-what-is-not-ported)).

## The request for a builder

**Status:** confirmed, and emulated.

A building under construction goes to `0x42d55f` in the building pass. A large type goes through `0x42d328` first ([Materials](construction-materials.md#the-building-passs-turn-of-a-site)).

The routine acts where the site's status has none of bits 2, 6, and 7. Bit 2 is a failed request. Bits 6 and 7 mean a worker is inside or on the way. Then the routine sets the progress word to 1. It asks the castle for job 3 with the tool in the stock word at `+4 + 0x22` of the storage item. That is `0x42d5fc`, through `Building_RequestWorker` (`0x42be63`).

A request that finds no worker sets bit 2, the building's request-failed bit. The retry scan of the main pass clears it a few passes later (`0x428728`, [Timing](timing.md)). The site then asks again.

A site that holds its materials and has no builder inside does not progress. Nothing but the builder's blows changes its progress word.

## The worker request

**Status:** confirmed for the search and the second way, emulated for both.

`Building_RequestWorker` (`0x42be63`) takes the job and two tool words. The second tool word is 0 in every handler of the program. The search runs over the flags from the building's flag, as [Settlers](settlers.md#the-request) describes.

At the castle's flag, the request first takes the settler that the castle's list of waiting settlers holds for the job. That list is the word at `0x42` plus twice the job. The settler leaves the castle (state 15), the building takes bit 7 (a worker on the way), and the list loses him.

Where the list holds none, a second way makes one. Two conditions must hold:

- The list's entry of the jobs without a job must hold a settler. That is job 21, word `0x6c`.
- The stock word of the tool must not be 0. That word is the word at `+4` plus the tool word. A job with no tool needs none, as the forester's.

The settler takes the job in bits 2 to 6 of his first byte. The stock loses a tool. He leaves the castle as a settler of the first way does.

The request fails with no settler of the job and no settler without a job, or with no tool. Then the building's handler sets bit 2.

Each handler asks for one job and tool. `ProgramTablesImporter` reads them from the handlers' instructions (`RuleTables.WorkerOf`, `BuilderRequest`):

| Asker | Handler (the request) | Job | Tool good (word / 2) |
| --- | --- | --- | --- |
| Building under construction | `0x42d5fc` | 3 builder | 17 (`0x22`) |
| Woodcutter's hut (type 2) | `0x42d952` | 5 woodcutter | 21 (`0x2a`) |
| Stonecutter's hut (type 4) | `0x42d994` | 7 | 23 (`0x2e`) |
| Forester's hut (type 9) | `0x42d9d6` | 8 | none |
| Sawmill (type 0x11) | `0x42da18` | 6 | 22 (`0x2c`) |

The castle's first settlers hold one builder (job 3), one woodcutter (job 5), one of job 6, and one of job 7. They hold none of job 8 and five without a job.

**Emulated:** a second site placed at once gets a settler without a job and a hammer. The stock holds 21 hammers at level 25, and the word falls by one. A finished forester gets a settler without a job.

The names of the tool goods are **inferred** from the job that asks for each (INV-0006). The engine names the builder's tool `Good 17` in the fixture and names none of the others.

**Emulated:** with no hammer in the stock, the second site's request fails at each of its turns. Bit 2 is set, which is the sign flag the routine leaves (`0x42d61a`). The retry scan clears it the next pass, and the site asks again 33 passes later. Once a hammer is in the stock, the next request makes a settler without a job the builder (`construction-builder.txt`, `builder-no-hammer`).

**Emulated** (`builder-no-jobless`): five roads have carriers that take the five settlers without a job. Then a second site's request fails at each of its turns in the same way. It also fails at a turn where the castle's list of settlers without a job is empty because the next one has not entered it yet. The castle's goods still go out to both sites.

**Not ported:** the second tool word, the storage's counter of settlers (the word at `0x40`), and the player's counts per job.

## The builder's turn

**Status:** confirmed, and emulated.

**The start (`0x43ce20`, entry 3 of the job start table `0x43ca54`).** The start runs where the builder has stepped through the site's door, in state 4 ([Settlers](settlers.md#the-walk)). He takes state 9 with these values:

- The frame is `0x62`. For the types 10, 17, 19, and 22 it is `0x64`. The mask `0x4a0400` tests the type.
- The timer is `0x7f`.
- The word at 12 holds the site's slot.
- Byte 11 is 1.
- The count of the materials taken is 0. It is the low nibble of byte 14.

He stays on the node. The site's status bit 6, a worker inside, was set when he entered ([Settlers](settlers.md#the-walk)). That bit makes the planks' rank `0xfe`.

**State 9 (`0x43ec82`).** The units since his last turn come off his timer, as in every waiting state. Once the timer has run out, he works until it is positive again. Byte 11 selects what he does:

- With byte 11 not negative, he looks for the next material. The order word of the type (`0x43f0ba`) says what it is. It is one 16-bit word for each type. The material of turn *n* is a plank where bit *n* is clear and a stone where it is set.
  - A material on site, counted by the high nibble of the fill byte, goes out of the fill by `0x10`. Byte 8 counts the planks and byte 9 counts the stones. It also goes out of the count the site still needs, which is the bytes at 16 and 17.
  - The count of the taken materials goes up. Byte 15, the blows left, is 8. Byte 11 is `0xff`.
  - With no material on site, the timer gains `0x100`. Where that would carry, the timer becomes `0xff` instead. He waits and asks again at each turn.
- With byte 11 negative, he makes a blow. The step of the type (`0x43f0fa`) is added to the progress word. The table holds two words for each type. The first applies while the progress word is below `0x8000`, and the second from there on. A carry out of the word finishes the building. Otherwise the blows left fall by one. At 0 he looks for the next material at once.
- After a material taken and after a blow, he draws once from the generator (`0x44257a`, the settler's generator). He takes the frame `0x66` plus the draw's low two bits, plus 4 for the second figure. He adds the time of that frame's step to the timer. The step time table is `0x43f248`. While that does not run the timer over, he goes on.

**Emulated:** the forester's 16 blows take about 100 passes each in a game without a view. A settler's turn comes every 16 or 17 passes. He takes the first material at the first turn after the first plank is on site. The plank is on site at pass 481 and he takes it at pass 491.

The tables hold these values for the types:

- The order word is 0 for the woodcutter, the stonecutter, and the forester. They need planks only.
- The order word is `0x000c` for the sawmill. Its third and fourth materials are stones.
- The step words are `0x1000` and `0x1000` for the woodcutter, the stonecutter, and the forester. That makes 16 blows.
- The step words are `0x0800` and `0x0556` for the sawmill. It takes 16 blows before the word's top bit and 24 after.

**The end (`0x43ef20`).** These values change:

- The progress word is 0.
- Status bit 6 is off.
- The word at 10, the worker's slot, is 0.
- Bit 7 of the type byte, the construction mark, is off.
- The item's word at 14 takes the pointer to the flag item. The engine keeps no such pointer.
- The site's flag asks for nothing. The class bytes at `0x42` and `0x44` are 0, and the ranks stay.

The player's counts of the type move. There is one less building site and one more building (`0x43f053` to `0x43f07b`). A counter at `+0x116` of the player grows by a byte of the table at `+0xdc` of the game state. This is **not ported**, as the player's other counts are not ([Removal](removal.md)).

For the types 0xb, 0x15, and 0x16, the buildings of a mission's start, the routine calls `0x45f402`. This is **not ported**.

The builder's byte 11 becomes `0xfe`. Bytes 14 and 15 become 0 and 2. His timer is 0, and he takes state `0x4a`.

**State 0x4a (`0x43e008`).** When the node south-east of him holds no settler, he takes state 7. That node is the flag's. He falls into the step onto the flag in the same turn (`0x43e036`, [Woodcutting](woodcutting.md#the-start-at-the-hut)). State 7 waits out the step and takes state 2 with mode `0xfe`, the walk to the storage.

The walk and the entry into the castle are the walk home's ([Settlers](settlers.md#the-walk)). Job 3's start routine begins with the test of mode `0xfe`. That test sends him into the storage (`0x43cd06`), as it does for every job but jobs 4 and 21.

**Emulated:** the builder is in the castle's list again 165 passes after the building stands.

**Emulated** (`construction-builder.txt`, `builder-waits-at-the-door`): the carrier of the flag's road can stand on the flag for a good at the end. Then the builder keeps state `0x4a` at the building's node at each of his turns. The first turn after the carrier has left steps him onto the flag.

## The finished building asks for its worker

A building whose construction mark has gone goes to the handler of its type in the next building pass (`0x42cd1d`). The handler asks for its worker as above.

**Emulated:** the forester's request is made at the first building turn after the end, which is 19 passes after it. A settler without a job walks out and goes in.

The work of each type is in its own note: [Stonecutting](stonecutting.md), [Forestry](forestry.md), and [Sawmilling](sawmilling.md). A worker whose job has a start routine in the table `0x43ca54` runs it at the door.

## The job numbers

The job table of the program lists these jobs:

- The builder is job 3, with the request above.
- The digger is job 2 (`0x42d328`, tool good 16, [The digger](digging.md)).
- The castle's own first settler is job 4. This settler stands by while the castle is built (state 10, `0x43f17a`) and carries goods out of the castle.

No building asks for job 4. The engine's `Job.Digger` is job 2, `Job.Builder` is job 3, and `Job.CastleSettler` is job 4.

## Drawing a site

**Status:** confirmed from the code of the draw routine for a building under construction (`0x44fa79`). **Emulated:** `site-draws.txt` holds draw runs of the building draw (`0x44e329`) on the forester's site and the sawmill's site through their progress words. `SiteDrawTests` compares the engine's layers with them. The images are drawn through the sprite routines of the platform layer (`0x44da4d`, `Gfx_DrawSpriteGrowing` `0x406350`).

The routine takes the building's type as its index `k` into two byte tables of object numbers:

- The scaffold's table is at `0x44fcfe`. It holds 186 for the types 2, 4, and 9, and 181 for the type 0x11.
- The finished buildings' table is at `0x44fd16`. It holds 168, 169, 170, and 160.

The routine reads the progress word `p`:

- `p` is 0: the object 144, a large site that has not started, and nothing else.
- `p` is below `0x8000`: the piles of goods on site, then the object 145, the base. Where `2p` as a 16-bit value is 3 or more, the scaffold's object follows, grown by `2p`.
- `p` is `0x8000` or more: the piles, the whole scaffold, and then the finished building's object grown by `2p - 0x10000` where that is not 0.

A grown sprite shows its bottom rows only. The top of the clip lies `fraction * height / 65536` rows above the bottom row. The program takes the sprite's height from its header. So `fraction * height >> 16` plus 1 rows show. A sprite's shadow is drawn whole.

The engine draws the layers through `SiteStages` under both sources. The History Edition takes the entries of the objects plus 1250 (`ObjectSprites.SiteLayerEntry`). The content set has a scaffold image for the small types (`building/stage-small`) and for the large types (`building/stage-large`).

**Not ported:** the piles of the planks and stones on site. The routine draws a good's sprite `0x44de9a` once for each good on site, 3 pixels apart. The planks and stones come from the fill bytes' high nibbles.

**The builder's figure.** His figure handler is stub 3 of `0x43f744`, at `0x43faf4`. It adds `0x500` to the figure byte for a byte below `0x80`. It adds `0x580` for a byte from `0x80`, which are the blows. It requests a sound at some of the work figures through `0x44fe13`. The engine leaves sounds out. The word's high byte picks the row of figure bases ([Settlers](settlers.md#drawing-the-walk-leads)). `RuleTables.BuilderFigureAdds` holds both adds, read from `0x43fb60` and `0x43fb55`.

The engine makes the record of a settler where the node holds his slot. So the builder inside the building is drawn at its node with the frame of his state. `SettlerFigures` draws the activities `Builds` and `LeavesSite` for job 3.

**Confirmed** by a draw run of the `builder` recording of `settler-draws.txt` on a `construct:` game. It shows the builder walking out, building with both adds, and walking home, drawn as the engine draws him. The `builder-waits` recording holds him in state `0x4a` at the building's node while a carrier stands on the flag. It also shows his step onto the flag. The engine draws both as the original does.

## In the engine

`BuilderRules`, the rules module of the builder (`JobRules<Builder>`), holds the start routine, state 9, the end, and state 0x4a. `HutWork.RegisterModule` fills the table of the job start routines (`0x43ca54`) that the door's step runs.

`SitePass.Turn` asks for the builder (`CastleRoster.AskForWorker`). `CastleRoster.RequestWorker` is `0x42be63` with its second way. `CastleStock.Take` takes the tool.

`RuleTables.WorkerOf`, `BuilderRequest`, `StoneAt`, `BlowStep`, and `BuilderFigureAdds` hold the tables as typed values. `Builds` and `LeavesSite` are the activities of states 9 and `0x4a`. They keep their codes for the exporters. The tests on made-up tables are `WorkerRequestTests` and `BuilderTests`.

# The builder and the worker request

How a building site asks for its builder, how the builder walks out and builds in state 9, how the finished building asks for its own
worker, and how a site is drawn as it grows. Addresses and routine names are in the [Program index](program-index.md#castle-and-build-sites);
the materials the builder works from are in [Materials for a building site](construction-materials.md); the site's placement is in
[Build sites](build-sites.md#placing-a-building).

**Emulated.** `tests/OpenSettlers.Tests/construction-builder.txt` holds checkpoints of two games of the harness verb `construct:`
(`tools/GhidraDump/README.md`): the castle's builder walks out to a forester's site, takes the two planks as they reach it, builds it in 16
blows, and goes home; the finished forester asks for its worker, and a settler without a job walks out to it. The second game places a woodcutter's
site beside it, which a settler without a job and a tool from the stock builds. `ConstructionBuilderTests` runs the engine through the same
commands: the builder's and the worker's 16 bytes, the site's 18 bytes, the flag's request bytes, and the generator match at each checkpoint, which
tests the number of blows, their draws, and their ticks. The goods reach the site at the passes the original delivers them (the engine's
hand-out lays a good on the castle's flag at once, [Materials](construction-materials.md#stand-ins-and-what-is-not-ported)), which the fixture
lists and the test adds to the site's fill.

## The request for a builder

**Status:** confirmed, and emulated.

A building under construction goes to `0x42d55f` in the building pass (a large type through `0x42d328` first,
[Materials](construction-materials.md#the-building-passs-turn-of-a-site)). Where the site's status has none of bits 2, 6, and 7 (no failed
request, no worker inside or on the way), the routine sets the progress word to 1 and asks the castle for job 3 with the tool in the stock word at `+4 +
0x22` of the storage item (`0x42d5fc`, through `Building_RequestWorker`, `0x42be63`). A request that finds no worker sets bit 2, the building's
request-failed bit, and the retry scan of the main pass (`0x428728`, [Timing](timing.md)) clears it a few passes later: the site then asks again.
A site that holds its materials and has no builder inside does not progress: nothing but the builder's blows changes its progress word.

## The worker request

**Status:** confirmed for the search and the second way, emulated for both.

`Building_RequestWorker` (`0x42be63`) takes the job and two tool words (the second is 0 in every handler of the program). The search runs over the flags from the
building's flag, as [Settlers](settlers.md#the-request) describes. At the castle's flag it first takes the settler that the castle's list of waiting
settlers (the word at `0x42` plus twice the job) holds for the job: he leaves the castle (state 15), the building takes bit 7 (a worker on the way),
and the list loses him. Where the list holds none, a second way makes one: the list's entry of the jobs without a job (job 21, word `0x6c`) must hold a
settler, and the stock word of the tool (the word at `+4` plus the tool word) must not be 0 (a job with no tool, the forester's, needs none). The settler
takes the job in bits 2 to 6 of his first byte, the stock loses a tool, and he leaves the castle as a settler of the first way does. With no settler of the job and
no settler without a job, or no tool, the request fails and the building's handler sets bit 2.

The handlers ask each for one job and tool, read from their instructions by `ProgramTablesImporter` (`RuleTables.WorkerOf`, `BuilderRequest`):

| Asker | Handler (the request) | Job | Tool good (word / 2) |
| --- | --- | --- | --- |
| Building under construction | `0x42d5fc` | 3 builder | 17 (`0x22`) |
| Woodcutter's hut (type 2) | `0x42d952` | 5 woodcutter | 21 (`0x2a`) |
| Stonecutter's hut (type 4) | `0x42d994` | 7 | 23 (`0x2e`) |
| Forester's hut (type 9) | `0x42d9d6` | 8 | none |
| Sawmill (type 0x11) | `0x42da18` | 6 | 22 (`0x2c`) |

The castle's 20 first settlers hold one builder (job 3), one woodcutter (5), one of job 6, and one of job 7, none of job 8, and five without a job. **Emulated:** a
second site placed at once gets a settler without a job and a hammer (the stock holds 21 at level 25; the word falls by one), and a finished forester gets a settler
without a job. The names of the tool goods are **inferred** from the job that asks for each (INV-0006); the engine names the builder's tool `Good 17` in the
fixture and names none of the others.

**Emulated:** with no hammer in the stock, the second site's request fails at each of its turns: bit 2 is set (the sign flag the routine leaves,
`0x42d61a`), the retry scan clears it the next pass, and the site asks again 33 passes later; once a hammer is in the stock the next request makes a
settler without a job the builder (`construction-builder.txt`, `builder-no-hammer`). **Emulated:** with five roads whose carriers take the five
settlers without a job, a second site's request fails at each of its turns in the same way, also at a turn where the castle's list of settlers without a
job is empty because the next one has not entered it yet; the castle's goods still go out to both sites (`builder-no-jobless`).
**Not ported:** the second tool word, the storage's counter of settlers (the word at `0x40`), and the player's counts per job.

## The builder's turn

**Status:** confirmed, and emulated.

**The start (`0x43ce20`, entry 3 of the job start table `0x43ca54`).** Where the builder has stepped through the site's door (state 4,
[Settlers](settlers.md#the-walk)), he takes state 9 with the frame `0x62` (`0x64` for the types 10, 17, 19, and 22: the mask `0x4a0400` tests the
type), the timer `0x7f`, the site's slot in the word at 12, byte 11 set to 1, and the count of the materials taken (the low nibble of byte 14) 0. He stays on the
node. The site's status bit 6 (a worker inside) was set when he entered ([Settlers](settlers.md#the-walk)), which makes the planks' rank `0xfe`.

**State 9 (`0x43ec82`).** The units since his last turn come off his timer, as in every waiting state; once it has run out he works until it is positive again:

- With byte 11 not negative he looks for the next material. The order word of the type (`0x43f0ba`, one 16-bit word for each type) says whether the
  material of turn *n* is a plank (bit *n* clear) or a stone (set). One on site (the high nibble of the fill byte 8 for the planks, 9 for the stones)
  goes out of the fill (minus `0x10`) and out of the count the site still needs (the bytes at 16 and 17); the count of the taken materials goes up, byte 15
  (the blows left) is 8, byte 11 is `0xff`. With none on site the timer gains `0x100`, or becomes `0xff` where that would not carry, and he waits; he asks again at each turn.
- With byte 11 negative he makes a blow: the step of the type (`0x43f0fa`, two words for each type, the first while the progress word is below `0x8000` and the second
  from there on) is added to the progress word. A carry out of the word finishes the building. Else the blows left fall by one; at 0 he looks for the next material
  at once.
- After a material taken and after a blow he draws once from the generator (`0x44257a`, the settler's generator), takes the frame `0x66` plus the draw's low two bits (plus 4 for the
  second figure), and adds the time of that frame's step (the step time table `0x43f248`) to the timer; while that does not run it over he goes on.

**Emulated:** the forester's 16 blows take about 100 passes each in a game without a view (a settler's turn comes every 16 or 17 passes), and the first material is taken at
the first turn after the first plank is on site (the plank is on site at the pass 481 and taken at the pass 491).
The tables hold, for the four types, the order words 0 (woodcutter, stonecutter, forester: all planks) and `0x000c` (sawmill: the third and fourth materials are
stones), and the steps `0x1000` and `0x1000` (woodcutter, stonecutter, forester: 16 blows) and `0x0800` and `0x0556` (sawmill: 16 blows before the word's top bit, 24 after).

**The end (`0x43ef20`).** The progress word is 0, status bit 6 is off, the word at 10 (the worker's slot) is 0, bit 7 of the type byte (the construction mark) is off,
and the item's word at 14 takes the pointer to the flag item (the engine keeps no such pointer). The site's flag asks for nothing (the class bytes at `0x42` and
`0x44` are 0; the ranks stay). The player's counts of the type move (one less building site, one more building; `0x43f053` to `0x43f07b`) and a counter at `+0x116` of the player
grows by a byte of the table at `+0xdc` of the game state: **not ported**, as the player's other counts are not ([Removal](removal.md)). For the types 0xb, 0x15, and 0x16 (the
buildings of a mission's start) the routine calls `0x45f402`: **not ported**. The builder's byte 11 becomes `0xfe`, bytes 14 and 15 become 0 and 2, his timer 0, and he takes state `0x4a`.

**State 0x4a (`0x43e008`).** When the node south-east of him (the flag's) holds no settler he takes state 7 and falls into the step onto the flag
(`0x43e036`, [Woodcutting](woodcutting.md#the-start-at-the-hut)) in the same turn; state 7 waits out the step and takes state 2 with mode `0xfe`, the walk to the storage. The
walk and the entry into the castle are the walk home's ([Settlers](settlers.md#the-walk)): job 3's start routine begins with the test of mode `0xfe`, which sends him
into the storage (`0x43cd06`) as every job's but the jobs 4 and 21 does. **Emulated:** the builder is in the castle's list again 165 passes after the building stands.
**Emulated** (`construction-builder.txt`, `builder-waits-at-the-door`): where the carrier of the flag's road stands on the flag for a good at the end, the
builder keeps state `0x4a` at the building's node at each of his turns, and the first turn after the carrier has left steps him onto the flag.

## The finished building asks for its worker

A building whose construction mark has gone goes to the handler of its type in the next building pass (`0x42cd1d`), which asks for its worker as above. **Emulated:** the forester's request
is made at the first building turn after the end (19 passes after it), by a settler without a job who walks out and goes in. The work of the types is in their own notes
([Stonecutting](stonecutting.md), [Forestry](forestry.md)); a worker whose job has no start routine in the engine (the sawmill's) stays in state 4 at the door.

## The job numbers

The job table of the program has the builder as job 3 (the request above), the digger as job 2 (`0x42d328`, tool good 16, [The digger](digging.md)), and the castle's own first settler as job 4: the settler that stands by while the castle is built
(state 10, `0x43f17a`) and carries goods out of the castle. No building asks for job 4. The engine's `Job.Digger` is job 2, `Job.Builder` job 3, and `Job.CastleSettler` job 4.

## Drawing a site

**Status:** confirmed from the code of the draw routine for a building under construction (`0x44fa79`); **emulated:** `site-draws.txt` holds draw runs of
the building draw (`0x44e329`) on the forester's site and the sawmill's site through their progress words, and `SiteDrawTests` compares the engine's layers
with them. The images
are drawn through the sprite routines of the platform layer (`0x44da4d`, `Gfx_DrawSpriteGrowing` `0x406350`).

The routine takes the building's type as its index `k` into two byte tables of object numbers, the scaffold's (`0x44fcfe`: 186 for the types 2, 4, and 9, 181 for the type 0x11) and the finished buildings' (`0x44fd16`: 168, 169, 170, and 160), and
reads the progress word `p`:

- `p` is 0: the object 144 (a large site that has not started), and nothing else.
- `p` is below `0x8000`: the piles of goods on site, then the object 145 (the base), then, where `2p` (16 bits) is 3 or more, the scaffold's object grown by `2p`.
- `p` is `0x8000` or more: the piles, the whole scaffold, then the finished building's object grown by `2p - 0x10000` where that is not 0.

A grown sprite shows its bottom rows only: the top of the clip lies `fraction * height / 65536` rows above the bottom row (the program takes the sprite's height from its header), so `fraction * height >> 16`
plus 1 rows show. A sprite's shadow is drawn whole. The engine draws the layers through `SiteStages` under both sources: the History Edition
takes the entries of the objects plus the 1250 (`ObjectSprites.SiteLayerEntry`) and the content set has a scaffold image for the small types (`building/stage-small`) and for the large (`building/stage-large`).
**Not ported:** the piles of the planks and stones on site (the routine draws a good's sprite `0x44de9a` once for each good on site, planks and stones from the fill bytes' high nibbles, at 3 pixels apart).

**The builder's figure.** His figure handler (stub 3 of `0x43f744`, `0x43faf4`) adds `0x500` to the figure byte for a byte below `0x80` and `0x580` for one from `0x80` (the blows), and requests a sound at some of the work figures through `0x44fe13`
(the engine leaves sounds out); the word's high byte picks the row of figure bases ([Settlers](settlers.md#drawing-the-walk-leads)). `RuleTables.BuilderFigureAdds` holds both adds, read from `0x43fb60` and `0x43fb55`.
The record of a settler is made where the node holds his slot, so the builder inside the building is drawn at its node with the frame of his state: `SettlerFigures` draws the activities `Builds` and `LeavesSite` for job 3.
**Confirmed** (draw run, the `builder` recording of `settler-draws.txt` on a `construct:` game): the builder walking out, building with
both adds, and walking home, drawn as the engine draws him. The `builder-waits` recording holds him in state `0x4a` at the building's node while a
carrier stands on the flag, and his step onto it, drawn as the engine draws him.

## In the engine

`BuilderBehavior` is the start routine, state 9, the end, and state 0x4a; `WoodcutterBehavior.RegisterStart` is the table of the job start routines (`0x43ca54`) the door's step runs. `SitePass.Turn` asks for the builder
(`CastleRoster.AskForWorker`), and `CastleRoster.RequestWorker` is `0x42be63` with its second way (`CastleStock.Take` takes the tool). `RuleTables.WorkerOf`, `BuilderRequest`, `StoneAt`, `BlowStep`, and `BuilderFigureAdds` hold the tables as typed values;
`Builds` and `LeavesSite` are the activities of the states 9 and `0x4a`, keeping their codes for the exporters. The tests on made-up tables are `WorkerRequestTests` and `BuilderTests`.

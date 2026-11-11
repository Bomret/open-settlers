# Materials for a building site

How a building site asks for the planks and stones its type needs, how the castle hands them out of its stock, how a good
that lies on a flag finds the site that asks for it, and how a delivery counts into the site. Addresses and routine names are in
the [Program index](program-index.md#construction); the placement that writes the site is in [Build sites](build-sites.md#placing-a-building);
the flag's turn that gives a good its destination is in [Goods at a flag](goods.md#the-turn-of-a-flags-goods).

**Emulated.** The harness verb `construct:` (`tools/GhidraDump/emulate.py`) is `remove:` with the phase routine `0x42965c` run at the
last phase of each cycle, as `Sim_StepPhase` (`0x4291c2`) runs it while the clock runs, and with the player's default words
(`0x445752`) written at the game's start. The other verbs skip both: the castle hands nothing out in them, and a site's request for
planks has the rank 0. The click `l@x,y,H` of the verb sets the heights of a node and its six neighbours (the ground a digger leaves
a large site). `tests/OpenSettlers.Tests/construction-materials.txt` holds the request words, the goods on site and on the way, the
castle's stock, and the generator after the passes of three games, and the engine matches them (`ConstructionMaterialsTests`). In the game
`two-sites-carried` the sites are placed at the pass 400, when both roads hold their carriers, and both requests are reachable at the
first hand-out: the forester's and the sawmill's requests rank alike (`0x3e`), and the castle hands that cycle's plank and stone to the
sawmill's flag, not to the forester's: both flags lie on the first level of the search, and of equal ranks the flag met first (the
directions are tried from 5 down to 0) keeps the lead.

## The request words

**Status:** confirmed.

A flag item holds two request words in the bytes `0x42` to `0x45`: a byte of classes at `0x42` with its rank at `0x43`, and a byte of
classes at `0x44` with its rank at `0x45`. Bit k of a class byte means the flag asks for the goods of class k. `Building_Place`
(`0x449a63`) writes `0x02` and `0x00` (class 1, rank 0) to the first word and `0x10` and `0x00` (class 4, rank 0) to the second: a
site's flag asks for planks in the first word and for stones in the second. The castle's flag holds `0xc0` and `0x80` (classes 6 and 7
and class 7), and the storage search reads bit 7 of the second word. A rank of 0 asks for nothing: every search takes only a rank above
the best it has, and the storage's hand-out only one above 15.

Taking the building down (`Building_Remove`, `0x4617fd`, [Removal](removal.md#taking-a-building-down)) clears the class bytes at `0x42` and
`0x44` of its flag (the ranks stay), so no search finds the flag afterwards and a plank in the castle's stock is not handed to a site that is
gone. The building pass no longer turns for a building that is taken down, so a rank above 15 that stood would otherwise stand for good.

A good's class and the word it is looked for in come from a table with two bytes for each good, `Good_DestinationClasses` (`0x464117`):
the plank (good 8) is class 1 of the first word, the stone (good 10) class 4 of the second, the log (good 7) class 5 of the second,
and a class byte with its high bit set (the good 9 and the goods from 17 on) sends the good to storage at once. The goods 8 and 10
are named from this evidence and from the stock: the hand-out that serves a request of class 1 takes the castle's stock word of the good
8 (**emulated:** 53 to 52 at the first hand-out), and the stonecutter's put at `0x438182` lays the good 10.

## The building pass's turn of a site

**Status:** confirmed for the rank, the levelling gate, and what the emulator shows of both.

The building pass sends a building under construction to `0x42d55f`, a large type first to `0x42d328`. For a large site whose
progress word is 0 the routine returns at once where a settler stands inside it or on the way to it (status bits 6 and 7), and
otherwise tests the ground: the node and its six neighbours must all lie at the height the site was given (the word at 14 of the
item). Only then does the progress word become 1, and the routine goes on. **Emulated:** the sawmill of the fixture `two-sites` has
the progress word 0 and rank 0 until the harness levels its ground; the same pass then sets 1 and its requests take their ranks. No
node of the generated map near the castle is level, so a large site waits for a digger ([The digger](digging.md#the-large-site-waits-for-the-ground)), who
levels the seven nodes and sets the progress word to 1 himself ([The digger](digging.md#the-end)).

Then for each good, the planks first and then the stones, the pass keeps the rank of the flag's request from the fill byte of the site
(byte 8 for the planks, byte 9 for the stones): its high nibble counts the goods on site, its low nibble the goods on the way. The
goods held are their sum. The rank is 0 where the site holds 8 or more or exactly the number it needs (the bytes at 16 and 17 of the
item). Otherwise the rank starts from a byte, shifted right by the goods held, shifted right by 2 more unless status bit 6 is set (a
builder inside), with bit 0 cleared. The start for the stones is `0xff`. The start for the planks is the high byte of the player word at
`+0x148`, which the player setup of a new game writes (`Player_SetDefaults2`, `0x445752`, called by `0x42055e`): `0xffdc`, so also
`0xff`. **Emulated:** a forester site with nothing held and no builder has the rank `0x3e`, one that holds one good `0x1e`, with the builder
inside `0xfe`.
A site never asks for more than it needs, and with no builder it stops asking at the second good (`0x0e` is below the 15 the hand-out
requires).

The same routine asks for the builder ([The builder](construction.md#the-request-for-a-builder)). **Not ported:** the branch of a player whose player-block byte `+0x163` has bit 1 set, which takes the site down at random (the computer opponent, an epic
non-goal).

## The request search

**Status:** confirmed for the search, emulated for the rank rule and the order.

A good on a flag with no destination whose class the table names looks for a request before it goes to storage (`0x464364` to
`0x46444c`, then `0x4644ab` to `0x464a3f`). The search starts at the good's flag and marks the flags it reaches, level by level, over the
roads that hold a carrier (the directions from north to east). The start flag is not looked at. A reached flag whose request word
asks for the class and whose rank is above the best so far becomes the best. After a level in which some flag was best, the best rank
grows by a quarter and one (`0x464906`); the search ends where the sum carries out of the byte or no flag is left. A farther request
therefore has to rank above what the nearer has grown to. With no request found the storage search follows. A found flag is the
good's destination; its rank becomes 0 (its rank shifted right by one where bit 0 is set, which the building pass never leaves set), and
the building of the flag counts one more good on the way in the fill byte of the word (byte 8 for the first, byte 9 for the second).

A good that gives up its destination (the flag cannot be reached) releases the request (`Good_ReleaseRequest`, `0x462ca4`): the building
at the destination flag counts one good less on the way, in the byte the good's kind names (`Good_Kinds`, `0x462d6d`), unless it is a
castle or a storehouse.

## The delivery counts into the site

**Status:** confirmed.

The carrier's handover at the building's flag (`Settler_DeliverGood`, `0x43c4ea`, [Goods at a flag](goods.md#the-delivery-into-the-castle))
adds `0x0f` to the fill byte of the good's kind: one more on site (the high nibble) and one less on the way (the low nibble). A site
asks for at most 8 goods, so the byte never carries and the castle's stock is not touched.

## The castle's hand-out

**Status:** confirmed for the routine and its order; the carrying out of the good is read from the emulator's runs only.

Once in each phase cycle (the pass at the last phase, 32), `0x42965c` runs, where the clock runs (`+0x1fe` is not 0). It first calls three
routines that touch the player's statistics only (`0x42a3f1`, `0x42a74a`, `0x42abac`), then draws once from the random generator
(`Rng_Step`, `0x466ade`). The draw taken to 0 to 7 picks one of three orders of twelve steps: the second table (`0x42a35d`) at 0, the
third (`0x42a3a7`) at 1, the first (`0x42a313`) at any other value. A step has three words: the offset of a request word (`0x42` or `0x44`),
a class, and the offset of a stock word from the first, which is the good that offset names (`good = offset / 2 + 1`), or -1 for the food
step (the goods 1, 3, and 6). The first order begins with the planks (class 1 of the first word, stock offset 14) and the stones (class 4
of the second, offset 18) and holds the logs (class 5 of the second, offset 12) fifth.

For each step, for each player, the routine lists the storages of the player whose stock holds the step's good (for the food step: any
of the three) and whose second outstanding slot is free, then searches from the flags of all listed storages at once, level by level over
the roads that hold a carrier, for the flag whose request word asks for the step's class with a rank above 15. Each storage keeps the
best rank it found, and after each level (that reached a new flag) the best rank of each storage grows by a quarter and one, to 255; a
storage whose best rank is 255 searches no further. Then each storage with a best rank hands out: the flag's rank becomes 0, the
flag's building counts one more good on the way, the stock word of the good falls by one, and the good, with the destination, goes
into the storage's first free outstanding slot (bytes `0x3a` and `0x3b` for the good, words `0x3c` and `0x3e` for the destination). **Emulated:**
at the first cycle after the sites' placement the castle of the fixture `two-sites` hands the sawmill a plank and a stone (one for each
step), its stock falls by one of each, and the forester, whose road holds no carrier yet, gets nothing.

The storage's first settler (the castle's job-4 settler, once the castle is built) carries the good out: when no settler is leaving the
castle (the count at `+0x4a` of the storage item is 0) the routine puts the settler into state 11 with the good and the destination in its
item, and otherwise the settler's state 12 takes the good at one of its turns. It walks out of the door, lays the good on the castle's
flag, and walks back in. **Emulated:** about 130 passes pass from the hand-out to the lay in the games of the fixtures, and while the two
outstanding slots are full the castle hands nothing more out.

## Stand-ins and what is not ported

- **Stand-in:** the engine lays the good on the castle's flag at once, on its first empty place, with the destination, and hands nothing out
  where the flag has none. It keeps no outstanding slots and runs no settler of the castle, so it hands out where the original's slots are
  still full. `ConstructionMaterialsTests` therefore stops at the passes before the first lay. The builder reaches the forester site at the pass
  211 in the engine as in the original (a rank with the builder inside is `0xfe`, not `0x3e`); `ConstructionBuilderTests` empties the castle of planks and
  stones and adds the goods to the site's fill at the passes the original delivered them ([The builder](construction.md)).
- The engine runs the phase routine only where a game asks for it (`GameFactory.Create(..., handsOutGoods: true)`, which the game view
  passes): the routine draws from the generator once in every cycle, which the emulator verbs other than `construct:` do not, so the
  scenarios that compare a game with those verbs leave it off.
- **Not ported:** the special branch for a storage with bit 1 of its second byte set, the three routines of the statistics, and the
  storehouse (the castle is the only storage).

## In the engine

`RuleTables.ClassOfGood`, `RuleTables.DistributionOrder`, and `RuleTables.PlankRankBase` hold the program's tables as typed values
(`ProgramTablesImporter` reads `0x464117`, the three tables at `0x42a313`, and the word of `0x445752`, and turns the class bytes, the
request word offsets, and the stock offsets into a `GoodClass`, a `DistributionStep` with its goods, and a rank). `Flag.RequestOf` and
`Request` are the request words: the class a word asks for is a `GoodClass`, which a site's placement takes from the table's rows of the
plank and the stone. `FlagRequestSearch` is the level-by-level search both the good's request search and the hand-out run.
`SitePass` is the building pass's turn of a site. `GoodsTransport.Destine` runs the request search and the storage search,
`GoodsTransport.ReleaseRequest` releases a request. `StorageDistribution` is the phase routine's hand-out; `MainPass` runs it at the last
phase. `CastleStock.Count`, `Take`, and `Add` count a good in the entry `good - 1` of the stock list (the storage word of the good g lies at
`4 + 2 * g`, so the list, which starts at the word at 6, is one entry behind the good number), and `Good.Log`, `Good.Plank`, and `Good.Stone`
name the goods.

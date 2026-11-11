# Materials for a building site

This note covers how a building site asks for the planks and stones its type needs. It also covers how the castle hands them out of its stock, how a good on a flag finds the site that asks for it, and how a delivery counts into the site. Addresses and routine names are in the [Program index](program-index.md#construction). The placement that writes the site is in [Build sites](build-sites.md#placing-a-building). The flag's turn that gives a good its destination is in [Goods at a flag](goods.md#the-turn-of-a-flags-goods).

**Emulated.** The harness verb `construct:` (`tools/GhidraDump/emulate.py`) is `remove:` with two additions. It runs the phase routine `0x42965c` at the last phase of each cycle, as `Sim_StepPhase` (`0x4291c2`) runs it while the clock runs. It also writes the player's default words (`0x445752`) at the game's start. The other verbs do neither. In them the castle hands nothing out, and a site's request for planks has the rank 0.

The click `l@x,y,H` of the verb sets the heights of a node and its six neighbours. That is the ground a digger leaves a large site.

The file `tests/OpenSettlers.Tests/construction-materials.txt` holds the request words, the goods on site and on the way, the castle's stock, and the generator. It records them after the passes of three games, and the engine matches them (`ConstructionMaterialsTests`).

In the game `two-sites-carried` both sites are placed at pass 400, when both roads hold their carriers. Both requests are reachable at the first hand-out. The forester's and the sawmill's requests rank alike (`0x3e`). The castle hands that cycle's plank and stone to the sawmill's flag, not to the forester's. Both flags lie on the first level of the search. Of equal ranks, the flag met first keeps the lead, because the directions are tried from 5 down to 0.

## The request words

**Status:** confirmed.

A flag item holds two request words in the bytes `0x42` to `0x45`. Byte `0x42` holds classes and byte `0x43` holds their rank. Byte `0x44` holds classes and byte `0x45` holds their rank. Bit k of a class byte means the flag asks for the goods of class k.

`Building_Place` (`0x449a63`) writes `0x02` and `0x00` to the first word, which is class 1 with rank 0. It writes `0x10` and `0x00` to the second word, which is class 4 with rank 0. So a site's flag asks for planks in the first word and for stones in the second.

The castle's flag holds `0xc0` and `0x80`. These are classes 6 and 7, and class 7. The storage search reads bit 7 of the second word.

A rank of 0 asks for nothing. Every search takes only a rank above the best it has. The storage's hand-out takes only a rank above 15.

`Building_Remove` (`0x4617fd`, [Removal](removal.md#taking-a-building-down)) clears the class bytes at `0x42` and `0x44` of the building's flag. The ranks stay. So no search finds the flag afterwards, and the castle's stock hands no plank to a site that is gone. The building pass does not turn for a building that is taken down. A rank above 15 that stood would otherwise stand for good.

The table `Good_DestinationClasses` (`0x464117`) holds two bytes for each good. They give a good's class and the word it is looked for in.

- The plank (good 8) is class 1 of the first word.
- The stone (good 10) is class 4 of the second word.
- The log (good 7) is class 5 of the second word.
- A class byte with its high bit set sends the good to storage at once. Good 9 and the goods from 17 on have that bit.

The names of goods 8 and 10 rest on two facts. The hand-out that serves a request of class 1 takes the castle's stock word of good 8. **Emulated:** that word goes from 53 to 52 at the first hand-out. The stonecutter's put at `0x438182` lays good 10.

## The building pass's turn of a site

**Status:** confirmed for the rank, the levelling gate, and what the emulator shows of both.

The building pass sends a building under construction to `0x42d55f`. A large type goes first to `0x42d328`.

For a large site, the routine returns at once where its progress word is 0 and a settler stands inside it or on the way to it. Those are status bits 6 and 7. Otherwise it tests the ground. The node and its six neighbours must all lie at the height the site was given, which is the word at 14 of the item. Only then does the progress word become 1, and the routine goes on.

**Emulated:** the sawmill of the fixture `two-sites` has progress word 0 and rank 0 until the harness levels its ground. The same pass then sets 1, and its requests take their ranks.

No node of the generated map near the castle is level. So a large site waits for a digger ([The digger](digging.md#the-large-site-waits-for-the-ground)). The digger levels the seven nodes and sets the progress word to 1 himself ([The digger](digging.md#the-end)).

Then the pass keeps the rank of the flag's request for each good, the planks first and then the stones. It reads the fill byte of the site, which is byte 8 for the planks and byte 9 for the stones. The high nibble counts the goods on site, and the low nibble counts the goods on the way. The goods held are their sum.

The rank is 0 where the site holds 8 or more goods, or exactly the number it needs. The needed numbers are the bytes at 16 and 17 of the item. Otherwise the rank starts from a byte. The pass shifts that byte right by the goods held. It shifts it right by 2 more unless status bit 6 is set, which means a builder is inside. It then clears bit 0.

The start byte for the stones is `0xff`. The start byte for the planks is the high byte of the player word at `+0x148`. The player setup of a new game writes that word (`Player_SetDefaults2`, `0x445752`, called by `0x42055e`). It is `0xffdc`, so the high byte is also `0xff`.

**Emulated:** a forester site with nothing held and no builder has the rank `0x3e`. A site that holds one good has `0x1e`. With the builder inside, the rank is `0xfe`. A site never asks for more than it needs. With no builder it stops asking at the second good, because `0x0e` is below the 15 that the hand-out requires.

The same routine asks for the builder ([The builder](construction.md#the-request-for-a-builder)).

**Not ported:** the branch of a player whose player-block byte `+0x163` has bit 1 set. That branch takes the site down at random. It belongs to the computer opponent, which is an epic non-goal.

## The request search

**Status:** confirmed for the search, emulated for the rank rule and the order.

A good on a flag with no destination looks for a request before it goes to storage, if the table names a class for it. The code is at `0x464364` to `0x46444c`, then `0x4644ab` to `0x464a3f`.

The search starts at the good's flag. It marks the flags it reaches, level by level, over the roads that hold a carrier. It tries the directions from north to east. The search does not look at the start flag.

A reached flag becomes the best so far where its request word asks for the class and its rank is above the best. After a level in which some flag was best, the best rank grows by a quarter and one (`0x464906`). The search ends where the sum carries out of the byte or no flag is left. So a farther request has to rank above what the nearer has grown to.

With no request found, the storage search follows.

A found flag is the good's destination. Its rank becomes 0. Where bit 0 is set, the rank is shifted right by one instead, but the building pass never leaves bit 0 set. The building of the flag counts one more good on the way in the fill byte of the word. That is byte 8 for the first word and byte 9 for the second.

A good can give up its destination when the flag cannot be reached. Then the good releases the request (`Good_ReleaseRequest`, `0x462ca4`). The building at the destination flag counts one good less on the way. It uses the byte that the good's kind names (`Good_Kinds`, `0x462d6d`). A castle or a storehouse does not count.

## The delivery counts into the site

**Status:** confirmed.

The carrier's handover at the building's flag is `Settler_DeliverGood` (`0x43c4ea`, [Goods at a flag](goods.md#the-delivery-into-the-castle)). It adds `0x0f` to the fill byte of the good's kind. That adds one good on site in the high nibble and removes one good on the way from the low nibble. A site asks for at most 8 goods, so the byte never carries and the castle's stock is not touched.

## The castle's hand-out

**Status:** confirmed for the routine and its order. The carrying out of the good is read from the emulator's runs only.

The routine `0x42965c` runs once in each phase cycle, at the pass of the last phase, 32. It runs only where the clock runs, which means `+0x1fe` is not 0.

It first calls three routines that touch the player's statistics only (`0x42a3f1`, `0x42a74a`, `0x42abac`). Then it draws once from the random generator (`Rng_Step`, `0x466ade`). The draw, taken to 0 to 7, picks one of three orders of twelve steps:

- At 0, the second table (`0x42a35d`).
- At 1, the third table (`0x42a3a7`).
- At any other value, the first table (`0x42a313`).

A step has three words. The first is the offset of a request word, `0x42` or `0x44`. The second is a class. The third is the offset of a stock word from the first, or -1 for the food step. The good that offset names is `offset / 2 + 1`. The food step covers goods 1, 3, and 6. The first order begins with the planks and the stones. The planks are class 1 of the first word with stock offset 14. The stones are class 4 of the second word with offset 18. The logs are class 5 of the second word with offset 12, and they come fifth.

For each step and each player, the routine lists the storages of the player. A listed storage holds the step's good in its stock, or any of the three food goods for the food step. Its second outstanding slot must be free.

Then the routine searches from the flags of all listed storages at once. It goes level by level over the roads that hold a carrier. It looks for the flag whose request word asks for the step's class with a rank above 15.

Each storage keeps the best rank it found. After each level that reached a new flag, the best rank of each storage grows by a quarter and one, up to 255. A storage whose best rank is 255 searches no further.

Then each storage with a best rank hands out:

- The flag's rank becomes 0.
- The flag's building counts one more good on the way.
- The stock word of the good falls by one.
- The good goes into the storage's first free outstanding slot, with the destination. The slot holds the good in bytes `0x3a` and `0x3b`, and the destination in words `0x3c` and `0x3e`.

**Emulated:** at the first cycle after the sites' placement, the castle of the fixture `two-sites` hands the sawmill a plank and a stone, one for each step. Its stock falls by one of each. The forester gets nothing, because its road holds no carrier yet.

The storage's first settler carries the good out. That is the castle's job-4 settler, once the castle is built. If no settler is leaving the castle, the count at `+0x4a` of the storage item is 0. Then the routine puts the settler into state 11 with the good and the destination in its item. Otherwise the settler's state 12 takes the good at one of its turns. The settler walks out of the door, lays the good on the castle's flag, and walks back in.

**Emulated:** about 130 passes pass from the hand-out to the lay in the games of the fixtures. While the two outstanding slots are full, the castle hands nothing more out.

## Stand-ins and what is not ported

- **Stand-in:** the engine lays the good on the castle's flag at once, on its first empty place, with the destination. It hands nothing out where the flag has no empty place. It keeps no outstanding slots and runs no settler of the castle. So it hands out where the original's slots are still full. `ConstructionMaterialsTests` therefore stops at the passes before the first lay.
- The builder reaches the forester site at pass 211 in the engine, as in the original. A rank with the builder inside is `0xfe`, not `0x3e`. `ConstructionBuilderTests` empties the castle of planks and stones. It adds the goods to the site's fill at the passes the original delivered them ([The builder](construction.md)).
- The engine runs the phase routine only where a game asks for it, with `GameFactory.Create(..., handsOutGoods: true)`. The game view passes that value. The routine draws from the generator once in every cycle. The emulator verbs other than `construct:` do not draw. So the scenarios that compare a game with those verbs leave the routine off.
- **Not ported:** the special branch for a storage with bit 1 of its second byte set, the three routines of the statistics, and the storehouse. The castle is the only storage.

## In the engine

`RuleTables.ClassOfGood`, `RuleTables.DistributionOrder`, and `RuleTables.PlankRankBase` hold the program's tables as typed values. `ProgramTablesImporter` reads `0x464117`, the three tables at `0x42a313`, and the word of `0x445752`. It turns the class bytes, the request word offsets, and the stock offsets into a `GoodClass`, a `DistributionStep` with its goods, and a rank.

`Flag.RequestOf` and `Request` are the request words. The class a word asks for is a `GoodClass`. A site's placement takes it from the table's rows of the plank and the stone.

`FlagRequestSearch` is the level-by-level search. Both the good's request search and the hand-out run it. `SitePass` is the building pass's turn of a site. `GoodsTransport.Destine` runs the request search and the storage search. `GoodsTransport.ReleaseRequest` releases a request. `StorageDistribution` is the phase routine's hand-out, and `MainPass` runs it at the last phase.

`CastleStock.Count`, `Take`, and `Add` count a good in the entry `good - 1` of the stock list. The storage word of good g lies at `4 + 2 * g`. The list starts at the word at 6, so it is one entry behind the good number. `Good.Log`, `Good.Plank`, and `Good.Stone` name the goods.

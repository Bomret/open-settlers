# New settlers

How the program makes settlers after the castle's 20 first settlers. A timer of each player makes a settler without a
job in one of the player's storages. Some of these settlers become knights where the storage holds a sword and a
shield. A cap per player stops the timer's settlers when the player holds its share of the settler table. Addresses and
routine names are in the [Program index](program-index.md#simulation); the first settlers are in
[Castle](castle.md); the clock is in [Timing](timing.md); the waiting list of a storage and the requests that take
settlers from it are in [Settlers](settlers.md#the-request) and [Carriers](carriers.md#the-request).

**Emulated.** The harness verb `supply` (`tools/GhidraDump/README.md`, "Emulator harness") runs `Player_MakeSettlers`
in a castle game with one storage, the castle. `tests/OpenSettlers.Tests/settler-supply.txt` holds its games at rate 0 and
rate 40, each with a stock that holds swords and shields and one that holds none. The recording shows the period, the
knights, and the knights owed below. It does not show the pick among several storages or the cap at work, because one
player with one storage never reaches the cap.

## The settler timer

**Status:** confirmed.

`Player_MakeSettlers` (`0x428a75`) runs for each of the four player blocks in every pass, from `0x4286d8`, the first
routine of the pass, after the object pass. It acts on a player block whose byte 2 has bit 6 (the player is in the
game) and bit 0 (the castle stands, which `Castle_Place` sets):

1. It takes the clock units since its last turn: the clock (`+0x206`) less the word at `+0x11e`, and stores the clock
   in `+0x11e`. `Castle_Place` stores the clock there after it creates the first settlers (`0x442e4c`), so the count
   starts when the castle is placed.
2. It takes those units off the countdown at `+0x120`. While the countdown does not go below zero, it does nothing more.
3. When it goes below zero, the routine makes one settler (below) and adds the period at `+0x122` to the countdown. It
   repeats this until the countdown is no longer below zero. So it makes one settler for each period that has passed,
   and the remainder carries into the next period. All of this is 16-bit arithmetic.

A settler that cannot be made (below) is not made later: its period passes all the same.

**The period** (`0x42055e`, at the start of a game): (60 − r) × 50 clock units, where r is the signed byte 3 of the
player's start parameters (`Game_StartParams`, `+0x1d6 + 4 × player`). The countdown starts at one period.

- A free game: `Game_SetStartParams` (`0x468717`) takes r of player 0 from the free-game setting byte at `+0x37c`
  (and of player 1 from `+0x37d` in a game of two people). Start-screen action 15 (`0x4690bb`) points at `+0x37c`
  and goes on to the bar code at `0x46919a`, which sets a value from 0 at the bar's bottom to 40 at its top
  ([Castle](castle.md#the-start-stock) describes the same code for the start stock). So the period is 3000 clock units
  at r = 0 and 1000 at r = 40: 375 and 125 passes of 8 units. **Emulated:** with the castle placed at clock 0, the first
  new settler is made in the pass that moves the clock to 3008 at r = 0 and to 1008 at r = 40, the first pass in which the
  countdown goes below zero, and one more follows every 375 or 125 passes.
- A mission: r is byte `0xf` of the mission's record.

**Where the free game's rate comes from.** `FreeGame_RandomizeOpponents` (`0x4692d4`) draws the setting bytes whenever the
free-game screen opens, as for the start stock level ([Castle](castle.md#the-start-stock)): the 19th draw is the level of
player 0 at `+0x37a`, and the 20th draw, scaled to 0 to 40 the same way, is its rate at `+0x37c`. The player then sets the
rate on the screen's bar. **Emulated:** `opponents:S0,S1,S2` gave the rate 4 for the seed 1234,5678,9ABC, 7 for
FFFF,0001,8001, 16 for 3905,0577,7739, and 27 for 6359,A0D2,B4FA, which the engine's `StartStock.SettlerRateOf` reproduces.

## Making one settler

**Status:** confirmed.

`Settler_CreateInStorage` (`0x44333d`) makes nothing when any of these holds:

- Bit 2 of the player block's byte 3 is clear ([The cap](#the-cap)).
- The settler table has no free slot (the free count at `+0x48` is 0).
- No storage of the player qualifies (below).

Otherwise it takes the first free slot of the settler table (`Table16_Allocate`) and picks a storage. It goes through
the slots of the storage table (`Table120`) in use, from slot 0 up to the table's slot count (`+0x266`). A storage
qualifies when its byte 0 is the player's number, bit 2 of its byte 1 is clear, and, in a knight try, its stock holds at
least one of good 25 and one of good 26. Of the storages that qualify, it takes the first whose word `+0x40` is 0, else
the one whose word `+0x40` is lowest, the first of equals. **Inferred:** word `+0x40` counts the settlers without a job
in the storage. This routine adds 1 to it, and the change into a knight and `Castle_CreateSettlers` take 1 from it for
each settler that takes a job.

In a knight try where no storage qualifies, the routine drops the condition of the weapons and goes through the storages
again. Where none qualifies then, it frees the slot again.

The new settler is a settler of job 21, without a job, of the player. It stands on the node of the storage's building
(the building's slot is word `+4` of the storage item). Its frame, timer, mode, and target are 0. Its stamp is the clock,
its state 1 (waiting in a storage), and its byte 14 the storage's slot. The storage's word `+0x40` and the player's word
`-0x10` each grow by 1.

At its turn in state 1 the settler enters the storage's list of waiting settlers ([Settlers](settlers.md#the-walk)).
There a worker request or a carrier request takes it as the settler without a job.

## Knights

**Status:** confirmed.

Before each settler, `Player_MakeSettlers` adds 20000 to the word at `+0x126`, which `0x42055e` starts at `0x8000`.
Where the sum carries, the word at `+0x10c`, the knights owed, grows by 1, to at most 2.

- With no knight owed, the routine makes a settler as above.
- With a knight owed, the try is a knight try (word `+0x10e` set to `0xffff`). Where a settler is made and its storage
  holds at least one of good 25 and one of good 26, the settler takes job 22: the player bits and bit 7 of byte 0
  stay. The knights owed fall by 1. The storage loses one of each good and 1 from its word `+0x40`. The player's word
  `-0x10` falls by 1, its word `-0xe` grows by 1, and its dword `+0x11a` grows by 1.

**Emulated:** from the start value `0x8000` the second and the fifth new settler carry, so each owes a knight. With
swords and shields in the castle, each of them is made a knight at once and the stock loses one of each. With none, the
knights owed grow to 2 and stay there, and every new settler is a settler without a job.

So 20000 of each 65536 settlers are owed as knights, about 31 %. An owed knight waits, up to two of them, until a storage
of the player holds the two goods. Goods 25 and 26 are the sword and the shield
([Food buildings](food-buildings.md#the-goods); which one is which is open). `Castle_CreateSettlers` turns three of the
first settlers into knights in the same way ([Castle](castle.md#what-the-placement-writes)).

## The cap

**Status:** confirmed for the arithmetic; **inferred** for what the counts are.

`Player_UpdateSettlerCaps` (`0x42a3f1`) runs once per phase cycle, from `Storage_HandOutGoods` at phase 32 while the
clock runs. It counts the players whose byte 2 has bit 6, n, and sums their dwords `+0x112`, A. Where the settler table
has fewer free slots (`+0x48`) than n, it changes no cap. Otherwise it shifts A right until it fits 16 bits, s steps, and
runs `Player_UpdateSettlerCap` (`0x42a567`) for each of those players:

1. a is the player's `+0x112` shifted right s steps. The share is K where a equals the shifted A, else
   ⌊⌊a × 65536 / A⌋ × K / 65536⌋, with K the word at `+0x4a`.
2. The cap is the share plus the word at `+0x58`, B.
3. Where the sum of the player's 27 words from `-0x3a` to `-6` is below the cap, the routine sets bit 2 of the player's
   byte 3; else it clears it.

**What the numbers are.** `Map_SetGeometry` (`0x42195c`) sets the free count at `+0x48` to C = (`Map_Width` / 32) ×
(`Map_Height` / 32) × 500, B to C / 8, and K to C − B × p, where p is the number of players whose byte 0 of the start
parameters is not 0. **Inferred:** C is the size of the settler table. So each player is sure of an eighth of the table,
and the players share the rest by their `+0x112`. That dword counts the nodes the player owns: `0x45eb4c`, a check of
`Territory_Update`, adds 1 to the player's count for a node that takes the player's owner bits and takes 1 from the
count of the player whose owner bits a node loses. **Inferred:** the 27 words count the player's settlers by kind. The
new settler counts in `-0x10`, a knight in `-0xe`, and the castle's first settler in `-0x3a`.

`Castle_CreateSettlers` sets bit 2 of byte 3 before it makes the first settlers, so they are made before any cap was
worked out.

**Slot 0.** `MapGen_InitParams` (`0x421530`) takes the settler table's first slot with `Table16_Allocate` at a game's start,
before any settler, and the allocator takes 1 off the free count `+0x48`. So the dummy in slot 0 counts against C: a game
holds at most C − 1 settlers. **Emulated:** on the 32 × 32 map the harness read the free count 500 after `Map_SetGeometry`,
(32 / 32) × (32 / 32) × 500. The maps of 64 × 32 and 64 × 64 nodes give 1000 and 2000. The table's slot count at `+0x25c`
is a second limit, which `0x41dbea` works out from the memory the program has; the free count is the limit the supply
checks.

The three dwords `+0x112`, `+0x116`, and `+0x11a` go back to 0 when they reach `0xffff0000` or more, at each turn of
`Player_MakeSettlers`, before its other checks.

## Open leads

- A harness game with several storages, to confirm the pick of the storage, and one that reaches the cap.
- The second writer of `+0x112` at `0x430661`, which lies outside every function of the dump.
- The order of the 27 counts of the player's settlers: the castle's first settler, of job 4, counts in the first word.
- Bit 2 of byte 1 of a storage item, which bars it from new settlers.
- The label of the free-game bar of action 15 on the screen.

## In the engine

`SettlerSupply` (`src/Application/SettlerSupply.cs`) holds the player's supply as typed state: the rate, the period, the
countdown, the clock of its last turn, the knight accumulator, and the knights owed. It names the original's constants with
their addresses. A game runs it where `GameFactory.Create` is given `settlerSupply` and a settler rate from 0 to 40, and refuses
any other rate. A game without it reads no rate, makes no new settler and digests as before.

- `PlaceCastleHandler` starts the supply at the castle's placement, with the clock of the placement. Before the placement the
  supply does not turn.
- `MainPass.Run` turns it after the objects' turn and before the phase step, as `0x4286d8` runs `Player_MakeSettlers`. The
  turn follows [The settler timer](#the-settler-timer) in 16-bit arithmetic, so the period holds across the clock's wrap.
- Each try feeds the knight accumulator, and makes a settler without a job in the next slot of `Game.Settlers`: on the
  castle's node, waiting in the castle (`WaitsInCastle`), stamped with the clock, with the history fill and a `settler-made`
  event. With a knight owed and a sword and a shield in `CastleStock`, it takes the knight's job 22 at once, the stock gives
  up one of each, and the debt falls by one.
- `SettlerTable.Capacity` is C for the game's map (`SettlerTable.SizeOf`): 500, 1000, or 2000. The engine's table has no
  dummy in slot 0, so it is full at C − 1 settlers (`SettlerTable.IsFull`), as the original's is. A try while the table is
  full makes nothing, and its period passes.
- `MainPass.Run` works out the cap at phase 32 of each cycle, whether or not the game hands out goods
  (`SettlerSupply.UpdateCap`): while the table has a free slot, the one player's cap is C (its share K plus the floor B),
  and the supply may make settlers while the table holds fewer. From `SettlerSupply.Start` until the first phase-32 update, it may make them.
  A try the cap bars makes nothing, and its period passes.
- The digest mixes the supply's state where the game runs it.
- The game view runs the supply in every game, its play runs included. Its rate is the launch flag `--rate`
  (`LaunchArgs.Rate`), which stands in for the free-game screen's bar, or else the rate the seed draws
  (`StartStock.SettlerRateOf`, `MapViewState.SettlerRate`). A layout's game takes the rate of the layout's seed. The play-run
  wrapper takes no rate, so a play run takes the seed's rate.
- `SettlerSupplyRecordingTests` matches `settler-supply.txt` pass by pass: the new settlers' bytes, the timer, the stock of
  goods 25 and 26, and the generator's words. `SettlerSupplyTests` holds the rules the recording does not reach.

Departures:

- One player and one storage, the castle. The pick of the storage with the fewest settlers without a job is not ported, and
  the castle always qualifies.
- The share of the cap by owned land among several players is not ported: one player's cap is the whole table, so the
  player's owned nodes (`+0x112`) are not counted, and the cap counts all settlers of the table where the original counts the
  player's 27 counts.
- The table's slot count from the program's memory (`+0x25c`) is not held.
- **Inferred:** a try that makes no settler, because the cap bars it or the table is full, makes no knight either, so an owed
  knight stays owed. The decompiled code of `0x428a75` leaves open what the knight test reads after such a try.
- The player's 27 counts of settlers by kind, its count of knights at `+0x11a`, the storage's word `+0x40`, and the resets
  of the player's counters at `0xffff0000` are not held.

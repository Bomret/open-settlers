# Goods at a flag

How a good that lies on a flag gets its destination and its direction, and how the carrier who waits next to the flag is told
to fetch it, how it lifts the good, swaps goods with each flag on its road, and delivers it into the castle. Addresses and
routine names are in the [Program index](program-index.md#simulation); the flag item's fields are in
[Item tables](item-tables.md); the carrier's walk to its road is in [Carriers](carriers.md); the woodcutter's put of a log is in
[Woodcutting](woodcutting.md#the-log).

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) is `wood:` in a game that starts as the original's game
start leaves its tables. `MapGen_InitParams` (`0x421530`) ends by taking the first slot of the flag table (`0x45d7ad`), of the
building table (`0x45da8f`), and of the settler table (`0x45e121`) for a dummy, so the first flag of a game is slot 1. The verb
`wood:` runs without the dummy: the castle's flag is slot 0 there, a good's destination word for it is 0, and 0 means none, so
in `wood:` runs a log never leaves its flag (the flag pass looks for the castle again at every turn). `goods:` takes the flag
slot's dummy only, the other tables keep the numbers the earlier verbs use. `goods:` and `remove:` also run
`Player_SetTransportPriorities` (`0x445570`) on the player block before the castle is placed, as `0x42055e` does at a new game,
so the [transport priorities](#transport-priorities) are in place; their start line `prio=` lists the 32 bytes the exchange reads
for the goods 0 to 31. `tests/OpenSettlers.Tests/flag-goods.txt` holds the flags' goods fields and the carriers' bytes after the
passes of the verb's games, and the engine matches them (`FlagGoodsTests`): a log laid by the woodcutter, a log laid by hand at
pass 5 before any carrier is on the road, and on a road of three flags logs and goods of different ranks laid by hand.

## The fields

**Status:** confirmed for the fields below.

- Byte 4 of a flag item, bit 7: the flag has goods that need a turn. A put sets it (`0x436b46`), the turn of the goods clears it,
  and a turn that could not finish sets it again, so a good that waits is looked at again at each phase cycle ([Timing](timing.md#rules-that-count-passes)).
- Bytes `0xc` to `0x13`: the 8 places. A place holds the good in its low 5 bits and, in bits 5 to 7, the direction the good was
  given plus one (0 for none yet).
- Bytes `0x14` to `0x23`: one word for each place, the slot of the flag the good goes to (0 for none).
- Bytes `0x3c` to `0x41`: one byte for each direction. Bit 7 means a good waits to go that way, bits 0 to 2 are its place, bits 3
  to 5 are the direction in which the road leaves the flag at its far end (written when the road is made, so a road that
  leaves its start flag in direction 3 and ends at a flag it leaves in direction 0 holds `0x00` at the first and `0x18` at the
  second).
- Bytes `0x42` to `0x45`: two request words with a priority byte after each (`0x42` and `0x43`, `0x44` and `0x45`); the castle's
  flag holds `0x00c0` and `0x0080`. Bit 7 of the word at `0x44` marks a flag with a storage. **Inferred** for the
  request words: the table below names them, no building asks for a good yet.

## The turn of a flag's goods

**Status:** confirmed for a log at a flag with one storage (the castle), for a good that waits with no carrier on the roads,
and for the push-out of a waiting good by one of a higher transport priority.

`0x46414d` takes, for each pass in the phases 0 to 31, the flags whose slot divided by 32 is the phase. For a flag in use (the
bitmap at `+0xa4` of the state) it first builds the load of its directions: for each place with a direction, bit `d` of byte
`k` of a 4-byte word is set when more than `k` goods head in direction `d` (`0x46427c`, kept at `+0x218` to `+0x21b`). With
bit 7 of byte 4 set, the turn clears it and goes through the places from the last to the first. A place with a good and no
direction does one of two things:

1. **No destination (word 0).** The table at `0x464117` (two bytes for each good: a class and the offset of a request word;
   `0xff` for a class marks a good that goes to storage at once) sends the good to the request search for a log (class 5, word
   at `0x44`): a search over the roads that hold a carrier for a flag whose request word has the class's bit set, and a
   priority above the best so far. When none is found, as long as no building requests a good, the storage search (`0x45d3ea`)
   runs from the flag: the flags are reached over the roads of which the status byte (byte 5, bit `d`) shows a carrier, from
   north to east, level by level; a flag whose byte `0x44` has bit 7 is the storage's, and the building's flag slot (item `+6`)
   is the answer. A flag that is a storage itself finds none. The answer goes into the place's destination word and the flag's
   bit 7 is set again, so the direction comes at the next turn. Where the search finds none, with no road of the flag holding a
   carrier (status bits 0 to 5 all clear), the bit is set again and the good waits: this is the good that lies at a flag until a
   carrier comes. **Not ported:** with some road of the flag holding a carrier and no storage reached, the original gives the
   good a direction at once (`0x464539`, from the leftover of a register).
2. **A destination.** The search for the direction (`0x464aa8`, `0x464bb2`, `0x464c36`) starts at the flag. It seeds the roads
   that hold a carrier by load: first those with no good heading that way, then those with exactly one, two, three, and last
   those with four or more (a seed marks the flag at the road's far end with the direction), as long as a road with a carrier is
   left. The seeds go into a queue in that order, directions from north to east within a load. Then, level by level, each flag in
   the queue marks the flags its carrier roads lead to with its own direction; when the destination is marked after a level, its
   mark is the good's direction `d`. A destination reached only through the flag itself (mark 6) or not at all gives the good up:
   the word is cleared (`0x462ca4` gives back a request, a storage counts none) and the bit set. With a direction `d`: where
   byte `0x3c + d` has no bit 7, it takes the place and bit 7. Where it has, the push-out (`0x46501a`) compares the
   [transport priorities](#transport-priorities) of the good in that place and the new one (the table of the player that byte 3
   of the flag names, read by each good's low 5 bits): only a strictly higher rank of the new good puts its place into the byte,
   which keeps bit 7 and bits 3 to 6. At an equal or lower rank the byte stays. The good's byte takes `d + 1` in bits 5 to 7
   either way, so a good that loses the place keeps its direction and waits on the flag until the exchange lines it up again.
   The word keeps the destination, and the bit stays clear.

The same pass then runs the road loop of [Carriers](carriers.md#the-flag-pass). With goods it also asks for a second carrier:
a road whose carriers are fewer than the table at `0x462acd` gives for its length class (1, 2, 3, 4, 6, 8, 11, 15 for the
classes 0 to 7, **inferred** to be the carriers the road wants) and with at least three goods heading that way asks `0x42b455`
(land) or `0x42b4bc` (water) again; a flag with 7 or more goods clears the road's carrier bit in the status for each road that has
three goods heading that way. **Not ported:** both, since they need 3 goods at one flag and the walk of a second carrier.

## The carrier told to fetch

**Status:** confirmed to the first turn in state 3.

The turns of state 0x42 (`0x42ff81`) and state 0x43 (`0x43001a`) are one routine in the original. A carrier in state 0x42
(next to a flag, on a road, `Mode` the direction the road leaves that flag, `Target` the flag) takes byte `0x3c + mode` of the
flag's item:

- **Bit 7 set** (a good waits here to go along this road): the carrier's heading (byte 14) is the low byte of its stamp plus 6,
  the heading it came in: it goes on toward the flag.
- **Else** the byte's bits 3 to 5 name the direction in which the road leaves the flag at its far end; byte `0x3c` plus that
  direction of that flag, with bit 7 set (a good waits at the far end), gives the heading the low byte of the stamp less 3,
  plus 6 where that went below 0: the carrier turns round. Where it has no bit 7 either, the carrier stays in state 0x42 and
  nothing changes.

It then takes state 0x43, which every turn does the rest: where the node the carrier stands on holds no settler (the word at
`+2` of the node's second record), the carrier takes the node, its stamp becomes the clock, its timer 0, its mode 0, its byte 15
0, and its state 3: at once, in the turn of state 0x42, where the node was free. Where a settler stands on it, the carrier
stays in state 0x43 and tries again at its next turn (16 passes later): in the second emulated game the woodcutter stands on
the node at the first turn (pass 169), and the carrier takes it at the next (185). The pointer to the flag's item that the
carrier holds in bytes 12 to 15 while it waits keeps its low word through state 0x43 and state 3 (the engine keeps the slot);
byte 14 is the heading and byte 15 is 0.

What follows in state 3 is below. The bytes `0x42fca1` to `0x42fef9` are the table of eight-byte jumps that the state byte
indexes (`0x42fc88`), and `0x43013b` to `0x4312ed` is the handler of state 0x34 (a soldier's work at a military building), so
neither holds carrier code; the carrier's states are `0x42`, `0x43`, 3 and 0xe.

## The carrier's exchange at a flag

**Status:** confirmed for a log carried over a road of three flags to the castle, and for the order goods at one flag leave in;
the swap and the drop are read and checked on made-up tables only (`ExchangeTests`), since no emulated game has a good that
goes the way a carrier came.

State 3 (`0x43ad78`) on a flag, when the carrier's timer has run out. The carrier's byte 11 is the good it carries (0 for none),
the word at 12 the destination of that good (the destination words of the flag's places are numbered as above, the slot plus
one), and the heading (byte 14) the direction of the road it works, as seen from this flag: the direction it came in turned
round. The order is:

1. Byte 15 negative: the carrier is sent home (`0x43aea4`, **not ported**).
2. A good carried and its destination the flag the carrier stands on: state 0xe, the delivery below.
3. Else the exchange (`0x43af07`) with byte `0x3c + heading` of the flag, then a step out of the flag along the heading:
   - **Bit 7 set** (a good waits to go along this road, in the place of bits 0 to 2): byte 15 is 0. A carrier that carries a good
     leaves it in that place (the byte as it is, so the place's direction bits are 0), sets bit 7 of the flag's byte 4, and takes
     the good that was there (the low 5 bits of its byte) and, swapped with its own, its destination word. A carrier that carries
     nothing takes the good and the word and empties the place. The road's byte then loses bit 7 and the place (`& 0x78`), and
     the places are scanned from 0 to 7: the good whose direction bits are the heading plus one and whose transport priority is
     greater than every one before it in the scan (a priority of 0 is never greater than the start value 0) takes the road's
     byte again, bit 7 and its place.
   - **Bit 7 clear:** a carrier that carries a good puts it, with its destination word, on the first empty place and sets bit 7 of
     the flag's byte 4; with all eight places full it keeps the good. A carrier that carries nothing does nothing.
   - The carrier then steps along the heading as on the road. A carrier that carries nothing and comes to a flag whose byte for
     its road has bit 7 set does not stop next to the flag (state 0x42) but steps onto it, and so takes the good there.

The order goods leave a flag in follows from the places the flag's turn gives the road's byte and from the exchange. The turn
visits the places from the last to the first, and only a place that finds the road's byte free takes it, so with two logs on the
places 0 and 1 the one on place 1 goes first. The other waits with its direction. When the carrier takes the first, the exchange's
scan lines up the good of the highest [transport priority](#transport-priorities) among those with the road's direction; of
goods of equal rank the one on the lowest place wins. In the emulated games both logs of the hut's flag reach the castle, and
with three goods on the places 0 to 2, the highest-ranked on place 2 and the lowest-ranked on place 1, the good on place 2 goes
first and the good on place 0 waits next.

## Transport priorities

**Status:** confirmed. A new game sets each player's transport priorities to one default order: `0x42055e`, with the player's
block at `+0x30` of the EDI block, calls `Player_SetTransportPriorities` (`0x445570`), which writes a rank for each good 1 to 26
into the player block at `-0x55 + good`. The routine is 26 steps of 8 bytes, one for each good in order; each loads its rank
as a constant in the instruction and stores it, so the rank of good g is the byte at `0x445571 + 8 * (g - 1)`. The ranks are 1
to 26, each once. The emulator's `prio=` line
shows these ranks after the routine runs.

The carrier's exchange (`0x43b054`) and the direction step of the flag's turn (`0x46501a`) read the rank by the good's low 5 bits.
For the goods 27 to 31 the read reaches past the table into two counters of the player block (`-0x3a` and `-0x36`) that
`Castle_CreateSettlers` adds to; no good has those numbers. Good 0 is no good. A rank of 0 never wins the exchange's scan, which
starts at 0 and takes only a greater rank.

The port reads the ranks from the routine's instructions in the install's program (`ProgramTables.TransportPriorities`), never
from source. It checks that every step starts with the load of a constant and that the ranks are 1 to 26 each once; an install whose program fails either
check is refused with a message naming the transport priority table, so the game never runs with every rank 0. `Game` gives the
goods 0 and 27 to 31 the rank 0. **Not ported:** the player's panel that reorders the ranks, and one table per player (the engine
runs one player).

## The delivery into the castle

**Status:** confirmed. The log counts into the stock word once for each log delivered; the **inferred** rule of the castle's bytes
(8 and 9 hold `0xffff` all along) is settled by the emulator.

State 0xe (`0x43c4ea`, entered at `0x43c45e`) with the carrier on the flag of the building it carries a good to, which is
north-west of the flag. The step into the building has the frame the height difference plus `0x5d` and takes its time from the
step table, as a step on the road does; the turn goes on at once when the timer did not wrap. When the timer has run out:

- **First half** (byte 15 is 0): a good carried is taken into the building unless bit 5 of the building's status is set (inferred:
  a building that takes no goods). Byte 9 of the building's item gains `0x0f` for a log (the good 7) and for every good from 9 on
  but 13; byte 8 gains it for the others. A carry out of the byte makes both bytes `0xffff` and adds one to the building's storage
  word for the good (offset `4 + 2 * good` of the storage item the building points to at `+0xe`: `0x12` for a log), where it
  stays at 50000 at most. The castle's bytes are `0xffff` from its placement (they read so before the first pass, and the
  delivered logs leave them so), so every good delivered to it counts: in the emulated game the castle's log word rises from 0 to 1
  at the pass of the carrier's second turn in state 0xe, and to 2 with the next log. The carrier turns the frame round (`0x0d`
  less the height difference), sets byte 15 to `0xff`, and adds half the time of that step to its timer.
- **Second half** (byte 15 not 0): byte 15 is 0, state 3, and the exchange runs again at the flag with the heading as it was (the
  carrier now carries nothing, so it may take a good for its road at once), then the carrier steps out.

## Drawing a carrier with a good

**Status:** confirmed from the code and the table's bytes and by the emulator's draw runs of the carrier with each of the goods
1 to 26.

The draw routine for job 0 (`0x43f8ae`, reached from `0x43f537`) copies byte 11 (the good) of a carrier in state 3 or 0xe to the
work register. Where the good is not 0, the routine doubles it, loads the address `0x43f4b7` (`0x43f904`), and adds the 16-bit
word at that address plus 2 * good to the figure word.

The table at `0x43f4b7` has 27 words, one for each good. Good 0 never reads its word. The word is added to the whole figure word.
The figure word holds the figure byte in the low byte. The carried word's high byte is the row of the figure bases (`0x44fff3`,
[Settlers](settlers.md#drawing-the-walk-leads)). Its low byte is 0 for every good.

The table's bytes from `0x43f4b7` are `00 00 | 00 30 | 00 35 | 00 3b | 00 41 | 00 46 | 00 4b | 00 14 | ...`. As little-endian
words they are `0x0000, 0x3000, 0x3500, 0x3b00, 0x4100, 0x4600, 0x4b00, 0x1400, ...`. The log (good 7) adds `0x1400`, so its
figure byte is drawn in the row `0x14` of the figure bases. A carrier in state 0x42 is not drawn, and its byte 11 is the road
direction. A carrier with no good (byte 11 0) adds nothing.

`ProgramTables.CarriedFigures` reads the 27 words from `0x43f4b7`. `SettlerFigures` draws a carrier on its road (state 3) or
handing a good over (state 0xe) with a good by the same rule for every good. `Sprites` and `Draws` share one function. The view
draws it in every wrap copy. A play run of `play/carrier-log.play` shows the carrier holding the log above his head and facing
the way he walks.

**Confirmed** by the draw runs of the `carrier`, `goods`, and `goods-alone` recordings
([Settlers](settlers.md#the-draw-path-emulated)). The original draws a carrier in state 3 or 0xe with a good (byte 11, each of
the goods 1 to 26 recorded, the log among them). It draws the figure byte of its frame's triple in the row the good's word
picks, with its overlay and a second figure, in every recorded pass. The engine's draws are the same.

## Open leads

- The request words at `0x42` to `0x45` of a flag item and the priority bytes: no building asks for a good yet (milestone 4).
- The counter in the player block that the put adds to (`+good - 0x6f`).
- What the original does with some road holding a carrier and no storage reached (`0x464539`).
- A carrier sent home (byte 15 negative, `0x43aea4`), and state 0x44 and later (`0x43007b`, `0x4300f1`).
- The passing of two settlers that come the other way at a flag (the emulated game of several carriers shows it at a flag where
  a carrier with a log and a carrier that is to fetch one meet): the swap of a step east, south-east, or south is ported with the
  walk home ([Settlers](settlers.md#walking-home)). `layouts/head-on-pass.jsonc` with `play/head-on-pass.play` meets a carrier
  walking home and a woodcutter walking out head on along one road, and `HeadOnPassTests` meets two carriers the same way.

## In the engine

`Game.Goods.cs`: `GoodsTurn`, `Destine`, `StorageSearch`, `Direct`, `Fetch` and `Claim` port the above; `LayGood` is the put.
`Exchange` and `WaitForRoad` are the carrier's exchange at a flag, `TransportPriority` reads a good's rank from
`ProgramTables.TransportPriorities`, `Deliver` and `Handover` state 0xe, `AddToStock` the castle's
count (`Game.CastleLogs` reads the log's). A carrier's byte 11 is `Settler.Mode` and its word at 12 `Settler.Target` (the
destination as the original numbers the flags while it carries a good). `Building.Fill` holds bytes 8 and 9.
`Game.FlagGoods(flag)` holds the places' bytes (the direction in bits 5 to 7), `FlagGoodsBit`, `FlagDestinations` (the slot plus
one, as the original numbers the flags, 0 for none), and `FlagDirectionBytes` read the other fields. `Game.Digest` holds all
of them.

# Goods at a flag

How a good that lies on a flag gets its destination and its direction, and how the carrier who waits next to the flag is told
to fetch it. Addresses and routine names are in the [Program index](program-index.md#simulation); the flag item's fields are
in [Item tables](item-tables.md); the carrier's walk to its road is in [Carriers](carriers.md); the woodcutter's put of a log is
in [Woodcutting](woodcutting.md#the-log). What a carrier does from state 3 on with a good to fetch (it lifts it, carries it to
the next flag, and hands it over) is not read yet: the next stage of slice 08.

**Emulated.** The harness verb `goods:` (`tools/GhidraDump/emulate.py`) is `wood:` in a game that starts as the original's game
start leaves its tables. `MapGen_InitParams` (`0x421530`) ends by taking the first slot of the flag table (`0x45d7ad`), of the
building table (`0x45da8f`), and of the settler table (`0x45e121`) for a dummy, so the first flag of a game is slot 1. The verb
`wood:` runs without the dummy: the castle's flag is slot 0 there, a good's destination word for it is 0, and 0 means none, so
in `wood:` runs a log never leaves its flag (the flag pass looks for the castle again at every turn). `goods:` takes the flag
slot's dummy only, the other tables keep the numbers the earlier verbs use. `tests/OpenSettlers.Tests/flag-goods.txt` holds the
flags' goods fields and the carrier's bytes after the passes of two games, and the engine matches them
(`FlagGoodsTests`): a log laid by the woodcutter, and a log laid by hand at pass 5 before any carrier is on the road.

## The fields

**Status:** confirmed for the fields below.

- Byte 4 of a flag item, bit 7: the flag has goods that need a turn. A put sets it (`0x436b46`), the turn of the goods clears it,
  and a turn that could not finish sets it again, so a good that waits is looked at again at each cycle of 33 passes.
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

**Status:** confirmed for a log at a flag with one storage (the castle), and for a good that waits with no carrier on the roads.

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
   byte `0x3c + d` has no bit 7, it takes the place and bit 7; where it has, the good in that place is replaced by the new
   one when the new good's priority (a table in the player's block, `+good - 0x55` from the pointer `+0x64 + 4 * player`) is
   higher, which the engine has no case for (one good). The good's byte takes `d + 1` in bits 5 to 7 either way. The word
   keeps the destination, and the bit stays clear.

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

**Not read** (stage 2 of slice 08): state 3 with mode 0 does here what it does for a carrier with no goods in the engine
(the walk along the road, and the stop next to the end flag), which is not the original's: in the emulated run the carrier is
blocked once by a settler (timer `0x7f`, frame 0x54), steps on, and takes mode 7 and a target of 1 at the end of its walk,
states the next stage reads. The goods' lifting, the carrying, and the castle's handover are in `0x42fca1` to `0x4312ed`, code
the dump has no function for.

## Open leads

- The request words at `0x42` to `0x45` of a flag item and the priority bytes: no building asks for a good yet (milestone 4).
- The player block's priority table (`+good - 0x55`), and the counter the put adds to (`+good - 0x6f`).
- What the original does with some road holding a carrier and no storage reached (`0x464539`).
- The state 3 walk with a good, and state 0x44 and later (`0x43007b`, `0x4300f1`): the next stage.

## In the engine

`Game.Goods.cs`: `GoodsTurn`, `Destine`, `StorageSearch`, `Direct`, `Fetch` and `Claim` port the above; `LayGood` is the put.
`Game.FlagGoods(flag)` holds the places' bytes (the direction in bits 5 to 7), `FlagGoodsBit`, `FlagDestinations` (the slot plus
one, as the original numbers the flags, 0 for none), and `FlagDirectionBytes` read the other fields. `Game.Digest` holds all
of them.

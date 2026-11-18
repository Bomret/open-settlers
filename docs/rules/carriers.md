# Carriers

How a road gets its carrier: the pass over the flag table that notices a road without one, the request to the castle, the
carrier's walk from the castle to the road, and the place where it waits. Addresses and routine names are in the
[Program index](program-index.md#simulation); the items are in [Item tables](item-tables.md); the walk the carrier shares
with the first worker is in [Settlers](settlers.md); the roads are in [Roads](roads.md).

**Emulated.** The harness verb `carrier:` (`tools/GhidraDump/emulate.py`) starts a game as the `road` verb does (castle,
flags, roads) and runs the program's own routines for each pass: `0x428728`, the phase step, `0x46414d`, `0x42cb2d`, and
`0x42f3d9`. The engine runs the same commands and matches the bytes of the castle's settlers 1 to 5 and the status and road
bytes of the flag items after every pass (`tests/OpenSettlers.Tests/carrier-walks.txt`), in three games: two roads built at
the same tick, a second road built from the end of the first after 400 passes, and three roads built at the same tick whose
carriers meet on the way. The `worker` verb runs `0x46414d` too, so `worker-walks.txt` holds the first worker's walk with
the carriers on the roads.

## The job

**Status:** confirmed.

A carrier is a settler of job 0 (byte 0 of its item, bits 2 to 6). The castle's 20 first settlers hold none, so the first
carrier of a game is made from a settler of job 21, one of the five the castle holds without a job (slots 1 to 5): the
request clears the job bits (`0x42bbea`, `& 0x83`) and no tool is taken from the stock. Job 1 is the boatman of a water road
(`0x42b4bc`): **inferred**, not ported.

## The flag pass

**Status:** confirmed for the road loop; the goods part of `0x46414d` is in [Goods at a flag](goods.md).

`0x46414d` runs once for each pass in the phases 0 to 31, after the phase step and before the building pass. It handles the
flags in use whose slot divided by 32 is the phase, in the order of their slots. A flag is therefore handled once for each phase
cycle: 33 passes in a game with 33 phases, 33 to 49 passes in a game with 49
([Timing](timing.md#rules-that-count-passes)). The first flags, slots 0 to 31, belong to phase 0. In a game with 33 phases,
they are handled in the pass with the number 32 of a game. After the flag's goods it
reaches `Flag_VisitRoads` at `0x464c36`, which looks at each direction from north (5) to east (0) that holds a road. The
rules below are for a road that is not busy (fewer than three of the flag's goods head along it); a busy road is
[below](#a-busy-road).

- The road byte (flag item byte `6 + direction`) holds the length class in bits 4 to 6, bit 7 while a carrier is on the way to
  the road, and the number of carriers on the road in the low nibble. The commit of a road writes the class
  (`0x4454e7`: [Roads](roads.md#drawing-a-road)) and clears the rest, at both ends.
- A road with a carrier on it (low nibble not 0) sets bit `direction` of the flag's status byte (byte 5), whether or not
  another carrier is on the way to it.
- A road whose byte is the class alone (no carrier on it or on the way), at a flag whose status bit 7 is clear, asks for one
  (`0x42b455`, for a land road; a water road asks `0x42b4bc`). A request that finds no carrier sets status bit 7, so the flag
  makes no more requests, for any of its roads, until the retry scan has cleared the bit.
- The retry scan (`0x428728`) clears bit 7 of the status of a window of flags from a cursor (`+0x56`) that wraps at the end
  of the table: the window is the buildings' (16, 30, or 55 by map size), the scan stops after 10 flags that had the bit set.
  It is the building scan of [Settlers](settlers.md#the-retry-scan) with the flag table's items (70 bytes) and bit 7.

A road split by a flag ([Roads](roads.md#a-flag-on-a-road)) leaves the half without the old carrier with its road bytes at the
class alone, so this loop asks for a carrier for it like for a new road.

The goods part of `0x46414d` (a good's destination and direction) is in [Goods at a flag](goods.md); it runs before this road
loop at the flag's turn and hands it the load of the flag's directions and the count of its goods.

## A busy road

**Status:** confirmed (`0x464c36`, the capacity read at `0x465189`, the clear at `0x46520b`). **Emulated** (`mill-walks.txt`,
`butcher-walks.txt`, `saw-walks.txt`, the scenario `flag-full` of each): a road whose flag holds eight stones for the castle
gets a second carrier, a settler without a job of the castle (in the mill's game it takes job 0 at the pass 1484 and enters
the road at the pass 1519), and the worker at that flag steps out at the original's pass.

A road is **busy** at a flag's turn when three or more of the flag's goods head along it (byte 2 of the load the goods part
builds, [Goods at a flag](goods.md#the-turn-of-a-flags-goods)). For a busy road the loop does this:

- It sets no bit in the status. A bit an earlier turn set stays.
- With a carrier on the way to it, it asks for nothing.
- Else it compares the carriers on the road with the byte `Road_CarrierCapacities` at `0x462acd` holds for the road's length
  class (1, 2, 3, 4, 6, 8, 11, and 15 for the classes 0 to 7). A road whose carriers **equal** that number asks for nothing; a
  road with fewer, or with more, asks for another carrier as a road with none does: `Road_RequestCarrier` (`0x42b455`) for a
  land road, `0x42b4bc` for a water road, only while status bit 7 is clear, and a request that finds none sets bit 7. The
  carrier the request sends counts as on the way at both ends, and enters the road as the first did.

At a flag whose count of goods is 7 or more (counted only on a turn that routed its goods,
[Goods at a flag](goods.md#the-turn-of-a-flags-goods)), the turn then clears the status bit of every busy road and of every
road that asked for a carrier in this turn (`0x46520b`). The flag writes only its own status; the flag at the far end keeps its
bit. The direction search and the storage search seed and follow only roads whose bit is set
([Goods at a flag](goods.md#the-turn-of-a-flags-goods)), so new goods at a crowded flag do not go along its busy roads.

## The request

**Status:** confirmed for a land road.

`0x42b455` is the search of `0x42be63` ([Settlers](settlers.md#the-route-search)) started at a road. It marks both ends of the
road (the flag the request comes from with 0, the flag at the far end with 0xff) and queues them in that order; every flag it
takes off the queue marks the flags its roads lead to (directions from 5 to 0, over the land roads only: the bits at `+4` of
the item) with its own mark. At the flag of a storage:

1. A settler of job 0 waits in the storage: the storage clears its entry and sends the settler.
2. None does, but a settler of job 21 waits: the storage clears that entry, the settler takes job 0, and the storage counts
   one fewer (`+0x40`). The search goes on for a storage with a settler of job 0 for a number of levels that a player's
   field (`+0x10a`) gives, and then takes this one (the engine has one storage).

A sent settler gets state 15, the direction of the road at the end its storage's flag was marked from as its mode (0 to 5),
and that end's flag as its target; the storage counts one more settler sent out (`+0x4a`), and bit 7 of the road byte is set at
both ends of the road. The end is the flag the request came from where the storage's flag carries the mark 0, the far end
where it carries 0xff. If no storage is reached, or it holds neither settler, the request fails.

The castle holds the settlers of job 21 in its list of waiting settlers: the slot of the one that last took its turn in state
1 (`0x438eeb`). The request takes it and empties the entry, so a second request, at the same flag or in the same pass, finds
none until another of the five has had a turn in state 1. A road built together with another therefore gets its carrier in
a later cycle: with two roads at the castle's flag, the road in the higher direction first, and the other one phase cycle later.

## The carrier's walk

**Status:** confirmed to the first turn in state 0x42.

- **State 15** (`0x43dd4a`) and **state 2** (`0x4398ea`) are the first worker's: the carrier steps onto the castle's flag
  (mode 255 is not tested, so the node of a building is not looked at), and walks the roads to its target flag by the route
  search ([Settlers](settlers.md#the-walk)).
- **At the target flag** with a mode of 0 to 5, state 2 takes the road in the direction of the mode: at both ends of the road
  bit 7 of the road byte goes out and the low nibble counts one more; the settler takes state 3 with the direction as its heading
  and the mode 0, and steps on at once.
- **State 3** (`0x43ad78`) is the walk along the road: when the timer has run out the carrier steps from a flag in the direction
  of its heading, from a node of the road in the road's other direction, through the step routines of `0x43b508`. The step is
  the one of state 2 (the shape `4 + 9 * direction + height difference`, the time of the step added to the timer, the carrier going
  on at once where the sum did not wrap). **Blocked:** where the next node holds a settler, the timer is 0x7f, the frame
  `0x51 + direction`, and the heading the direction less 6; at the next turn after the timer the carrier tries the same
  direction again. A step tried again goes through the table at `0x43adea`, whose six entries (`0x43b83a`, `0x43ba9f`,
  `0x43be55`, `0x43c0c0`, `0x43c20a`, `0x43c350`) enter the step routines after their check for the flag at the end of the
  road: a step tried again is never a last step. A step in state 3 leaves byte 15 as it is; only the step of state 2
  (`0x43a64c`) clears it. A step east, south-east, or south onto a settler coming the other way, whose own heading is that direction's opposite
  less 6, passes it instead ([Settlers](settlers.md#walking-home), *Passing*; `HeadOnPassTests` reaches it with two carriers).
- **The last step.** Where the next node is the flag at the end of the road and the carrier's mode is 0, the carrier stays on
  its node: its timer takes 0x3f, its frame `0x6e` plus its heading, the low byte of its stamp the heading less 6, its mode the
  direction in which the road leaves that flag (the heading before it) and the dword at 12 a pointer to that flag's item (the
  engine keeps the slot as the target); the node's settler word is cleared, so the carrier does not hold the node against
  others; and it takes **state 0x42**. At a road that already holds two carriers (the road's byte, low nibble; two come about on a
  [busy road](#a-busy-road) and where two roads are joined, [Removal](removal.md#taking-a-flag-down)) the carrier does not wait: its timer takes 0x3f, its frame is the heading's,
  its heading goes down by 6 as for any step that is tried again, and it counts the turn in byte 15 (the next state). The heading held the
  direction back along the road, so at its next turn the carrier steps back the way it came and walks to its next turn next to the flag at
  the other end; the count outlasts those steps. At the fourth turn the count
  starts again, and if the road still holds two the carrier leaves it: both ends' road bytes count one carrier less and byte 15 takes `0xff`
  (`0x43bc52`). The count goes back to 0 where the carrier takes a good in the exchange, delivers one, or is told to fetch one
  (`0x43001a`), so only four turns in a row with nothing to take release it. With a good waiting at the flag at the far end the
  carrier does nothing more than set the timer, frame, and heading.
  **Emulated** for the two carriers of a joined road (the game `join`, [Removal](removal.md)) and for a busy road that drains
  (`busy-road-walks.txt`, `drain`: four stones make the mill's road busy, the second carrier joins it at the pass 1519, and once
  the goods are gone the carrier of slot 2 reaches its fourth empty turn at the pass 2674); the far end's waiting good is read
  from the code (**inferred**).
- **Marked to go.** A carrier whose byte 15 is negative that steps onto a flag leaves for a storage before anything else at that
  flag (`0x43aea4`, in `Settler_WalkRoad`): it takes state 2, mode `0xfe`, byte 15 0, the word at 12 0, and the timer 0, keeps its
  frame and heading, and steps no further in that turn. It neither delivers the good it may carry nor takes or leaves a good at
  the flag. The walk to the storage is the walk home of [Settlers](settlers.md#walking-home). **Emulated** (`drain`): the
  released carrier reaches the castle's flag at the pass 2806 and the castle's door at the pass 2971. A carrier the removal of
  its road marks ([Removal](removal.md#taking-a-road-down)) takes the same branch at the next flag it reaches. A carrier that
  the removal marks while it hands a good over (state 0xe) is the exception: the second half of the handover (`Settler_DeliverGood`,
  `0x43c4ea`) reads byte 15 not 0 as its own mark, sets it to 0, and the carrier goes on along its road in state 3, with no walk to a
  storage. Confirmed, read from the code.
- **State 0x42** (`0x42ff81`): the item does not change while no good waits to go along the carrier's road (in over 20 turns of the
  emulator, 350 passes). With a good at either end of the road the carrier is told to fetch it and takes state 3 again:
  [Goods at a flag](goods.md#the-carrier-told-to-fetch).

So the carrier of a road stands on the node of the road next to the flag at the road's far end, the end its storage's flag was
not marked from, however long the road.

## Drawing

**Status:** confirmed for a carrier on its road (state 3, with no good and with each of the goods 1 to 26), handing a good over (state 0xe), walking
to its road (states 2 and 5), walking home (0x19, 0x10, and 2 after a road was taken down), and waiting next to a flag (state 0x42) or for its
turn to go home (states 0x44 and 0x45: no settler record) by the draw runs of the `carrier`, `goods`, `goods-alone`, `rejoined`, `dismissed`, and `dismissed-with-log`
recordings ([Settlers](settlers.md#the-draw-path-emulated), `tests/OpenSettlers.Tests/settler-draws.txt`).

The carrier is drawn as the walking worker is ([Settlers](settlers.md#drawing-the-walk-leads)) in states 2, 3, 5, 0xe, 0x10, and
0x19. It uses the figure tables' row of job 0, the figure base 0. A carrier in state 3 or 0xe that carries a good adds the good's
word of the table at `0x43f4b7`. The high byte of that word is the row. The log's word is `0x1400`
([Goods at a flag](goods.md#drawing-a-carrier-with-a-good)).

The settlers' draw path has no record for a carrier that waits off its node (states 0x42 to 0x45). The carrier's figure
handler (`0x43f8ae`) returns at once in state 0x42, and the record builder needs the node's settler word, which the carrier
cleared. The tile pass draws such a carrier instead.

**Status:** confirmed by the code read below and by the `carrier`, `goods`, and `remove` emulator runs with the node marks
(`m=`, `tools/GhidraDump/README.md`).

- **The mark.** As the carrier takes state 0x42, `Carrier_WaitAtEnd` at `0x43bc52` writes the player, with bit 7 set, to byte 1
  of record B of its node. `Settler_CarrierWaits` at `0x42ff81` (its state 0x43 part) and `Settler_StateDismissed` at `0x43007b`
  clear the byte when the carrier takes the node again. A carrier that a removal sends to state 0x44 or 0x45 keeps the mark
  until then (the `remove` runs: the mark clears in the pass the carrier takes state 3 again).
- **The draw.** `Render_DrawRowTiles` at `0x44d5b4` calls `Render_AddWaitingCarrier` at `0x44d77e` for each tile of a row
  whose mark has bit 7. That routine appends a draw record to a list that `Render_DrawViewRows` draws right after the row's
  settler records, as it draws them (`Gfx_DrawSettlerRecords`). The record's colour is the player of the mark.
- **The figure.** Where bit 6 of the node's flag byte is set, the figure word is `0x203` at the node's position. Otherwise the
  routine takes a phase word from a table of 16 (`0x44d87d`) by the address of the node's record, adds the clock, and takes the
  figure word from a table of 128 (`0x44d89d`) at that sum shifted right 3. The words lie in the figure rows 0 and `0x88`. The
  position is the node's, less 4 times its height, plus two signed bytes from the table at `0x674923` for the six road direction
  bits of the node's flag byte.

So a waiting carrier stands, and moves, on the road node next to the flag at its road's far end. On a road of two steps that
node is the middle of the road.

## Open leads

- The goods part of `0x46414d`, the carrier's turns in
  state 0x42 and 3 with a good, and the castle's handover (state 0xe) are in [Goods at a flag](goods.md). `0x42abed` is no
  storage handover: it is the player block's per-turn warning check (it posts messages 10 and 11 through `Msg_Post` from
  counters at `+0x14` and `+0x18` of a 120-byte item).
- The boatman of a water road (`0x42b4bc`, job 1, good 8) and the settlers' search level count (`+0x10a` of the player block).
- The passing of two settlers on a road (ported: [Settlers](settlers.md#walking-home)), and a carrier that goes home when its road goes
  away (ported: [Removal](removal.md#taking-a-road-down)).
- Which flags' carriers a flag pass of a player with a computer opponent sees: the phase count (33) is inferred.
- A carrier released from a busy road whose road is taken down before it reaches a flag: the removal's gather skips a marked
  carrier, and what the original does with it is not recovered.
- What a carrier that walked home does in the castle after it enters: in `drain` it comes out and goes in again every 82 passes
  (states 4, 0x19, 0x10, 2), which no rule note covers yet.

## In the engine

`src/Application/CarrierBehavior.cs` (`Game.Carriers`): `FlagPass`, `VisitFlag`, `RequestCarrier`, `EnterRoad`, `WalkRoad`, and
`Wait` port the above. `GoodsTransport.Turn` hands `VisitFlag` a `GoodsLoad`: `Busy`, the directions three or more goods head,
and `Crowded`, a count of 7 or more goods on a turn that routed them. A water road asks for no carrier, busy or not, since its
boatman is not ported.
The flag's typed state is `Flag` (`CarrierRequestFailed`, the status byte's bit 7) with a `FlagSide` for each direction
(`Length`, `CarrierOnTheWay`, `Carriers`, `HoldsCarrier`, `FarDirection`). The `FlagTable` that `Game.Flags` returns keeps them.
`FlagEncoder.Status` and `FlagEncoder.RoadByte` write them as the flag item's bytes. `Carrier` is job 0. The
carrier of a road is found by its activity: a settler of job 0 in `WaitsAtFlag` (state 0x42) waits next to the flag `Flag` on the road
that leaves it in direction `Direction`. A carrier on its road is `WalksRoad` with the `Load` it carries. A carrier marked to go
has `Marked` set (byte 15 = 0xff). `Game.Digest` holds the flags' status and road bytes (through `FlagEncoder`) and the flag
scan's cursor.

`src/MapView/SettlerFigures.cs` places a waiting carrier as the tile pass does, and the History Edition source
(`SettlerFigureTables`) picks its figure and offset from the tables `ProgramTablesImporter` reads
(`IdlePhases`, `IdleFigures`, `IdleOffsets`). The term holds the node's column, lake mark, and roads. The engine keeps no mark byte: a carrier of job 0 in states 0x42 to 0x45 stands
for the mark, since the carrier enters those states only from 0x42 and leaves them only by taking its node. **Stand-in:** the
original picks the phase by the address of the node's record; the engine picks it by the node's column, which gives the
same phase where the map buffer starts on a multiple of 64 bytes.

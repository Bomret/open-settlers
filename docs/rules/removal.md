# Removal

This note records what the player's clicks that take a flag, a road, or a building down do in the program, and how
the engine reproduces it. Addresses and routine names are in the [Program index](program-index.md#roads); the walk
home that the settlers take is in [Settlers](settlers.md#walking-home), the goods in [Goods at a flag](goods.md), and
the roads' items in [Roads](roads.md) and [Carriers](carriers.md).

## The clicks and the check

**Status:** confirmed, emulated.

The click handler (`0x440d23`) has two routines for a removal. The remove click (`Click_Remove`, `0x4615cf`) runs the
build-site evaluator (`0x44b6e6`) on the node under the cursor and branches on the class it leaves at `+0x100` of the
player block: class 2 takes a flag down (`Flag_TakeDown`, `0x462153`), class 3 takes a building down (`Building_Remove`,
`0x4617fd`), and anything else asks for the error sound and changes nothing. The road click (`Click_RemoveRoad`,
`0x462d88`) takes a road down (`Road_TakeDown`, `0x462e1d`) where the class is 4. The classes the removal acts on:

| Class | The node | Click |
| --- | --- | --- |
| 1 | A flag the click refuses: a flag with a building at its north-west (the flag byte's bit 4 and a building class there), with a road of water, with a road count other than two, or with two roads to the same flag. | refused |
| 2 | A flag with no road, or with exactly two roads of land to different flags. | takes the flag down |
| 3 | A building that stands (the item's bit 5, the mark of a building that is taken down, is clear). | takes the building down |
| 4 | A node of a road that holds no flag: its flag byte's road bits are not 0, not exactly `0x02`, and not exactly `0x10` (the marks of a building and of the flag of a building). | takes the road down |

The node must belong to the player (the owner bits of the height byte). The castle's node gets class 0: its object's
class in the program's table is one the check passes over, so a click on the castle is refused. The castle's flag has
the castle at its north-west, so it is class 1 and refused. **Emulated:** the verb `remove` with the click `c@x,y` (the
check alone) and `s@0,0` (the check on every node) gives the classes of two games, and the engine's `Game.ClickClass`
gives the same class 1 to 4 for every node; the nodes with another class the check gives (5 to 7: nodes where
something may be built) are not judged. The flag button's and the road button's stand-ins in the engine ask the game which
command a click on a node means (`Game.RemovalAt`).

## Taking a road down

**Status:** confirmed, emulated.

`Road_TakeDown` takes the first two road bits of the clicked node (the lowest, then the next one above; a second bit
of 4 with bit 5 also set is taken for 5) and runs `0x463204` for each. That walk clears the bit of the direction at the node
and then, node after node, the bit that points back and the bit that leads on, until it reaches the flag at the end of the road. A road
that returns to its start flag is taken down the same way: the two walks from the click each end at the flag. **Emulated:** the game `road-long` (a road of six steps from 14,8 to 20,8, clicked at 17,8 with two nodes on each side, `removals.txt`) clears the flag byte of every node of the road, the bit back and the bit forward alike (`0x09` becomes `0x00` at 15,8 to 19,8), and the engine holds the same bytes. On the way:

- The carriers that wait on a node next to a flag (the states `0x42`, `0x43`, and `0x45`) are told to go home
  (`0x463ef9`): they take state `0x44`.
- A settler that stands on a node of the road in state 2 or 3 is sent home (`0x46385b`) with no regard to its heading; at the
  first node and at the flag at the end it must have come along the road (its heading, the direction back, is the direction of the
  walk).
- A settler dismissed this way takes state `0x19`, the search for the nearest flag with a road, with the mode 0. Before that:
  in state 2 with the mode `0xff` (a worker on its way to a building at a flag) the building's mark of a worker on the way
  goes, and where the mark was clear its request counter (byte 8) goes down; in state 2 with a road direction as its mode (a
  carrier on its way to a road) the carrier-on-the-way bit of the road's byte at both ends goes; in any other state a good it
  carries (its mode byte) is counted lost (below).
- At the flag at the end the flag's item loses the road's bit in the status byte, the road's byte loses the bit 7 (a carrier on the
  way) and keeps its length class and carrier count (the next road built there overwrites them), settlers on their way to the
  end of this road (state 1, 2, 15, or 5 or 7 on their way to state 2, with the flag as target and the road's direction as mode)
  take the mode `0xfe` and target 0 where the bit 7 had been set, the road's direction byte loses its good and place bits, and
  each good on a place with that direction loses it and gets the flag its turn again.

State `0x44` (`0x43007b`): the carrier takes the node it stands on where the node's settler word is 0, takes the clock as its stamp, the
timer 0, state `0x19`, and the mode 0 (the boatman, job 1, takes `0x1a`: not ported). Else it tries again at its next turn.

**Emulated:** the games `road-with-log` (a carrier that waits next to the flag, at the pass 785 of the chain game; it goes home
through the states `0x44`, `0x19`, `0x10`, and 2 with the mode `0xfe`) and `road-waiting` match the settlers' 16 bytes, the flag
items, and the map bytes after the click and after every pass until the carriers reach the castle's flag
(`tests/OpenSettlers.Tests/removals.txt`). **Not run (inferred):** a carrier on the road that carries a good at the click (the
code that counts it lost is read, and the counting back is shared with the other routines below, whose cases the games reach);
the settler in state 2 with the mode `0xff` or a road direction (a worker or carrier on its way to the road).

## Taking a flag down

**Status:** confirmed, emulated for a flag with two roads and for a flag with no road.

`Flag_TakeDown` first deals with the settler that stands on the flag's node: in state 5, 7, or `0x4a` its next state (byte 15) becomes
`0x19`; in state 2, where the node has no road bit, it takes state `0x19`. A flag with road bits then joins its two roads:

1. The program walks the first and the last road bit's road to the flag at its end (`Road_GatherSettlers`, `0x463a40`), counting the
   steps and listing the settlers on the road: a carrier in state 3 that is not marked to go (the next state is not `0xff`) takes
   the next state 0 and joins the list; at the flag where the walk starts and at the flag at the end it must have come along the
   road; at the flag at the end a carrier in state `0xe` also joins; a carrier that waits next to a flag (state `0x42` or `0x43`)
   takes state `0x45` and joins.
2. The two far flags' items are joined: each far flag's direction byte for the road keeps its good bits and takes the other far
   flag's direction in bits 3 to 5, each is marked as the road's other end, and the carrier-on-the-road bits of the two ends
   are cleared. The new road's length is the sum of the two, its class (`0x4454e7`) goes into the road bytes of both ends. The join writes
neither the road bits nor the land bits (`+3`, `+4`) of the far flags: each keeps the kind it had for its road, so taking
down a flag between two water roads leaves a water road (read in `0x462153`, not run: no emulated map holds a water road
at a flag; [Roads](roads.md#water-roads)).
3. With settlers on the old roads the status bit of the road is set at both ends, and the road byte's low nibble takes their
   number, up to the number its class holds (the table at `0x462acd`, read from the program at run time). Past that, the
   settlers of the lists are marked to go from the first: a waiting carrier (state `0x45`) takes state `0x44`, any other settler
   takes the next state `0xff` and a good it carries is counted lost.
4. Settlers on their way to either far flag by this road (the target and the mode are the far flag and its direction) take the
   mode `0xfe` and target 0.

The flag then goes as a flag with no road does: its flag bit is cleared (the road bits stay: they are the new road's), the
settlers on their way to the flag take the mode `0xfe` and target 0, the object byte keeps its top bit only, the link is 0, each good
that lies on its places is counted lost, and the slot is freed (`0x45d974`: the slot's bit in the bitmap goes, and the table's count falls
past the free slots at its end; the item keeps its bytes). A flag with no road skips the join. The player blocks' request words that name the flag
(`0x462bbc`) are cleared; the engine keeps none (not ported).

A carrier in state 3 that is not on the list walks on through the node that was a flag as along one road. The joined road
may hold two carriers at once, and a carrier that comes to its end then counts its turns before it may wait
([Carriers](carriers.md#the-carriers-walk)).

**Emulated:** the game `join` (the through flag at 18,6 of the chain game at the pass 600, with two waiting carriers that go to state
`0x45`, take state `0x43`, and go on along the joined road) and the game `hut-flag-freed` (a flag with a log, a log destined for it,
and a hut, each taken down) match the same bytes. **Not run (inferred):** the case of more settlers than the class holds (the road
byte's low nibble, the mark `0xff`); `RemovalLogicTests` holds a test of the engine's own logic for it on made-up tables.

## Taking a building down

**Status:** confirmed for the woodcutter's hut (the type 2), emulated.

`Building_Remove` marks the building (bit 5 of its status byte, the mark of a building that is taken down; the castle's node's class 0
and the other types' branches are not reached by the click) and clears the bit `0x02` of the node's flag byte and the bit `0x10` of the
flag's. It clears the building's fill bytes, takes the status bits 3 and 4 off, puts `0x7ff` in the word that held the worker's slot
and the clock in the word at 14. If the worker is inside (bit 6) the bit goes and the worker walks home: a settler of the job 4
loses its job and mode, its timer is 0, and it takes state `0x19` where its node's settler word is its own slot, else `0x1c` (the
claim of the node, then `0x19`). A worker that is outside (walking to the hut, or cutting) is not touched by this routine.

Then what waits for the building at its flag is released (`Flag_ReleaseGoods`, `0x4312ed`): a settler on its way to the flag with the
mode `0xff` or `0xfe` takes the mode `0xfe` and target 0; a carrier that carries a good to the flag has the destination 0; each good on a
place of any flag whose destination is the flag loses it and gets the flag its turn again, and where the good had a direction the
direction goes and the byte of that direction is made again (the good of the greatest transport priority that holds it, as
[Goods at a flag](goods.md#the-carrier-told-to-fetch) states). Last, if no road leaves the flag (its road bits, after the bit `0x10` is
cleared, are 0) and it holds a flag, the flag is taken down as above, with the goods that lie on it counted lost.

The building burns: the building pass (`0x42cb2d`) takes the clock since its last turn off the countdown at each of its turns (once per phase cycle,
[Settlers](settlers.md#the-pass)), and when the word wraps `Building_Free` (`0x42cf9d`) clears the node's mark `0x40`, object, and link, and frees the slot (the table's count
falls past the free slots at its end). It takes about 8 turns, 264 passes.

**Emulated:** the games `hut-inside` (the worker is inside at the pass 571; it takes state `0x19` and walks home to the castle; the building
burns until its slot is free at the pass 857) and `hut-flag-freed` match. **Inferred, not ported:** the buildings' and the castle's counts in
the player block, the storage items' request fields (the last loop of `0x4312ed`), a worker that is outside the hut (what it does when
it comes back to a hut that is gone is not recovered), the military buildings' and the castle's branches (the territory), and a good whose
destination flag holds no building (the program reads a null pointer there; the engine leaves it).

## A good that is lost

**Status:** confirmed by the code; the case of a log with the castle as its destination is emulated.

`Good_CountLost` (`0x462c8d`) takes the good's code and the destination word. The goods 14 and 15 count the total of resource 1 (state `+0x4c`)
down by one. A good whose kind (the table at `0x462d6d`, read from the program at run time) is 0 or 1 lowers byte 8 or byte 9 of the item of the building at the destination flag by one, as a byte,
unless the type with the construction bit and the type bits (a castle that stands, `0x18`, or the type `0xa`): the castle that is built still counts (**emulated**:
the byte 9 of the castle's fill fell in `hut-flag-freed`).

## The castle and its flag

**Status:** confirmed, emulated.

The castle's node and its flag cannot be taken down by a click: the check gives the flag class 1 (the castle stands at its north-west) and the node
class 0 (see the table). The branch of `Building_Remove` for the type `0x18` (settlers sent home from the castle, its storage freed, the territory
updated with `0x45e375`) is called by `0x45ed5f` as well; it is not reached by the clicks and is not ported. **Emulated:** the game `refusals` clicks the castle's flag,
the castle, the hut's flag, and a flag as a road, a node of no road, and a node of nobody's: each gets sound 4 (the error) and changes nothing.

## In the engine

- `Game.ClickClass`, `Game.RemovalAt` (the command a click means), `CommandKind.RemoveFlag` (5), `RemoveBuilding` (6), and `RemoveRoad` (7): `src/Simulation/Game.Removal.cs`.
- The table slots: a flag's or building's slot freed is `Flag.Free` or `Building.Free()`; the next flag or building takes the first free slot (`0x45d7ad`, `0x45da8f`).
- `ProgramTables.GoodKinds` (`0x462d6d`) and `RoadCapacities` (`0x462acd`).
- The states `0x44` and `0x45` (`Dismissed`, `Rejoined`) and the carrier's end-of-road counting (`Wait` in `src/Simulation/Game.Carriers.cs`).
- Departures: the engine's flag slot 0 is the castle's flag, so the original's test of a waiting settler's target (state 1) is left out ([Goods at a flag](goods.md) states the dummy); a carrier that waits has the
  flag's slot in the target where the original keeps a pointer, so the bytes of a settler in state `0x19` that was one are masked in the test.
- The drawing: the stand-in button "Remove" in `MapViewControl`; a hut that burns is not drawn (its fire is not read).
- Tests: `RemovalTests` (the verb's games and classes, `removals.txt`) and `RemovalLogicTests` (the engine's own logic, made-up tables).

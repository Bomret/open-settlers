# Settlers and goods in a layout

A layout ([ADR 0009](../adr/0009-layouts-set-up-a-game-by-a-hand-drawn-map-and-commands.md)) may start settlers and put
goods on flags. The original has no such start: every settler and good comes about in a run of its routines. The engine
therefore starts a settler only in a state that one of the original's routines writes, an **entry state** (INV-0005), and
puts a good as the original's put does. Addresses are in the [Program index](program-index.md#simulation) and refer to the
[reference build](AGENTS.md#reference-build). The entries apply at tick 0 in the order the file lists them, after the
castle, flags, roads, and buildings the file lists before them.

**Status:** the engine's own form (a stand-in for the layout's file keys); the fields each entry state sets are the original's,
confirmed by the emulated worker walks ([Settlers](settlers.md#the-walk)) and carrier walks ([Carriers](carriers.md#the-carriers-walk)).

## Where a settler comes from

A placed settler is one of the castle's own 20 first settlers ([Castle](castle.md)) that waits in the castle (state 1) with the
job the entry names. The request that sends a worker (`0x42be63`) and the one that sends a carrier (`0x42b455`) empty the
castle's entry in its list of waiting settlers; a placed settler empties it the same way where it names him. The castle's
counter of settlers sent out rises with the request and falls with the departure from the castle (`0x43dd4a`), so a placed
settler leaves it as it was. A castle that holds no such settler refuses the entry: a second woodcutter is one the castle
does not hold. **Stand-in:** the original sends the settler its list names, the one that last took its turn in state 1; at
tick 0 no turn has run, so the engine takes the lowest slot of the job. For a carrier it takes a settler of job 0, else one
of job 21 made a carrier, as `0x42b455` does.

## Entry states

| Entry | Key | State the settler is in | Routine that writes it |
| --- | --- | --- | --- |
| Woodcutter in his hut | `{"job": "woodcutter", "state": "in-hut", "x", "y"}`, the hut's node | state 4 at the hut's node, the step through the door just taken: the timer holds the step's time, the target the part of it the door's speed leaves, the stamp the clock, the mode 255 | the entering of `Settler_Walk` (`0x4398ea`), which hands over to `Settler_PassDoor` (`0x43c96b`, then `0x43cf15`) |
| Carrier waiting on his road | `{"job": "carrier", "state": "waiting-on-road", "x", "y", "direction"}`, a flag of the road and the direction the road leaves it | state `0x42` on the node of the road next to the flag at the road's far end, the end the castle is not reached from first | `Carrier_WaitAtEnd` (`0x43bc52`), after the road's entering in `Settler_Walk` (`0x4398ea`) |

**The woodcutter.** The entry takes the settler through the entering of `0x4398ea`: the hut's status gets bit 6 (it has its
worker) and loses bit 7 (a worker on the way), the hut's worker is the settler, the settler stands on the hut's node, and the
node's settler word names him. The hut asks for no second worker (`0x42d945` tests bits 6 and 7 of the status). At its next
turn the door routine (`0x43c96b`) lets him out into the search for a tree (state `0x12`, [Woodcutting](woodcutting.md#the-start-at-the-hut)).
The fields the walk would leave that no later routine reads before it sets them (the heading) are the castle's.

**The carrier.** The entry names the road by one of its flags and the direction. The road is a land road with no carrier or
carrier on the way, joined to the castle by land roads. The settler takes job 0, state `0x42`, the node next to the far flag,
the timer `0x3f`, the frame `0x6e` plus the direction the road leaves that flag, the low byte of the stamp that direction less
6, the mode that direction, the target that flag's slot, and no settler word on the node; both ends of the road count one
carrier in the low nibble of their road bytes and bit 7 is clear (`0x4398ea`, [Carriers](carriers.md#the-carriers-walk)). A
good at either end of the road then sends him on at his next turn (`0x42ff81`).

## Goods

`{"x", "y", "place": 0 to 7, "good": 1 to 25}` puts the good on the place of the flag at the node as `Flag_PutGood` (`0x436b46`)
does: the place's destination is cleared and the flag's goods bit is set, so the next turn of the flag's goods gives it a
destination and a direction ([Goods at a flag](goods.md#the-turn-of-a-flags-goods)). The original's put takes the first empty
place and checks nothing; the layout names the place, and the engine refuses a node with no flag, a place outside the flag's 8,
and a place that holds a good. The player block's counter of goods on flags (`0x436b46`) is not ported.

## Not supported

Every other state of a settler (mid-walk, mid-cut, in a building other than the woodcutter's hut), every other job, and
custom castle contents have no entry state here: the entry is refused when the file is read.

## In the engine

`Layout.Placement.cs` reads the `settlers` and `goods` keys; `Game.Entry.cs` holds `PlaceWoodcutterInHut`,
`PlaceCarrierOnRoad`, and `PlaceGood`. A settler for a hut or road the layout has not built before it, or a good on a flag
that does not stand, fails the load with the entry named.

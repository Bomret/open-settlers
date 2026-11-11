# Settlers and goods in a layout

A layout ([ADR 0009](../adr/0009-layouts-set-up-a-game-by-hand-drawn-map-and-commands.md)) may start settlers and put
goods on flags. The original has no such start. Every settler and good comes about in a run of its routines.

The engine therefore starts a settler only in a state that one of the original's routines writes. Such a state is an
**entry state** ([INV-0005](../invariants.md#inv-0005)). The engine puts a good as the original's put does. Addresses are
in the [Program index](program-index.md#simulation) and refer to the [reference build](AGENTS.md#reference-build). The
entries apply at tick 0 in the order the file lists them, after the castle, flags, roads, and buildings that the file
lists before them.

**Status:** the engine's own form, a stand-in for the layout's file keys. The fields each entry state sets are the
original's. The emulated worker walks ([Settlers](settlers.md#the-walk)) and carrier walks
([Carriers](carriers.md#the-carriers-walk)) confirm them.

## Where a settler comes from

A placed settler is one of the 20 first settlers of the [castle](castle.md). He waits in the castle in state 1 (the
[walk](settlers.md#the-walk) lists the states) with the job the entry names. [Settlers](settlers.md#the-items) lists the
jobs.

The request that sends a worker (`0x42be63`) and the request that sends a carrier (`0x42b455`) empty the castle's entry
in its list of waiting settlers ([the request](settlers.md#the-request)). A placed settler empties it the same way
where it names him. The castle's counter of settlers sent out rises with the request and falls with the departure from
the castle (`0x43dd4a`). A placed settler therefore leaves the counter as it was.

A castle that holds no such settler refuses the entry. A second woodcutter or stonecutter is one the castle does not hold.

The original sends the settler its list names, the one that last took its turn in state 1. The engine takes that
settler where the list names one. **Stand-in:** where the list names none, the engine takes the lowest slot of the
job. This is the case at tick 0, because no turn has run. For a carrier the engine takes a settler of job 0. Else it
takes one of job 21, which is a jobless settler, and makes him a carrier, as `0x42b455` does.

## Entry states

| Entry | Key | Routine that writes it |
| --- | --- | --- |
| Woodcutter, stonecutter, or forester in his building | the `buildings` entry of the type (below), the building's node | the entering of `Settler_Walk` (`0x4398ea`), which hands over to `Settler_PassDoor` (`0x43c96b`, then `0x43cf15`) |
| Carrier waiting on his road | `{"job": "carrier", "state": "waiting-on-road", "x", "y", "direction"}`, a flag of the road and the direction the road leaves it | `Carrier_WaitAtEnd` (`0x43bc52`), after the road's entering in `Settler_Walk` (`0x4398ea`) |

The woodcutter is in state 4 at the hut's node, with the step through the door just taken. The timer holds the step's
time. The target holds the part of it that the door's speed leaves. The stamp holds the clock, and the mode is 255.

The carrier is in state `0x42` on the node of the road next to the flag at the road's far end. The far end is the end
that the castle is not reached from first.

**The woodcutter and the stonecutter.** The mission start (`0x41f2d2`) creates a building's settlers inside the finished building, so a
woodcutter's or a stonecutter's finished-building entry stands his worker in the building and the `settlers` list has no entry for
either. The stonecutter's goes on at `0x43cfdd` into the search for a heap, state `0x15` ([Stonecutting](stonecutting.md)), and the
forester's, a settler without a job made the forester, at `0x43d009` into the search for a node, state `0x13` ([Forestry](forestry.md)). The
entry takes the settler through the entering of `0x4398ea`. The hut's status gets bit 6, which
means it has its worker, and loses bit 7, which means a worker is on the way. The hut's worker is the settler. The
settler stands on the hut's node, and the node's settler word names him. The hut asks for no second worker, because
`0x42d945` tests bits 6 and 7 of the status. The engine refuses the entry where a settler stands on the hut's door or on its flag.

At his next turn the door routine (`0x43c96b`) lets him out into the search for a tree
([Woodcutting](woodcutting.md#the-start-at-the-hut)), which is state `0x12`. The walk would leave some fields that no
later routine reads before it sets them, such as the heading. Those fields hold the castle's values.

**The carrier.** The entry names the road by one of its flags and the direction. The road is a land road with no
carrier or carrier on the way, and land roads join it to the castle. The settler takes these fields
([Carriers](carriers.md#the-carriers-walk)):

- Job 0, state `0x42`, and the node next to the far flag.
- The timer `0x3f` and the frame `0x6e` plus the direction the road leaves that flag.
- The low byte of the stamp, which is that direction less 6.
- The mode, which is that direction, and the target, which is that flag's slot.
- No settler word on the node.

Both ends of the road count one carrier in the low nibble of their road bytes, and bit 7 is clear (`0x4398ea`). A good
at either end of the road then sends him on at his next turn (`0x42ff81`).

## Goods

`{"x", "y", "place": 0 to 7, "good": 1 to 25}` puts the good on the place of the flag at the node, as `Flag_PutGood`
(`0x436b46`) does. The place's destination is cleared and the flag's goods bit is set. The next turn of the flag's
goods then gives the good a destination and a direction ([Goods at a flag](goods.md#the-turn-of-a-flags-goods)).

The original's put takes the first empty place and checks nothing. The layout names the place. The engine refuses a
node with no flag, a place outside the flag's 8 places, and a place that holds a good. The engine does not port the
player block's counter of goods on flags (`0x436b46`).

## Finished buildings

`{"type": "woodcutter" | "stonecutter" | "forester" | "sawmill", "x", "y"}` in the `buildings` list stands a finished building
of that type on the node, with its flag south-east of it. The entry state is the building the original's mission start
leaves: `0x41f2d2` places it through `Building_Place` (`0x449a63`, at `0x41f2f6`) and finishes it ([Build
sites](build-sites.md#the-four-types)). The placement and its refusals are those of the building site, since the evaluator
judges the node the same way, and a node that holds no site for the type's size, or no room for the flag, fails the load with
the entry named. A type that M4 does not build (the storehouse, the castle, any other name) and an entry with no type fail the
read, naming the types. **Stand-ins:** the finished building has no status bit 4 and no pointer in its item, and its progress
word is 0, so the digests of the M3 layouts stay (`Game.PlaceFinishedBuilding`). Its object code is 2 for the three small
types, following the size-table caller `0x42cf9d` of `Building_Place`, where the mission start writes 2 only for type `0xb` and
3 for every other type. The woodcutter's worker is the castle's woodcutter, and the stonecutter's the castle's stonecutter, walked in as above, where the mission
start creates a new settler. The forester's worker is a settler without a job that the castle makes the forester (the request's second way, no tool,
[Forestry](forestry.md)), walked in the same way. The sawmill stands with no worker inside: its worker comes through the building's worker request as the game runs
(`CastleRoster.HandleBuilding`, [Sawmilling](sawmilling.md#the-building-and-the-job)). A player's command places a building
site and never a finished building.

## Not supported

Some settlers have no entry state here. These are every other state of a settler, every other job, and custom castle
contents: mid-walk, mid-cut, and in a building other than the woodcutter's hut, the stonecutter's, or the forester's, and a woodcutter, a stonecutter, or a forester apart from his finished-building entry. The file reader refuses the entry.

## In the engine

`Layout.Placement.cs` reads the `buildings`, `settlers`, and `goods` keys. `Game.PlaceFinishedBuilding` stands the finished building
(`BuildingPlacement.PlaceFinished`). For a woodcutter, a stonecutter, or a forester the entry then calls `WalkBehavior.StandWorkerInHut` (`Game.Walks`), the entry state of
the settler in his hut.
`CarrierBehavior.PlaceCarrierOnRoad` and `GoodsTransport.PlaceGood` are the carrier's and the good's. A settler for a road that the layout has not built before it fails the
load with the entry named. A good on a flag that does not stand fails it the same way.

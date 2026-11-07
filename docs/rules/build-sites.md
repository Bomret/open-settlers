# Build sites

This note records how the program judges a node of the map when the player points at it: whether a flag, a
building, or the first castle may be put there. The click handlers, the build marker on the screen, and the
computer players all ask the same routine. Addresses and routine names are in the
[Program index](program-index.md#castle-and-build-sites); the castle itself is in [Castle](castle.md).

## The evaluator

**Status:** confirmed for the routine and its results, **inferred** for the names of the result values.

`BuildSite_Evaluate` at `0x44b6e6` takes the node in the player block (column at `+0xfc`, row at `+0xfe`),
clears the two result bytes at `+0x100` and `+0x101`, and leaves its judgment there, with a height at `+0x102`.
The engine calls the three values the node state, the site, and the height (`BuildSiteResult`). Two helpers hold
copies of the middle of the routine (`0x44ba52`, `0x44bd72`); they give the same results, entered from other
callers, and the engine ports the one routine.

It reads the 37 nodes that lie within three steps of the node, in the order of the spiral table: the node
itself, its six neighbors (east, south-east, south, west, north-west, north), the 12 nodes of the second ring,
and the 18 of the third. The routine at `0x41f6a5` turns the spiral table into the offsets of those nodes once
at the start of a game. **Emulated:** the harness (`site:N:S0,S1,S2`) ran the evaluator on every node of nine
maps, the sizes 1 to 3 for three seeds each, and the engine's results match on every node
(`tests/OpenSettlers.Tests/build-sites.txt`). The command `castle:N:S0,S1,S2:L:x,y:sites` places a castle and
runs the evaluator again on every node for the player who now owns territory; five such maps match as well
(`castle-sites.txt`). The nodes of the castle and of its flag are left out of both comparisons, because their
objects take the flag and building branch that the engine does not hold.

Each node holds, among others, these bytes: the flag byte (record A byte 0), the height byte (byte 1: the low 5
bits are the height, the top 3 the owner), the terrain byte (byte 2: the upper triangle in the high nibble, the
lower in the low nibble), and the object byte (byte 3: the low 7 bits are the object code). The owner of a node
is the player's number plus 4, or 0 for a node nobody owns. The class of an object code is the size byte of
the program's table (`0x41e640`, read from `Siedler1.exe` at run time by `ProgramTables`): the code 1 is a flag (class 3), the codes 2 to 4 are buildings (classes 4, 5,
and 6, the castle), the generated trees and stones are classes 1 and 2, and no other object is a class 3 or more.

## The node state

**Status:** confirmed.

The node must be owned by the asking player: its owner bits equal the player's number plus 4 if the player's
castle stands, and 0 if it does not. Otherwise nothing is judged and all three values stay 0. A node whose
object is a flag or a building is judged from the flag and building tables; the engine holds none yet and gives
0 for those nodes.

For a node with no such object, the node state is:

- 4 if the node has any of its low six flag bits set, unless those bits are exactly 2 or exactly 0x10, in which
  case nothing is judged;
- else 5 if a flag stands on the south-east node (its object class is 3);
- else 7 if the flag byte of the south-east node has no low six bits set;
- else 6.

A node whose own object is a tree or a stone (class 1 or 2) ends here with the site 0.

## The site

**Status:** confirmed for the conditions; the meaning of the site values is **inferred** from where they are
used (`0x442244` puts a flag where the site is not 0 and the node state is 4, 6, or 7: **emulated** by the harness verbs
`flag` and `castle` with sites on the pairs 6.0, 6.1, 7.0, 7.1 to 7.5 and 0.0, none of the node states 4 and 5 occurs
on those maps, so those two are read from the routine only; `0x442630` puts the first
castle where the node state is 7 and the site is 5).

The conditions follow one another, and the judgment ends, with the values reached so far, at the first one that
fails. A terrain code is water when its low two bits above bit 1 are clear (codes 0 to 3). The triangles named
are those of the node's own tile (upper and lower), the lower of the west tile, both of the north-west tile, and
the upper of the north tile: the six triangles around the node.

1. The node's own object has class 0 (otherwise the judgment ended with the node state, site 0).
2. At least one of the tested triangles is not water: both of the node's own tile, the lower of the west tile,
   both of the north-west tile, and the upper of the north tile.
3. A flag on a node of the first ring ends the judgment for node state 4. With no flag there and a player with
   a castle, the site becomes 1, and a node of state 4 ends the judgment here.
4. No node of the first ring holds a building (class above 3). Unless the node state is 5, the south-east node
   holds no object at all.
5. None of five nodes holds a flag: the east and south neighbors and the second-ring entries 7, 8, and 14 of
   the spiral.
6. The upper triangle of the east tile, the lower of the south tile, and both triangles of the south-east
   tile are not water, and all six neighbors have the node's owner.
7. The six triangles around the node are each plain land (codes 4 to 7), or at least one is mountain (codes 11
   to 14), or at least one is another kind (water, shore, snow). Another kind ends the judgment. Mountain ends it
   with the site 2 for a player with a castle (the decompiler's listing of this store shows 0; **Emulated:** the
   harness reads 2 on every such node). Plain land only lets the site rise to 3 for a player with a castle.
8. Each neighbor of the first ring has the class 0, 1, or 3, and each of the 12 nodes of the second ring has a
   class of 4 or less.
9. The node's own tile and the north-west tile hold code 5 in both triangles, the west tile's lower triangle and
   the north tile's upper triangle hold code 5 (the plain meadow of the generator).
10. The heights of the 12 nodes of the second ring differ by less than 9 (the largest minus the smallest).

If every condition holds the site is 4, or 5 for a player without a castle, and the height is the weighted mean
of the node (counted twice) and its six neighbors, divided by 8 and then kept between the second ring's largest
height less 4 (not below 0) and its smallest plus 4, and at least 1.

**Not ported (`simplicity:`):** a building of code 3 within the third ring widens the range of heights in step 9
through its own height field (condition 10). No building of that code exists before the construction slices. The evaluator also
sets two bits of the player's flags that tell the screen where to draw its marker; they change no result.

## Placing a building

**Status:** confirmed for what `0x449a63` writes and for the node states it takes; the click that reaches it is **open**.

A building comes with its flag. `0x449a63` takes a slot of the building table and, unless the node state is 5, a slot
of the flag table. Where the node state is 5, a flag stands on the south-east node and the building takes it. Otherwise
the routine writes a new flag on the south-east node as the flag placement does ([Flags](flags.md)): the flag object,
the flag bit, the deposit moved to the neighbors, and the new slot as the node's link. The player then joins that flag
to the others with a road.

**The node states.** `0x449a63` has two callers, and no player's click is one of them: the placement at the start of a
mission (`0x41f2d2`), which takes the node as the evaluator leaves it, and the end of a building's timer (`0x42cf9d`),
which judges the node again and places a building only where the node state is 5, 6, or 7. So the program places a
building on a node of state 5, 6, or 7. The building buttons of the game view (actions 2, 3, 4, `0x11`, `0x12`, and
`0x17` of `0x440d23`) only open the building panels, and the click on a panel's building is not recovered.

**Node state 6: the flag node holds a road.** The state means the south-east node (the building's flag node) has road
bits and no flag. `0x449a63` writes the flag there like any other, then, where the node state is 6, calls the split of
the road on that node (`Road_SplitAtFlag`, `0x4662e2`), as `Flag_Place` does for a node state of 4
([Roads](roads.md#a-flag-on-a-road)). The building's flag therefore stands on the road and cuts it into two roads. The flag's item also takes bit `0x40`
at `+4`, which a placement of any building sets. **Confirmed, emulated:** `building-state6` in `road-splits.txt`
places a building of type 2 on the node 17,5 whose flag node 18,6 is the middle of a road of 4 steps: the road splits as
a flag put there by the button would split it, the node's flag byte takes the building's bit `0x10`, and the new flag
is slot 3.

## In the engine

`OpenSettlers.Simulation.BuildSite.Evaluate` (`src/Simulation/BuildSite.cs`). The engine departs from the program
in two places: a node with a flag or a building object gives 0, 0, 0, and the code 3 height term is left out
(above). `BuildSiteResult.AllowsFlag` is the flag button's rule; the flag placement is in [Flags](flags.md). `Game.PlaceBuilding`
places a woodcutter's hut on a site of 3 or more with the node state 5 (a flag with no building), 6, or 7. It puts the
flag down for the node states 6 and 7, and for 6 it splits the road under the flag (`Game.SplitRoad`) before it sets the
hut's mark (bit `0x10`) at the flag, so the mark is never read as a road bit. The click's map bytes and the flag items
are those of `building-state6` (`RoadSplitTests`).

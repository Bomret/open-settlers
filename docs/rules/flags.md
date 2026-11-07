# Flags

This note records where the program lets the player put a flag, what the placement writes, and how a flag is
drawn. Addresses and routine names are in the [Program index](program-index.md#castle-and-build-sites); the
judgment of a node is in [Build sites](build-sites.md); the table the flags live in is in
[Item tables](item-tables.md).

## Where a flag may go

**Status:** confirmed.

The flag button (`0x442244`) runs the build-site evaluator on the node under the cursor and goes on to the
placement (`0x4422c5`) where the site is not 0 and the node state is 4, 6, or 7. Otherwise it asks for the error
sound and changes nothing. A node state of 4 is a node on a road, and the placement then splits the road
([Roads](roads.md#a-flag-on-a-road)). The placement itself checks nothing more about the node: the evaluator holds every
condition, among them the spacing to other flags and objects:

- A node that holds a flag, a building, a tree, or a stone gets no site, so no flag goes on it.
- A flag on any of the six neighbors keeps the site at 0 for a player with a castle, so two flags never stand next
  to each other. A building next to the node leaves the site at 1, so a flag may stand beside a building.
- The node must belong to the player, and its flag bits must not be exactly 2 or exactly 0x10 (the bits of a
  building's node and of a flag's node).
- At least one of the six triangles around the node is land.

**Emulated:** the harness verb `flagput:N:S0,S1,S2:L:cx,cy:x,y;...` places the castle at `cx,cy` and then runs the
button (`0x442244`, through the placement to its end) on each listed tile in turn, keeping the map between tiles.
`FlagPlacementTests` replays 329 tiles of four castles on three maps (`flag-placements.txt`) and matches every
outcome, every changed byte, the link, and the total of resource 1. The tiles run along the map's edges, so the
placements and the spacing checks cross the wrap on both axes; the deposit moves below cross the wrap in rows.

**Departure.** Before the castle stands the original's button still places a flag on the first castle's sites
(emulated by `flag:`, node state 7 and site 5). The engine refuses every command but `PlaceCastle` before the
castle, as [Castle](castle.md#where-the-castle-comes-from) states.

## What the placement writes

**Status:** confirmed for the writes, emulated.

For a node the evaluator allowed:

1. The placement takes the first free slot of the flag table (`Table70`, allocator `0x45d7ad`). A full table ends
   the placement with no change. The slot number is the flag's identity; the castle's flag took slot 0.
2. The node's flag byte takes the bit 0x80 (ORed in), and its object byte takes the code 1, a flag, and keeps bit 7
   (the water mark).
3. The node's resource byte held a deposit or a lake detail. Its value moves to the nodes around, in the order east,
   north-east, north, south, south-east (`0x449f58` with `0x44a0aa`; the node itself comes fourth in the walk and
   holds the flag by then): a node whose object code is 1 to 4 takes none; an empty byte takes all of it; a byte of
   the same type (the top 3 bits) adds the amount up to 31 and passes the rest on; a byte of another type is
   skipped. What no node takes is lost, and the total of resource 1 (state `+0x4c`) falls by it when the deposit is
   of type 1. A lake detail is a byte of type 0, so it merges with another type-0 byte in the same way.
   **Inferred:** a byte with type bits and amount 0 still moves to the first empty neighbour (the first call of
   `0x44a0aa` runs with a carry of 0), and the move crosses the column wrap; both are read from the decompile, no
   emulator run backs them.
4. The slot number is stored as the node's link, in the byte the resource layer shares (the same byte as step 3).
5. **A road under the node.** Where the evaluator's node state is 4, the placement ends with `Road_SplitAtFlag`
   (`0x4662e2`): the node's road becomes two roads that end at the new flag, the new flag's item takes the two roads'
   bits, lengths, links, and carriers, and the two old end flags link to the new flag. The flag button, a click in road
   mode with the second pointer button held, and a building whose flag node holds a road all reach it. **Confirmed,
   emulated;** every field and the carrier of each half are in [Roads](roads.md#a-flag-on-a-road).

The flag table's item is cleared to zero and the placement fills no field of it itself; the road commit ([Roads](roads.md))
fills the road bits and the links to the other flags. Where the node holds road bits (node state 4), step 5 below does. **Inferred (not read from the allocator's limit):** the table has a slot limit (state
`+0x258`) that `Game_Init` (`0x41dbea`) computes from a division of a value it reads at start-up. It is not
recovered, and the harness sets 100. The engine holds 256 flags, the most a one-byte link names. A flag past that
is refused (`simplicity:` ceiling).

The placement sets **no border bits.** `0x45f402` (the two border bits of a new flag from the owners around it)
runs from the castle placement only (`0x442704`); the flag button's path never reaches it, and the emulated map
bytes show no change to a neighbor's flag byte. The castle's flag keeps the bits 0x90 as [Castle](castle.md) states.

`0x4617fd` is not a placement. It takes a building down: it clears the building's bits on its node and the
flag's, sends the worker home, releases what waits for it, and takes the flag down where no road leaves it
([Removal](removal.md#taking-a-building-down)); a flag is taken down by `0x462153`
([Removal](removal.md#taking-a-flag-down)).

`0x4422c5` has two more callers, both in the road click step (`0x443f62`, inside the click handler `0x440c24`): it
places a flag, without the button's check, on a node of a road or on an empty node when the second pointer button is held
([Roads](roads.md#the-pointer-button-that-places-a-flag-on-a-road)). Those calls judge the node with the evaluator first
and place a flag where the site is not 0; unlike the button they ask for no node state in particular.

## How a flag is drawn

**Status:** confirmed for the entry, **inferred** for the colour.

The row routine (`0x44d5b4`) hands an object of code 1 to `Render_DrawFlag` (`0x44db26`). It draws the flag with
the object number 0x80, plus the animation frame (the frame counter shifted right by 3, 2 bits), plus the node's
object byte masked with 0xc0 and shifted right by 4; `Gfx_DrawObjectSprite` (`0x44da4d`) adds 1250 and draws
through the same position rule as every object ([Map objects](map-objects.md#where-the-sprite-sits)). With frame 0 and the top bits
clear the entry is 1378, a sprite of 16 by 19 pixels in the install's `loca/SPAD.PA`. The routine also draws, per
road direction, the goods that wait (the bytes at `+0xc` to `+0x13` of the flag's item); the road itself is a strip
drawn with the terrain ([Roads](roads.md#how-a-road-is-drawn)).

**Inferred:** the top bits of the object byte pick the colour of the flag. A flag on a node whose object byte has
bit 7 set (a water triangle) would draw entry 1386 in the original; the engine always draws 1378.

## In the engine

`CommandKind.PlaceFlag` takes the node in `Command.X` and `Command.Y`. `Game` judges it with
`BuildSite.Evaluate` and `BuildSiteResult.AllowsFlag` for the player with a castle, and `FlagPlacement.Apply`
(`src/Simulation/FlagPlacement.cs`) writes it. `Game.Flags` lists the flags by table slot (`Flag(X, Y)`); the
castle's flag is the first. The digest holds the flags and the total of resource 1. A refused command changes no
state. The map view's stand-in flag button (`src/Viewer/MapViewControl.cs`) turns a click into `PlaceFlag`;
`ObjectSprites.FlagEntry` and `ObjectDraws` draw the flags with the objects. Departures: the table limit, the
border bits that are not set anyway, the colour bits, the refusal before the castle, the goods that wait, and the
split of a road under the new flag (the engine's placement leaves the road's items alone: [Roads](roads.md#a-flag-on-a-road)).

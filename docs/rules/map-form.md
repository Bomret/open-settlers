# Map form and the generation passes

This note records the map sizes, the layout of the map in the original's memory, the canonical form the
engine and the emulator harness both write for comparison, and the generation passes that fill the heights,
the terrain, the objects, and the resources. It covers `Map_SetGeometry`, `MapGen_InitParams`, the seeding of
`MapGen_Generate`, and the passes `MapGen_ClearMap` to `MapGen_CountResource1`. The minimap build and the
progress bar are not covered. Addresses and routine names are in the
[Program index](program-index.md#map-generation).

## Map sizes

**Status:** confirmed for the sizes, inferred for the set.

A map has a size value. `StartScreen_StartGame` at `0x4685bc` takes the value from `Game_SetStartParams`
at `0x468717` and starts the map at 32 columns and 32 rows. For each step of the value above 1 it doubles
the columns, then the rows, then the columns again (`0x4685da` to `0x468603`). **Emulated:** the harness
ran those instructions: value 1 gives 32 by 32 tiles, value 2 gives 64 by 32, and value 3 gives 64 by 64.
A value of 0 would count down through 65535 steps, so it is no size.

`Game_SetStartParams` hands on 3 for a mission, and for any other start the free-game size value at state
`+0x362`. The program sets that value once, at start-up (`Game_InitSettings` at `0x424fca`): the low 4 bits
of the memory class at `+0x30c`, at most 3. The save loader lowers it to the memory class when the saved
value is higher (`0x4604ed`). **Inferred:** the original offers exactly the sizes 1, 2, and 3, because no
click handler of the free-game page writes `+0x362`, and the memory class of a normal start is at least 1.
The engine refuses any other value with a message and generates nothing.

## The original's layout

**Status:** confirmed by the emulated passes.

`Map_SetGeometry` at `0x42195c` derives masks and step sizes from the columns and rows. A tile is 4 bytes,
and the buffer holds two records for each tile: record A at the tile's column times 4 within its row, and
record B 4 times the columns further on in the same row. A row is therefore 8 times the columns long.
The offsets of the six neighbors are stored as masked values, so adding one to a tile offset and masking
wraps the column and the row on their own: the map wraps at its edges in both directions. The engine
keeps a column and a row and wraps them with the same effect.

The passes of this note use these bytes of a tile: byte 0 of record A (flags), byte 1 (height), byte 2
(terrain, two nibbles), byte 3 (a working mark of `MapGen_KeepMainLand`, zero before and after it, and from
the object passes on the object code), and byte 0 of record B (lake detail, and from the deposit passes on
the resource deposit). Bytes 1 to 3 of record B stay 0 through the whole generation (the harness checks it
after every pass).

The six neighbors of a tile, in the order the passes visit them: east, south-east (one column east and one
row down), south (one row down), west, north-west (one column west and one row up), and north (one row up).

## The parameters

`MapGen_InitParams` at `0x421530` stores the water level 20 and the lake radius 14 first, then clears other
generator fields. **Partial:** only those first two stores are ported and emulated; the clearing of the other
fields is not run. **Inferred:** the passes of this note read only those two values.

## The canonical map form

**Status:** the engine's own form (a stand-in for comparison; the original has no such form).

The canonical form is a list of layers in a fixed order. A layer is one byte for each tile, in row-major
order: the tile in column `x` and row `y` is entry `y * columns + x`. The layers of this note:

| Number | Layer | Source in the original |
| --- | --- | --- |
| 0 | `flags` | byte 0 of record A; bit 6 marks the middle of a lake |
| 1 | `height` | byte 1 of record A; the low 5 bits are the height, and the top 3 bits stay 0 through the generation (a castle sets them, [Castle](castle.md)) |
| 2 | `terrain` | byte 2 of record A; the upper triangle in the high nibble, the lower in the low nibble |
| 3 | `lake` | byte 0 of record B; a draw from 0 to 7 where a lake has its middle |
| 4 | `object` | byte 3 of record A; the low 7 bits are the object code (0 for none), bit 7 marks a tile with a water triangle |
| 5 | `resource` | byte 0 of record B; the high 3 bits are the resource type (0 for none), the low 5 bits the amount |

Layers 3 and 5 are the same bytes: the original keeps the lake detail and the resource deposit in one byte,
so after the deposit passes the `lake` digest equals the `resource` digest, and a deposit ring tile that
holds a lake detail is judged by it as a deposit. A deposit lands on mountain terrain and a lake detail
lies on water, so the two meet only where a ring reaches a lake middle. The flag byte (layer 0) takes bit 6
from `MapGen_ResolveObjectOverlap` as well: it marks a tile that carries a large object.

The digest of a layer is the FNV-1a 64-bit hash of its bytes in that order. The digest line after a step
names the size value, the map seed words, the step, the three generator words, and the digests of the
layers in layer order; after `CountResource1` it ends with the total of resource type 1. The engine's
`Map.Digest` and the harness command `gen` compute the same digests from the same layout. The steps are the
seeding (`Seed`, which has the generator words only, because the buffer holds nothing the generator wrote)
and the passes `ClearMap`, `SeedHeights`, `SubdivideHeights`, `LimitSlopes`, `FillLakes`,
`LowerToWaterLevel`, `ClassifyTerrain`, `KeepMainLand`, `ScaleHeights`, `GradeWaterDepth`,
`SetShoreTerrain`, `PlacePatches`, `BlendPatchEdges`, `MarkPeaks`, `ScatterObjects1` to `ScatterObjects15`,
`PlaceDeposits3`, `PlaceDeposits2`, `PlaceDeposits1`, `PlaceDeposits4`, `ResolveObjectOverlap`,
`MarkWaterAdjacent`, and `CountResource1`.

**How later layers join.** A layer that a later pass adds, such as objects or resources, takes the next
layer number and its own digest. The layers above keep their numbers, their meaning, and their digests
after every step of this note, so the digests pinned for these steps stay true. A later pass that changes
a layer above changes that layer's digest after its own step only. The generator words after a step cover
the number of draws, so a pass's draw count is part of what a digest line checks.

## The passes

**Status:** confirmed; every pass is **emulated** for the three sizes and three seeds in
`tests/OpenSettlers.Tests/map-digests.txt`.

Each pinned line is a harness line `EMU gen size=N pass=P rng=W0,W1,W2 flags=… height=… terrain=… lake=…
object=… resource=… [total=T]` rewritten as `N S0,S1,S2 P W0,W1,W2 D0 … D5 [total=T]`: the size value, the
seed words the `gen:N:S0,S1,S2` command was given (the harness line does not repeat them), the pass name,
the generator words, and the six layer digests in layer order without their names. The `Seed` line has no
digests.

1. **Seeding.** `MapGen_Generate` copies the map seed into the random generator and steps it twice. A pass
   draws the new first word of the generator. Two draws.
2. **`MapGen_ClearMap`.** Zeroes every byte of the buffer. No draw.
3. **`MapGen_SeedHeights`.** Walks the tiles in row-major order and, on every 16th column of every 16th
   row, draws once and stores the low byte of the draw, at most 250, as the height. One draw per such
   tile: columns times rows divided by 256.
4. **`MapGen_SubdivideHeights`.** Draws once and takes an amplitude from the low 7 bits plus 128, and a
   spread of 0.6 times that (the amplitude times `0x9999`, shifted right by 16). Then four sweeps with a
   half distance of 8, 4, 2, and 1 tiles. A sweep visits the tiles at multiples of twice the half distance,
   in row-major order, and for each sets three midpoints from the tile's own height and the height of the
   tile one full distance away: east, south, and south-east. Each midpoint draws once; its height is the
   mean of the two heights plus the draw times the amplitude divided by 65536, minus the spread, kept from
   0 to 250. After a sweep the amplitude and spread halve. Draws: 1 plus 3 times columns times rows times
   (1/256 + 1/64 + 1/16 + 1/4).
   **Static and emulated:** before the first midpoint of the pass the original's second height still has
   the high byte of the amplitude draw in its upper half, so the first midpoint's mean includes it. The
   engine keeps that word. After a midpoint the word holds the new height.
5. **`MapGen_LimitSlopes`.** Repeats a scan until it changes nothing. The scan visits the tiles in row-major
   order and for each compares the tile's height, read before its three neighbors are corrected, with the
   south, south-east, and east neighbor, in that order. A neighbor that differs by more than 32 is set to
   32 above or below the tile's height. No draw.
6. **`MapGen_FillLakes`.** For each level from 0 to 20, visits the tiles in row-major order. A tile whose
   height equals the level starts a lake when none of its six neighbors is higher than 20; otherwise its
   height becomes 0. A lake marks the tile 255 and its six neighbors 254, then grows ring by ring up to
   radius 14: a ring tile whose six neighbors are all at most 20 or marked, and one of which is marked
   255, becomes 255 and marks its neighbors 254; a ring that adds no tile ends the growth. At the end every
   mark above 253 within the radius drops by 2, so the lake tiles read 253 and the edge tiles 252. The
   pass then visits all tiles once more: height 0 becomes 21, a tile at 253 draws once and gets the lake
   detail (the draw's low 3 bits), flag bit 6, and height 19, and a tile at 252 becomes 20. One draw for
   each lake middle.
7. **`MapGen_LowerToWaterLevel`.** Subtracts 19 from every height, so that lake tiles read 0 and shore
   tiles 1. No draw.
8. **`MapGen_ClassifyTerrain`.** Sets the terrain of each tile from two sums. The upper triangle uses the
   heights of the tile, its south-east, and its south neighbor; the lower triangle uses those of the tile,
   its east, and its south-east neighbor. A sum maps to a terrain code: below 3 gives 0, below 384 gives 5,
   below 416 gives 6, below 448 gives 11, below 480 gives 12, below 528 gives 13, below 560 gives 14, and
   any other sum gives 15. No draw.
9. **`MapGen_KeepMainLand`.** Clears byte 3. Scans for the first tile with a height above 0 and floods from
   it through the six neighbors of every tile that has land in a triangle that touches the neighbor. If
   the flooded tiles times 4 are at least the number of tiles, every unreached land tile gets height 0
   and terrain 0, and the triangles around it lose their terrain, and the pass clears byte 3 again. If
   not, the scan goes on from the next tile. No draw. **Stand-in:** where the scan runs through the map
   twice without such a mass, the original scans without end; the engine refuses with an error.
10. **`MapGen_ScaleHeights`.** Replaces every height by its value plus 6, divided by 8, rounded down. No
    draw.

### Terrain detail, objects, and resources

**Status:** confirmed; every pass is **emulated** for the same sizes and seeds. `MapGen_PlaceTerrainAndObjects`
at `0x42336d` runs passes 11 to 25 in this order and `MapGen_Generate` runs the two after it. The base count
`P` of patches, clusters, and deposits is `Map_SetGeometry`'s columns divided by 32 times rows divided by 32
(1, 2, and 4 for the three sizes). Terrain is two nibbles: the upper triangle in the high nibble. A tile
position wraps at both edges.

11. **`MapGen_GradeWaterDepth`.** Three `MapGen_ReplaceTerrainNear` runs, each with a target, a near, and a
    replacement terrain: (0, 5, 3), (0, 3, 2), (0, 2, 1). No draw.
    `MapGen_ReplaceTerrainNear` (`0x42355f`) visits the tiles in row-major order. For a tile it reads the 3 by
    3 terrain bytes around it, rows north to south, then in order: the upper triangle, when its terrain is the
    target and a triangle of a fixed set in the neighborhood has the near terrain, takes the replacement;
    then the lower triangle, when it still is the target, likewise. The neighborhood is read before the
    tile's own change. The upper triangle looks at, with cells 0 to 8 in the order read, the lower and upper
    triangles of cell 0, the upper of 1, both of 3, the lower of 4, the upper of 5, the lower of 6, both of
    7, and both of 8 (`MapGen_HasTerrainNearUpper`, `0x423789`). The lower triangle looks at both of 0 and
    1, the upper of 2, the lower of 3, the upper of 4, both of 5, the lower of 7, and both of 8
    (`MapGen_HasTerrainNearLower`, `0x4238b2`).
12. **`MapGen_SetShoreTerrain`.** One run (5, 3, 4). No draw.
13. **`MapGen_PlacePatches`.** `P` times: up to 200 times draw a column, then a row (`MapGen_RandomPos`,
    `0x423aa3`: two draws, each the new first word masked with the columns or rows minus 1) until the tile
    holds terrain `0x55` in both triangles. Then for the 256 spiral indexes from 255 down to 0, around that
    tile: if both triangles of the tile and the lower triangles of its west and south tiles are 5 or 10, its
    lower triangle becomes 10; then, if both its triangles and the upper triangles of its east and north tiles
    are 5 or 10, its upper triangle becomes 10 (`MapGen_CanPatchLower`, `MapGen_CanPatchUpper`). A pass of 200
    misses places nothing.
14. **`MapGen_BlendPatchEdges`.** Runs (10, 5, 7), (10, 7, 8), (10, 8, 9), then `MapGen_ResetTypes7To9`
    (every nibble from 7 to 9 becomes 5), then (5, 10, 9), (5, 9, 8), (5, 8, 7). No draw.
15. **`MapGen_MarkPeaks`.** Every tile whose height, with its low 5 bits (a 32 reads as 0), is above 25 and
    not below its east, south-east, and south neighbors and above its west, north-west, and north neighbors
    gets object code `0x52`. No draw.
16. to 25 (15 passes). **The object scatter passes** (`0x424175` to `0x4246f4`), each calling
    `MapGen_ScatterObjects` (`0x424755`) with its own parameters. The count is `P` times a factor. A pass
    makes that many clusters: up to 100 times draw a random position until `MapGen_FitsTerrain`
    (`0x424802`) holds (the terrain of the tile's both triangles, the west tile's lower triangle, the north-west
    tile's both, and the north tile's lower, all from the low up to but not including the high terrain
    code), then for each of the cluster's objects draw once, masked with the pass's index mask, for a spiral
    offset around that tile (`MapGen_RandomSpiralPos`, `0x423f07`), and where `MapGen_FitsTerrain` holds and
    the tile has no object draw once more: the object code is the draw's low byte masked with the code mask,
    plus the code base. The parameters, in pass order, are the count factor, the index mask, the terrain
    range, the code base, the code mask, and the objects in a cluster:

    | Pass | Factor | Index mask | Terrain | Base | Code mask | Cluster |
    | --- | --- | --- | --- | --- | --- | --- |
    | `0x424175` | 8 | `0xff` | 5 to 6 | `0x08` | `0x0f` | 10 |
    | `0x4241e3` | 1 | `0x3f` | 5 to 6 | `0x08` | `0x07` | 45 |
    | `0x424247` | 1 | `0x3f` | 4 to 6 | `0x10` | `0x07` | 30 |
    | `0x4242ab` | 1 | `0x7f` | 5 to 6 | `0x08` | `0x0f` | 20 |
    | `0x42430f` | 1 | `0x3f` | 5 to 6 | `0x48` | `0x07` | 40 |
    | `0x424373` | 1 | `0xff` | 5 to 6 | `0x48` | `0x07` | 15 |
    | `0x4243d7` | 1 | `0xff` | 5 to 6 | `0x5c` | `0x00` | 2 |
    | `0x42443b` | 1 | `0xff` | 5 to 6 | `0x50` | `0x01` | 6 |
    | `0x42449f` | 1 | `0x7f` | 2 to 3 | `0x1c` | `0x03` | 50 |
    | `0x424503` | 1 | `0xff` | 5 to 6 | `0x53` | `0x00` | 5 |
    | `0x424567` | 1 | `0xff` | 5 to 6 | `0x54` | `0x01` | 10 |
    | `0x4245cb` | 1 | `0x0f` | 10 | `0x56` | `0x01` | 2 |
    | `0x42462f` | 1 | `0x7f` | 8 to 10 | `0x5a` | `0x01` | 6 |
    | `0x424693` | 1 | `0x7f` | 0 to 2 | `0x58` | `0x01` | 8 |
    | `0x4246f4` | 1 | `0x3f` | 10 | `0x18` | `0x03` | 6 |

    The terrain column names the codes that fit (the program stores the range as a low code and a code above
    the high one).
26. to 29. **`MapGen_PlaceDeposits3`, `2`, `1`, `4`** (`0x424a03`, `0x4249bc`, `0x424976`, `0x424a58`), each
    calling `MapGen_PlaceDeposits` (`0x424a9e`) for a resource type 3, 2, 1, or 4 with `9P`, `4P`, `2P`, and
    `2P` deposits on terrain 11 to 14. A deposit: up to 100 times draw a random position until
    `MapGen_FitsTerrain` holds, then draw once for the first amount (the draw masked with 12, plus 8: 8, 12,
    16, or 20), and fill rings of 1, 6, 12, 18, and 24 tiles from the spiral table, in index order from 0,
    each ring with an amount 4 less than the one before, stopping when the amount reaches 0 (from 20 the
    fifth ring has 4, so a sixth ring would get 0 and the walk ends first)
    (`MapGen_FillDepositRing`, `0x424be5`). A ring tile takes the deposit (type times 32 plus the amount) when
    its byte is 0 or its amount is below the ring's. **Site `0x424ad8`:** the position test reads the byte
    after the one the pass writes (byte 1 of record B, where the pass writes byte 0). No pass writes that
    byte, so the test holds and the engine leaves it out.
30. **`MapGen_ResolveObjectOverlap`.** For each tile whose object is large, in row-major order: if the flag
    bit 6 is clear on the west, north-west, and north tiles, set it on the tile; otherwise clear the object
    code and keep bit 7. A large object is one whose class byte in the program's table (`0x41e640`), read
    from the install's `Siedler1.exe` at run time by `ProgramTables`, is above 1, compared unsigned. No draw.
31. **`MapGen_MarkWaterAdjacent`.** Sets bit 7 of the object byte on each tile whose upper terrain nibble or
    lower terrain nibble is below 4. No draw.
32. **`MapGen_CountResource1`.** Sums the amounts of the deposits of resource type 1 into the total at state
    `+0x4c`. No draw.

**The spiral table.** `MapGen_SpiralPos` (`0x423b30`) adds the column and row offsets of an index of the
table at state `+0x80` to a tile. The program builds the table at start-up (`0x41e726`): entry 0 is the
tile, and each later group of six entries is the six turns `(x, y)` to `(x - y, x)` of a seed `(r, j)`. A
ring `r` has the seeds `j` from 1 to `r - 1`, the nearest to the middle of the ring (`|2j - r|` smallest)
first and the lower `j` first on a tie, then `j` = 0. The engine builds the first 256 entries, the most the
passes index. **Emulated:** their digest matches the program's.

**Draws.** A pass draws at least one value per random position, spiral offset, and object. Their count is
part of what the generator words after each pass check.

## In the engine

`src/Adapters/Simulation/MapSize.cs`, `Map.cs`, `MapLayer.cs`, `MapPass.cs`, `MapObject.cs`, `ResourceDeposit.cs`, and
`MapGenerator.cs`. A caller reads the objects through `Map.Objects` (a column, a row, and a code for each
tile with an object) or from `Map.Layer(MapLayer.Object)` with the mask `0x7f`, and the deposits through
`Map.Deposits` or `Map.Layer(MapLayer.Resource)`. The engine departs from the program in two places: it
keeps columns and rows where the program keeps masked offsets, and it throws where the program would scan
without end.

The harness command `gen:N:S0,S1,S2` in `tools/GhidraDump/EmulateMapStart.java` runs `MapGen_Generate`
for the size value N and the map seed words from the original's size code through the end of
`MapGen_CountResource1`, and prints the digest line after the seeding and after each pass. It skips the
progress bar. It fills the buffer with a pattern first, so that `MapGen_ClearMap` is checked. It builds the
spiral table with the program's builder and points the object size table at the program's data first.

## A hand-drawn map

**Status:** the engine's own form (a stand-in; the original has no hand-drawn maps). A layout file
([ADR 0009](../adr/0009-layouts-set-up-a-game-by-hand-drawn-map-and-commands.md)) draws the height, terrain, and
object layers. `src/Adapters/Simulation/Layout.cs` derives from them the bytes the generator derives. The map has one of the
sizes above, written as its size value in the layout's `size`.

**The grids.** `height`, `terrain`, `terrainLower`, and `lakes` are lists of strings. One string is one row of the
map and one character is one tile, in row-major order as the layers are. The upper triangle of a tile is the high
nibble of the terrain layer and the lower triangle is the low nibble. A layout with no `terrainLower` grid gives each
tile a lower triangle of the upper terrain.

**Lake marks.** `lakes` is an optional grid of `.` and `L`. Each `L` sets Flags bit 6 on its tile and each `.` leaves
it clear. A layout thus carries the generator's marks exactly, for the lake tiles and for the large objects of
`MapGen_ResolveObjectOverlap` alike. Where `lakes` is present, it replaces the derived rule below. Where it is absent,
the derived rule applies.

**Height characters.** `0` to `9` draw the heights 0 to 9 and `a` to `v` the heights 10 to 31. These are the 32 values
of the low 5 bits of the layer.

**Terrain letters.** One letter stands for each terrain code 0 to 15, in the order of the codes: `w x y z b g h i j k p
m n o q s`. The letters are the engine's own labels, because the original names no terrain in the program. They are
chosen where this note and [Build sites](build-sites.md) give the code a role:

| Letter | Code | Role |
| --- | --- | --- |
| `w`, `x`, `y`, `z` | 0 to 3 | water, from the deepest to the shallowest (`MapGen_GradeWaterDepth` grades 0 to 3 by the distance from land) |
| `b` | 4 | shore (`MapGen_SetShoreTerrain`) |
| `g`, `h` | 5, 6 | plain land; 5 is the plain meadow the generator and the building rules use |
| `i`, `j`, `k` | 7, 8, 9 | the codes the patch passes hand through; `MapGen_ResetTypes7To9` turns 7 to 9 back to 5 |
| `p` | 10 | the patch of `MapGen_PlacePatches` |
| `m`, `n`, `o`, `q` | 11 to 14 | mountain |
| `s` | 15 | the highest code, drawn for the highest sums of `MapGen_ClassifyTerrain` |

**Objects.** `objects` lists `{x, y, code}`. The code is an object code from 1 to 127, and a tile holds at most one.

**Derived bytes.** The loader sets these bytes as the generator does. A rule that reads them thus sees the same bytes
for a hand-drawn tile as for a generated tile of the same terrain:

- Bit 7 of the object layer on each tile with a water triangle (a terrain nibble below 4). This is the rule of
  `MapGen_MarkWaterAdjacent` (`0x4232ef`). The object code of the tile keeps its low 7 bits.
- Bit 6 of the flags layer on the middle of a lake, where the layout has no `lakes` grid. This is the mark
  `MapGen_FillLakes` (`0x422a0e`) sets.

The generator marks a lake middle from a state the layout does not draw. Its lake tiles, the ring around them, and
the land `MapGen_KeepMainLand` drops all end at height 0. The loader therefore marks a tile when its height is 0, its
two triangles are water, and its six neighbors have height 0. The shore ring of a drawn lake has a neighbor above
height 0 and stays unmarked.

The rule was checked against 150 generated maps, redrawn from their heights and terrain, on the tiles that hold no
object. It marks every tile the generator marks. It also marks tiles at height 0 that the generator leaves unmarked,
because the layout cannot draw what tells them apart. Such a tile carries the flag where a generated tile of the same
height and terrain does not. The `lakes` grid matches the generator exactly.

**Not derived.** The lake detail (layer 3) stays 0. The generator draws it, and a hand-drawn map leaves the game's
random draws alone. Bit 6 of the flags layer for a large object (`MapGen_ResolveObjectOverlap`) is not set by a rule,
because it needs the program's class table. A `lakes` grid marks it by hand. The resource layer is empty, so the total
of resource 1 is 0.

**The random generator.** The layout's `seed` is in the free-game screen form, as `--seed` takes it. It sets the
game's random generator as `MapGen_Generate` sets it: the map seed words (`Seed.ToMapSeed`) stepped twice, as in the
seeding step above. A hand-drawn map draws nothing from the generator, so the game starts from that state.

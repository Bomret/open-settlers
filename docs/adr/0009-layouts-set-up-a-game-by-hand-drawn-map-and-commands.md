# 9. Layouts set up a game by a hand-drawn map and commands

**Status:** Accepted

A layout is a JSONC file that sets up a game in the game view. It draws the whole map by hand: a height
grid, an upper and a lower terrain grid, and the objects with their object codes, at one of the sizes the
original offers. The loader writes the map layers and sets the bytes that the generator derives from
others, such as the water bit of the object layer, from what the grids draw. The layout's seed starts the
game's random generator. The castle, flags, roads, and buildings are commands the loader applies one by
one in the file's order, through the path a click takes; a refused command fails the load. A layout
places a settler or a good only in an entry state ([INV-0005](../invariants.md#inv-0005)), and a placed
settler is one of the castle's own.

## Considered options

- A generated map from a seed, with the layout placing on it: placements depend on where the seed puts
  hills and trees, and earlier play runs broke on castles on hills, huts on trees, and roads blocked by
  trees.
- Shapes (fills, rectangles, circles) for the terrain: shorter files, but they cannot draw every map, and
  overlaps need order rules. Shapes can come later as a shortcut that fills the grids.
- One terrain letter per tile: the original keeps a terrain per triangle, so the format would change later.
- Settlers in any state, written field by field: a hand-set combination of the original's settler bytes
  can be one the rules never produce.
- Settlers added beside the castle's own: the hut still calls the castle's worker, and the castle's count
  disagrees with the map.
- Clicks in a play script: fragile coordinates and game time spent on setup.

## Consequences

- A play run reaches the moment under test without clicks, and the `fast-forward` play step ticks
  without drawing until a settler's state holds.
- The map a layout draws is no map of the original's generator; the rules that run on it are the
  original's.
- Custom castle contents, resource deposits, and shapes are later extensions of the format.

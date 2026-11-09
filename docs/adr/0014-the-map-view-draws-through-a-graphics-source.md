# 14. The map view draws through a graphics source

**Status:** Accepted

Amends [ADR 0007](0007-the-engine-draws-the-map-on-the-gpu.md): the palette is no longer the one source of
color.

The map view draws through a graphics source. The draw lists name what is drawn in game terms: a terrain
triangle by its terrain, its side, and the heights of its three nodes; a map object by its kind and its
animation step; a settler by its job, activity, heading, animation step, player, and carried good; a road.
Draw order and placement stay shared. A source turns each term into an RGBA image with an anchor point, or
into nothing for an effect it does not draw.

The History Edition source keeps the original's encoding inside itself: entry numbers, archives, the slope
and mask tables, the program's figure tables, and the palette, which it applies when it loads an image.
The shaders sample RGBA images.

## Considered options

The decision weighed these options:

- Each source builds the whole draw list from the game: the History Edition builders would move behind
  the seam unchanged, but every other source would write its own draw order and placement.
- Keep the 8-bit palette in the seam: the shaders stay as they are, but every source would need a
  palette and indexed images, limited to 256 colors.
- Keep entry numbers in the seam and let another source supply an image per entry: another source would
  have to speak the original's encoding.

## Consequences

The decision has these consequences:

- No class of the map view outside the History Edition source reads the install, an archive, or an entry
  number.
- Converting palette indexes at load gives the same colors as the shader lookup, because the engine
  never changes the palette while a map is shown.
- The simulation does not depend on the graphics source.

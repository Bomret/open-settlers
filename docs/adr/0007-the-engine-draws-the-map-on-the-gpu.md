# 7. The engine draws the map on the GPU

**Status:** Accepted

**Amended by:** [ADR 0014](0014-the-map-view-draws-through-a-graphics-source.md)

The engine draws the map's terrain and objects with Godot on the GPU. It does not copy the original's
software drawing. The terrain triangles are meshes. The original's fill masks and textures, and the
object sprites, are textures that hold palette indexes. A shader looks each index up in the palette, so
the palette stays the one source of color.

The project plans to add zoom, high window resolutions, and maps larger than the original's. The
original draws pixel by pixel into a buffer of a fixed size ([graphics images](../rules/graphics-images.md)),
and that approach would have to be replaced to add any of the three.

## Considered options

The decision weighed these options:

- Copy the original's software drawing: draw palette indexes into a screen-sized buffer with the
  original's masks, then convert the buffer to colors each frame. It reuses the M1 readers as they are
  and matches the original's look exactly. It fixes the resolution and the map size, and zoom would
  need a rewrite.

## Consequences

The decision has these consequences:

- The engine rebuilds the shape of each terrain triangle from the masks, instead of blitting them.
- A palette change reaches every drawn pixel through the shader lookup.
- Zoom, larger resolutions, and larger maps need no change to the drawing approach.

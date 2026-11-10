#!/usr/bin/env python3
"""Writes the terrain and road art of the Open Settlers content set: content/open-settlers/manifest.json and its PNGs.

The art is the project's own: a flat color per terrain code and a plain strip per road direction, drawn here from
nothing but numbers. It holds nothing derived from an install (ADR 0015). Run it from the repository root:

    python3 tools/open-settlers-art.py

The terrain triangles are 32 pixels wide and 20 high, the node spacing and row spacing of the map. An upper triangle
hangs from its apex at the top, a lower triangle from its apex at the bottom. Each triangle is 2 pixels wider on each
side than the geometry, so neighbors overlap and leave no seam, and a 12-row skirt of its color below it, which the
rows painted after it cover. The skirt closes the gaps a slope opens. The images are flat: they ignore the slope.
"""

import json
import os
import struct
import zlib

OUT = os.path.join("content", "open-settlers")
WIDTH = 32
HEIGHT = 20
BLEED = 2
SKIRT = 12

# One color per terrain code, 0 to 15: water, plain land, in-between ground, mountain, snow.
TERRAIN_COLORS = [
    (30, 70, 150), (34, 80, 160), (40, 90, 170), (46, 100, 180),
    (90, 160, 60), (84, 150, 56), (100, 170, 70), (76, 140, 52),
    (96, 120, 80), (200, 180, 110), (150, 130, 90),
    (130, 110, 90), (120, 100, 85), (110, 95, 80), (100, 90, 80),
    (235, 240, 245),
]
ROAD_COLOR = (150, 110, 60)
STRIP = 5


def png(path, width, height, rows):
    """Writes an RGBA PNG; rows holds one bytes object of 4 * width bytes per pixel row."""
    raw = b"".join(b"\x00" + row for row in rows)

    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body))

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


def triangle(color, upper):
    """The triangle's rows, then SKIRT full rows below it, on a canvas BLEED wider on each side."""
    canvas = WIDTH + 2 * BLEED
    rows = []
    for y in range(HEIGHT + SKIRT):
        if y < HEIGHT:
            half = (WIDTH // 2) * ((y + 1) if upper else (HEIGHT - y)) // HEIGHT + BLEED
        else:
            half = canvas // 2
        row = bytearray(canvas * 4)
        for x in range(canvas // 2 - half, canvas // 2 + half):
            row[x * 4 : x * 4 + 4] = bytes((*color, 255))
        rows.append(bytes(row))
    return rows


def strip(direction):
    """A road strip: level, or leaning down to the east side (south-east) or the west side (south) by one row spacing."""
    width = WIDTH if direction == "east" else WIDTH // 2
    height = STRIP if direction == "east" else HEIGHT + STRIP
    rows = [bytearray(width * 4) for _ in range(height)]
    for x in range(width):
        if direction == "east":
            top = 0
        elif direction == "south-east":
            top = x * HEIGHT // (WIDTH // 2)
        else:
            top = (width - 1 - x) * HEIGHT // (WIDTH // 2)
        for y in range(top, top + STRIP):
            rows[y][x * 4 : x * 4 + 4] = bytes((*ROAD_COLOR, 255))
    return width, height, [bytes(r) for r in rows]


def main():
    manifest = {}
    for code, color in enumerate(TERRAIN_COLORS):
        for upper in (True, False):
            name = "upper" if upper else "lower"
            file = f"terrain/{code}-{name}.png"
            png(os.path.join(OUT, file), WIDTH + 2 * BLEED, HEIGHT + SKIRT, triangle(color, upper))
            # The anchor hangs the image from the triangle's apex: the top middle of an upper triangle, the bottom middle of a lower.
            manifest[f"terrain/{code}/{name}"] = {
                "image": file,
                "anchor": [-(WIDTH // 2 + BLEED), 0 if upper else -HEIGHT],
            }
    for direction in ("east", "south-east", "south"):
        file = f"road/{direction}.png"
        width, height, rows = strip(direction)
        png(os.path.join(OUT, file), width, height, rows)
        # The term's position is the strip's top left corner: the map view puts it 2 pixels above the line, which the
        # strip's 5 rows center on.
        manifest[f"road/{direction}"] = {"image": file, "anchor": [0, 0]}
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Writes the art of the Open Settlers content set: content/open-settlers/manifest.json and its PNGs.

The art is the project's own: a flat color per terrain code, a plain strip per road direction, and crude shapes for the
flag, the map objects, the castle, the woodcutter's hut, the goods, and one pose per job and kind of activity for the
settlers, all drawn here from nothing but numbers. It holds nothing derived from an install (ADR 0015). A settler's
player shows as the pixels of the mark color (255, 0, 255), which OpenSettlersSource replaces with the player's color.
The water ripple and the shadows have no art: the source draws nothing for them. Run it from the repository root:

    python3 tools/open-settlers-art.py [<folder>]

It writes into content/open-settlers, or into <folder> where given. The unit test
RepositoryContentTests.TheOpenSettlersContentSetIsTheGeneratorsOutput writes the set into a temporary folder and fails
where content/open-settlers holds any file it does not write the same, apart from the .gdignore. Run the script after a
change to it and commit its output.

The terrain triangles are 32 pixels wide and 20 high, the node spacing and row spacing of the map. An upper triangle
hangs from its apex at the top, a lower triangle from its apex at the bottom. Each triangle is 8 pixels wider on each
side than the geometry, so neighbors overlap and leave no seam where their heights differ, and it carries a skirt of
its color below it, which the rows painted after it cover. The skirt is as deep as the largest drop a node can have:
31 height units (Node.MaxHeight) of 4 pixels (TerrainTriangles.HeightStep). The images are flat: they ignore the slope.
"""

import json
import os
import struct
import sys
import zlib

OUT = os.path.join("content", "open-settlers")
WIDTH = 32
HEIGHT = 20
BLEED = 8
SKIRT = 31 * 4

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


class Canvas:
    """A transparent RGBA canvas with the few shapes the object art is drawn from."""

    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.pixels = [[(0, 0, 0, 0)] * width for _ in range(height)]

    def dot(self, x, y, color):
        if 0 <= x < self.width and 0 <= y < self.height:
            self.pixels[y][x] = (*color, 255)

    def rect(self, x, y, width, height, color):
        for yy in range(y, y + height):
            for xx in range(x, x + width):
                self.dot(xx, yy, color)

    def disc(self, cx, cy, radius, color):
        for y in range(cy - radius, cy + radius + 1):
            for x in range(cx - radius, cx + radius + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= radius * radius + radius:
                    self.dot(x, y, color)

    def triangle(self, top, base_y, half_base, color):
        """An upright triangle: apex (top = (x, y)), base at base_y, half_base pixels each side."""
        apex_x, apex_y = top
        for y in range(apex_y, base_y + 1):
            half = half_base * (y - apex_y) // max(1, base_y - apex_y)
            for x in range(apex_x - half, apex_x + half + 1):
                self.dot(x, y, color)

    def line(self, x0, y0, x1, y1, color, thick=1):
        steps = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(steps + 1):
            x = x0 + (x1 - x0) * i // steps
            y = y0 + (y1 - y0) * i // steps
            self.rect(x, y, thick, thick, color)

    def outline(self, color):
        """Draws a one pixel outline around the covered pixels."""
        covered = [[p[3] > 0 for p in row] for row in self.pixels]
        for y in range(self.height):
            for x in range(self.width):
                if covered[y][x]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < self.width and 0 <= ny < self.height and covered[ny][nx]:
                        self.dot(x, y, color)
                        break

    def save(self, path):
        png(os.path.join(OUT, path), self.width, self.height, [b"".join(bytes(p) for p in row) for row in self.pixels])


# The mark colour of a settler's art: the source replaces these pixels with the player's colour (OpenSettlersSource.MarkColour).
MARK = (255, 0, 255)
INK = (40, 30, 25)
WOOD = (110, 75, 40)
LEAF = (50, 120, 45)


def shade(color, amount):
    return tuple(max(0, min(255, c + amount)) for c in color)


def tree(variant):
    """A tree, 20 by 30: the foot of the trunk is at the term's position."""
    c = Canvas(20, 30)
    c.rect(8, 18, 4, 12, WOOD)
    c.disc(10, 11, 8, shade(LEAF, 25 * variant))
    c.outline(INK)
    return c, (-10, -29)


def bush(variant):
    c = Canvas(14, 12)
    c.disc(7, 7, 5, shade((80, 140, 60), 30 * variant))
    c.outline(INK)
    return c, (-7, -11)


def rock(size):
    side = 8 + 4 * size
    c = Canvas(side, side * 2 // 3)
    c.triangle((side // 2, 0), side * 2 // 3 - 1, side // 2 - 1, (125, 125, 130))
    c.outline(INK)
    return c, (-(side // 2), -(side * 2 // 3 - 1))


def stump():
    c = Canvas(10, 8)
    c.rect(1, 2, 8, 6, WOOD)
    c.rect(1, 1, 8, 2, shade(WOOD, 50))
    c.outline(INK)
    return c, (-5, -7)


def plant(variant):
    c = Canvas(10, 12)
    c.line(5, 11, 5, 4, (60, 130, 50))
    c.disc(5, 3, 2, (230, 200, 60) if variant else (220, 90, 120))
    c.outline(INK)
    return c, (-5, -11)


def falling(step, second):
    """A cut tree leaning over: step 1 to 5 of its fall, leaning further each step."""
    c = Canvas(40, 30)
    foot_x, foot_y = 6, 28
    tip_x = foot_x + 3 + 4 * step
    tip_y = foot_y - 26 + 4 * step
    c.line(foot_x, foot_y, tip_x, tip_y, WOOD, 3)
    c.disc(tip_x, tip_y, 6, shade(LEAF, 25 if second else 0))
    c.outline(INK)
    return c, (-foot_x, -foot_y)


def litter(variant):
    c = Canvas(12, 8)
    c.rect(0, 4, 12, 3, shade((120, 100, 80), 12 * (variant % 4)))
    c.outline(INK)
    return c, (-6, -7)


def flag():
    """The flag, 16 by 19 with its foot at the term's position: ObjectDraws.FlagGeometry is this box."""
    c = Canvas(16, 19)
    c.rect(0, 0, 2, 19, INK)
    c.rect(2, 0, 14, 9, (210, 50, 50))
    c.outline(INK)
    return c, (0, -18)


def castle():
    c = Canvas(64, 52)
    stone = (150, 150, 155)
    c.rect(8, 16, 48, 36, stone)
    for x in (4, 44):
        c.rect(x, 4, 16, 48, shade(stone, -20))
        for crenel in range(x, x + 16, 6):
            c.rect(crenel, 0, 4, 5, shade(stone, -20))
    c.rect(26, 34, 12, 18, WOOD)
    c.rect(28, 36, 8, 16, INK)
    c.rect(30, 22, 4, 6, INK)
    c.outline(INK)
    return c, (-32, -51)


def hut():
    c = Canvas(30, 30)
    c.rect(4, 14, 22, 16, shade(WOOD, 30))
    c.triangle((15, 2), 14, 15, (150, 60, 40))
    c.rect(12, 20, 7, 10, INK)
    c.rect(20, 18, 4, 4, (200, 220, 240))
    c.outline(INK)
    return c, (-15, -29)


def good(code):
    """A good, 8 by 8: its colour comes from its number; the log (7) is a brown bar."""
    c = Canvas(8, 8)
    if code == 7:
        c.rect(0, 2, 8, 4, WOOD)
        c.rect(0, 2, 2, 4, shade(WOOD, 60))
    else:
        hue = (code * 37) % 255
        color = (60 + hue % 160, 60 + (hue * 3) % 160, 60 + (hue * 7) % 160)
        c.rect(1, 1, 6, 6, color)
    c.outline(INK)
    return c, (-4, -7)


JOBS = {"carrier": (60, 100, 170), "woodcutter": (60, 130, 60), "type-one-hut": (170, 110, 40)}
KINDS = ("walks", "carries", "cuts", "door", "hands-over", "waits")


def figure(job_color, kind):
    """A settler, 14 by 22 with its feet at the term's position: a head, a body of its job's color, and a player mark on
    the chest. Each kind of activity has its own pose."""
    c = Canvas(14, 22)
    skin = (235, 195, 160)
    if kind == "waits":
        c.disc(7, 11, 3, skin)
        c.rect(3, 14, 8, 8, job_color)
        c.rect(5, 16, 4, 3, MARK)
    elif kind == "door":
        c.disc(7, 4, 3, skin)
        c.rect(5, 8, 4, 14, job_color)
        c.rect(6, 11, 2, 3, MARK)
    else:
        c.disc(7, 4, 3, skin)
        c.rect(3, 8, 8, 10, job_color)
        c.rect(5, 10, 4, 3, MARK)
        c.rect(3, 18, 3, 4, INK)
        c.rect(8, 18, 3, 4, INK)
    if kind == "carries":
        c.rect(1, 2, 12, 3, WOOD)
        c.line(3, 8, 2, 4, skin)
        c.line(10, 8, 11, 4, skin)
    elif kind == "cuts":
        c.line(11, 8, 13, 17, skin)
        c.line(13, 10, 13, 19, (200, 200, 205), 2)
    elif kind == "hands-over":
        c.line(10, 10, 13, 12, skin, 2)
    c.outline(INK)
    return c, (-7, -21)


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

    def add(key, file, art):
        canvas, anchor = art
        canvas.save(file)
        manifest[key] = {"image": file, "anchor": list(anchor)}

    add("object/flag", "object/flag.png", flag())
    # Map objects by code (docs/rules/map-objects.md): trees 8 to 23, saplings 24 to 31, boulders 32 to 82, the stump 83
    # (0x53), plants 84 to 92, falling trees 93 to 102 (0x5d to 0x66), anything else a heap. Codes of one kind that look alike
    # share a file and so an image.
    for code in range(8, 128):
        key = f"object/{code}"
        if code < 24:
            add(key, f"object/tree-{code // 16}.png", tree(code // 16))
        elif code < 32:
            add(key, f"object/bush-{(code - 24) // 4}.png", bush((code - 24) // 4))
        elif code < 83:
            add(key, f"object/rock-{code % 3}.png", rock(code % 3))
        elif code == 83:
            add(key, "object/stump.png", stump())
        elif code < 93:
            add(key, f"object/plant-{code % 2}.png", plant(code % 2))
        elif code < 103:
            step, second = (code - 93) % 5 + 1, code >= 98
            add(key, f"object/fall-{int(second)}-{step}.png", falling(step, second))
        else:
            add(key, f"object/litter-{code % 4}.png", litter(code))
    add("building/castle", "building/castle.png", castle())
    add("building/woodcutter", "building/woodcutter.png", hut())
    for code in range(32):
        add(f"good/{code}", f"good/{code}.png", good(code))
    for job, color in JOBS.items():
        for kind in KINDS:
            add(f"settler/{job}/{kind}", f"settler/{job}-{kind}.png", figure(color, kind))
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        OUT = sys.argv[1]
    main()

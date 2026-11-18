#!/usr/bin/env python3
"""Writes the art of the basic content set: content/basic/manifest.json and its PNGs.

The art is the project's own: a flat color per terrain code, a plain strip per road direction, and crude shapes for the
flag, the map objects, the castle, the huts of the woodcutter, the stonecutter, and the forester, the sawmill, the six food
buildings (fisher, farm, baker, mill, pig farm, butcher), the building sites that have not started with the scaffolds they grow, the goods, the border posts
(a pole on land and a post in water), one pose per job and kind of activity for the
settlers, and a flame per size and phase, all drawn here from nothing but numbers. The flames of a small and of a large building that burns,
and of an early site, stand at places of the project's own choosing, read from nothing (ADR 0021). It holds nothing derived from an install (ADR 0015). A settler's
player shows as the pixels of the mark color (255, 0, 255), which OpenSettlersSource replaces with the player's color.
The water ripple and the shadows have no art: the source draws nothing for them. Run it from the repository root:

    python3 tools/basic-art.py [<folder>]

It writes into content/basic, or into <folder> where given. The unit test
RepositoryContentTests.TheBasicContentSetIsTheGeneratorsOutput writes the set into a temporary folder and fails
where content/basic holds any file it does not write the same, apart from the .gdignore and the hand-written JSONC
files of content/basic/rules/ that the rule tables port and the walk steps port read. Run the script after a
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

OUT = os.path.join("content", "basic")
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


def sapling(variant):
    """A young tree a forester planted, 10 by 16: a thin stem and a small crown, the second kind with the crown higher."""
    c = Canvas(10, 16)
    c.rect(4, 7 + variant, 2, 9 - variant, WOOD)
    c.disc(5, 5 + variant, 3, shade(LEAF, 20 + 25 * variant))
    c.outline(INK)
    return c, (-5, -15)


def field(stage):
    """A field of the farmer, 24 by 14: rows of green shoots growing taller with the stage (0 to 5), of ripe ears that darken with each stage (6 to 11), or of
    stubble (12, the harvested field)."""
    c = Canvas(24, 14)
    for row in range(3):
        for column in range(5):
            x, y = 2 + column * 4 + (row % 2) * 2, 4 + row * 4
            if stage == 12:
                c.rect(x, y + 2, 1, 2, (150, 125, 70))
            elif stage < 6:
                c.rect(x, y + 3 - stage // 2, 2, 1 + stage // 2, shade(LEAF, 10 * stage))
            else:
                age = stage - 6
                c.rect(x, y, 2, 4, (225 - 5 * age, 195 - 6 * age, 70 - 4 * age))
    c.outline(INK)
    return c, (-12, -9)


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


def stonecutter():
    """The stonecutter's hut: a grey hut with a heap of stones at its side."""
    c = Canvas(34, 30)
    c.rect(4, 14, 20, 16, (150, 150, 155))
    c.triangle((14, 2), 13, 14, (110, 110, 120))
    c.rect(11, 20, 7, 10, INK)
    c.disc(28, 25, 5, (190, 190, 195))
    c.disc(24, 27, 3, (170, 170, 175))
    c.outline(INK)
    return c, (-14, -29)


def forester():
    """The forester's hut: a hut with a green roof and a sapling at its side."""
    c = Canvas(34, 30)
    c.rect(4, 14, 20, 16, shade(WOOD, 30))
    c.triangle((14, 2), 13, 14, (60, 140, 60))
    c.rect(11, 20, 7, 10, INK)
    c.rect(28, 20, 2, 10, WOOD)
    c.disc(29, 18, 4, LEAF)
    c.outline(INK)
    return c, (-14, -29)


def sawmill():
    """The sawmill, a large building: a wide shed with a blade, and a pile of planks."""
    c = Canvas(52, 36)
    c.rect(4, 14, 36, 22, shade(WOOD, 20))
    c.triangle((22, 2), 20, 13, (120, 70, 50))
    c.rect(16, 22, 10, 14, INK)
    c.disc(32, 24, 5, (200, 200, 205))
    c.rect(42, 26, 8, 10, shade(WOOD, 60))
    c.outline(INK)
    return c, (-22, -35)


def fisher():
    """The fisher's hut: a small hut with a blue roof and a net hung on poles at its side."""
    c = Canvas(34, 30)
    c.rect(4, 14, 20, 16, shade(WOOD, 30))
    c.triangle((14, 2), 13, 14, (50, 100, 170))
    c.rect(11, 20, 7, 10, INK)
    c.rect(26, 16, 2, 14, WOOD)
    c.rect(32, 16, 2, 14, WOOD)
    c.rect(26, 18, 8, 6, (170, 190, 200))
    c.outline(INK)
    return c, (-14, -29)


def farm():
    """The farm, a large building: a long barn with a yellow roof and a strip of grain in front."""
    c = Canvas(52, 36)
    c.rect(4, 12, 40, 24, (150, 70, 50))
    c.triangle((24, 0), 12, 22, (220, 190, 70))
    c.rect(18, 22, 12, 14, INK)
    c.rect(44, 28, 8, 8, (230, 200, 80))
    c.outline(INK)
    return c, (-22, -35)


def baker():
    """The baker, a large building: a house with a chimney and a loaf over the door."""
    c = Canvas(52, 36)
    c.rect(4, 14, 36, 22, (210, 190, 150))
    c.triangle((22, 2), 12, 20, (170, 80, 50))
    c.rect(32, 0, 5, 12, (120, 70, 60))
    c.rect(16, 22, 10, 14, INK)
    c.disc(40, 28, 4, (200, 140, 70))
    c.outline(INK)
    return c, (-22, -35)


def mill():
    """The mill, a small building: a tower with four sails."""
    c = Canvas(34, 30)
    c.rect(9, 10, 16, 20, (200, 200, 195))
    c.triangle((17, 2), 8, 10, (140, 90, 60))
    c.rect(14, 20, 6, 10, INK)
    c.line(17, 12, 2, 2, WOOD, 2)
    c.line(17, 12, 32, 22, WOOD, 2)
    c.line(17, 12, 32, 2, WOOD, 2)
    c.line(17, 12, 2, 22, WOOD, 2)
    c.outline(INK)
    return c, (-14, -29)


def pig_farm():
    """The pig farm, a large building: a long sty with a pink pig in a pen at its side."""
    c = Canvas(52, 36)
    c.rect(4, 14, 36, 22, shade(WOOD, 10))
    c.triangle((22, 4), 18, 14, (120, 90, 70))
    c.rect(16, 24, 10, 12, INK)
    c.rect(41, 26, 10, 10, shade(WOOD, 50))
    c.disc(46, 31, 3, (240, 160, 170))
    c.outline(INK)
    return c, (-22, -35)


def butcher():
    """The butcher, a large building: a house with a red roof and a hook with a joint of meat over the door."""
    c = Canvas(52, 36)
    c.rect(4, 14, 36, 22, (225, 225, 225))
    c.triangle((22, 2), 12, 20, (190, 40, 40))
    c.rect(16, 24, 10, 12, INK)
    c.rect(41, 14, 2, 6, INK)
    c.disc(42, 24, 4, (200, 70, 80))
    c.outline(INK)
    return c, (-22, -35)


def site(large):
    """A building site that has not started: stakes and a board, wider for a large building."""
    width = 34 if large else 22
    c = Canvas(width, 20)
    c.rect(0, 14, width, 4, shade(WOOD, 40))
    c.rect(2, 4, 2, 14, WOOD)
    c.rect(width - 4, 4, 2, 14, WOOD)
    c.rect(2, 4, width - 4, 2, WOOD)
    c.outline(INK)
    return c, (-(width // 2), -19)


def scaffold(large):
    """The scaffold of a building under construction, the size of the finished building of its kind (34 by 30, or 52 by 36 for a
    large one): wooden posts and beams that a site grows from the bottom up as its progress rises."""
    width, height = (52, 36) if large else (34, 30)
    c = Canvas(width, height)
    left, right = 4, width - 12
    c.rect(left, 8, 2, height - 8, WOOD)
    c.rect(right, 8, 2, height - 8, WOOD)
    c.rect((left + right) // 2, 8, 2, height - 8, WOOD)
    for beam in (10, 18, 26):
        c.rect(left, beam, right - left + 2, 2, shade(WOOD, 30))
    c.line(left, height - 2, right, 12, WOOD)
    c.outline(INK)
    return c, ((-22, -35) if large else (-14, -29))


def heap(stage):
    """A heap of stones, stage 0 (code 0x48) the largest to stage 7 (0x4f) the last stone: a pile of grey stones that shrinks."""
    c = Canvas(22, 16)
    size = 8 - stage
    c.disc(11, 15 - size, size, shade(STONE, -25))
    if stage < 6:
        c.disc(5 + stage, 14 - size // 2, (size + 1) // 2, STONE)
    if stage < 3:
        c.disc(17 - stage, 14 - size // 2, (size + 1) // 2, shade(STONE, 15))
    c.outline(INK)
    return c, (-11, -15)


def good(code):
    """A good, 8 by 8: its colour comes from its number; the log (7) is a brown bar and the stone (10) a grey stone, and the
    food goods have a shape each: fish (1), a pig (2), a joint of meat (3), a sheaf of grain (4), a sack of flour (5), and
    a loaf of bread (6)."""
    c = Canvas(8, 8)
    if code == 7:
        c.rect(0, 2, 8, 4, WOOD)
        c.rect(0, 2, 2, 4, shade(WOOD, 60))
    elif code == 10:
        c.disc(4, 4, 3, STONE)
        c.disc(3, 3, 1, shade(STONE, 40))
    elif code == 1:
        c.disc(3, 4, 3, (110, 160, 210))
        c.triangle((7, 4), 1, 1, (110, 160, 210))
    elif code == 2:
        c.disc(4, 4, 3, (240, 160, 170))
        c.rect(1, 1, 2, 2, (240, 160, 170))
    elif code == 3:
        c.disc(4, 4, 3, (190, 60, 70))
        c.disc(3, 3, 1, (230, 120, 120))
    elif code == 4:
        c.rect(3, 1, 2, 6, (230, 200, 80))
        c.rect(1, 2, 2, 3, (230, 200, 80))
        c.rect(5, 2, 2, 3, (230, 200, 80))
    elif code == 5:
        c.rect(1, 2, 6, 5, (245, 245, 240))
        c.rect(2, 1, 4, 2, (225, 225, 215))
    elif code == 6:
        c.rect(0, 3, 8, 4, (200, 140, 70))
        c.rect(1, 2, 6, 2, (220, 165, 90))
    else:
        hue = (code * 37) % 255
        color = (60 + hue % 160, 60 + (hue * 3) % 160, 60 + (hue * 7) % 160)
        c.rect(1, 1, 6, 6, color)
    c.outline(INK)
    return c, (-4, -7)


MARKER = (245, 225, 70)


def border_post(water):
    """A border post standing with its foot on the term's position: a black and white pole on land, a red post in water.
    Both ignore the slope of the ground."""
    c = Canvas(3, 10)
    if water:
        c.rect(0, 0, 3, 10, (200, 40, 40))
    else:
        for y in range(0, 10, 2):
            c.rect(0, y, 3, 2, INK if y % 4 == 0 else (235, 235, 230))
    return c, (-1, -9)


def marker(kind):
    """A build marker, standing with its foot on the node: a pennant for a flag, a pick for a mine, a small house for a
    small building, and a wide house for a large building, in one bright colour with an outline so it reads over any
    ground. The original draws no shadow under its markers, and these have none."""
    if kind == "flag":
        c = Canvas(8, 12)
        c.rect(1, 0, 1, 12, MARKER)
        c.triangle((2, 1), 5, 0, MARKER)
        c.rect(2, 1, 5, 4, MARKER)
    elif kind == "mine":
        c = Canvas(12, 12)
        c.line(2, 10, 9, 3, MARKER, 2)
        c.rect(5, 1, 6, 2, MARKER)
        c.rect(1, 10, 3, 2, MARKER)
    elif kind == "small":
        c = Canvas(12, 11)
        c.triangle((5, 1), 5, 5, MARKER)
        c.rect(2, 5, 8, 6, MARKER)
    else:
        c = Canvas(18, 13)
        c.triangle((8, 1), 6, 8, MARKER)
        c.rect(1, 6, 16, 7, MARKER)
    c.outline(INK)
    return c, (-(c.width // 2), -(c.height - 1))


FLAME_OUTER = (240, 120, 30)
FLAME_INNER = (250, 220, 80)


def flame(size, phase):
    """A flame standing with its foot on its place: a tongue of orange around a yellow core, small or large. Its height
    rises and falls with the phase, so the flames flicker."""
    width, height = (6, 9) if size == "small" else (10, 15)
    lift = (0, 1, 2, 1, 0, 2, 1, 2)[phase]
    height += lift
    c = Canvas(width, height)
    c.triangle((width // 2, 0), height - 1, width // 2, FLAME_OUTER)
    c.triangle((width // 2, height // 3), height - 1, width // 4, FLAME_INNER)
    return c, (-(width // 2), -(height - 1))


# The flames of a burning building as (size, x, y) from its node: two over a small building, three over a large one, and one
# over a site below 16000 progress. OpenSettlersSource gives each building type the small or the large list by the size of its
# scaffold. The places are the project's own.
FLAMES = {
    "small": [("small", -6, -12), ("large", 4, -18)],
    "large": [("large", -14, -16), ("small", 2, -26), ("large", 14, -14)],
    "early-site": [("small", 0, -8)],
}


def road_marker(kind, difference=0):
    """A road marker, centred on its node: a ring for the end of the road, a red cross for a blocked neighbour, a hooked
    arrow pointing back for the way back, and for a slope a box filled up to a line that rises or falls by the height
    difference, green up to 1 either way, yellow at 2, and red at 3 and 4. Each reads apart from the others."""
    if kind == "end":
        c = Canvas(11, 11)
        c.disc(5, 5, 4, MARKER)
        for y in range(3, 8):
            for x in range(3, 8):
                if (x - 5) ** 2 + (y - 5) ** 2 <= 4:
                    c.pixels[y][x] = (0, 0, 0, 0)
    elif kind == "blocked":
        c = Canvas(11, 11)
        c.line(1, 1, 8, 8, (210, 40, 40), 2)
        c.line(8, 1, 1, 8, (210, 40, 40), 2)
    elif kind == "way-back":
        c = Canvas(13, 11)
        c.line(4, 5, 11, 5, MARKER, 2)
        c.line(11, 5, 11, 9, MARKER, 2)
        c.triangle((1, 5), 5, 0, MARKER)
        c.line(1, 5, 4, 2, MARKER, 2)
        c.line(1, 5, 4, 8, MARKER, 2)
    else:
        size = abs(difference)
        color = (70, 170, 70) if size <= 1 else (235, 200, 50) if size == 2 else (210, 50, 40)
        c = Canvas(14, 12)
        c.rect(0, 0, 14, 12, (60, 70, 140))
        c.rect(1, 1, 12, 10, (215, 215, 220))
        left, right = 6 + difference, 6 - difference
        for x in range(1, 13):
            top = left + (right - left) * (x - 1) // 11
            c.rect(x, top, 1, 11 - top, color)
    c.outline(INK)
    return c, (-(c.width // 2), -(c.height // 2))


JOBS = {
    "carrier": (60, 100, 170),
    "woodcutter": (60, 130, 60),
    "stonecutter": (140, 140, 150),
    "fisher": (170, 110, 40),
    "builder": (190, 90, 60),
    "forester": (90, 160, 80),
    "digger": (120, 90, 50),
    "sawmiller": (200, 160, 90),
    "miller": (235, 230, 205),
    "pig-farmer": (225, 150, 160),
    "farmer": (210, 190, 80),
    "baker": (225, 185, 120),
    "butcher": (200, 70, 70),
}
STONE = (185, 185, 190)
GRAIN = (225, 195, 70)
FISH = (110, 160, 210)
KINDS = ("walks", "carries", "cuts", "door", "hands-over", "waits")


def figure(job_color, kind, load=WOOD):
    """A settler, 14 by 22 with its feet at the term's position: a head, a body of its job's color, and a player mark on
    the chest. Each kind of activity has its own pose; the load of the pose that carries is the job's."""
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
        c.rect(1, 2, 12, 3, load)
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
    # Map objects by code (docs/rules/map-objects.md): trees 8 to 23, palms 24 to 27 and plants 28 to 31 (drawn as
    # bushes), boulders 32 to 71 and 80 to 82, stone heaps 72 to 79, the stump 83 (0x53), plants 84 to 92, falling trees
    # 93 to 102 (0x5d to 0x66), saplings 103 and 104 (0x67, 0x68), fields 105 to 111 and 121 to 126 (0x69 to 0x6f, 0x79 to 0x7e), and litter in the rest. Codes of one kind that look alike
    # share a file and so an image.
    for code in range(8, 128):
        key = f"object/{code}"
        if code < 24:
            add(key, f"object/tree-{code // 16}.png", tree(code // 16))
        elif code < 32:
            add(key, f"object/bush-{(code - 24) // 4}.png", bush((code - 24) // 4))
        elif 72 <= code < 80:
            add(key, f"object/heap-{code - 72}.png", heap(code - 72))
        elif code < 83:
            add(key, f"object/rock-{code % 3}.png", rock(code % 3))
        elif code == 83:
            add(key, "object/stump.png", stump())
        elif code < 93:
            add(key, f"object/plant-{code % 2}.png", plant(code % 2))
        elif code < 103:
            step, second = (code - 93) % 5 + 1, code >= 98
            add(key, f"object/fall-{int(second)}-{step}.png", falling(step, second))
        elif code in (103, 104):
            add(key, f"object/sapling-{code - 103}.png", sapling(code - 103))
        elif code < 111:
            add(key, f"object/field-{code - 105}.png", field(code - 105))
        elif code == 111:
            add(key, "object/field-harvested.png", field(12))
        elif 121 <= code < 127:
            add(key, f"object/field-{code - 115}.png", field(code - 115))
        else:
            add(key, f"object/litter-{code % 4}.png", litter(code))
    add("building/castle", "building/castle.png", castle())
    add("building/woodcutter", "building/woodcutter.png", hut())
    add("building/stonecutter", "building/stonecutter.png", stonecutter())
    add("building/forester", "building/forester.png", forester())
    add("building/sawmill", "building/sawmill.png", sawmill())
    add("building/fisher", "building/fisher.png", fisher())
    add("building/farm", "building/farm.png", farm())
    add("building/baker", "building/baker.png", baker())
    add("building/mill", "building/mill.png", mill())
    add("building/pig-farm", "building/pig-farm.png", pig_farm())
    add("building/butcher", "building/butcher.png", butcher())
    add("building/site-small", "building/site-small.png", site(False))
    add("building/site-large", "building/site-large.png", site(True))
    add("building/stage-small", "building/stage-small.png", scaffold(False))
    add("building/stage-large", "building/stage-large.png", scaffold(True))
    for code in range(32):
        add(f"good/{code}", f"good/{code}.png", good(code))
    for kind in ("flag", "mine", "small", "large"):
        add(f"marker/{kind}", f"marker/{kind}.png", marker(kind))
    for kind in ("end", "blocked", "way-back"):
        add(f"road-marker/{kind}", f"road-marker/{kind}.png", road_marker(kind))
    for difference in range(-4, 5):
        add(f"road-marker/slope/{difference}", f"road-marker/slope-{difference}.png", road_marker("slope", difference))
    add("border/pole", "border/pole.png", border_post(water=False))
    add("border/water", "border/water.png", border_post(water=True))
    for job, color in JOBS.items():
        for kind in KINDS:
            load = STONE if job == "stonecutter" else GRAIN if job == "farmer" else FISH if job == "fisher" else WOOD
            add(f"settler/{job}/{kind}", f"settler/{job}-{kind}.png", figure(color, kind, load))
    for size in ("small", "large"):
        for phase in range(8):
            add(f"flame/{size}/{phase}", f"flame/{size}-{phase}.png", flame(size, phase))
    for name, flames in FLAMES.items():
        manifest[f"flames/{name}"] = {"flames": [{"size": size, "at": [x, y]} for size, x, y in flames]}
    with open(os.path.join(OUT, "manifest.json"), "w") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


if __name__ == "__main__":
    if len(sys.argv) > 1:
        OUT = sys.argv[1]
    main()

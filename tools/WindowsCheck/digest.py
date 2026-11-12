"""Computes the canonical map digests from a raw dump of the original's map buffer.

Usage: python3 -I digest.py <dump-file> <size> <seed> <rng> <total>

  dump-file  the raw bytes of the map buffer, kept outside the repository (INV-0001)
  size       the map size value, 1, 2, or 3
  seed       the map seed words, as in map-digests.txt, for example 6359,A0D2,B4FA
  rng        the three generator words read after MapGen_Generate, for example 7590,F842,468D
  total      the resource type 1 total, a decimal number

It prints one row in the form of windows-run.txt. The layout and the layers are those of
docs/rules/map-form.md; the digest is FNV-1a 64 over the layer in row-major order. The script reads the
dump and prints digests only: it writes no file and keeps no byte.
"""

import sys

SIZES = {1: (32, 32), 2: (64, 32), 3: (64, 64)}
BASIS = 0xCBF29CE484222325
PRIME = 0x100000001B3
MASK = (1 << 64) - 1


def main(argv):
    if len(argv) != 6:
        sys.exit(__doc__)
    path, size, seed, rng, total = argv[1:]
    if size not in {str(k) for k in SIZES}:
        sys.exit("the size must be 1, 2, or 3")
    width, height = SIZES[int(size)]
    with open(path, "rb") as handle:
        dump = handle.read()
    if len(dump) != height * 8 * width:
        sys.exit(f"size {size} needs {height * 8 * width} bytes, the dump has {len(dump)}")
    hashes = [BASIS] * 6
    for y in range(height):
        for x in range(width):
            a = y * 8 * width + x * 4
            b = a + 4 * width
            # flags, height, terrain, lake, object (record A byte 3), resource (record B byte 0, as the lake)
            for layer, at in enumerate((a, a + 1, a + 2, b, a + 3, b)):
                hashes[layer] = ((hashes[layer] ^ dump[at]) * PRIME) & MASK
    print(f"Windows run | {size} {seed.upper()} CountResource1 {rng.upper()} "
          + " ".join(f"{h:016X}" for h in hashes) + f" total={total}")


if __name__ == "__main__":
    main(sys.argv)

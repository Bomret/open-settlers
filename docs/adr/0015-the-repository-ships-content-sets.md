# 15. The repository ships its own content sets

**Status:** Accepted

Amends [ADR 0006](0006-the-engine-reads-what-siedler1-reads.md) and [ADR 0001](0001-read-original-files-at-runtime.md).

The engine reads the files of a History Edition install that `Siedler1.exe` reads, and the content sets
that the repository ships under `content/`. A content set holds graphics only: PNG images and one JSON
manifest that maps game terms to images. The engine reads a set at runtime with Godot's
`Image.LoadFromFile`, with no import into Godot resources. Its folder carries a `.gdignore`. Rule data
comes only from `Siedler1.exe`, under every graphics source.

## Considered options

The decision weighed these options:

- Generate the second set's images in code: no files, but no shape a later set or a mod could follow.
- Ship rule data with the set as well: it would either copy the original's values to disk, against ADR
  0001, or invent them, which is rule modding.

## Consequences

The decision has these consequences:

- A content set is the repository's own work and holds nothing derived from the install.
- The engine still needs an install to run, because the rules come from it.

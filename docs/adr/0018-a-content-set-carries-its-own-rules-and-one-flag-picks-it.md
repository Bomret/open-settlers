# 18. A content set carries its own rules, and one flag picks it

**Status:** Accepted

Amends [ADR 0015](0015-the-repository-ships-content-sets.md),
[ADR 0014](0014-the-map-view-draws-through-a-graphics-source.md), and
[ADR 0017](0017-install-data-enters-through-import-ports.md).

A contributor without a copy of Die Siedler must be able to run the map view, the game view, layouts, and play runs.
A content set is therefore where a run gets all of its game data. That data is its graphics source, its rule data,
and its walk steps. There are two sets. The History Edition set is the install, read through the History Edition
source. The basic set is the repository's own, in `content/basic/`. It holds PNG images, the manifest, and data files
with stand-in rules, which are values of the project's own making. The launch flag `--content-set basic|history-edition`
picks the set and replaces `--graphics`. A run on the basic set reads nothing from the install and needs none.

`OpenSettlers.Import.OpenSettlers` reads the basic set and implements the graphics source, the rule tables port, and
the walk steps port. The data files are hand-written JSONC in readable terms: directions, sizes, materials, and speeds.
The source encodes them into the rule tables, so the original's encodings stay at the edge (ADR 0013). The settlers'
step offsets come from the set the run uses. The install's entry 2 is read only under the History Edition set.

## Considered options

The decision weighed these options:

- Write the stand-in values in C#, as the tests' made-up tables are. This needs no file format. Every change to a
  value needs a build, and nothing prepares content defined by data.
- Ship the stand-in rules as a separate set beside a graphics-only set. This keeps ADR 0015's graphics-only sets. It
  needs a second selection, and no run needs install rules under stand-in graphics or the reverse.
- Fall back to the stand-in rules when no install is given. This adds no flag. A missing or misspelled `SIEDLER_DIR`
  would silently change the game.
- Write the files in the program's encodings, so the reader passes values through. This needs no encoding code. An
  editor would need to know codes such as a walk row's 4 × (direction + 1).
- Keep `--graphics` beside a flag for the rules. This keeps the digest comparison across graphics sources. It costs
  two flags and combinations nobody uses.

## Consequences

The decision has these consequences:

- The stand-in rules are not the original's rules. A seed gives another map and another game on the basic set than on
  the History Edition set. The fidelity checks (INV-0003) and the digest goldens hold for the History Edition set.
- The stand-in values are invented, never taken from `Siedler1.exe` (INV-0001). They make the game runnable and
  testable; they do not approximate the original.
- The same seed and commands no longer give one digest across content sets, so the digest clause of INV-0009 and
  `play/graphics-digest.play` go. The simulation still names no graphics source.
- The basic set's art stays plain: it exists for development and verification without the original, not as a
  replacement for its looks.

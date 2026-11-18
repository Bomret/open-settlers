# 21. The graphics source places the flames of a burning building

**Status:** Accepted

The original draws a building that was taken down as usual and then draws flames over it. Each building type has a
list of flames in `Siedler1.exe` (`0x44e114`), and each flame has a size and an offset from the building. The map
view asks the graphics source for the flames of a building, as sizes and offsets in game terms. The map view then
places one flame draw term for each flame, with its phase. The History Edition source reads the lists from the
install's program. The basic set brings flame placements of its own making, with one plain image for every size and
phase.

## Considered options

The decision weighed these options:

- Write the lists in the map view, as it holds the places of a flag's goods. This needs no port member. A list
  picks the image's size as well as its place, which INV-0010 keeps inside the History Edition source. The basic set
  could then only draw the original's places, which are values taken from the program (INV-0001).
- Carry the flames on the building's draw term, and let a source answer with several images. This needs no new term.
  Every source and the quad builder would change to answer one term with several images.
- A stand-in mark, such as a tinted building or one generic mark. It is not the original's look, so a later port
  would replace it.

## Consequences

The decision has these consequences:

- A flame's size, its offset, and its phase are game terms. Only the History Edition source maps a size and a phase
  to an archive entry.
- The flames of a burning building differ between the two content sets.
- The type that picks a list is read only inside the source, so a change in how building types are named
  changes the source and not the map view.

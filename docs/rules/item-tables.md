# Item tables

The game state block holds four tables of fixed-size records ("items"). Each table has a base pointer, a
slot bitmap with one bit per slot, and a high-water mark. The program does not keep a list of free
slots: an allocator scans the bitmap for the first clear bit.

## The four tables

**Status:** confirmed for the sizes, the bitmaps, and the allocator pairs. The roles are in the next
section.

| Table | Item size | Base, bitmap (state `+`) | High-water mark | Allocate, free |
| --- | --- | --- | --- | --- |
| `Table70` | 70 bytes | `+0x98`, `+0xa4` | `+0x25e` | `0x45d7ad`, `0x45d974` |
| `Table18` | 18 bytes | `+0x9c`, `+0xa8` | `+0x260` | `0x45da8f`, `0x45dc0d` |
| `Table16` | 16 bytes | `+0xa0`, `+0xac` | `+0x262` | `0x45e121`, `0x45e273` |
| `Table120` | 120 bytes | `+0xf0`, `+0xf4` | `+0x264` (a slot limit) | `0x45dd1c`, `0x45e012` |

The allocator of `Table70` also stops at a slot limit at `+0x258`. Each allocator takes the first free
slot, sets its bit, clears the item to zero, and raises the high-water mark when the new slot is at it.
Each release routine clears the bit of the slot and lowers the high-water mark past the free slots at
its end. The sizes are the item strides in the allocators.

The program index lists the fields ([Program index](program-index.md#structures-and-data)); the save
code writes `Table70` and `Table18` as their own records and turns their links into offsets and back
(`Table_ToOffsets70`, `Table_ToPointers70`, `Table_ToOffsets18`, `Table_ToPointers18`).

## Roles

**Status:** inferred, from the sizes and from what the per-phase routines read.

- `Table70` holds the flags: its items hold links to other `Table70` items (the save code turns six of
  them into offsets), so it is the graph that roads join.
- `Table18` holds the buildings: each item keeps a countdown of clock units that its routine reduces by
  the units since the item's last turn.
- `Table16` holds the settlers: the routine dispatches each item on its state byte ([Settlers](settlers.md#the-items)).
- `Table120` holds the storage inventories. No routine of the tick walks it.

The per-field layout of the items is not settled; the milestone scope file keeps those leads.

## How a pass walks a table

**Status:** confirmed.

During phases 0 to 31 of a phase cycle ([Timing](timing.md#rules-that-count-passes)), each pass calls
`0x46414d` for `Table70`, `0x42cb2d` for `Table18`, and `0x42f3d9` for `Table16`. Each routine reads the
current phase and works a slice of its table:

- `Table70` and `Table18`: 32 slots, from slot `phase * 32`. A routine stops at the high-water mark of
  its table, so slots past it get no turn. A slot with a clear bit is skipped.
- `Table16`: the phase modulo 16 picks the first slot of the slice, which then takes every 16th slot up to the
  high-water mark; each set slot is dispatched on its state byte through `0x42fc81`. **Emulated:** a settler's
  turns lie 16 passes apart within a cycle (`worker-walks.txt`). The gap to the next cycle is 17 passes in a
  game with 33 phases and 17 to 33 passes in a game with 49 ([Settlers](settlers.md#the-pass)).

Phases 32 and above call none of these routines. A `Table70` or `Table18` slot therefore gets one turn per phase
cycle, a `Table16` slot two, and a cycle is 33 or 49 passes long.

## In the engine

`OpenSettlers.Simulation.Game` runs the tick (ADR 0005). It holds the flags, roads, buildings, and settlers
as lists whose index is the slot, and the castle's list of waiting settlers; the storage item and the flag items'
other fields are not held. The pass walks the building and settler lists as the phase says
([Settlers](settlers.md#the-pass)); the flag table has no pass yet.

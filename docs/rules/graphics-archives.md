# Graphics archives

The install holds the graphics in three archives in `loca/`: `SPAD.PA`, `SPAE.PA`, and `SPAF.PA`, one for
each language the program offers (`Loca_FileTable` at `0x67d614` lists them). The three files of the
reference install have the same length and the same table. This note covers how the program loads an
archive, how it finds an entry, the slots it fills with copies, and the kind of each entry. What a graphic
holds is in [Graphics images](graphics-images.md).

## Loading

**Status:** confirmed. **Platform layer.**

`Platform_RunGame` at `0x407480` checks that the archive of the chosen language exists, then calls
`Archive_Load` at `0x405e00`. That routine opens the file, takes its length, allocates a buffer of that
length, and reads the whole file into it with `Archive_ReadFile` at `0x405d70`. The buffer's address is the
archive base (`Archive_Base` at `0x6812ec`); the program keeps the archive in memory for the whole run and
frees it in `Archive_Free` at `0x405f60`. The file has no compression: an entry is a run of bytes in the
buffer.

## The header and the table

**Status:** confirmed for the table rows (every reader at `0x406120`, `0x406610`, `0x4071c0`, and `0x4073d0`
finds an entry through them). **Inferred** for the three header words: the program reads the length, the
row count, and the flag only in `Archive_Free`, and no game code checks the length against the file.

The file starts with a header of eight bytes and continues with the table. All numbers are little-endian.

- Bytes 0 to 3: the length of the file.
- Bytes 4 and 5: the number of table rows, the header's own row included (4000 in the reference files).
- Bytes 6 and 7: a flag. `Archive_Free` frees the entries one by one when it is 1 and the buffer once
  otherwise. The reference files hold 0.
- From byte 8, one row of eight bytes for each entry: the entry's size, then its offset from the start of
  the file.

The header takes the place of row 0, so the first entry is row 1. The program numbers an entry by its row,
and so does the engine, everywhere: the palette is entry 3. `0x407250` loads the palette by calling `0x4071c0`
with row 3, and the startup in `Platform_RunGame` (the call at `0x4075a7`) calls it the same way with row
3998; `0x4071c0` reads the row's 768 bytes as 256 colour triples. Each reader computes the address of an entry as
the archive base plus the row's offset, and uses no address at all when the offset is 0.

An entry is **present** when its offset is not 0 and **absent** when it is 0. The program never looks at the
size to decide. The reference files hold 2494 present entries, 255 aliases (below), and 1250 absent slots in
rows 1 to 3999.

## Slots filled with copies

**Status:** confirmed.

After loading, `Archive_FillAliases` at `0x407290` copies the size and offset of some rows over other rows.
It copies without looking at what the destination row held. The copies are, in row numbers:

- Row 1352 to rows 1363 to 1368.
- Row 1602 to rows 1613 to 1618.
- For each of 48 groups of six rows from row 3450 (3450 to 3455, 3456 to 3461, up to 3732 to 3737): the
  first row of the group to the five rows after it.
- Rows 3762, 3763, and 3764 to rows 3765, 3766, and 3767.

Every destination is empty in the reference files, so the copies fill empty slots. A destination shows as
the entry it repeats and is an **alias**; the engine counts it as an alias and never as an entry of its own.
No source row is a destination, so a copy never chains. A source that is absent leaves its destinations
absent. The engine applies a copy only where both rows lie inside the table the header declares. A kind
claims a source row and never a destination: an alias follows its source, and a kind that claims a
destination makes that alias fail with a reason naming both rows.

## What the entries are

The 2494 present entries of the reference files have exactly one kind each, and the check fails a present entry
that no kind claims. The ranges, each with the note that gives its evidence:

| Entries | Kind | Status | Note |
| --- | --- | --- | --- |
| 3, 3997, 3998 | palette | confirmed | [Graphics images](graphics-images.md#the-palette) |
| 432 entries in 13 ranges | raw image | confirmed | [Graphics images](graphics-images.md#raw-images) |
| 321 to 372, 610 to 619, 750 to 793, 810 to 853, and the ranges from 1250 to 3779 | sprite | confirmed; the upper ends inferred | [Graphics images](graphics-images.md#transparent-sprites) |
| 630 to 645 | sprite over color 8 | confirmed | [Graphics images](graphics-images.md#sprites-drawn-over-color-8) |
| 4, 60 to 256, 1500 to 1693 (where present) | mask | confirmed; some ends inferred | [Graphics images](graphics-images.md#masks) |
| 260 to 292, 300 to 309 | fill texture | confirmed | [Graphics images](graphics-images.md#masks) |
| 2 | rule data | confirmed | below |
| 448 to 521, 3900 to 3994, 3999 | unread | inferred | below |

The glyphs of the text are in the sprite ranges: the characters' glyphs are entries 750 to 793 and their
shadows 810 to 853, drawn by `0x4511d5` ([Graphics images](graphics-images.md#transparent-sprites)). Entries 3000
to 3040 are player-color overlays of the settler routine, not glyphs.

### Rule data

**Status:** confirmed for the reader and the layout; the meaning is pending.

Entry 2 is read once, at start-up, by `RuleData_Load` at `0x41e8e0`, called from `Game_InitSettings`. It finds the
entry through `Archive_FindEntry` at `0x4073d0`, skips the first 4 bytes, and byte-swaps the next 200 dwords in
place: the entry stores them big-endian. The address after the skipped bytes goes into the game state block's field
`+0xb0`, and the routine at `0x43f537`, reached from `0x42f512`, indexes it. The reference entry (30528 bytes, the
same in the three languages) holds, after the first 4 bytes, 200 offsets in ascending order, counted from that
address. The first is the length of the table itself and the last is the end of the entry, so the offsets mark off
the records that follow. Some offsets repeat, so some records span no bytes. What a record holds, and which rule each
one serves, is pending: the browser lists the entry as rule data, with its location, and shows no byte preview.

### Entries no routine reads

**Status:** inferred. The search found no reader; it did not exclude a reader that takes its entry number from a
table or a script that the search did not follow.

The entries are 448 to 521 (74 entries, including 490), 3900 to 3994 (the 43 present ones, 3901 to 3988 and 3990
to 3994), and 3999. The browser lists each with its location and "not read by the program".

The search covered how the program reaches the archive at all. Sixteen instructions in the whole program refer to
`Archive_Base`. They lie in the unloading routine, the alias fill, the palette loader, `Archive_FindEntry`, the
start-up that loads the archive, and the drawing routines between `0x4060b0` and `0x406650`, which includes
`0x406420`. So an entry is read only if a number reaches one of those: a palette load, an entry finder call, or a
draw call. The disassembly of the game code band then gave these results:

- No drawing call or entry find takes an immediate number from 448 to 521 or from 3900 to 3999. The only
  immediates in 3900 to 3999 are the clamp of a value to 3999 at `0x47664e` and `0x478294`, which the code then
  scales as a multiplier and never as an entry number. The immediates in 448 to 521 are screen sizes, coordinates,
  and flags.
- No base added to the entry field lies in 448 to 521, except two table offsets: 420 at `0x44ef27` and 460 at
  `0x44f12d` index a table of byte pairs that gives a sprite index for base 320, which the sprite note covers.
- The data sections hold no table of 16-bit numbers with a run of values from these ranges, except two runs in
  the read-only data that read as ascending prime numbers, which are not lists of entries.
- 3990 to 3994 hold XMIDI music containers and 3999 decodes as a 16 x 16 sprite. The program loads its music from
  `snd/music_1.midi` ([Music file](music-file.md)) and its sounds from `snd/`, which is where a reader of the original
  samples and the XMIDI data would be.

## Corrupt archives

**Status:** stand-in. The program checks none of these; the engine reports them.

The engine reports an archive as unreadable when the file is shorter than its header (8 bytes), shorter
than its table (the row count times 8), or when the length in the header differs from the file's length. Each message names
its offset: the end of the header or of the table, or offset 0 for the header's length word. An
entry whose offset plus size lies beyond the end of the file is reported on its own, with its number and
offset, and the other entries stay readable.

## In the engine

`ArchiveFile.Read` in `src/Formats/Archive.cs` reads bytes in and gives entries out, as present, alias, or
absent. `ArchiveEntryKind` in `src/Assets` lists the entries of the three archives and is the base of every
kind that reads an entry. Row 0 is the header and no entry: asking for it throws. A kind claims entries by their number, the same in all three
files. `UnrecoveredEntryKind` stands for a present entry that no kind claims: the check fails it with the file, the
entry, and the reason, so a new install or a gap in the map shows. It also reports a missing or corrupt archive
once, for the file. `RuleDataKind` and `UnreadEntryKind` list entry 2 and the entries no routine reads, each with its
location and size, and neither shows bytes.

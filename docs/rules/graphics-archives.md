# Graphics archives

The install holds the graphics in three archives in `loca/`: `SPAD.PA`, `SPAE.PA`, and `SPAF.PA`, one for
each language the program offers (`Loca_FileTable` at `0x67d614` lists them). The three files of the
reference install have the same length and the same table. This note covers how the program loads an
archive, how it finds an entry, and the slots it fills with copies. It does not cover what an entry holds.

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
kind that reads an entry. Row 0 is the header and no entry: asking for it throws. A kind claims entries by their number in its own file, and the browser lists an
entry no kind claims with its location and size.

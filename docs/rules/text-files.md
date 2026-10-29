# Text files

The install holds the program's texts as one file per language in `loca/`: `eng.txt`, `ger.txt`, and
`fre.txt`. This note covers how the program reads one and what counts as a line it accepts. It does not
cover what each text says or where the program shows it.

## Which file the program reads

**Status:** inferred. **Platform layer.**

`Platform_RunGame` at `0x407480` picks a language from the language table `Loca_FileTable` at `0x67d614`
and then calls `Text_LoadLanguage` at `0x412570`. Each table row holds an archive and a text file of one
language. The row of English names `loca/eng.txt`, the row of German `loca/ger.txt`, and the row of
French `loca/fre.txt`. Which row is chosen follows from the language setting. That the text file is the
one that reaches the reader follows from the table and the call, not from a single address.

`Text_LoadLanguage` fails when the file cannot be opened (the program then reports that it cannot load
the language file). It does not check that the file is complete: it asks for 224 fixed keys one after
another, and the reference install's three files hold exactly those 224 keys. A key the file lacks
makes `Text_Lookup` answer with no value, and `Text_LoadLanguage` then reads through that null pointer
at once, so the program crashes (inferred from the code, not observed). After each lookup it
appends a byte of value 255 after the end of the found text, which marks the text's end for the code
that draws it (inferred).

## The line format

**Status:** confirmed. **Platform layer.**

`Text_ReadFile` at `0x410e30` puts the application-folder prefix in front of the file name and hands it
to `Text_ParseFile` at `0x410f40`. The parser opens the file in the C runtime's text mode and reads it
one line at a time into a buffer of 256 bytes. The same parser also has a second caller, `0x416d70`,
which this note does not cover.

- **Line end.** A line ends at a line feed or at a carriage return. A carriage return followed by a line
  feed is one line end, because the text mode folds the pair. The last line needs no line end: the end
  of the file ends it, and the program counts its bytes by the same two limits below (the end-of-file
  marker the loop stores is not counted, so an unterminated line of 254 bytes is read and one of 4 bytes is
  skipped, as with a line end). The text mode treats a Ctrl-Z byte as the end of the file (inferred from the
  runtime's behaviour, not from an address).
- **Too long.** A line of 255 bytes or more before its line end overruns the buffer, and the program
  stops with a range-check failure. Lines of up to 254 bytes are safe.
- **Too short.** A line of fewer than 5 bytes before its line end is skipped. An empty line is one of these.
- **Key.** The key is everything before the first equals sign on the line, spaces included. A line
  without an equals sign is skipped.
- **Value.** The program looks for the first double quote after the equals sign and for the next double
  quote after that one. The value is what lies between them, spaces and all, and may be empty. Anything
  between the equals sign and the first quote, and everything after the second quote, is ignored. A
  line without both quotes is skipped.
- **Text.** The program works on bytes. The reference install's files hold ASCII only.
- **A zero byte** ends what the program sees of a line, because it searches the line as a C string.

A skipped line leaves no message and does not stop the reading of the lines after it.

## Keys and lookup

**Status:** confirmed. **Platform layer.**

The parser stores each pair in an ordered map from key to value text. A key that appears again replaces
the earlier value, so the last line of that key wins. `Text_Lookup` at `0x4111a0` finds a value by key
with a byte-wise, case-sensitive comparison, and answers with no value when the key is absent.

## In the engine

The engine reads a text file as bytes in and keys with values out, plus the lines the program would
skip, with their numbers and reasons (`src/Formats/TextFile.cs`). The asset check reports such a line
as a failure of its file, because a line the program skips in a shipped file is a defect there. A blank
line counts, though the program skips it without harm. The browser shows one table of all keys, one
value column per language, with an empty cell where a language lacks the key (`src/Assets/TextKind.cs`).

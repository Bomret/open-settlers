# Music file

The install holds one music file, `snd/music_1.midi`. This note covers where the program loads it and what
the file holds. It does not cover how the program plays the notes.

## Which file the program loads

**Status:** confirmed. **Platform layer.**

`Music_ApplyOption` at `0x419a60` is the only routine that opens a music file. The timer thread
(`Timer_ThreadMain`, `0x406c90`) calls it. It compares the music option with the state of the player.
When they differ it logs "Changing music to" and the new value. Switching the option off stops the
player (`Music_Stop`, `0x415390`). Switching it on reads the whole of `snd/music_1.midi` into memory and
hands the bytes and their length to `Music_LoadFile` at `0x415130`, then starts the stream and
applies a second stored option (`0x415480`, inferred to be the volume), pausing the stream around that
call when it plays.

No other music file name occurs in the program's strings, so the program loads no other file of the
`snd/` folder as music.

## The file format

**Status:** confirmed for the header and track tags and the chunk lengths, inferred for the rest.
**Platform layer.**

The file is a Standard MIDI File. A header chunk tagged `MThd` holds a length of 6, then the format, the
track count, and the time division, each in two bytes. Every number is big-endian. A track chunk tagged
`MTrk` follows for each track, with a four-byte length and then that many bytes of events.

`Music_LoadFile` refuses the file when the header tag is wrong, when the header length is not 6, or when
any of the announced tracks lacks the track tag. It keeps the format, the track count, and the division
and records each track's start and length. A track of length zero counts as finished. It then hands each
track to a routine that reads its events (`0x415940`, not read for this note) and builds the playback
stream. The reference build's file is format 0 with one
track.

An event is a delta time, in a variable-length number of up to four bytes, and a status byte. A status
byte below 0x80 reuses the previous status (running status). The events are channel events, meta events
(tag 0xFF, a type, a length, data), and system-exclusive events. A tempo meta event (type 0x51) sets the
microseconds per quarter note, 500000 until one occurs.

## In the engine

The engine reads the file as bytes in and tracks out (`src/Formats/MidiFile.cs`): the tracks with their
event counts and the length in seconds from the tempo changes of all tracks. A bad header tag, a bad track
tag, a chunk past the end of the file, a missing track, and an event cut short are unreadable and name the
reason. The program checks the events in routines this note does not cover, so what it does with a cut event
is open. The browser shows the tracks and the length and does not play
the file.

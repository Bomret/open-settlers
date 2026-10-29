# Sound files

The install holds the sound effects as numbered files in `snd/`. This note covers the files and how the
program turns one into a playable buffer. It does not cover which game event plays which number.

## Which files the program loads

**Status:** confirmed. **Platform layer.**

`Sound_LoadFile` at `0x406fb0`, which `Platform_RunGame` calls at start-up, tries the numbers 1 to 99 in
turn and builds the name `snd/NN.SND` with the number in two digits. A number with no file is skipped without a message, so
the install may have gaps. The install of the reference build holds 39 of the 99 numbers. The number is
the key of the loaded sound; the program looks a sound up by it.

The routine reads the whole file into memory with the C runtime's file calls and hands the bytes and
their length to `Sound_CreateBuffer` at `0x40b8c0`.

## The file format

**Status:** confirmed. **Platform layer.**

A sound file has no header, no length field, and no trailer. Every byte of the file is one sample.
The samples are unsigned 8-bit values of one channel, so the value 128 is silence. The file length in
bytes is the number of samples.

The program passes the file to the sound buffer as it is. It converts no sample.

## The sample rate

**Status:** confirmed. **Platform layer.**

`Sound_CreateBuffer` at `0x40b8c0` describes every sound to DirectSound with one fixed format: PCM, one
channel, 8 bits per sample, one byte per sample frame, and **8403** samples per second. The rate is part
of the routine, not of the file, so every sound plays at 8403 Hz. The buffer holds the whole file, and the
routine copies the file's bytes into it.

## An empty file

**Status:** inferred.

An empty file gives a buffer of length zero, which DirectSound is not expected to accept. The program then
holds no sound for that number. The engine reports an empty sound file as unreadable.

## In the engine

The engine reads a sound file as bytes in and samples plus the rate 8403 out (`src/Formats/SoundFile.cs`).
Godot's 8-bit stream format is signed, so the preview flips the sign bit of every sample before playback.

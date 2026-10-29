using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Formats;

/// <summary>One track of a music file: its position and how many events it holds.</summary>
public sealed record MidiTrack
{
    /// <summary>The track's position in the file, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>The number of events, meta and system-exclusive events included.</summary>
    public required int EventCount { get; init; }
}

/// <summary>A music file: its tracks, the header's format and time division, and its length.</summary>
public sealed record MusicFile
{
    /// <summary>The header's file format (0, 1, or 2).</summary>
    public required int Format { get; init; }

    /// <summary>The header's time division: ticks per quarter note, or the frame-based form when the top bit is set.</summary>
    public required int Division { get; init; }

    /// <summary>The tracks in file order.</summary>
    public required IReadOnlyList<MidiTrack> Tracks { get; init; }

    /// <summary>The length in seconds of the longest track, from the tempo changes of all tracks.</summary>
    public required double LengthSeconds { get; init; }
}

/// <summary>
/// The music file reader (<c>docs/rules/music-file.md</c>): a Standard MIDI File of a header chunk and track
/// chunks. Events are counted, not interpreted, except tempo changes, which give the length.
/// </summary>
public static class MidiFile
{
    const int DefaultMicrosecondsPerQuarter = 500_000;

    /// <summary>
    /// Reads a music file. Throws <see cref="InvalidDataException"/>, with the reason in the message, for a bad
    /// header or track tag, a chunk past the end of the file, a missing track, or an event cut short.
    /// </summary>
    public static MusicFile Read(byte[] bytes)
    {
        if (bytes.Length < 14 || !HasTag(bytes, 0, "MThd"))
        {
            throw new InvalidDataException("the header chunk is missing or has a bad tag");
        }

        var headerLength = ReadInt(bytes, 4, 4);
        if (headerLength < 6 || 8 + (long)headerLength > bytes.Length)
        {
            throw new InvalidDataException("the header chunk is cut short or past the end of the file");
        }

        var format = ReadInt(bytes, 8, 2);
        var trackCount = ReadInt(bytes, 10, 2);
        var division = ReadInt(bytes, 12, 2);
        var position = 8 + headerLength;

        var tracks = new List<MidiTrack>();
        var tempos = new List<(long Tick, int Microseconds)>();
        var ends = new List<long>();
        for (var index = 0; index < trackCount; index++)
        {
            if (position + 8 > bytes.Length)
            {
                throw new InvalidDataException($"track {index} is missing: the file ends before its chunk");
            }

            if (!HasTag(bytes, position, "MTrk"))
            {
                throw new InvalidDataException($"track {index} has a bad track chunk tag");
            }

            var length = ReadInt(bytes, position + 4, 4);
            var start = position + 8;
            if (length < 0 || start + (long)length > bytes.Length)
            {
                throw new InvalidDataException($"track {index} runs past the end of the file");
            }

            var (count, end) = ReadEvents(bytes, start, start + length, index, tempos);
            tracks.Add(new MidiTrack { Index = index, EventCount = count });
            ends.Add(end);
            position = start + length;
        }

        return new MusicFile
        {
            Format = format,
            Division = division,
            Tracks = tracks,
            LengthSeconds = ends.Count == 0 ? 0 : Seconds(ends.Max(), tempos, division),
        };
    }

    static (int Count, long EndTick) ReadEvents(
        byte[] bytes,
        int start,
        int end,
        int track,
        List<(long Tick, int Microseconds)> tempos
    )
    {
        var position = start;
        var tick = 0L;
        var count = 0;
        var status = 0;
        while (position < end)
        {
            tick += ReadVariable(bytes, ref position, end, track);
            if (position >= end)
            {
                throw Cut(track);
            }

            if (bytes[position] >= 0x80)
            {
                status = bytes[position++];
            }
            else if (status == 0)
            {
                throw new InvalidDataException($"track {track} has a data byte with no running status");
            }

            if (status == 0xFF)
            {
                if (position >= end)
                {
                    throw Cut(track);
                }

                var type = bytes[position++];
                var length = ReadVariable(bytes, ref position, end, track);
                Need(position, length, end, track);
                if (type == 0x51 && length == 3)
                {
                    tempos.Add((tick, ReadInt(bytes, position, 3)));
                }

                position += (int)length;
                status = 0;
            }
            else if (status is 0xF0 or 0xF7)
            {
                var length = ReadVariable(bytes, ref position, end, track);
                Need(position, length, end, track);
                position += (int)length;
                status = 0;
            }
            else if (status >= 0xF1)
            {
                throw new InvalidDataException(
                    $"track {track} holds status byte 0x{status:X2}, which a file never holds"
                );
            }
            else
            {
                var data = status is >= 0xC0 and <= 0xDF ? 1 : 2;
                Need(position, data, end, track);
                position += data;
            }

            count++;
        }

        return (count, tick);
    }

    static long ReadVariable(byte[] bytes, ref int position, int end, int track)
    {
        long value = 0;
        for (var i = 0; i < 4; i++)
        {
            if (position >= end)
            {
                throw Cut(track);
            }

            var b = bytes[position++];
            value = (value << 7) | (uint)(b & 0x7F);
            if (b < 0x80)
            {
                return value;
            }
        }

        throw new InvalidDataException($"track {track} holds a variable-length number longer than four bytes");
    }

    static void Need(int position, long length, int end, int track)
    {
        if (position + length > end)
        {
            throw Cut(track);
        }
    }

    static InvalidDataException Cut(int track) => new($"track {track} has an event cut short by the end of its chunk");

    static double Seconds(long endTick, List<(long Tick, int Microseconds)> tempos, int division)
    {
        if ((division & 0x8000) != 0)
        {
            var frames = 256 - (division >> 8);
            var ticksPerFrame = division & 0xFF;

            return endTick / (double)(frames * ticksPerFrame);
        }

        if (division == 0)
        {
            return 0;
        }

        var seconds = 0.0;
        var tick = 0L;
        var microseconds = DefaultMicrosecondsPerQuarter;
        foreach (var (at, next) in tempos.OrderBy(t => t.Tick))
        {
            if (at >= endTick)
            {
                break;
            }

            seconds += (at - tick) * (double)microseconds / division / 1e6;
            tick = at;
            microseconds = next;
        }

        return seconds + (endTick - tick) * (double)microseconds / division / 1e6;
    }

    static bool HasTag(byte[] bytes, int at, string tag) =>
        at + 4 <= bytes.Length && tag.Select((c, i) => bytes[at + i] == c).All(b => b);

    static int ReadInt(byte[] bytes, int at, int size)
    {
        var value = 0;
        for (var i = 0; i < size; i++)
        {
            value = (value << 8) | bytes[at + i];
        }

        return value;
    }
}

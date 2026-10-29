using System;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>A sound: unsigned 8-bit mono PCM samples and the rate at which the program plays them.</summary>
public sealed class Sound
{
    /// <summary>The samples as stored: unsigned, 0x80 is silence.</summary>
    public required ReadOnlyMemory<byte> Samples { get; init; }

    /// <summary>The sample rate in hertz.</summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// The samples as signed 8-bit values, one byte each, as Godot's 8-bit <c>AudioStreamWAV</c> format reads them:
    /// the sign bit of each stored sample flipped.
    /// </summary>
    public byte[] SignedSamples()
    {
        var signed = Samples.ToArray();
        for (var i = 0; i < signed.Length; i++) {
            signed[i] ^= 0x80;
        }

        return signed;
    }
}

/// <summary>
/// The sound file reader (<c>docs/rules/sound-files.md</c>). A sound file has no header: every byte is one
/// sample.
/// </summary>
public static class SoundFile
{
    /// <summary>The sample rate the program's platform layer gives DirectSound for every sound.</summary>
    public const int SampleRate = 8403;

    /// <summary>Reads one sound file. Throws <see cref="InvalidDataException"/> for an empty file.</summary>
    public static Sound Read(byte[] bytes) =>
        bytes.Length == 0
            ? throw new InvalidDataException("the sound file is empty")
            : new Sound { Samples = bytes, SampleRate = SampleRate };
}

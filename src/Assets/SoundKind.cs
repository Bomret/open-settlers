using System.Collections.Generic;
using System.IO;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of a sound: the sound itself, which the view plays.</summary>
public sealed record SoundPreview : Preview
{
    /// <summary>The sound to play.</summary>
    public required Sound Sound { get; init; }
}

/// <summary>
/// The sound files <c>snd/01.SND</c> to <c>snd/99.SND</c>, the numbers the program tries in turn
/// (<c>docs/rules/sound-files.md</c>). A number with no file is a gap, not a failure; a folder with no sound file
/// at all is one failure.
/// </summary>
public sealed class SoundKind : AssetKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "sound";

    /// <summary>The first and the last sound file number the program tries.</summary>
    public const int FirstNumber = 1,
        LastNumber = 99;

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override IEnumerable<Asset> Enumerate(Install install)
    {
        var found = false;
        for (var number = FirstNumber; number <= LastNumber; number++)
        {
            var file = $"snd/{number:00}.SND";
            if (!install.Exists(file))
            {
                continue;
            }

            found = true;

            yield return new Asset
            {
                Kind = KindName,
                File = file,
                Name = $"{number:00}.SND",
                Load = () => Preview(SoundFile.Read(install.ReadAll(file))),
            };
        }

        if (!found)
        {
            yield return new Asset
            {
                Kind = KindName,
                File = "snd",
                Name = "snd",
                Load = () =>
                    throw new FileNotFoundException(
                        $"no sound file: the program tries snd/{FirstNumber:00}.SND to snd/{LastNumber:00}.SND and the install holds none"
                    ),
            };
        }
    }

    static Preview Preview(Sound sound) =>
        new SoundPreview
        {
            Sound = sound,
            Summary = $"{sound.Samples.Length} samples, unsigned 8-bit mono, {sound.SampleRate} Hz",
        };
}

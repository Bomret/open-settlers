using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenSettlers.Formats;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The music file <c>snd/music_1.midi</c>, the only music file the program loads
/// (<c>docs/rules/music-file.md</c>). A missing file is a failure.
/// </summary>
sealed class MusicKind : AssetKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "music";

    /// <summary>The install-relative path of the music file.</summary>
    public const string File = "snd/music_1.midi";

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override IEnumerable<Asset> Enumerate(Install install)
    {
        yield return new Asset
        {
            Kind = KindName,
            File = File,
            Name = "music_1.midi",
            Load = () =>
                install.Exists(File)
                    ? Preview(MidiFile.Read(install.ReadAll(File)))
                    : throw new FileNotFoundException(
                        "the file is missing; the program loads it when the music option is on"
                    ),
        };
    }

    static Preview Preview(MusicFile music)
    {
        var tracks = string.Join(", ", music.Tracks.Select(t => $"track {t.Index + 1}: {t.EventCount} events"));

        return new MusicPreview
        {
            TrackEventCounts = music.Tracks.Select(t => t.EventCount).ToList(),
            LengthSeconds = music.LengthSeconds,
            Summary =
                $"{music.Tracks.Count} {(music.Tracks.Count == 1 ? "track" : "tracks")} ({tracks}), {music.LengthSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s",
        };
    }
}

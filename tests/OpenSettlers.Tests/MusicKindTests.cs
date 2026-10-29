using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MusicKindTests
{
    static Install Open(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);

        return install!;
    }

    static readonly byte[] Valid =
    [
        .. "MThd"u8.ToArray(),
        0,
        0,
        0,
        6,
        0,
        0,
        0,
        1,
        0,
        96,
        .. "MTrk"u8.ToArray(),
        0,
        0,
        0,
        8,
        96,
        0x90,
        60,
        100,
        0,
        0xFF,
        0x2F,
        0,
    ];

    [TestMethod]
    public void OnlyTheMusicFileTheProgramLoadsIsListed()
    {
        using var fake = new FakeInstall().Put("snd/music_1.midi", Valid).Put("snd/other.midi", Valid);

        var assets = new MusicKind().Enumerate(Open(fake)).ToList();

        CollectionAssert.AreEqual(new[] { "snd/music_1.midi" }, assets.Select(a => a.File).ToList());
    }

    [TestMethod]
    public void TheMusicFilePreviewsAsItsTracksAndLength()
    {
        using var fake = new FakeInstall().Put("snd/music_1.midi", Valid);

        var preview = (MusicPreview)new MusicKind().Enumerate(Open(fake)).Single().Load();

        Assert.AreEqual(1, preview.Music.Tracks.Count);
        Assert.AreEqual(0.5, preview.Music.LengthSeconds, 1e-9);
        StringAssert.Contains(preview.Summary, "1 track");
        StringAssert.Contains(preview.Summary, "0.5 s");
    }

    [TestMethod]
    public void ACorruptMusicFileFailsWithItsReasonAndNoPreview()
    {
        using var fake = new FakeInstall().Put("snd/music_1.midi", [.. Valid[..14], (byte)'X', .. Valid[15..]]);

        var asset = new MusicKind().Enumerate(Open(fake)).Single();

        Assert.ThrowsExactly<InvalidDataException>(() => asset.Load());
    }

    [TestMethod]
    public void AMissingMusicFileFailsWithItsFile()
    {
        using var fake = new FakeInstall();

        var asset = new MusicKind().Enumerate(Open(fake)).Single();

        Assert.AreEqual("snd/music_1.midi", asset.File);
        Assert.ThrowsExactly<FileNotFoundException>(() => asset.Load());
    }
}

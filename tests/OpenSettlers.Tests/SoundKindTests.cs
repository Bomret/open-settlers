using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SoundKindTests
{
    static Install Open(FakeInstall fake)
    {
        Install.TryOpen(LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _);

        return install!;
    }

    [TestMethod]
    public void TheSoundFilesListInNumberOrderAndAGapIsNoAsset()
    {
        using var fake = new FakeInstall().Put("snd/10.SND", [1]).Put("snd/02.SND", [1]).Put("snd/99.SND", [1]);

        var assets = new SoundKind().Enumerate(Open(fake)).ToList();

        CollectionAssert.AreEqual(new[] { "snd/02.SND", "snd/10.SND", "snd/99.SND" }, assets.Select(a => a.File).ToList());
    }

    [TestMethod]
    public void AFileOutsideTheNumbersTheProgramTriesIsNotListed()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", [1]).Put("snd/00.SND", [1]).Put("snd/100.SND", [1]);

        var assets = new SoundKind().Enumerate(Open(fake)).ToList();

        CollectionAssert.AreEqual(new[] { "snd/01.SND" }, assets.Select(a => a.File).ToList());
    }

    [TestMethod]
    public void ASoundFilePreviewsAsItsSoundAtTheProgramsRate()
    {
        using var fake = new FakeInstall().Put("snd/05.SND", [0x80, 0x90]);

        var preview = (SoundPreview)new SoundKind().Enumerate(Open(fake)).Single().Load();

        Assert.AreEqual(8403, preview.Sound.SampleRate);
        Assert.AreEqual(2, preview.Sound.Samples.Length);
    }

    [TestMethod]
    public void AFolderWithNoSoundFileIsOneFailureThatNamesTheFolderAndTheReason()
    {
        using var fake = new FakeInstall();
        Directory.CreateDirectory(Path.Combine(fake.Root, "snd"));

        var asset = new SoundKind().Enumerate(Open(fake)).Single();

        Assert.AreEqual("snd", asset.File);
        var failure = Assert.ThrowsExactly<FileNotFoundException>(() => asset.Load());
        StringAssert.Contains(failure.Message, "no sound file");
    }

    [TestMethod]
    public void AnEmptySoundFileFailsOnLoadAndItsNeighboursStillLoad()
    {
        using var fake = new FakeInstall().Put("snd/01.SND", []).Put("snd/02.SND", [0x80]);

        var assets = new SoundKind().Enumerate(Open(fake)).ToList();

        Assert.ThrowsExactly<InvalidDataException>(() => assets[0].Load());
        Assert.IsInstanceOfType<SoundPreview>(assets[1].Load());
    }

    [TestMethod]
    public void TheSoundKindRegistersItselfThroughDiscover()
    {
        Assert.IsTrue(AssetKind.Discover().Any(k => k is SoundKind));
    }
}

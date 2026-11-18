using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SoundFileTests
{
    [TestMethod]
    public void TheSamplesAreTheBytesOfTheFile()
    {
        var sound = SoundFile.Read([0x80, 0xFF, 0x00, 0x7F]);

        CollectionAssert.AreEqual(new byte[] { 0x80, 0xFF, 0x00, 0x7F }, sound.Samples.ToArray());
    }

    [TestMethod]
    public void TheSampleRateIsTheOneTheProgramGivesDirectSound()
    {
        var sound = SoundFile.Read([0x80]);

        Assert.AreEqual(8403, sound.SampleRate);
    }

    [TestMethod]
    public void AnEmptyFileIsUnreadable()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => SoundFile.Read([]));

        StringAssert.Contains(failure.Message, "empty");
    }

    [TestMethod]
    public void TheSignedSamplesFlipTheSignBitOfEachUnsignedSample()
    {
        var sound = SoundFile.Read([0x80, 0xFF, 0x00, 0x7F]);

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x7F, 0x80, 0xFF }, sound.SignedSamples());
    }
}

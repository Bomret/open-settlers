using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class MidiFileTests
{
    static byte[] Header(int format, int tracks, int division) =>
        [
            .. "MThd"u8.ToArray(),
            0,
            0,
            0,
            6,
            (byte)(format >> 8),
            (byte)format,
            (byte)(tracks >> 8),
            (byte)tracks,
            (byte)(division >> 8),
            (byte)division,
        ];

    static byte[] Ev(params byte[] bytes) => bytes;

    static byte[] Track(params byte[][] parts)
    {
        var events = parts.SelectMany(p => p).ToArray();

        return
        [
            .. "MTrk"u8.ToArray(),
            (byte)(events.Length >> 24),
            (byte)(events.Length >> 16),
            (byte)(events.Length >> 8),
            (byte)events.Length,
            .. events,
        ];
    }

    static byte[] Tempo(int delta, int microsecondsPerQuarter) =>
        [
            (byte)delta,
            0xFF,
            0x51,
            3,
            (byte)(microsecondsPerQuarter >> 16),
            (byte)(microsecondsPerQuarter >> 8),
            (byte)microsecondsPerQuarter,
        ];

    static readonly byte[] EndOfTrack = [0, 0xFF, 0x2F, 0];

    static byte[] File(byte[] header, params byte[][] tracks) => [.. header, .. tracks.SelectMany(t => t)];

    [TestMethod]
    public void TheHeaderGivesFormatTrackCountAndDivision()
    {
        var music = MidiFile.Read(File(Header(1, 2, 96), Track(EndOfTrack), Track(EndOfTrack)));

        Assert.AreEqual(1, music.Format);
        Assert.AreEqual(96, music.Division);
        Assert.AreEqual(2, music.Tracks.Count);
    }

    [TestMethod]
    public void ATrackCountsEveryEventIncludingMetaEventsAndSystemExclusive()
    {
        var track = Track(
            Ev(0, 0x90, 60, 100),
            Ev(0, 0xF0, 2, 1, 0xF7),
            Ev(4, 0x80, 60, 0),
            Ev(0, 0xFF, 0x01, 1, (byte)'x'),
            EndOfTrack
        );

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(5, music.Tracks.Single().EventCount);
    }

    [TestMethod]
    public void RunningStatusReusesTheLastStatusByte()
    {
        var track = Track(Ev(0, 0x90, 60, 100), Ev(1, 61, 100), Ev(1, 62, 100), EndOfTrack);

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(4, music.Tracks.Single().EventCount);
    }

    [TestMethod]
    public void RunningStatusWorksForOneDataByteEvents()
    {
        var track = Track(Ev(0, 0xC0, 5), Ev(0, 6), EndOfTrack);

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(3, music.Tracks.Single().EventCount);
    }

    [TestMethod]
    public void ADeltaTimeMayTakeSeveralBytes()
    {
        // 0x81 0x00 is 128 ticks; at 96 ticks per quarter and 500000 us per quarter that is 128/96*0.5 s.
        var track = Track(Ev(0x81, 0x00, 0x90, 60, 100));

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(128 / 96.0 * 0.5, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void WithoutATempoEventTheLengthUsesHalfASecondPerQuarter()
    {
        var track = Track(Ev(96, 0x90, 60, 100), Ev(96, 0x80, 60, 0), EndOfTrack);

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(1.0, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void ATempoChangeAppliesFromItsTickOn()
    {
        // 96 ticks at 500000, then 96 ticks at 1000000: 0.5 s + 1 s.
        var track = Track(Tempo(0, 500000), Ev(96, 0x90, 60, 100), Tempo(0, 1000000), Ev(96, 0x80, 60, 0), EndOfTrack);

        var music = MidiFile.Read(File(Header(0, 1, 96), track));

        Assert.AreEqual(1.5, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void ATempoEventOfAnotherTrackAppliesToTheLongerTrack()
    {
        var conductor = Track(Tempo(0, 1000000), EndOfTrack);
        var notes = Track(Ev(96, 0x90, 60, 100), EndOfTrack);

        var music = MidiFile.Read(File(Header(1, 2, 96), conductor, notes));

        Assert.AreEqual(1.0, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void TheLengthIsThatOfTheLongestTrack()
    {
        var shortTrack = Track(Ev(48, 0x90, 60, 100));
        var longTrack = Track(Ev(0x81, 0x00, 0x90, 60, 100));

        var music = MidiFile.Read(File(Header(1, 2, 96), shortTrack, longTrack));

        Assert.AreEqual(128 / 96.0 * 0.5, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void AFrameBasedDivisionCountsTicksPerSecondFromFramesAndTicksPerFrame()
    {
        // -25 frames per second, 40 ticks per frame: 1000 ticks per second.
        var track = Track(Ev(0x87, 0x68, 0x90, 60, 100));

        var music = MidiFile.Read(File(Header(0, 1, 0xE728), track));

        Assert.AreEqual(1.0, music.LengthSeconds, 1e-9);
    }

    [TestMethod]
    public void ABadHeaderTagIsUnreadable()
    {
        var bytes = File(Header(0, 1, 96), Track(EndOfTrack));
        bytes[0] = (byte)'X';

        var error = Assert.ThrowsExactly<InvalidDataException>(() => MidiFile.Read(bytes));

        StringAssert.Contains(error.Message, "header");
    }

    [TestMethod]
    public void AHeaderLengthAboveSixIsUnreadable()
    {
        byte[] header = [.. Header(0, 1, 96), 0];
        header[7] = 7;

        var error = Assert.ThrowsExactly<InvalidDataException>(() => MidiFile.Read(File(header, Track(EndOfTrack))));

        StringAssert.Contains(error.Message, "the header length is not 6");
    }

    [TestMethod]
    public void AFileShorterThanTheHeaderIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() => MidiFile.Read(Header(0, 1, 96)[..10]));

        StringAssert.Contains(error.Message, "the header chunk is cut short");
    }

    [TestMethod]
    public void AFrameBasedDivisionOfZeroTicksPerFrameGivesLengthZero()
    {
        var track = Track(Ev(0x87, 0x68, 0x90, 60, 100));

        var music = MidiFile.Read(File(Header(0, 1, 0xE700), track));

        Assert.AreEqual(0.0, music.LengthSeconds);
    }

    [TestMethod]
    public void ABadTrackTagIsUnreadable()
    {
        var bytes = File(Header(0, 1, 96), Track(EndOfTrack));
        bytes[14] = (byte)'X';

        var error = Assert.ThrowsExactly<InvalidDataException>(() => MidiFile.Read(bytes));

        StringAssert.Contains(error.Message, "bad track chunk tag");
    }

    [TestMethod]
    public void ATrackChunkPastTheEndOfTheFileIsUnreadable()
    {
        var bytes = File(Header(0, 1, 96), Track(Ev(0, 0x90, 60, 100)));

        var error = Assert.ThrowsExactly<InvalidDataException>(() => MidiFile.Read(bytes[..^2]));

        StringAssert.Contains(error.Message, "past the end");
    }

    [TestMethod]
    public void ATrackMissingFromTheFileIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MidiFile.Read(File(Header(0, 2, 96), Track(EndOfTrack)))
        );

        StringAssert.Contains(error.Message, "is missing");
    }

    [TestMethod]
    public void AnEventCutShortByTheEndOfItsChunkIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MidiFile.Read(File(Header(0, 1, 96), Track(Ev(0, 0x90, 60))))
        );

        StringAssert.Contains(error.Message, "cut short");
    }

    [TestMethod]
    public void AMetaEventWhoseDataRunsPastItsChunkIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MidiFile.Read(File(Header(0, 1, 96), Track(Ev(0, 0xFF, 0x01, 5, (byte)'a'))))
        );

        StringAssert.Contains(error.Message, "cut short");
    }

    [TestMethod]
    public void ADeltaTimeCutShortIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MidiFile.Read(File(Header(0, 1, 96), Track(Ev(0x81))))
        );

        StringAssert.Contains(error.Message, "cut short");
    }

    [TestMethod]
    public void ADataByteWithoutARunningStatusIsUnreadable()
    {
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            MidiFile.Read(File(Header(0, 1, 96), Track(Ev(0, 60, 100))))
        );

        StringAssert.Contains(error.Message, "running status");
    }
}

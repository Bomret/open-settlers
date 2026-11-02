using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Every expected value below is the output of the emulator harness (tools/GhidraDump/emulate.py) running the
// seed code of the reference Siedler1.exe: the parse of typed characters (0x467e12 to 0x467f39), the display of
// the groups (0x467831), and the mask step (0x468b3d) (ADR 0008). A command is quoted with each test.
[TestClass]
public sealed class SeedTests
{
    private static Seed Parse(string screen)
    {
        Assert.IsTrue(Seed.TryParse(screen, out var seed, out var problem), problem);
        return seed;
    }

    [TestMethod]
    public void GroupsOfZeroGiveAllZeroWords()
    {
        // Emulator: parse:1111111111111111 gives 0000,0000,0000 (digit 1 is the value 0).
        Assert.AreEqual(new Seed(0x0000, 0x0000, 0x0000), Parse("1111 1111 1111 1111"));
    }

    [TestMethod]
    public void GroupsOfFffGiveAllOnesWords()
    {
        // Emulator: parse:8888888888888888 gives FFFF,FFFF,FFFF (digit 8 is the value 7).
        Assert.AreEqual(new Seed(0xFFFF, 0xFFFF, 0xFFFF), Parse("8888 8888 8888 8888"));
    }

    [TestMethod]
    public void GroupsAcrossTheWordBoundariesGiveTheEmulatorWords()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739. The 24 bits of two groups spread over
        // bytes of two words.
        Assert.AreEqual(new Seed(0x3905, 0x0577, 0x7739), Parse("1234 5678 1234 5678"));
    }

    [TestMethod]
    public void AMixOfBoundaryGroupsGivesTheEmulatorWords()
    {
        // Emulator: parse:1111888811118888 gives 0F00,00FF,FF0F.
        Assert.AreEqual(new Seed(0x0F00, 0x00FF, 0xFF0F), Parse("1111-8888-1111-8888"));
    }

    [TestMethod]
    public void TheLowestDigitOfTheLastGroupReachesTheLastWord()
    {
        // Emulator: parse:1111111111118111 gives 0000,0000,000E.
        Assert.AreEqual(new Seed(0x0000, 0x0000, 0x000E), Parse("1111 1111 1111 8111"));
    }

    [TestMethod]
    public void ZeroWordsShowAsGroupsOfDigitOne()
    {
        // Emulator: show:0000,0000,0000 gives groups 000, 000, 000, 000 as 1111 each.
        Assert.AreEqual("1111 1111 1111 1111", new Seed(0, 0, 0).ToScreen());
    }

    [TestMethod]
    public void AllOnesWordsShowAsGroupsOfDigitEight()
    {
        // Emulator: show:FFFF,FFFF,FFFF gives groups FFF, FFF, FFF, FFF as 8888 each.
        Assert.AreEqual("8888 8888 8888 8888", new Seed(0xFFFF, 0xFFFF, 0xFFFF).ToScreen());
    }

    [TestMethod]
    public void GroupsAcrossTheWordBoundariesShowAsTheEmulatorShowsThem()
    {
        // Emulator: show:1234,5678,9ABC gives groups 123:1554, 812:5133, BC5:6816, ABC:6385. The second group
        // takes bits from two words and the third group from two words.
        Assert.AreEqual("1554 5133 6816 6385", new Seed(0x1234, 0x5678, 0x9ABC).ToScreen());
    }

    [TestMethod]
    public void MixedWordsShowAsTheEmulatorShowsThem()
    {
        // Emulator: show:00FF,F000,0FF0 gives groups 00F:1128, 000:1111, F0F:8528, FF0:8871.
        Assert.AreEqual("1128 1111 8528 8871", new Seed(0x00FF, 0xF000, 0x0FF0).ToScreen());
    }

    [TestMethod]
    public void TheMapSeedOfZeroWordsIsTheMasks()
    {
        // Emulator: mask:0000,0000,0000 gives 5A5A,A5A5,C3C3.
        Assert.AreEqual(new MapSeed(0x5A5A, 0xA5A5, 0xC3C3), new Seed(0, 0, 0).ToMapSeed());
    }

    [TestMethod]
    public void TheMapSeedOfAllOnesWordsIsTheInvertedMasks()
    {
        // Emulator: mask:FFFF,FFFF,FFFF gives A5A5,5A5A,3C3C.
        Assert.AreEqual(new MapSeed(0xA5A5, 0x5A5A, 0x3C3C), new Seed(0xFFFF, 0xFFFF, 0xFFFF).ToMapSeed());
    }

    [TestMethod]
    public void TheMapSeedOfAKnownSeedMatchesTheEmulator()
    {
        // Emulator: mask:1234,5678,9ABC gives 486E,F3DD,597F.
        Assert.AreEqual(new MapSeed(0x486E, 0xF3DD, 0x597F), new Seed(0x1234, 0x5678, 0x9ABC).ToMapSeed());
    }

    [TestMethod]
    public void ATypedSeedBecomesTheMapSeedTheEmulatorGives()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739; mask:3905,0577,7739 gives the map seed below.
        Assert.AreEqual(new MapSeed(0x635F, 0xA0D2, 0xB4FA), Parse("1234 5678 1234 5678").ToMapSeed());
    }

    [TestMethod]
    [DataRow("", "empty")]
    [DataRow("   ", "empty")]
    [DataRow("1111 1111 1111", "4 groups")]
    [DataRow("1111 1111 1111 1111 1111", "4 groups")]
    [DataRow("1111111111111111", "4 groups")]
    [DataRow("1111 1111 1111 111", "4 characters")]
    [DataRow("1111 1111 1111 11111", "4 characters")]
    [DataRow("1111 1111 1111 1119", "'9'")]
    [DataRow("1111 1111 1111 1110", "'0'")]
    [DataRow("1111 1111 1111 111G", "'G'")]
    [DataRow("1111 1111 1111 FFFF", "'F'")]
    public void AMalformedScreenSeedIsRefusedWithItsReason(string screen, string reason)
    {
        Assert.IsFalse(Seed.TryParse(screen, out _, out var problem));
        StringAssert.Contains(problem, reason);
    }
}

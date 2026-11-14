using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Every expected value below is the output of the emulator harness (tools/GhidraDump/emulate.py) running the
// seed code of the reference Siedler1.exe: the parse of typed characters (0x467e12 to 0x467f39), the display of
// the groups (0x467831), and the mask step (0x468b3d) (ADR 0008). A command is quoted with each test.
[TestClass]
public sealed class SeedTests
{
    static Seed Parse(string screen)
    {
        Assert.IsTrue(Seed.TryParse(screen, out var seed, out var problem), problem);
        return seed;
    }

    [TestMethod]
    [DataRow("1234567812345\0" + "78")]
    [DataRow("1234 5678 1234 56\0" + "8")]
    public void ANulCharacterIsNotADigitFrom1To8(string screen)
    {
        Assert.IsFalse(Seed.TryParse(screen, out _, out var problem));
        Assert.AreEqual("'\0' is not a digit from 1 to 8", problem);
    }

    [TestMethod]
    public void GroupsOfZeroGiveAllZeroWords()
    {
        // Emulator: parse:1111111111111111 gives 0000,0000,0000 (digit 1 is the value 0).
        Assert.AreEqual(
            new Seed
            {
                W0 = 0x0000,
                W1 = 0x0000,
                W2 = 0x0000,
            },
            Parse("1111 1111 1111 1111")
        );
    }

    [TestMethod]
    public void GroupsOfFffGiveAllOnesWords()
    {
        // Emulator: parse:8888888888888888 gives FFFF,FFFF,FFFF (digit 8 is the value 7).
        Assert.AreEqual(
            new Seed
            {
                W0 = 0xFFFF,
                W1 = 0xFFFF,
                W2 = 0xFFFF,
            },
            Parse("8888 8888 8888 8888")
        );
    }

    [TestMethod]
    public void GroupsAcrossTheWordBoundariesGiveTheEmulatorWords()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739. The 24 bits of two groups spread over
        // bytes of two words.
        Assert.AreEqual(
            new Seed
            {
                W0 = 0x3905,
                W1 = 0x0577,
                W2 = 0x7739,
            },
            Parse("1234 5678 1234 5678")
        );
    }

    [TestMethod]
    public void AMixOfBoundaryGroupsGivesTheEmulatorWords()
    {
        // Emulator: parse:1111888811118888 gives 0F00,00FF,FF0F.
        Assert.AreEqual(
            new Seed
            {
                W0 = 0x0F00,
                W1 = 0x00FF,
                W2 = 0xFF0F,
            },
            Parse("1111 8888 1111 8888")
        );
    }

    [TestMethod]
    public void TheLowestDigitOfTheLastGroupReachesTheLastWord()
    {
        // Emulator: parse:1111111111118111 gives 0000,0000,000E.
        Assert.AreEqual(
            new Seed
            {
                W0 = 0x0000,
                W1 = 0x0000,
                W2 = 0x000E,
            },
            Parse("1111 1111 1111 8111")
        );
    }

    [TestMethod]
    public void ZeroWordsShowAsGroupsOfDigitOne()
    {
        // Emulator: show:0000,0000,0000 gives groups 000, 000, 000, 000 as 1111 each.
        Assert.AreEqual(
            "1111 1111 1111 1111",
            new Seed
            {
                W0 = 0,
                W1 = 0,
                W2 = 0,
            }.ToScreen()
        );
    }

    [TestMethod]
    public void AllOnesWordsShowAsGroupsOfDigitEight()
    {
        // Emulator: show:FFFF,FFFF,FFFF gives groups FFF, FFF, FFF, FFF as 8888 each.
        Assert.AreEqual(
            "8888 8888 8888 8888",
            new Seed
            {
                W0 = 0xFFFF,
                W1 = 0xFFFF,
                W2 = 0xFFFF,
            }.ToScreen()
        );
    }

    [TestMethod]
    public void GroupsAcrossTheWordBoundariesShowAsTheEmulatorShowsThem()
    {
        // Emulator: show:1234,5678,9ABC gives groups 123:1554, 812:5133, BC5:6816, ABC:6385. The second group
        // takes bits from two words and the third group from two words.
        Assert.AreEqual(
            "1554 5133 6816 6385",
            new Seed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            }.ToScreen()
        );
    }

    [TestMethod]
    public void MixedWordsShowAsTheEmulatorShowsThem()
    {
        // Emulator: show:00FF,F000,0FF0 gives groups 00F:1128, 000:1111, F0F:8528, FF0:8871.
        Assert.AreEqual(
            "1128 1111 8528 8871",
            new Seed
            {
                W0 = 0x00FF,
                W1 = 0xF000,
                W2 = 0x0FF0,
            }.ToScreen()
        );
    }

    [TestMethod]
    public void TheMapSeedOfZeroWordsIsTheMasks()
    {
        // Emulator: mask:0000,0000,0000 gives 5A5A,A5A5,C3C3.
        Assert.AreEqual(
            new MapSeed
            {
                W0 = 0x5A5A,
                W1 = 0xA5A5,
                W2 = 0xC3C3,
            },
            new Seed
            {
                W0 = 0,
                W1 = 0,
                W2 = 0,
            }.ToMapSeed()
        );
    }

    [TestMethod]
    public void TheMapSeedOfAllOnesWordsIsTheInvertedMasks()
    {
        // Emulator: mask:FFFF,FFFF,FFFF gives A5A5,5A5A,3C3C.
        Assert.AreEqual(
            new MapSeed
            {
                W0 = 0xA5A5,
                W1 = 0x5A5A,
                W2 = 0x3C3C,
            },
            new Seed
            {
                W0 = 0xFFFF,
                W1 = 0xFFFF,
                W2 = 0xFFFF,
            }.ToMapSeed()
        );
    }

    [TestMethod]
    public void TheMapSeedOfAKnownSeedMatchesTheEmulator()
    {
        // Emulator: mask:1234,5678,9ABC gives 486E,F3DD,597F.
        Assert.AreEqual(
            new MapSeed
            {
                W0 = 0x486E,
                W1 = 0xF3DD,
                W2 = 0x597F,
            },
            new Seed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            }.ToMapSeed()
        );
    }

    [TestMethod]
    public void ATypedSeedBecomesTheMapSeedTheEmulatorGives()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739; mask:3905,0577,7739 gives the map seed below.
        Assert.AreEqual(
            new MapSeed
            {
                W0 = 0x635F,
                W1 = 0xA0D2,
                W2 = 0xB4FA,
            },
            Parse("1234 5678 1234 5678").ToMapSeed()
        );
    }

    [TestMethod]
    public void ATypedSeedWithoutGroupSeparatorsParsesLikeTheGroupedForm()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739; the original takes the 16 digits as typed.
        Assert.AreEqual(Parse("1234 5678 1234 5678"), Parse("1234567812345678"));
    }

    [TestMethod]
    public void ATypedSeedDoesNotShowAsItWasTyped()
    {
        // Emulator: parse:1234567812345678 gives 3905,0577,7739; show:3905,0577,7739 gives groups 390, 739,
        // 390, 739 as 2731 4582 2731 4582. The original's parse and display are not inverse (map-start.md).
        var seed = Parse("1234 5678 1234 5678");
        Assert.AreEqual("2731 4582 2731 4582", seed.ToScreen());
    }

    [TestMethod]
    [DataRow(null, "empty")]
    [DataRow("", "empty")]
    [DataRow("   ", "3 characters")]
    [DataRow("111111111111111", "15 characters")]
    [DataRow("11111111111111111", "17 characters")]
    [DataRow("1111 1111 1111", "14 characters")]
    [DataRow("1111 1111 1111 1111 1111", "24 characters")]
    [DataRow(" 1111111111111111", "17 characters")]
    [DataRow("1111111111111111 ", "17 characters")]
    [DataRow(" 1111 1111 1111 1111 ", "21 characters")]
    [DataRow("1111-1111-1111-1111", "19 characters")]
    [DataRow("1111-1111 1111 1111", "19 characters")]
    [DataRow("1111  1111 1111 111", "19 characters")]
    [DataRow("11111 111 1111 1111", "19 characters")]
    [DataRow("111111111111111 9", "17 characters")]
    [DataRow("1111111111111119", "'9'")]
    [DataRow("1111111111111110", "'0'")]
    [DataRow("111111111111111G", "'G'")]
    [DataRow("1111 1111 1111 FFFF", "'F'")]
    public void AMalformedScreenSeedIsRefusedWithItsReason(string? screen, string reason)
    {
        Assert.IsFalse(Seed.TryParse(screen, out _, out var problem));
        StringAssert.Contains(problem, reason);
    }
}

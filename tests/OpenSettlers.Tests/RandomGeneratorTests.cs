using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Every expected value below is the output of the emulator harness (tools/GhidraDump/emulate.py) running
// Rng_Step at 0x466ade of the reference Siedler1.exe from the stated state words (ADR 0008).
[TestClass]
public sealed class RandomGeneratorTests
{
    [TestMethod]
    public void AllZeroWordsStayAllZero()
    {
        // Emulator: rng:0000,0000,0000 gives 0000,0000,0000.
        Assert.AreEqual(
            new RandomGenerator
            {
                W0 = 0x0000,
                W1 = 0x0000,
                W2 = 0x0000,
            },
            new RandomGenerator
            {
                W0 = 0,
                W1 = 0,
                W2 = 0,
            }.Step()
        );
    }

    [TestMethod]
    public void AllOnesWordsStepToTheEmulatorState()
    {
        // Emulator: rng:FFFF,FFFF,FFFF gives 0001,8000,7FFF.
        Assert.AreEqual(
            new RandomGenerator
            {
                W0 = 0x0001,
                W1 = 0x8000,
                W2 = 0x7FFF,
            },
            new RandomGenerator
            {
                W0 = 0xFFFF,
                W1 = 0xFFFF,
                W2 = 0xFFFF,
            }.Step()
        );
    }

    [TestMethod]
    public void SmallWordsStepToTheEmulatorState()
    {
        // Emulator: rng:0001,0002,0003 gives 0000,8003,8002.
        Assert.AreEqual(
            new RandomGenerator
            {
                W0 = 0x0000,
                W1 = 0x8003,
                W2 = 0x8002,
            },
            new RandomGenerator
            {
                W0 = 1,
                W1 = 2,
                W2 = 3,
            }.Step()
        );
    }

    [TestMethod]
    public void WordsAtTheSignBoundaryStepToTheEmulatorState()
    {
        // Emulator: rng:8000,0001,FFFE gives 7FFF,7FFF,FFFF.
        Assert.AreEqual(
            new RandomGenerator
            {
                W0 = 0x7FFF,
                W1 = 0x7FFF,
                W2 = 0xFFFF,
            },
            new RandomGenerator
            {
                W0 = 0x8000,
                W1 = 0x0001,
                W2 = 0xFFFE,
            }.Step()
        );
    }

    [TestMethod]
    public void SixStepsFollowTheEmulatorSequence()
    {
        // Emulator: steps:6:1234,5678,9ABC gives the first words below after each step.
        ushort[] first = [0xF210, 0x3D2C, 0xEB3F, 0xB326, 0xE356, 0x6D98];
        var generator = new RandomGenerator
        {
            W0 = 0x1234,
            W1 = 0x5678,
            W2 = 0x9ABC,
        };
        var seen = new ushort[first.Length];

        for (var i = 0; i < first.Length; i++)
        {
            generator = generator.Step();
            seen[i] = generator.W0;
        }

        CollectionAssert.AreEqual(first, seen);
        Assert.AreEqual(
            new RandomGenerator
            {
                W0 = 0x6D98,
                W1 = 0x7060,
                W2 = 0x4561,
            },
            generator
        );
    }
}

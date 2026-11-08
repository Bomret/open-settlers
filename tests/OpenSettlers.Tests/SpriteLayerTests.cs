using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SpriteLayerTests
{
    // Godot keeps a vertex color in 8 bits a channel: the low byte of the layer goes in red, the high byte in green, each
    // a whole number of 255ths, which the shaders read back as red + 256 * green.
    [TestMethod]
    public void ALayerIsSplitIntoItsLowByteInRedAndItsHighByteInGreen()
    {
        Assert.AreEqual((44 / 255f, 1 / 255f), SpriteLayer.Channels(300));
        Assert.AreEqual((255 / 255f, 7 / 255f), SpriteLayer.Channels(2047));
    }

    // The settlers' sprites take more layers than one 8-bit channel holds: the woodcutter's lie past 255.
    [TestMethod]
    public void TheSettlersSpritesTakeMoreLayersThanOneChannelHolds()
    {
        var graphics = SettlerGraphics.Load(
            TestProgram.OpenInstall(),
            TestProgram.Tables(),
            Job.CarrierCode,
            Job.WoodcutterCode,
            RuleTables.TypeOneHutJob
        );

        Assert.IsGreaterThan(256, graphics.Images.Count);
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SpriteLayerTests
{
    // Godot keeps a vertex color in 8 bits a channel; every layer an array texture of 2048 layers holds survives that.
    [TestMethod]
    public void EveryLayerOfAnArrayTextureSurvivesTheEightBitsOfAVertexColor()
    {
        for (var layer = 0; layer < 2048; layer++)
        {
            var (red, green) = SpriteLayer.Channels(layer);

            Assert.AreEqual(layer, SpriteLayer.Decode((byte)Math.Round(red * 255), (byte)Math.Round(green * 255)));
        }
    }

    // The settlers' sprites take more layers than one 8-bit channel holds: the woodcutter's lie past 255.
    [TestMethod]
    public void TheSettlersSpritesTakeMoreLayersThanOneChannelHolds()
    {
        var graphics = SettlerGraphics.Load(
            TestProgram.OpenInstall(),
            TestProgram.Tables(),
            Settler.CarrierJob,
            Settler.WoodcutterJob,
            ProgramTables.TypeOneHutJob
        );

        Assert.IsGreaterThan(256, graphics.Images.Count);
    }
}

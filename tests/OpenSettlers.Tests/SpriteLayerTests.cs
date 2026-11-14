using System.Linq;
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
    [TestCategory(Needs.Install)]
    public void TheSettlersSpritesTakeMoreLayersThanOneChannelHolds()
    {
        var install = TestProgram.OpenInstall();
        var tables = TestProgram.Tables();
        var pair = SettlerPair.Of(install, tables);
        var source = HistoryEditionSource.Load(install, tables);
        var map = MapGenerator.Generate(
            TestProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        var layers = new ImageLayers();
        var walks = new Walks
        {
            Errand = new ToStorage(),
            Heading = new Heading { Value = 0 },
        };
        var back = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(0, 0),
                Purpose = new BackToHut { Carrying = true },
            },
        };
        var carriers =
            from good in Enumerable.Range(1, RuleTables.CarriedFigureCount - 1)
            from frame in Enumerable.Range(0, 0x79)
            from step in Enumerable.Range(0, 128)
            select new Settler(
                new Carrier(),
                0,
                4,
                3,
                0,
                new WalksRoad
                {
                    Load = new Load
                    {
                        Good = new Good { Code = good },
                        Destination = null,
                    },
                    Heading = new Heading { Value = 0 },
                }
            )
            {
                Frame = frame,
                Timer = (ushort)(step * 8),
            };
        var others =
            from job in new Job[] { new Woodcutter(), new TypeOneHut() }
            from frame in Enumerable.Range(4, 0x79 - 4)
            from step in Enumerable.Range(0, 128)
            from activity in new Activity[] { walks, back }
            select new Settler(job, 0, 4, 3, 0, activity) { Frame = frame, Timer = (ushort)(step * 8) };
        var settlers = carriers.Concat(others);

        foreach (var settler in settlers)
        {
            ObjectQuads.Flat(pair.Terms(map, [settler], 0), source, layers);
        }

        Assert.IsGreaterThan(256, layers.Images.Count);
    }
}

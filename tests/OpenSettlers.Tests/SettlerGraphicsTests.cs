using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SettlerGraphicsTests
{
    [TestMethod]
    public void TheSpritesOfTheCarrierAndTheWoodcutterLoadFromTheInstallInTheLayersAnArrayTextureHolds()
    {
        // The sequences name figures for every job, and some of the entries they reach are no sprites: the load leaves
        // those out and keeps the rest below the layer limit of 256.
        var graphics = SettlerGraphics.Load(
            TestProgram.OpenInstall(),
            TestProgram.Tables(),
            Settler.CarrierJob,
            Settler.WoodcutterJob
        );

        Assert.IsGreaterThan(0, graphics.Images.Count);
        Assert.IsLessThanOrEqualTo(256, graphics.Images.Count);
    }
}

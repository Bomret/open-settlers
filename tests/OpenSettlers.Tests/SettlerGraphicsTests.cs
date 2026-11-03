using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class SettlerGraphicsTests
{
    private static SettlerGraphics Load() =>
        SettlerGraphics.Load(
            TestProgram.OpenInstall(),
            TestProgram.Tables(),
            Settler.CarrierJob,
            Settler.WoodcutterJob
        );

    [TestMethod]
    public void TheSpritesOfTheCarrierAndTheWoodcutterLoadFromTheInstallInTheLayersAnArrayTextureHolds()
    {
        // The sequences name figures for every job, and some of the entries they reach are no sprites: the load leaves
        // those out. Below the layer limit of 2048 the limit leaves none of the rest out.
        var graphics = Load();

        Assert.IsGreaterThan(0, graphics.Images.Count);
        Assert.IsLessThan(2048, graphics.Images.Count);
    }

    // The original draws the overlay of the figure 1850 + i from the entry 2500 + i (0x406610, docs/rules/graphics-images.md).
    // Sprites names each figure followed by its overlay; and a second figure can fall in the figures' range too (3150 plus a
    // negative offset), so the pairs come from there. A
    // figure byte of another job can reach past 2500, where no overlay range lies; those are left out.
    [TestMethod]
    public void EveryFigureOfTheCarrierAndTheWoodcutterLoadsItsColourOverlay()
    {
        var graphics = Load();
        var colour = SettlerFigures.ColourOf(0);
        var figures = new[] { Settler.CarrierJob, Settler.WoodcutterJob }
            .Select(job => graphics.Figures.Sprites(job, colour, Game.GoodLog).ToArray())
            .SelectMany(sprites => sprites.Zip(sprites.Skip(1)))
            .Where(pair =>
                pair.First is { Colour: -1, Entry: < 2500 }
                && pair.Second.Colour == colour
                && graphics.Layers.ContainsKey(pair.First)
            )
            .Select(pair => (Figure: pair.First.Entry, Overlay: pair.Second.Entry))
            .Distinct()
            .ToArray();
        var missing = figures
            .Where(f => f.Overlay != f.Figure - 1850 + 2500 || !graphics.Layers.ContainsKey((f.Overlay, colour)))
            .ToArray();

        Assert.IsGreaterThan(0, figures.Length);
        Assert.IsEmpty(
            missing,
            $"{missing.Length} of {figures.Length} figures have no overlay: {string.Join(", ", missing.Take(20))}"
        );
    }

    // A made-up overlay of two pixels, one drawn and one transparent: only the drawn pixel shows in the colour.
    [TestMethod]
    public void AColourOverlayKeepsItsTransparentPixelsTransparent()
    {
        var overlay = new ObjectImage(2, 1, 0, 0, [1, 0], [255, 0]);

        var filled = SettlerGraphics.Fill(overlay, 0x40);

        CollectionAssert.AreEqual(new byte[] { 0x40, 0x40 }, filled.Indices);
        CollectionAssert.AreEqual(new byte[] { 255, 0 }, filled.Coverage);
    }
}

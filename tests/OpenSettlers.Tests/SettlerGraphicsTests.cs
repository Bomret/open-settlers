using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
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

    private static Map Map() =>
        MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);

    private static Settler Woodcutter(int frame, int timer, int state, int mark = 0, int heading = 0) =>
        new(Settler.WoodcutterJob, 0, 4, 3, 0, state, 0)
        {
            Frame = frame,
            Timer = (ushort)timer,
            Target = mark << 8,
            Heading = heading,
        };

    // The table rule at the program's addresses (docs/rules/settlers.md): the woodcutter's walk is the row 0x0b of the bases
    // (48, 54), the cut a figure byte from 0x80 in the row 0x0f (272, none), the way back with the log the row 0x10 (48, 60).
    [TestMethod]
    public void TheWoodcuttersWalkAndCutAreDrawnWithTheEntriesTheTableRuleGives()
    {
        var figures = Load().Figures;
        var map = Map();

        // Frame 4 with no time left: the figure byte 7 (offsets 7, 0): 1850 + 48 + 7, 2500 + 48 + 7, 3150 + 54 + 0.
        var walk = figures.Draws(map, [Woodcutter(4, 0, 2)], 0).Select(d => d.Entry);
        // The way back with the log: the same byte in the row 0x10: 1850 + 48 + 7, 2500 + 48 + 7, 3150 + 60 + 0.
        var back = figures.Draws(map, [Woodcutter(4, 0, 0x10, 0x80, 1)], 0).Select(d => d.Entry);
        // Frame 0x74 with 800 units left: the triple 100 holds the figure byte 0x83, the word 0x0f03: 1850 + 272 + 3, 2500 + 272 + 3.
        var cut = figures.Draws(map, [Woodcutter(0x74, 800, 0x11)], 0).Select(d => d.Entry);

        CollectionAssert.AreEqual(new[] { 1905, 2555, 3204 }, walk.ToArray());
        CollectionAssert.AreEqual(new[] { 1905, 2555, 3210 }, back.ToArray());
        CollectionAssert.AreEqual(new[] { 2125, 2775 }, cut.ToArray());
    }

    private static Settler LoadedCarrier(int frame, int timer = 0) =>
        new(Settler.CarrierJob, 0, 4, 3, 0, 3, 0)
        {
            Frame = frame,
            Timer = (ushort)timer,
            Mode = Game.GoodLog,
        };

    // The carried table at 0x43f4b7 holds 0x1400 for the log: the carrier's figure word is its figure byte in the row 0x14,
    // so each of the six directions (the step shapes 4 + 9 * direction on level ground) is drawn with the figure of the byte
    // the sequence gives in that row: 1850 + base + offset, the overlay 2500 + base + offset.
    [TestMethod]
    public void ACarrierCarryingALogIsDrawnInEachDirectionWithTheEntriesTheCarriedRowGives()
    {
        var install = TestProgram.OpenInstall();
        var tables = TestProgram.Tables();
        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var sequences = archive.Data(archive[2]).ToArray();
        var figures = Load().Figures;
        var map = Map();

        Assert.AreEqual(0x1400, tables.CarriedFigures[Game.GoodLog]);
        for (var direction = 0; direction < 6; direction++)
        {
            var frame = 4 + 9 * direction;
            var start =
                4
                + (
                    (sequences[4 + 4 * frame] << 24)
                    | (sequences[5 + 4 * frame] << 16)
                    | (sequences[6 + 4 * frame] << 8)
                    | sequences[7 + 4 * frame]
                );
            var figure = sequences[start];
            var first = tables.FigureBases[2 * 0x14] + tables.FigureOffsets[2 * figure];

            var entries = figures.Draws(map, [LoadedCarrier(frame)], 0).Select(d => d.Entry).Take(2).ToArray();

            CollectionAssert.AreEqual(new[] { 1850 + first, 2500 + first }, entries, $"direction {direction}");
        }
    }

    // Draws and Sprites apply the one rule: every entry a carrier holding a log is drawn with was loaded.
    [TestMethod]
    public void EveryEntryACarrierCarryingALogIsDrawnWithIsAmongThoseLoaded()
    {
        var graphics = Load();
        var map = Map();
        var missing = new HashSet<(int, int)>();
        foreach (var frame in Enumerable.Range(4, 55))
        {
            foreach (var timer in Enumerable.Range(0, 1024).Where(timer => timer % 8 == 0))
            {
                foreach (var draw in graphics.Figures.Draws(map, [LoadedCarrier(frame, timer)], 0))
                {
                    if (!graphics.Layers.ContainsKey((draw.Entry, draw.Colour)))
                    {
                        missing.Add((draw.Entry, draw.Colour));
                    }
                }
            }
        }

        Assert.IsEmpty(missing, $"entries drawn and not loaded: {string.Join(", ", missing.Take(20))}");
    }

    // The sequences name figure bytes for every job; the woodcutter draws only those of the frames he takes: the step shapes
    // 4 to 58 (4 + 9 * direction + height difference), the claim's 0x51 and 0x52, and the cut's 0x74 to 0x78.
    [TestMethod]
    public void EveryEntryTheWoodcutterIsDrawnWithIsAmongThoseLoadedForHisJob()
    {
        var graphics = Load();
        var map = Map();
        var frames = Enumerable.Range(4, 55).Concat([0x51, 0x52]).Concat(Enumerable.Range(0x74, 5));
        var missing = new HashSet<(int, int)>();
        foreach (var frame in frames)
        {
            foreach (var timer in Enumerable.Range(0, 1024).Where(timer => timer % 8 == 0))
            {
                foreach (
                    var woodcutter in new[] { Woodcutter(frame, timer, 2), Woodcutter(frame, timer, 0x10, 0x80, 1) }
                )
                {
                    foreach (var draw in graphics.Figures.Draws(map, [woodcutter], 0))
                    {
                        if (!graphics.Layers.ContainsKey((draw.Entry, draw.Colour)))
                        {
                            missing.Add((draw.Entry, draw.Colour));
                        }
                    }
                }
            }
        }

        Assert.IsEmpty(missing, $"entries drawn and not loaded: {string.Join(", ", missing.Take(20))}");
    }

    [TestMethod]
    public void TheSpritesOfTheCarrierAndTheWoodcutterLoadFromTheInstallInTheLayersAnArrayTextureHolds()
    {
        // The sequences name figures for every job, and some of the entries they reach are no sprites: the load leaves
        // those out. Below the layer limit of 2048 the limit leaves none of the rest out.
        var graphics = Load();

        Assert.IsGreaterThan(0, graphics.Images.Count);
        Assert.IsLessThan(2048, graphics.Images.Count);
        Assert.IsTrue(graphics.Shifts.Values.Any(shift => shift != (0, 0)), "the figures shift their second figures");
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

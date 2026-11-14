using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// What the History Edition source draws for the settlers of the install: the picks of the exe's figure tables over the terms
// of the shared placement, and the images the archive holds for them.
[TestClass]
public sealed class HistoryEditionSettlersTests
{
    static (SettlerPair Pair, HistoryEditionSource Source) Load()
    {
        var install = TestProgram.OpenInstall();
        var tables = TestProgram.Tables();
        var pair = SettlerPair.Of(install, tables);

        return (pair, HistoryEditionSource.Load(install, tables));
    }

    static Map Map() =>
        MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0]);

    static Settler Woodcutter(int frame, int timer, Activity activity) =>
        new(new Woodcutter(), 0, 4, 3, 0, activity) { Frame = frame, Timer = (ushort)timer };

    static readonly Activity Walking = new Walks(new ToStorage(), new Heading(0));

    static readonly Activity BackWithLog = new WalksFree(new Trip(new Vector(0, 0), new BackToHut(true)));

    // The table rule at the program's addresses (docs/rules/settlers.md): the woodcutter's walk is the row 0x0b of the bases
    // (48, 54), the cut a figure byte from 0x80 in the row 0x0f (272, none), the way back with the log the row 0x10 (48, 60).
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheWoodcuttersWalkAndCutAreDrawnWithTheEntriesTheTableRuleGives()
    {
        var pair = Load().Pair;
        var map = Map();

        // Frame 4 with no time left: the figure byte 7 (offsets 7, 0): 1850 + 48 + 7, 2500 + 48 + 7, 3150 + 54 + 0.
        var walk = pair.Draws(map, [Woodcutter(4, 0, Walking)], 0).Select(d => d.Entry);
        // The way back with the log: the same byte in the row 0x10: 1850 + 48 + 7, 2500 + 48 + 7, 3150 + 60 + 0.
        var back = pair.Draws(map, [Woodcutter(4, 0, BackWithLog)], 0).Select(d => d.Entry);
        // Frame 0x74 with 800 units left: the triple 100 holds the figure byte 0x83, the word 0x0f03: 1850 + 272 + 3, 2500 + 272 + 3.
        var cut = pair.Draws(map, [Woodcutter(0x74, 800, new Cuts(new Vector(0, 0), false, 0, 0))], 0)
            .Select(d => d.Entry);

        CollectionAssert.AreEqual(new[] { 1905, 2555, 3204 }, walk.ToArray());
        CollectionAssert.AreEqual(new[] { 1905, 2555, 3210 }, back.ToArray());
        CollectionAssert.AreEqual(new[] { 2125, 2775 }, cut.ToArray());
    }

    static Settler LoadedCarrier(int frame, int timer = 0) =>
        new(new Carrier(), 0, 4, 3, 0, new WalksRoad(new Load(new Good(WoodcutterRules.GoodLog), null), new Heading(0)))
        {
            Frame = frame,
            Timer = (ushort)timer,
        };

    // The carried table at 0x43f4b7 holds 0x1400 for the log: the carrier's figure word is its figure byte in the row 0x14,
    // so each of the six directions (the step shapes 4 + 9 * direction on level ground) is drawn with the figure of the byte
    // the sequence gives in that row: 1850 + base + offset, the overlay 2500 + base + offset.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ACarrierCarryingALogIsDrawnInEachDirectionWithTheEntriesTheCarriedRowGives()
    {
        var pair = Load().Pair;
        var map = Map();
        // The figure and the overlay entry of each direction, as the install's tables give them in the row 0x14.
        int[][] expected =
        [
            [1905, 2555],
            [1913, 2563],
            [1921, 2571],
            [1929, 2579],
            [1937, 2587],
            [1945, 2595],
        ];

        for (var direction = 0; direction < 6; direction++)
        {
            var entries = pair.Draws(map, [LoadedCarrier(4 + 9 * direction)], 0).Select(d => d.Entry).Take(2).ToArray();

            CollectionAssert.AreEqual(expected[direction], entries, $"direction {direction}");
        }
    }

    // Every term the figure tables pick an entry for has an image in the archive: the source draws what the tables pick.
    static HashSet<(int, int)> PickedWithNoImage(
        (SettlerPair Pair, HistoryEditionSource Source) kit,
        IEnumerable<Settler> settlers
    )
    {
        var map = Map();
        var missing = new HashSet<(int, int)>();
        foreach (var settler in settlers)
        {
            foreach (var term in kit.Pair.Terms(map, [settler], 0))
            {
                if (kit.Pair.Pick(term) is { } pick && kit.Source.Draw(term)?.Image is null)
                {
                    missing.Add((pick.Entry, pick.Colour));
                }
            }
        }

        return missing;
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryEntryACarrierCarryingALogIsDrawnWithHasAnImage()
    {
        var settlers = Enumerable
            .Range(4, 55)
            .SelectMany(frame =>
                Enumerable.Range(0, 1024).Where(timer => timer % 8 == 0).Select(timer => LoadedCarrier(frame, timer))
            );

        var missing = PickedWithNoImage(Load(), settlers);

        Assert.IsEmpty(missing, $"entries picked with no image: {string.Join(", ", missing.Take(20))}");
    }

    // The carrier carries every good the carried table covers, on its road (state 3) and at the castle (state 0xe), in any
    // frame the sequences hold.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryEntryACarrierCarryingAnyGoodIsDrawnWithHasAnImage()
    {
        // The frames the simulation gives a carrier: the step shapes, the claim's, the handover's, and the waiting
        // carrier's; frame 0 is a settler's before its first step.
        var settlers =
            from good in Enumerable.Range(1, RuleTables.CarriedFigureCount - 1)
            from state in new[] { 3, 0xe }
            from frame in Enumerable.Range(0, 0x79)
            from step in Enumerable.Range(0, 128)
            let load = new Load(new Good(good), null)
            select new Settler(
                new Carrier(),
                0,
                4,
                3,
                0,
                state == 3 ? new WalksRoad(load, new Heading(0)) : new HandsOver(load, new Heading(0))
            )
            {
                Frame = frame,
                Timer = (ushort)(step * 8),
            };

        var missing = PickedWithNoImage(Load(), settlers);

        Assert.IsEmpty(missing, $"entries picked with no image: {string.Join(", ", missing.Take(20))}");
    }

    // The settler of the type 1 hut walks to his hut (states 5, 2, 4) in the frames of the step shapes and the claim, and
    // walks back with a load: his rows are those of his handler.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryEntryTheSettlerOfTheTypeOneHutIsDrawnWithHasAnImage()
    {
        var frames = Enumerable.Range(4, 55).Concat([0x51, 0x52]).Concat(Enumerable.Range(0x74, 5));
        var settlers =
            from frame in frames
            from step in Enumerable.Range(0, 128)
            from activity in new Activity[]
            {
                Walking,
                BackWithLog,
                new Cuts(new Vector(0, 0), false, 1, 0),
                new Cuts(new Vector(0, 0), false, 0, 0),
            }
            select new Settler(new TypeOneHut(), 0, 4, 3, 0, activity) { Frame = frame, Timer = (ushort)(step * 8) };

        var missing = PickedWithNoImage(Load(), settlers);

        Assert.IsEmpty(missing, $"entries picked with no image: {string.Join(", ", missing.Take(20))}");
    }

    // The sequences name figure bytes for every job; the woodcutter draws only those of the frames he takes: the step shapes
    // 4 to 58 (4 + 9 * direction + height difference), the claim's 0x51 and 0x52, and the cut's 0x74 to 0x78.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryEntryTheWoodcutterIsDrawnWithHasAnImage()
    {
        var frames = Enumerable.Range(4, 55).Concat([0x51, 0x52]).Concat(Enumerable.Range(0x74, 5));
        var settlers =
            from frame in frames
            from timer in Enumerable.Range(0, 1024).Where(timer => timer % 8 == 0)
            from activity in new[] { Walking, BackWithLog }
            select Woodcutter(frame, timer, activity);

        var missing = PickedWithNoImage(Load(), settlers);

        Assert.IsEmpty(missing, $"entries picked with no image: {string.Join(", ", missing.Take(20))}");
    }

    // The original draws the overlay of the figure 1850 + i from the entry 2500 + i (0x406610, docs/rules/graphics-images.md).
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void EveryFigureOfTheCarrierAndTheWoodcutterHasItsColourOverlay()
    {
        var kit = Load();
        var map = Map();
        var pairs = Enumerable
            .Range(4, 55)
            .SelectMany(frame =>
                Enumerable
                    .Range(0, 128)
                    .SelectMany(step => new[] { LoadedCarrier(frame, step * 8), Woodcutter(frame, step * 8, Walking) })
            )
            .Select(settler => kit.Pair.Terms(map, [settler], 0))
            .Where(terms => kit.Pair.Pick(terms[0]) is not null && kit.Pair.Pick(terms[1]) is not null)
            .Select(terms => (Figure: kit.Pair.Pick(terms[0])!.Value, Overlay: kit.Pair.Pick(terms[1])!.Value))
            .ToArray();

        Assert.IsNotEmpty(pairs);
        Assert.IsTrue(pairs.All(pair => pair.Overlay.Entry == pair.Figure.Entry - 1850 + 2500));
    }

    // The second figure hangs from where its own header puts it, moved by the header bytes 0 and 1 of its figure
    // (0x4066d2, 0x4066e4): the woodcutter's walk in frame 4 is the figure 1905 and the second figure 3204.
    [TestMethod]
    [TestCategory(Needs.Install)]
    public void TheSecondFigureOfAWalkingWoodcutterIsAnchoredByTheHeaderOfItsFigure()
    {
        var kit = Load();
        var archive = Formats.ArchiveFile.Read(TestProgram.OpenInstall().ReadAll(TerrainGraphics.ArchiveFile));
        var figure = Formats.SpriteFile.Read(archive.Data(archive[1905]));
        var secondSprite = Formats.SpriteFile.Read(archive.Data(archive[3204]));
        var terms = kit.Pair.Terms(Map(), [Woodcutter(4, 0, Walking)], 0);

        var second = kit.Source.Draw(terms[2])!.Image!;

        Assert.AreEqual(secondSprite.Header.OffsetX + figure.Header.First, second.OffsetX);
        Assert.AreEqual(secondSprite.Header.OffsetY + figure.Header.Second, second.OffsetY);
    }

    // A made-up overlay of two pixels, one drawn and one transparent: only the drawn pixel shows in the colour.
    [TestMethod]
    public void AColourOverlayKeepsItsTransparentPixelsTransparent()
    {
        var overlay = new ObjectImage(2, 1, 0, 0, [1, 2, 3, 255, 0, 0, 0, 0]);
        var bytes = new byte[768];
        bytes[3 * 0x40] = 64;
        bytes[3 * 0x40 + 1] = 68;
        bytes[3 * 0x40 + 2] = 72;

        var filled = SettlerSprites.Fill(overlay, OpenSettlers.Formats.PaletteFile.Read(bytes), 0x40);

        CollectionAssert.AreEqual(new byte[] { 64, 68, 72, 255, 0, 0, 0, 0 }, filled.Rgba);
    }
}

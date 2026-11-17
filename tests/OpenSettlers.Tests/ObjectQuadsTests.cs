using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectQuadsTests
{
    static readonly ObjectImage Flag = new()
    {
        Width = 3,
        Height = 1,
        OffsetX = -1,
        OffsetY = -2,
        Rgba = new byte[12],
    };
    static readonly ObjectImage FlagShadow = new()
    {
        Width = 4,
        Height = 2,
        OffsetX = 1,
        OffsetY = -1,
        Rgba = new byte[32],
    };
    static readonly ObjectImage GoodImage = new()
    {
        Width = 2,
        Height = 2,
        OffsetX = 3,
        OffsetY = 1,
        Rgba = new byte[16],
    };

    // A source that draws a flag with a shadow, a woodcutter's hut with no shadow, every good as one image, a ripple on
    // even tiles only, and nothing else.
    sealed class FakeSource : IGraphicsSource
    {
        public ObjectImage? Draw(TerrainTriangle triangle) => null;

        public ObjectImage? Draw(RoadStrip strip) => null;

        public ObjectImage? Draw(BuildMarker marker) => null;

        public IReadOnlyList<Flame> Flames(BuildingType type, bool earlySite) => [];

        public ObjectImage? Draw(FlameDraw flame) => null;

        public ObjectDrawing? Draw(MapObjectDraw draw) =>
            draw.Object == MapObject.Flag ? new ObjectDrawing { Image = Flag, Shadow = FlagShadow } : null;

        public ObjectDrawing? Draw(BuildingDraw draw) =>
            draw.Type == BuildingType.Woodcutter ? new ObjectDrawing { Image = Flag, Shadow = null } : null;

        public ObjectDrawing? Draw(GoodAtFlag draw) => new ObjectDrawing { Image = GoodImage, Shadow = null };

        // A settler's figure has a shadow, its overlay none, and its second figure no image.
        public ObjectDrawing? Draw(SettlerDraw draw) =>
            draw.Part switch
            {
                SettlerPart.Figure => new ObjectDrawing { Image = Flag, Shadow = FlagShadow },
                SettlerPart.Overlay => new ObjectDrawing { Image = GoodImage, Shadow = null },
                _ => null,
            };

        public RippleImage? Draw(WaterRipple ripple) =>
            ripple.Tile % 2 == 0 ? new RippleImage { Image = Flag, Surface = [1, 2, 3, 255] } : null;

        public int ObjectHeadroom => 2;
    }

    static MapObjectDraw FlagAt(int row, int x, int y) =>
        new()
        {
            Row = row,
            X = x,
            Y = y,
            Object = MapObject.Flag,
            Step = 0,
        };

    // The runs of objects alone, in bands of 16 rows.
    static List<SpriteRun> ObjectRuns(ObjectTerm[] terms, IGraphicsSource source, ImageLayers layers, int rows) =>
        ObjectQuads.Runs(terms, [], source, layers, new ImageLayers(), rows, 16);

    [TestMethod]
    public void AQuadHangsFromTheDrawPositionByTheImagesOffsets()
    {
        var quad = ObjectRuns([FlagAt(0, 100, 50)], new FakeSource(), new ImageLayers(), 1)[0].Quads[1];

        Assert.AreEqual(99, quad.X);
        Assert.AreEqual(48, quad.Y);
        Assert.AreEqual(3, quad.Width);
        Assert.AreEqual(1, quad.Height);
    }

    [TestMethod]
    public void AShadowIsDrawnRightBeforeItsImageFromItsOwnOffsets()
    {
        var layers = new ImageLayers();

        var runs = ObjectRuns([FlagAt(0, 100, 50), FlagAt(0, 200, 50)], new FakeSource(), layers, 1);

        CollectionAssert.AreEqual(
            new[]
            {
                new ObjectQuad
                {
                    X = 101,
                    Y = 49,
                    Width = 4,
                    Height = 2,
                    Layer = 0,
                },
                new ObjectQuad
                {
                    X = 99,
                    Y = 48,
                    Width = 3,
                    Height = 1,
                    Layer = 1,
                },
                new ObjectQuad
                {
                    X = 201,
                    Y = 49,
                    Width = 4,
                    Height = 2,
                    Layer = 0,
                },
                new ObjectQuad
                {
                    X = 199,
                    Y = 48,
                    Width = 3,
                    Height = 1,
                    Layer = 1,
                },
            },
            runs.Single().Quads
        );
        Assert.HasCount(2, layers.Images);
    }

    [TestMethod]
    public void ATermTheSourceDrawsNothingForLeavesNoQuadAndNoLayer()
    {
        var layers = new ImageLayers();
        var terms = new ObjectTerm[]
        {
            new MapObjectDraw
            {
                Row = 0,
                X = 100,
                Y = 50,
                Object = new MapObject { Code = 40 },
                Step = 0,
            },
        };

        var runs = ObjectRuns(terms, new FakeSource(), layers, 1);

        Assert.IsEmpty(runs);
        Assert.IsEmpty(layers.Images);
    }

    [TestMethod]
    public void AnImageWithNoPixelLeavesNoQuad()
    {
        var empty = new ObjectDrawing
        {
            Image = new ObjectImage
            {
                Width = 0,
                Height = 0,
                OffsetX = 0,
                OffsetY = 0,
                Rgba = [],
            },
            Shadow = null,
        };
        var layers = new ImageLayers();

        var runs = ObjectRuns(
            [
                new GoodAtFlag
                {
                    Row = 0,
                    X = 1,
                    Y = 1,
                    Good = new Domain.Good { Code = 7 },
                },
            ],
            new EmptySource(empty),
            layers,
            1
        );

        Assert.IsEmpty(runs);
        Assert.IsEmpty(layers.Images);
    }

    sealed class EmptySource(ObjectDrawing answer) : IGraphicsSource
    {
        public ObjectImage? Draw(TerrainTriangle triangle) => null;

        public ObjectImage? Draw(RoadStrip strip) => null;

        public ObjectImage? Draw(BuildMarker marker) => null;

        public IReadOnlyList<Flame> Flames(BuildingType type, bool earlySite) => [];

        public ObjectImage? Draw(FlameDraw flame) => null;

        public ObjectDrawing? Draw(MapObjectDraw draw) => answer;

        public ObjectDrawing? Draw(BuildingDraw draw) => answer;

        public ObjectDrawing? Draw(GoodAtFlag draw) => answer;

        public ObjectDrawing? Draw(SettlerDraw draw) => answer;

        public RippleImage? Draw(WaterRipple ripple) => null;

        public int ObjectHeadroom => 0;
    }

    [TestMethod]
    public void TermsOfOneBandFormOneRunAndABandWithoutTermsAddsNoRun()
    {
        var terms = new ObjectTerm[]
        {
            FlagAt(0, 100, 50),
            new BuildingDraw
            {
                Row = 0,
                X = 132,
                Y = 50,
                Type = BuildingType.Woodcutter,
            },
            FlagAt(35, 100, 450),
        };

        // Rows 0 to 15, 16 to 31, and 32 to 39: the middle band holds no term.
        var runs = ObjectRuns(terms, new FakeSource(), new ImageLayers(), 40);

        Assert.HasCount(2, runs);
        Assert.HasCount(3, runs[0].Quads);
        Assert.AreEqual(448, runs[1].Quads[1].Y);
    }

    [TestMethod]
    public void TheTermsKeepTheOrderTheyAreGivenWithinABand()
    {
        var terms = new ObjectTerm[]
        {
            new GoodAtFlag
            {
                Row = 0,
                X = 10,
                Y = 10,
                Good = new Domain.Good { Code = 7 },
            },
            FlagAt(0, 100, 50),
            new GoodAtFlag
            {
                Row = 0,
                X = 20,
                Y = 10,
                Good = new Domain.Good { Code = 7 },
            },
        };

        var runs = ObjectRuns(terms, new FakeSource(), new ImageLayers(), 1);

        CollectionAssert.AreEqual(new[] { 13, 101, 99, 23 }, runs.Single().Quads.Select(q => q.X).ToList());
    }

    [TestMethod]
    public void RipplesTheSourceDrawsGetQuadsAndTheSurfaceTheyShowOver()
    {
        var layers = new ImageLayers();

        var (quads, surface) = ObjectQuads.Ripples(
            [
                new WaterRipple
                {
                    Row = 0,
                    X = 100,
                    Y = 50,
                    Tile = 0,
                    Step = 0,
                },
                new WaterRipple
                {
                    Row = 0,
                    X = 132,
                    Y = 50,
                    Tile = 1,
                    Step = 0,
                },
                new WaterRipple
                {
                    Row = 1,
                    X = 100,
                    Y = 70,
                    Tile = 2,
                    Step = 0,
                },
            ],
            new FakeSource(),
            layers
        );

        CollectionAssert.AreEqual(
            new[]
            {
                new ObjectQuad
                {
                    X = 99,
                    Y = 48,
                    Width = 3,
                    Height = 1,
                    Layer = 0,
                },
                new ObjectQuad
                {
                    X = 99,
                    Y = 68,
                    Width = 3,
                    Height = 1,
                    Layer = 0,
                },
            },
            quads
        );
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 255 }, surface);
        Assert.HasCount(1, layers.Images);
    }

    static SettlerDraw SettlerAt(SettlerPart part, int row, int x, int y) =>
        new()
        {
            Row = row,
            X = x,
            Y = y,
            Part = part,
            Job = new Carrier(),
            Activity = new WalksRoad
            {
                Load = null,
                Heading = new Heading { Value = 0 },
            },
            Heading = null,
            Frame = 7,
            Step = 0,
            Player = 0,
            Carried = Good.None,
        };

    // The settlers are one list in the order the terms come: a settler's shadow right before its figure, then its overlay,
    // and a part the source has no image for leaves no quad.
    [TestMethod]
    public void TheSettlersTermsFormOneListInTheOrderTheyComeWithTheShadowBeforeTheFigure()
    {
        var layers = new ImageLayers();

        var quads = ObjectQuads.Flat(
            [
                SettlerAt(SettlerPart.Figure, 30, 100, 50),
                SettlerAt(SettlerPart.Overlay, 30, 100, 50),
                SettlerAt(SettlerPart.SecondFigure, 30, 100, 50),
                SettlerAt(SettlerPart.Figure, 90, 200, 400),
            ],
            new FakeSource(),
            layers
        );

        CollectionAssert.AreEqual(
            new[]
            {
                new ObjectQuad
                {
                    X = 101,
                    Y = 49,
                    Width = 4,
                    Height = 2,
                    Layer = 0,
                },
                new ObjectQuad
                {
                    X = 99,
                    Y = 48,
                    Width = 3,
                    Height = 1,
                    Layer = 1,
                },
                new ObjectQuad
                {
                    X = 103,
                    Y = 51,
                    Width = 2,
                    Height = 2,
                    Layer = 2,
                },
                new ObjectQuad
                {
                    X = 201,
                    Y = 399,
                    Width = 4,
                    Height = 2,
                    Layer = 0,
                },
                new ObjectQuad
                {
                    X = 199,
                    Y = 398,
                    Width = 3,
                    Height = 1,
                    Layer = 1,
                },
            },
            quads
        );
    }

    [TestMethod]
    public void NoSettlerTermsGiveNoQuadAndNoLayer()
    {
        var layers = new ImageLayers();

        var quads = ObjectQuads.Flat([SettlerAt(SettlerPart.SecondFigure, 0, 0, 0)], new FakeSource(), layers);

        Assert.IsEmpty(quads);
        Assert.IsEmpty(layers.Images);
    }

    [TestMethod]
    public void NoRippleDrawnMeansNoQuadNoSurfaceAndNoLayer()
    {
        var layers = new ImageLayers();

        var (quads, surface) = ObjectQuads.Ripples(
            [
                new WaterRipple
                {
                    Row = 0,
                    X = 100,
                    Y = 50,
                    Tile = 1,
                    Step = 0,
                },
            ],
            new FakeSource(),
            layers
        );

        Assert.IsEmpty(quads);
        Assert.IsNull(surface);
        Assert.IsEmpty(layers.Images);
    }

    static BuildingDraw HutAt(int row, int x) =>
        new()
        {
            Row = row,
            X = x,
            Y = 20 * row,
            Type = BuildingType.Woodcutter,
        };

    static List<(RunKind Kind, List<int> Xs)> Runs(ObjectTerm[] objects, SettlerDraw[] settlers, int rows) =>
        [
            .. ObjectQuads
                .Runs(objects, settlers, new FakeSource(), new ImageLayers(), new ImageLayers(), rows, 16)
                .Select(run => (run.Kind, run.Quads.Select(q => q.X).ToList())),
        ];

    // 0x44d3ae draws each row's objects and then the row's settlers, from the top row down: a settler a row above a
    // building is painted over by it, and a settler in the building's row paints over it.
    [TestMethod]
    public void ARowsObjectsPaintBeforeItsSettlersAndAfterTheSettlersOfTheRowAbove()
    {
        var runs = Runs(
            [HutAt(5, 132)],
            [SettlerAt(SettlerPart.Overlay, 4, 300, 80), SettlerAt(SettlerPart.Overlay, 5, 400, 100)],
            8
        );

        Assert.HasCount(3, runs);
        Assert.AreEqual((RunKind.Settlers, 303), (runs[0].Kind, runs[0].Xs.Single()));
        Assert.AreEqual((RunKind.Objects, 131), (runs[1].Kind, runs[1].Xs.Single()));
        Assert.AreEqual((RunKind.Settlers, 403), (runs[2].Kind, runs[2].Xs.Single()));
    }

    [TestMethod]
    public void TermsOfOneKindInOneBandJoinOneRunAndARunEndsAtItsBand()
    {
        var runs = Runs([HutAt(0, 10), HutAt(1, 20), HutAt(20, 30)], [], 21);

        Assert.HasCount(2, runs);
        CollectionAssert.AreEqual(new[] { 9, 19 }, runs[0].Xs);
        CollectionAssert.AreEqual(new[] { 29 }, runs[1].Xs);
        Assert.IsTrue(runs.All(run => run.Kind == RunKind.Objects));
    }

    // A frame that moves a settler's record up a row (0x43f402) can put a settler of row 0 in row -1.
    [TestMethod]
    public void ASettlerMovedAboveTheTopRowPaintsBeforeTheTopRowsObjects()
    {
        var runs = Runs([HutAt(0, 10)], [SettlerAt(SettlerPart.Overlay, -1, 300, 0)], 4);

        CollectionAssert.AreEqual(new[] { RunKind.Settlers, RunKind.Objects }, runs.Select(r => r.Kind).ToList());
    }

    [TestMethod]
    public void ATermTheSourceDrawsNothingForStartsNoRun()
    {
        var runs = Runs([HutAt(0, 10), HutAt(0, 20)], [SettlerAt(SettlerPart.SecondFigure, 0, 300, 0)], 1);

        Assert.HasCount(1, runs);
    }

    [TestMethod]
    public void TheSettlersQuadsTakeTheirLayersFromTheSettlersImagesAndTheObjectsFromTheObjects()
    {
        var objectLayers = new ImageLayers();
        var settlerLayers = new ImageLayers();

        var runs = ObjectQuads.Runs(
            [FlagAt(0, 100, 50)],
            [SettlerAt(SettlerPart.Overlay, 0, 300, 50)],
            new FakeSource(),
            objectLayers,
            settlerLayers,
            1,
            16
        );

        CollectionAssert.AreEqual(new[] { 0, 1 }, runs[0].Quads.Select(q => q.Layer).ToList());
        Assert.AreEqual(0, runs[1].Quads.Single().Layer);
        CollectionAssert.AreEqual(new[] { FlagShadow, Flag }, objectLayers.Images.ToList());
        CollectionAssert.AreEqual(new[] { GoodImage }, settlerLayers.Images.ToList());
    }
}

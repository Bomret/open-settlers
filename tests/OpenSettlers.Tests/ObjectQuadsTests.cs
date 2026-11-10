using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ObjectQuadsTests
{
    static readonly ObjectImage Flag = new(3, 1, -1, -2, new byte[12]);
    static readonly ObjectImage FlagShadow = new(4, 2, 1, -1, new byte[32]);
    static readonly ObjectImage GoodImage = new(2, 2, 3, 1, new byte[16]);

    // A source that draws a flag with a shadow, a woodcutter's hut with no shadow, every good as one image, a ripple on
    // even tiles only, and nothing else.
    sealed class FakeSource : IGraphicsSource
    {
        public ObjectImage? Draw(TerrainTriangle triangle) => null;

        public ObjectImage? Draw(RoadStrip strip) => null;

        public ObjectDrawing? Draw(MapObjectDraw draw) =>
            draw.Object == MapObject.Flag ? new ObjectDrawing(Flag, FlagShadow) : null;

        public ObjectDrawing? Draw(BuildingDraw draw) =>
            draw.Type == BuildingType.Woodcutter ? new ObjectDrawing(Flag, null) : null;

        public ObjectDrawing? Draw(GoodAtFlag draw) => new ObjectDrawing(GoodImage, null);

        // A settler's figure has a shadow, its overlay none, and its second figure no image.
        public ObjectDrawing? Draw(SettlerDraw draw) =>
            draw.Part switch
            {
                SettlerPart.Figure => new ObjectDrawing(Flag, FlagShadow),
                SettlerPart.Overlay => new ObjectDrawing(GoodImage, null),
                _ => null,
            };

        public RippleImage? Draw(WaterRipple ripple) =>
            ripple.Tile % 2 == 0 ? new RippleImage(Flag, [1, 2, 3, 255]) : null;

        public int ObjectHeadroom => 2;
    }

    static MapObjectDraw FlagAt(int row, int x, int y) => new(row, x, y, MapObject.Flag, 0);

    [TestMethod]
    public void AQuadHangsFromTheDrawPositionByTheImagesOffsets()
    {
        var bands = ObjectQuads.Bands([FlagAt(0, 100, 50)], new FakeSource(), new ImageLayers(), 1, 16);

        Assert.AreEqual(99, bands[0][1].X);
        Assert.AreEqual(48, bands[0][1].Y);
        Assert.AreEqual(3, bands[0][1].Width);
        Assert.AreEqual(1, bands[0][1].Height);
    }

    [TestMethod]
    public void AShadowIsDrawnRightBeforeItsImageFromItsOwnOffsets()
    {
        var layers = new ImageLayers();

        var bands = ObjectQuads.Bands([FlagAt(0, 100, 50), FlagAt(0, 200, 50)], new FakeSource(), layers, 1, 16);

        CollectionAssert.AreEqual(
            new[]
            {
                new ObjectQuad(101, 49, 4, 2, 0),
                new ObjectQuad(99, 48, 3, 1, 1),
                new ObjectQuad(201, 49, 4, 2, 0),
                new ObjectQuad(199, 48, 3, 1, 1),
            },
            bands[0]
        );
        Assert.HasCount(2, layers.Images);
    }

    [TestMethod]
    public void ATermTheSourceDrawsNothingForLeavesNoQuadAndNoLayer()
    {
        var layers = new ImageLayers();
        var terms = new ObjectTerm[] { new MapObjectDraw(0, 100, 50, new MapObject(40), 0) };

        var bands = ObjectQuads.Bands(terms, new FakeSource(), layers, 1, 16);

        Assert.IsEmpty(bands);
        Assert.IsEmpty(layers.Images);
    }

    [TestMethod]
    public void AnImageWithNoPixelLeavesNoQuad()
    {
        var empty = new ObjectDrawing(new ObjectImage(0, 0, 0, 0, []), null);
        var layers = new ImageLayers();

        var bands = ObjectQuads.Bands(
            [new GoodAtFlag(0, 1, 1, new Domain.Good(7))],
            new EmptySource(empty),
            layers,
            1,
            16
        );

        Assert.IsEmpty(bands);
        Assert.IsEmpty(layers.Images);
    }

    sealed class EmptySource(ObjectDrawing answer) : IGraphicsSource
    {
        public ObjectImage? Draw(TerrainTriangle triangle) => null;

        public ObjectImage? Draw(RoadStrip strip) => null;

        public ObjectDrawing? Draw(MapObjectDraw draw) => answer;

        public ObjectDrawing? Draw(BuildingDraw draw) => answer;

        public ObjectDrawing? Draw(GoodAtFlag draw) => answer;

        public ObjectDrawing? Draw(SettlerDraw draw) => answer;

        public RippleImage? Draw(WaterRipple ripple) => null;

        public int ObjectHeadroom => 0;
    }

    [TestMethod]
    public void TermsOfTheSameRowsFormOneBandAndABandWithoutTermsIsLeftOut()
    {
        var terms = new ObjectTerm[]
        {
            FlagAt(0, 100, 50),
            new BuildingDraw(0, 132, 50, BuildingType.Woodcutter),
            FlagAt(20, 100, 450),
        };

        var bands = ObjectQuads.Bands(terms, new FakeSource(), new ImageLayers(), 21, 16);

        Assert.HasCount(2, bands);
        Assert.HasCount(3, bands[0]);
        Assert.AreEqual(448, bands[1][1].Y);
    }

    [TestMethod]
    public void TheTermsKeepTheOrderTheyAreGivenWithinABand()
    {
        var terms = new ObjectTerm[]
        {
            new GoodAtFlag(0, 10, 10, new Domain.Good(7)),
            FlagAt(0, 100, 50),
            new GoodAtFlag(0, 20, 10, new Domain.Good(7)),
        };

        var bands = ObjectQuads.Bands(terms, new FakeSource(), new ImageLayers(), 1, 16);

        CollectionAssert.AreEqual(new[] { 13, 101, 99, 23 }, bands[0].Select(q => q.X).ToList());
    }

    [TestMethod]
    public void RipplesTheSourceDrawsGetQuadsAndTheSurfaceTheyShowOver()
    {
        var layers = new ImageLayers();

        var (quads, surface) = ObjectQuads.Ripples(
            [new WaterRipple(0, 100, 50, 0, 0), new WaterRipple(0, 132, 50, 1, 0), new WaterRipple(1, 100, 70, 2, 0)],
            new FakeSource(),
            layers
        );

        CollectionAssert.AreEqual(new[] { new ObjectQuad(99, 48, 3, 1, 0), new ObjectQuad(99, 68, 3, 1, 0) }, quads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 255 }, surface);
        Assert.HasCount(1, layers.Images);
    }

    static SettlerDraw SettlerAt(SettlerPart part, int row, int x, int y) =>
        new(row, x, y, part, Job.Carrier, new WalksRoad(null, new Heading(0)), null, 7, 0, 0, Good.None);

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
                new ObjectQuad(101, 49, 4, 2, 0),
                new ObjectQuad(99, 48, 3, 1, 1),
                new ObjectQuad(103, 51, 2, 2, 2),
                new ObjectQuad(201, 399, 4, 2, 0),
                new ObjectQuad(199, 398, 3, 1, 1),
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

        var (quads, surface) = ObjectQuads.Ripples([new WaterRipple(0, 100, 50, 1, 0)], new FakeSource(), layers);

        Assert.IsEmpty(quads);
        Assert.IsNull(surface);
        Assert.IsEmpty(layers.Images);
    }
}

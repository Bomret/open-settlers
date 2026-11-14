using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class OpenSettlersSourceTests
{
    // A content set folder in the temp folder. A PNG file holds "<width>x<height>:<fill>", which FakeDecoder turns
    // into an image; any other text does not decode.
    sealed class ContentFolder : IDisposable
    {
        public ContentFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "open-settlers-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Entries = [];
            foreach (var key in Keys)
            {
                Entries[key] = Entry("a.png", "0, 0");
            }

            Png("a.png", "2x2:9");
        }

        public string Root { get; }

        // The manifest's entries by key, the raw JSON of each value; a test replaces one.
        public Dictionary<string, string> Entries { get; }

        public string? Extra { get; set; }

        public void Png(string path, string text) => Text(path, text);

        public void Text(string path, string text)
        {
            var full = Path.Combine(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }

        public static string Entry(string image, string anchor) =>
            $$"""{ "image": "{{image}}", "anchor": [{{anchor}}] }""";

        public OpenSettlersSource Load()
        {
            var lines = Entries.Select(entry => $"\"{entry.Key}\": {entry.Value}").Concat(Extra is null ? [] : [Extra]);
            Text(OpenSettlersSource.ManifestFile, "{\n" + string.Join(",\n", lines) + "\n}");

            return OpenSettlersSource.Load(Root, new FakeDecoder());
        }

        public void Dispose() => Directory.Delete(Root, true);
    }

    sealed class FakeDecoder : IImageDecoder
    {
        public DecodedImage? Decode(string path)
        {
            var text = File.ReadAllText(path);
            var colon = text.IndexOf(':');
            var cross = text.IndexOf('x');
            if (colon < 0 || cross < 0)
            {
                return null;
            }

            var width = int.Parse(text[..cross]);
            var height = int.Parse(text[(cross + 1)..colon]);
            var rgba = new byte[width * height * 4];
            if (text[(colon + 1)..] == "mark")
            {
                // The first pixel carries the mark colour, every other pixel is (10, 20, 30).
                for (var pixel = 0; pixel < width * height; pixel++)
                {
                    byte[] value = pixel == 0 ? [255, 0, 255, 255] : [10, 20, 30, 255];
                    value.CopyTo(rgba, pixel * 4);
                }
            }
            else
            {
                Array.Fill(rgba, byte.Parse(text[(colon + 1)..]));
            }

            return new DecodedImage
            {
                Width = width,
                Height = height,
                Rgba = rgba,
            };
        }
    }

    static readonly string[] BuildingKeys =
    [
        "building/castle",
        "building/woodcutter",
        "building/stonecutter",
        "building/forester",
        "building/sawmill",
        "building/site-small",
        "building/site-large",
        "building/stage-small",
        "building/stage-large",
    ];

    static IEnumerable<string> Keys =>
        Enumerable
            .Range(0, 16)
            .SelectMany(code => new[] { $"terrain/{code}/upper", $"terrain/{code}/lower" })
            .Concat(["road/east", "road/south-east", "road/south", "object/flag"])
            .Concat(Enumerable.Range(8, 120).Select(code => $"object/{code}"))
            .Concat(BuildingKeys)
            .Concat(Enumerable.Range(0, 32).Select(code => $"good/{code}"))
            .Concat(
                from job in new[]
                {
                    "carrier",
                    "woodcutter",
                    "stonecutter",
                    "type-one-hut",
                    "builder",
                    "forester",
                    "digger",
                    "sawmiller",
                }
                from kind in new[] { "walks", "carries", "cuts", "door", "hands-over", "waits" }
                select $"settler/{job}/{kind}"
            );

    static TerrainTriangle Triangle(int code, bool upper) =>
        new()
        {
            Column = 0,
            Row = 0,
            Upper = upper,
            Terrain = new Terrain { Code = code },
            Apex = 0,
            Left = 0,
            Right = 0,
            X = 100,
            Y = 50,
        };

    [TestMethod]
    public void ATerrainTriangleDrawsTheImageOfItsTerrainAndOrientationHungFromItsAnchor()
    {
        using var folder = new ContentFolder();
        folder.Png("five-upper.png", "4x3:5");
        folder.Entries["terrain/5/upper"] = ContentFolder.Entry("five-upper.png", "-2, -1");

        var image = folder.Load().Draw(Triangle(5, upper: true))!;

        Assert.AreEqual((4, 3, -2, -1), (image.Width, image.Height, image.OffsetX, image.OffsetY));
        Assert.AreEqual(4 * 3 * 4, image.Rgba.Length);
        Assert.AreEqual(5, image.Rgba[0]);
    }

    [TestMethod]
    public void TheLowerTriangleOfATerrainDrawsItsOwnImage()
    {
        using var folder = new ContentFolder();
        folder.Png("five-lower.png", "6x1:6");
        folder.Entries["terrain/5/lower"] = ContentFolder.Entry("five-lower.png", "0, 0");

        var image = folder.Load().Draw(Triangle(5, upper: false))!;

        Assert.AreEqual((6, 1), (image.Width, image.Height));
    }

    [TestMethod]
    [DataRow(RoadDirection.East, "road/east", 3)]
    [DataRow(RoadDirection.SouthEast, "road/south-east", 4)]
    [DataRow(RoadDirection.South, "road/south", 5)]
    public void ARoadStripDrawsTheImageOfItsDirection(RoadDirection direction, string key, int width)
    {
        using var folder = new ContentFolder();
        folder.Png("strip.png", $"{width}x1:1");
        folder.Entries[key] = ContentFolder.Entry("strip.png", "1, 2");

        var image = folder
            .Load()
            .Draw(
                new RoadStrip
                {
                    X = 10,
                    Y = 20,
                    Direction = direction,
                    Difference = 0,
                    Slope = 0,
                    Ground = new Terrain { Code = 5 },
                }
            )!;

        Assert.AreEqual((width, 1, 1, 2), (image.Width, image.Height, image.OffsetX, image.OffsetY));
    }

    [TestMethod]
    public void TermsThatShareAFileAndAnAnchorShareOneImageInstance()
    {
        using var folder = new ContentFolder();
        var source = folder.Load();

        Assert.AreSame(source.Draw(Triangle(4, true)), source.Draw(Triangle(7, false)));
        Assert.AreSame(source.Draw(Triangle(4, true)), source.Draw(Triangle(4, true)));
    }

    [TestMethod]
    public void TheSameFileWithAnotherAnchorIsAnotherImage()
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/4/upper"] = ContentFolder.Entry("a.png", "1, 1");
        var source = folder.Load();

        Assert.AreNotSame(source.Draw(Triangle(4, true)), source.Draw(Triangle(7, true)));
    }

    [TestMethod]
    public void ATerrainCodeTheSetHasNoKeyForDrawsNothing()
    {
        using var folder = new ContentFolder();

        Assert.IsNull(folder.Load().Draw(Triangle(16, true)));
    }

    [TestMethod]
    public void AMapObjectDrawsTheImageOfItsCodeWithNoShadowWhateverItsStep()
    {
        using var folder = new ContentFolder();
        folder.Png("tree.png", "5x7:4");
        folder.Entries["object/13"] = ContentFolder.Entry("tree.png", "-2, -6");
        var source = folder.Load();

        var drawing = source.Draw(
            new MapObjectDraw
            {
                Row = 0,
                X = 0,
                Y = 0,
                Object = new MapObject { Code = 13 },
                Step = 0,
            }
        )!;

        Assert.IsNull(drawing.Shadow);
        Assert.AreEqual(
            (5, 7, -2, -6),
            (drawing.Image!.Width, drawing.Image.Height, drawing.Image.OffsetX, drawing.Image.OffsetY)
        );
        Assert.AreSame(
            drawing.Image,
            source
                .Draw(
                    new MapObjectDraw
                    {
                        Row = 5,
                        X = 9,
                        Y = 9,
                        Object = new MapObject { Code = 13 },
                        Step = 77,
                    }
                )!
                .Image
        );
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(7)]
    [DataRow(128)]
    public void ACodeTheGameDrawsNoObjectForDrawsNothing(int code)
    {
        using var folder = new ContentFolder();

        Assert.IsNull(
            folder
                .Load()
                .Draw(
                    new MapObjectDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Object = new MapObject { Code = (byte)code },
                        Step = 0,
                    }
                )
        );
    }

    [TestMethod]
    public void TheFlagDrawsItsOwnImage()
    {
        using var folder = new ContentFolder();
        folder.Png("flag.png", "3x4:2");
        folder.Entries["object/flag"] = ContentFolder.Entry("flag.png", "0, -3");

        var image = folder
            .Load()
            .Draw(
                new MapObjectDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Object = MapObject.Flag,
                    Step = 8,
                }
            )!
            .Image!;

        Assert.AreEqual((3, 4, 0, -3), (image.Width, image.Height, image.OffsetX, image.OffsetY));
    }

    [TestMethod]
    [DataRow("object/flag")]
    [DataRow("object/8")]
    [DataRow("object/127")]
    public void AnObjectKeyTheManifestLacksIsRefusedByFileAndKey(string key)
    {
        using var folder = new ContentFolder();
        folder.Entries.Remove(key);

        Refuses(folder, "manifest.json", key);
    }

    [TestMethod]
    public void TheWaterRippleDrawsNothing()
    {
        using var folder = new ContentFolder();

        Assert.IsNull(
            folder
                .Load()
                .Draw(
                    new WaterRipple
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Tile = 0,
                        Step = 0,
                    }
                )
        );
    }

    [TestMethod]
    [DataRow(0x18, "building/castle")]
    [DataRow(2, "building/woodcutter")]
    [DataRow(4, "building/stonecutter")]
    [DataRow(9, "building/forester")]
    [DataRow(0x11, "building/sawmill")]
    public void ABuildingDrawsItsOwnImageWithNoShadow(int type, string key)
    {
        using var folder = new ContentFolder();
        folder.Png("building.png", "9x8:6");
        folder.Entries[key] = ContentFolder.Entry("building.png", "-4, -7");

        var drawing = folder
            .Load()
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = new BuildingType { Code = type },
                }
            )!;

        Assert.IsNull(drawing.Shadow);
        Assert.AreEqual(
            (9, 8, -4, -7),
            (drawing.Image!.Width, drawing.Image.Height, drawing.Image.OffsetX, drawing.Image.OffsetY)
        );
    }

    [TestMethod]
    [DataRow(0, "building/site-large")]
    [DataRow(1, "building/site-small")]
    public void ABuildingSiteDrawsTheImageOfItsProgressWhateverItsType(int progress, string key)
    {
        using var folder = new ContentFolder();
        folder.Png("site.png", "9x8:6");
        folder.Entries[key] = ContentFolder.Entry("site.png", "-4, -7");
        var source = folder.Load();

        var drawing = source.Draw(
            new BuildingDraw
            {
                Row = 0,
                X = 0,
                Y = 0,
                Type = BuildingType.Sawmill,
                SiteProgress = (ushort)progress,
            }
        )!;

        Assert.IsNull(drawing.Shadow);
        Assert.AreEqual((9, 8), (drawing.Image!.Width, drawing.Image.Height));
        Assert.AreSame(
            drawing.Image,
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Woodcutter,
                        SiteProgress = (ushort)progress,
                    }
                )!
                .Image
        );
    }

    [TestMethod]
    public void ASiteGrowsItsScaffoldOverItsBaseAsItsProgressRises()
    {
        using var folder = new ContentFolder();
        folder.Png("base.png", "9x2:6");
        folder.Png("scaffold.png", "9x8:5");
        folder.Entries["building/site-small"] = ContentFolder.Entry("base.png", "-4, -1");
        folder.Entries["building/stage-small"] = ContentFolder.Entry("scaffold.png", "-4, -7");
        var source = folder.Load();

        // Twice the progress word is the fraction of the scaffold's height that shows from the bottom: (0x200 * 8 >> 16) + 1 = 1 row, and
        // (0x6000 * 8 >> 16) + 1 = 4 rows; the base is 2 rows high and its bottom row is the scaffold's.
        var low = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0x0100,
                }
            )!
            .Image!;
        var high = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0x3000,
                }
            )!
            .Image!;

        Assert.AreEqual((2, -1), (low.Height, low.OffsetY));
        Assert.AreEqual((4, -3), (high.Height, high.OffsetY));
        Assert.AreEqual(5, high.Rgba[0], "the scaffold shows in the rows above the base");
    }

    [TestMethod]
    public void ASiteInTheSecondHalfDrawsTheWholeScaffoldAndTheFinishedBuildingGrowingOverIt()
    {
        using var folder = new ContentFolder();
        folder.Png("scaffold.png", "9x8:5");
        folder.Png("hut.png", "9x8:4");
        folder.Entries["building/stage-small"] = ContentFolder.Entry("scaffold.png", "-4, -7");
        folder.Entries["building/forester"] = ContentFolder.Entry("hut.png", "-4, -7");
        var source = folder.Load();

        var first = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0x8000,
                }
            )!
            .Image!;
        var later = source
            .Draw(
                new BuildingDraw
                {
                    Row = 0,
                    X = 0,
                    Y = 0,
                    Type = BuildingType.Forester,
                    SiteProgress = 0xc000,
                }
            )!
            .Image!;

        // The whole scaffold alone at 0x8000; then the building's bottom (0x8000 * 8 >> 16) + 1 = 5 rows over it.
        Assert.AreEqual(5, first.Rgba[0]);
        Assert.AreEqual(5, later.Rgba[0], "the rows above the grown building still show the scaffold");
        Assert.AreEqual(4, later.Rgba[(7 * 9) * 4], "the building's rows cover the scaffold's at the bottom");
    }

    [TestMethod]
    public void ABuildingTypeTheGameDrawsNoBuildingForDrawsNothing()
    {
        using var folder = new ContentFolder();

        Assert.IsNull(
            folder
                .Load()
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.None,
                    }
                )
        );
    }

    [TestMethod]
    [DataRow("building/castle")]
    [DataRow("building/woodcutter")]
    [DataRow("building/stonecutter")]
    [DataRow("building/forester")]
    [DataRow("building/sawmill")]
    [DataRow("building/site-small")]
    [DataRow("building/site-large")]
    [DataRow("building/stage-small")]
    [DataRow("building/stage-large")]
    public void ABuildingKeyTheManifestLacksIsRefusedByFileAndKey(string key)
    {
        using var folder = new ContentFolder();
        folder.Entries.Remove(key);

        Refuses(folder, "manifest.json", key);
    }

    [TestMethod]
    public void AGoodDrawsTheImageOfItsLowFiveBitsWithNoShadow()
    {
        using var folder = new ContentFolder();
        folder.Png("log.png", "4x2:7");
        folder.Entries["good/7"] = ContentFolder.Entry("log.png", "-2, -2");
        var source = folder.Load();

        var drawing = source.Draw(
            new GoodAtFlag
            {
                Row = 0,
                X = 0,
                Y = 0,
                Good = new Good { Code = 7 },
            }
        )!;

        Assert.IsNull(drawing.Shadow);
        Assert.AreEqual(
            (4, 2, -2, -2),
            (drawing.Image!.Width, drawing.Image.Height, drawing.Image.OffsetX, drawing.Image.OffsetY)
        );
        Assert.AreSame(
            drawing.Image,
            source
                .Draw(
                    new GoodAtFlag
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Good = new Good { Code = 7 + 32 },
                    }
                )!
                .Image
        );
    }

    [TestMethod]
    [DataRow("good/0")]
    [DataRow("good/31")]
    public void AGoodKeyTheManifestLacksIsRefusedByFileAndKey(string key)
    {
        using var folder = new ContentFolder();
        folder.Entries.Remove(key);

        Refuses(folder, "manifest.json", key);
    }

    static SettlerDraw Settler(
        Job job,
        Activity activity,
        SettlerPart part = SettlerPart.Figure,
        Heading? heading = null,
        int frame = 0,
        int step = 0,
        int player = 0,
        Good? carried = null
    ) =>
        new()
        {
            Row = 0,
            X = 0,
            Y = 0,
            Part = part,
            Job = job,
            Activity = activity,
            Heading = heading,
            Frame = frame,
            Step = step,
            Player = player,
            Carried = carried ?? Good.None,
        };

    static Activity Walking(Heading? heading = null) =>
        new WalksRoad { Load = null, Heading = heading ?? new Heading { Value = 0 } };

    static Activity Cutting() =>
        new Cuts
        {
            Back = new Vector(0, 0),
            SecondVariant = false,
            Count = 0,
            Memory = 0,
        };

    [TestMethod]
    public void AWoodcutterWalkingCuttingAndCarryingEachDrawsItsOwnFrame()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("cuts.png", "2x2:2");
        folder.Png("carries.png", "2x2:3");
        folder.Entries["settler/woodcutter/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/woodcutter/cuts"] = ContentFolder.Entry("cuts.png", "0, 0");
        folder.Entries["settler/woodcutter/carries"] = ContentFolder.Entry("carries.png", "0, 0");
        var source = folder.Load();

        var walks = source.Draw(Settler(new Woodcutter(), Walking()))!.Image!;
        var cuts = source.Draw(Settler(new Woodcutter(), Cutting()))!.Image!;
        var carries = source.Draw(Settler(new Woodcutter(), Walking(), carried: new Good { Code = 7 }))!.Image!;

        Assert.AreEqual((1, 2, 3), (walks.Rgba[0], cuts.Rgba[0], carries.Rgba[0]));
    }

    [TestMethod]
    public void AWoodcuttersWayBackWithALogIsDrawnInTheCarriesPose()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("carries.png", "2x2:3");
        folder.Entries["settler/woodcutter/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/woodcutter/carries"] = ContentFolder.Entry("carries.png", "0, 0");
        var source = folder.Load();

        var withLog = source
            .Draw(
                Settler(
                    new Woodcutter(),
                    new WalksFree
                    {
                        Trip = new Trip
                        {
                            Remaining = new Vector(2, 1),
                            Purpose = new BackToHut { Carrying = true },
                        },
                    }
                )
            )!
            .Image!;
        var withoutLog = source
            .Draw(
                Settler(
                    new Woodcutter(),
                    new WalksFree
                    {
                        Trip = new Trip
                        {
                            Remaining = new Vector(2, 1),
                            Purpose = new BackToHut { Carrying = false },
                        },
                    }
                )
            )!
            .Image!;

        Assert.AreEqual((3, 1), (withLog.Rgba[0], withoutLog.Rgba[0]));
    }

    [TestMethod]
    public void TheFrameOfAJobAndActivityStaysTheSameAcrossStepsFramesAndHeadings()
    {
        using var folder = new ContentFolder();
        var source = folder.Load();

        var first = source
            .Draw(Settler(new Woodcutter(), Walking(new Heading { Value = 0 }), frame: 7, step: 0))!
            .Image;
        var other = source
            .Draw(Settler(new Woodcutter(), Walking(new Heading { Value = 3 }), frame: 31, step: 40))!
            .Image;
        var waiting = source.Draw(Settler(new Woodcutter(), new WaitsOutStep { Then = Walking() }, step: 5))!.Image;

        Assert.AreSame(first, other);
        Assert.AreSame(first, waiting);
    }

    [TestMethod]
    public void ASettlerDrawsItsFigureOnlyAndNoShadow()
    {
        using var folder = new ContentFolder();
        var source = folder.Load();

        Assert.IsNull(source.Draw(Settler(new Carrier(), Walking()))!.Shadow);
        Assert.IsNull(source.Draw(Settler(new Carrier(), Walking(), SettlerPart.Overlay)));
        Assert.IsNull(source.Draw(Settler(new Carrier(), Walking(), SettlerPart.SecondFigure)));
    }

    [TestMethod]
    public void AStonecutterWalkingCuttingAndCarryingTheStoneEachDrawsItsOwnFrame()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("cuts.png", "2x2:2");
        folder.Png("carries.png", "2x2:3");
        folder.Entries["settler/stonecutter/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/stonecutter/cuts"] = ContentFolder.Entry("cuts.png", "0, 0");
        folder.Entries["settler/stonecutter/carries"] = ContentFolder.Entry("carries.png", "0, 0");
        var source = folder.Load();
        var outbound = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(2, 1),
                Purpose = new ToHeap { Back = new Vector(-2, -1) },
            },
            EyesHeaps = true,
        };
        var back = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(2, 1),
                Purpose = new BackToHut { Carrying = true },
            },
        };

        var walks = source.Draw(Settler(new Stonecutter(), outbound))!.Image!;
        var cuts = source
            .Draw(
                Settler(
                    new Stonecutter(),
                    new CutsStone
                    {
                        Back = new Vector(0, 0),
                        Phase = 1,
                        Quarter = 0,
                    }
                )
            )!
            .Image!;
        var carries = source.Draw(Settler(new Stonecutter(), back))!.Image!;

        Assert.AreEqual((1, 2, 3), (walks.Rgba[0], cuts.Rgba[0], carries.Rgba[0]));
    }

    [TestMethod]
    public void AForesterWalkingAndPlantingEachDrawsItsOwnFrame()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("cuts.png", "2x2:2");
        folder.Entries["settler/forester/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/forester/cuts"] = ContentFolder.Entry("cuts.png", "0, 0");
        var source = folder.Load();
        var outbound = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(2, 1),
                Purpose = new ToSpot { Back = new Vector(-2, -1) },
            },
        };

        var walks = source.Draw(Settler(new Forester(), outbound))!.Image!;
        var plants = source
            .Draw(
                Settler(
                    new Forester(),
                    new Plants
                    {
                        Back = new Vector(-2, -1),
                        Planted = false,
                        Memory = 8,
                    }
                )
            )!
            .Image!;

        Assert.AreEqual((1, 2), (walks.Rgba[0], plants.Rgba[0]));
    }

    [TestMethod]
    public void AJobTheGameDrawsNoFigureForDrawsNothing()
    {
        using var folder = new ContentFolder();

        Assert.IsNull(folder.Load().Draw(Settler(new CastleSettler(), Walking())));
    }

    [TestMethod]
    [DataRow("carrier")]
    [DataRow("woodcutter")]
    [DataRow("stonecutter")]
    [DataRow("type-one-hut")]
    [DataRow("builder")]
    [DataRow("forester")]
    [DataRow("digger")]
    [DataRow("sawmiller")]
    public void EachJobDrawsAnImageForEveryActivityKind(string job)
    {
        using var folder = new ContentFolder();
        var source = folder.Load();
        var jobs = new Dictionary<string, Job>
        {
            ["carrier"] = new Carrier(),
            ["woodcutter"] = new Woodcutter(),
            ["stonecutter"] = new Stonecutter(),
            ["type-one-hut"] = new TypeOneHut(),
            ["builder"] = new Builder(),
            ["forester"] = new Forester(),
            ["digger"] = new Digger(),
            ["sawmiller"] = new Sawmiller(),
        };
        Activity[] activities =
        [
            new CutsStone
            {
                Back = new Vector(0, 0),
                Phase = 1,
                Quarter = 0,
            },
            new Plants
            {
                Back = new Vector(0, 0),
                Planted = true,
                Memory = 8,
            },
            new Digs
            {
                Pass = 15,
                Target = 10,
                Node = 6,
                Phase = 1,
            },
            new Saws { Sawing = true },
            new WaitsOutStep { Then = new PutsDownGood { Good = Good.Plank } },
            Walking(),
            new Walks
            {
                Errand = new ToStorage(),
                Heading = new Heading { Value = 0 },
            },
            new Door { TimeLeft = 0 },
            new HandsOver
            {
                Load = null,
                Heading = new Heading { Value = 0 },
            },
            Cutting(),
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = new Vector(0, 0),
                    Purpose = new BackToHut { Carrying = false },
                },
            },
            new SearchesHome(),
            new WaitsAtFlag
            {
                Flag = 0,
                Direction = 0,
                Approach = new Heading { Value = 0 }.Retried,
            },
        ];

        foreach (var activity in activities)
        {
            Assert.IsNotNull(source.Draw(Settler(jobs[job], activity))?.Image, activity.ToString());
        }
    }

    [TestMethod]
    public void ACarrierWaitingOffTheRoadDrawsTheWaitsFrameAndNotTheWalksFrame()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("waits.png", "2x2:2");
        folder.Entries["settler/carrier/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/carrier/waits"] = ContentFolder.Entry("waits.png", "0, 0");
        var source = folder.Load();

        var walks = source.Draw(Settler(new Carrier(), Walking()))!.Image!;
        var waits = source
            .Draw(
                Settler(
                    new Carrier(),
                    new WaitsAtFlag
                    {
                        Flag = 0,
                        Direction = 0,
                        Approach = new Heading { Value = 0 }.Retried,
                    }
                )
            )!
            .Image!;

        Assert.AreEqual((1, 2), (walks.Rgba[0], waits.Rgba[0]));
    }

    [TestMethod]
    public void ASawmillerSawingDrawsTheCutsFrameAndOneWhoStepsOutWithAPlankTheCarriesFrame()
    {
        using var folder = new ContentFolder();
        folder.Png("walks.png", "2x2:1");
        folder.Png("carries.png", "2x2:2");
        folder.Png("cuts.png", "2x2:3");
        folder.Entries["settler/sawmiller/walks"] = ContentFolder.Entry("walks.png", "0, 0");
        folder.Entries["settler/sawmiller/carries"] = ContentFolder.Entry("carries.png", "0, 0");
        folder.Entries["settler/sawmiller/cuts"] = ContentFolder.Entry("cuts.png", "0, 0");
        var source = folder.Load();

        var sawing = source.Draw(Settler(new Sawmiller(), new Saws { Sawing = true }))!.Image!;
        var carrying = source
            .Draw(Settler(new Sawmiller(), new WaitsOutStep { Then = new PutsDownGood { Good = Good.Plank } }))!
            .Image!;
        var walking = source
            .Draw(
                Settler(
                    new Sawmiller(),
                    new Walks
                    {
                        Errand = new ToStorage(),
                        Heading = new Heading { Value = 0 },
                    }
                )
            )!
            .Image!;

        Assert.AreEqual((3, 2, 1), (sawing.Rgba[0], carrying.Rgba[0], walking.Rgba[0]));
    }

    [TestMethod]
    [DataRow("settler/carrier/walks")]
    [DataRow("settler/woodcutter/cuts")]
    [DataRow("settler/type-one-hut/waits")]
    public void ASettlerKeyTheManifestLacksIsRefusedByFileAndKey(string key)
    {
        using var folder = new ContentFolder();
        folder.Entries.Remove(key);

        Refuses(folder, "manifest.json", key);
    }

    [TestMethod]
    public void ThePlayerShowsAsItsColourOverTheMarkPixelsOfTheFigureAndNothingElse()
    {
        using var folder = new ContentFolder();
        folder.Png("marked.png", "2x1:mark");
        folder.Entries["settler/carrier/walks"] = ContentFolder.Entry("marked.png", "0, 0");
        var source = folder.Load();

        var first = source.Draw(Settler(new Carrier(), Walking(), player: 0))!.Image!;
        var second = source.Draw(Settler(new Carrier(), Walking(), player: 1))!.Image!;

        var (r, g, b) = OpenSettlersSource.PlayerColour(1);
        CollectionAssert.AreEqual(new byte[] { r, g, b, 255, 10, 20, 30, 255 }, second.Rgba);
        Assert.AreNotEqual(OpenSettlersSource.PlayerColour(0), OpenSettlersSource.PlayerColour(1));
        CollectionAssert.AreNotEqual(first.Rgba, second.Rgba);
        CollectionAssert.AreEqual(new byte[] { 10, 20, 30, 255 }, first.Rgba[4..]);
    }

    // The rule is the doc comment of PlayerColours: a player takes the colour of its number modulo the number of colours.
    // Modulo is the mathematical one, so -1 takes the colour of 7 and -8, 8, and int.MinValue (a multiple of 8) the colour of 0.
    [TestMethod]
    [DataRow(-1, 7)]
    [DataRow(-8, 0)]
    [DataRow(-9, 7)]
    [DataRow(8, 0)]
    [DataRow(9, 1)]
    [DataRow(int.MinValue, 0)]
    [DataRow(int.MaxValue, 7)]
    public void APlayerNumberOutOfTheEightTakesTheColourOfItsNumberModuloEight(int player, int same)
    {
        Assert.AreEqual(OpenSettlersSource.PlayerColour(same), OpenSettlersSource.PlayerColour(player));
    }

    [TestMethod]
    public void ANegativePlayerIsDrawnInItsColour()
    {
        using var folder = new ContentFolder();
        folder.Png("marked.png", "2x1:mark");
        folder.Entries["settler/carrier/walks"] = ContentFolder.Entry("marked.png", "0, 0");
        var source = folder.Load();

        var image = source.Draw(Settler(new Carrier(), Walking(), player: -1))!.Image!;

        var (r, g, b) = OpenSettlersSource.PlayerColour(7);
        CollectionAssert.AreEqual(new byte[] { r, g, b, 255, 10, 20, 30, 255 }, image.Rgba);
    }

    [TestMethod]
    public void TheSameFrameAndPlayerAnswerOneImageInstance()
    {
        using var folder = new ContentFolder();
        folder.Png("marked.png", "2x1:mark");
        folder.Entries["settler/carrier/walks"] = ContentFolder.Entry("marked.png", "0, 0");
        var source = folder.Load();

        var one = source.Draw(Settler(new Carrier(), Walking(), frame: 3, player: 2))!.Image;
        var two = source
            .Draw(Settler(new Carrier(), Walking(new Heading { Value = 4 }), frame: 9, step: 12, player: 2))!
            .Image;
        var other = source.Draw(Settler(new Carrier(), Walking(), player: 3))!.Image;

        Assert.AreSame(one, two);
        Assert.AreNotSame(one, other);
    }

    [TestMethod]
    public void ThePlayersBeyondTheColoursTheSetHasShareTheirColours()
    {
        using var folder = new ContentFolder();
        folder.Png("marked.png", "2x1:mark");
        folder.Entries["settler/carrier/walks"] = ContentFolder.Entry("marked.png", "0, 0");
        var source = folder.Load();

        var low = source.Draw(Settler(new Carrier(), Walking(), player: 1))!.Image!;
        var high = source.Draw(Settler(new Carrier(), Walking(), player: 1 + OpenSettlersSource.PlayerColours))!.Image!;

        CollectionAssert.AreEqual(low.Rgba, high.Rgba);
    }

    [TestMethod]
    public void TheObjectHeadroomIsTheTallestObjectBuildingOrGoodImage()
    {
        using var folder = new ContentFolder();
        folder.Png("tall.png", "3x40:5");
        folder.Entries["building/castle"] = ContentFolder.Entry("tall.png", "0, 0");

        Assert.AreEqual(40, folder.Load().ObjectHeadroom);
    }

    static void Refuses(ContentFolder folder, params string[] parts)
    {
        var e = Assert.Throws<InvalidDataException>(() => folder.Load());
        foreach (var part in parts)
        {
            StringAssert.Contains(e.Message, part);
        }
    }

    [TestMethod]
    public void AMissingManifestIsRefusedByItsFileName()
    {
        using var folder = new ContentFolder();

        var e = Assert.Throws<InvalidDataException>(() => OpenSettlersSource.Load(folder.Root, new FakeDecoder()));

        StringAssert.Contains(e.Message, "manifest.json");
    }

    [TestMethod]
    public void AMalformedManifestIsRefusedByItsFileName()
    {
        using var folder = new ContentFolder();
        folder.Extra = "this is not json";

        Refuses(folder, "manifest.json", "not valid JSON");
    }

    [TestMethod]
    public void AManifestThatIsNotAnObjectIsRefusedByItsFileName()
    {
        using var folder = new ContentFolder();
        folder.Text(OpenSettlersSource.ManifestFile, "[1, 2]");

        var e = Assert.Throws<InvalidDataException>(() => OpenSettlersSource.Load(folder.Root, new FakeDecoder()));

        StringAssert.Contains(e.Message, "manifest.json");
    }

    [TestMethod]
    public void ATermWithNoImageEntryIsRefusedByFileAndKey()
    {
        using var folder = new ContentFolder();
        folder.Entries.Remove("terrain/9/lower");

        Refuses(folder, "manifest.json", "terrain/9/lower", "no image");
    }

    [TestMethod]
    public void AnEntryWithoutAnImagePathIsRefusedByFileAndKey()
    {
        using var folder = new ContentFolder();
        folder.Entries["road/south"] = """{ "anchor": [0, 0] }""";

        Refuses(folder, "manifest.json", "road/south", "no image");
    }

    [TestMethod]
    public void AMissingPngIsRefusedByFileKeyAndPath()
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/3/upper"] = ContentFolder.Entry("gone.png", "0, 0");

        Refuses(folder, "manifest.json", "terrain/3/upper", "gone.png", "missing");
    }

    [TestMethod]
    public void AnUndecodablePngIsRefusedByFileKeyAndPath()
    {
        using var folder = new ContentFolder();
        folder.Png("bad.png", "not an image");
        folder.Entries["road/east"] = ContentFolder.Entry("bad.png", "0, 0");

        Refuses(folder, "manifest.json", "road/east", "bad.png", "cannot be decoded");
    }

    [TestMethod]
    public void AnImageWithNoPixelIsRefusedAsUndecodable()
    {
        using var folder = new ContentFolder();
        folder.Png("none.png", "0x0:0");
        folder.Entries["road/east"] = ContentFolder.Entry("none.png", "0, 0");

        Refuses(folder, "road/east", "none.png", "cannot be decoded");
    }

    // A decoder whose images claim a size their pixels do not fill: 2 x 2 pixels, 15 bytes of RGBA.
    sealed class ShortDecoder : IImageDecoder
    {
        public DecodedImage? Decode(string path) =>
            new()
            {
                Width = 2,
                Height = 2,
                Rgba = new byte[15],
            };
    }

    [TestMethod]
    public void AnImageWhoseRgbaLengthIsNotFourBytesAPixelIsRefusedAsUndecodable()
    {
        using var folder = new ContentFolder();
        folder.Load();

        var e = Assert.Throws<InvalidDataException>(() => OpenSettlersSource.Load(folder.Root, new ShortDecoder()));

        StringAssert.Contains(e.Message, "manifest.json");
        StringAssert.Contains(e.Message, "a.png");
        StringAssert.Contains(e.Message, "cannot be decoded");
    }

    [TestMethod]
    [DataRow("5")]
    [DataRow("null")]
    [DataRow("[\"a.png\"]")]
    [DataRow("\"\"")]
    public void AnImageValueThatIsNoStringIsRefusedByFileAndKey(string image)
    {
        using var folder = new ContentFolder();
        folder.Entries["road/south"] = $$"""{ "image": {{image}}, "anchor": [0, 0] }""";

        Refuses(folder, "manifest.json", "road/south", "no image");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("1")]
    [DataRow("1, 2, 3")]
    [DataRow("1.5, 2")]
    [DataRow("\"a\", 2")]
    [DataRow("1, null")]
    public void AnAnchorOfNotTwoIntegersIsRefusedByFileAndKey(string anchor)
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/2/upper"] = ContentFolder.Entry("a.png", anchor);

        Refuses(folder, "manifest.json", "terrain/2/upper", "anchor");
    }

    [TestMethod]
    public void AMissingAnchorIsRefusedByFileAndKey()
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/2/upper"] = """{ "image": "a.png" }""";

        Refuses(folder, "manifest.json", "terrain/2/upper", "anchor");
    }

    [TestMethod]
    public void AnAnchorThatIsNotAnArrayIsRefusedByFileAndKey()
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/2/upper"] = """{ "image": "a.png", "anchor": "0,0" }""";

        Refuses(folder, "manifest.json", "terrain/2/upper", "anchor");
    }

    [TestMethod]
    public void ADuplicateKeyIsRefusedByFileAndKey()
    {
        using var folder = new ContentFolder();
        folder.Extra = "\"road/east\": " + ContentFolder.Entry("a.png", "0, 0");

        Refuses(folder, "manifest.json", "road/east", "duplicate");
    }

    [TestMethod]
    [DataRow("/etc/passwd.png")]
    [DataRow("\\\\\\\\server\\\\share\\\\a.png")]
    [DataRow("C:\\\\a.png")]
    [DataRow("../a.png")]
    [DataRow("sub/../../a.png")]
    [DataRow("sub\\\\..\\\\a.png")]
    public void APathOutsideTheSetIsRefusedByFileAndKey(string path)
    {
        using var folder = new ContentFolder();
        folder.Entries["terrain/6/lower"] = ContentFolder.Entry(path, "0, 0");

        Refuses(folder, "manifest.json", "terrain/6/lower", "outside the content set");
    }

    // Reads the size of a PNG from its header; the pixels are a stand-in, since only Godot decodes them.
    sealed class HeaderDecoder : IImageDecoder
    {
        public DecodedImage? Decode(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (
                bytes.Length < 24
                || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            )
            {
                return null;
            }

            var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));

            return new DecodedImage
            {
                Width = width,
                Height = height,
                Rgba = new byte[width * height * 4],
            };
        }
    }

    static string RepoPath(string relative)
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return Path.Combine(dir, relative);
    }

    [TestMethod]
    public void TheCheckedInContentSetLoadsAndDrawsEveryTerrainCodeAndRoadDirection()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/open-settlers"), new HeaderDecoder());

        for (var code = 0; code < OpenSettlersSource.TerrainCodes; code++)
        {
            Assert.AreEqual(
                (48, 144),
                (source.Draw(Triangle(code, true))!.Width, source.Draw(Triangle(code, true))!.Height)
            );
            Assert.AreEqual(-20, source.Draw(Triangle(code, false))!.OffsetY);
        }

        foreach (var direction in Enum.GetValues<RoadDirection>())
        {
            Assert.IsNotNull(
                source.Draw(
                    new RoadStrip
                    {
                        X = 0,
                        Y = 0,
                        Direction = direction,
                        Difference = 0,
                        Slope = 0,
                        Ground = new Terrain { Code = 5 },
                    }
                )
            );
        }
    }

    // Every term the shared builders can emit, drawn through the checked-in set. A term the set has no entry for fails
    // here, whether a builder gained a term or the manifest lost a key.
    [TestMethod]
    public void TheCheckedInContentSetCoversEveryObjectTermTheBuildersEmit()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/open-settlers"), new HeaderDecoder());
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
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[1] = 3;
        var game = GameFactory.Create(new RuleTables(classes, TestProgram.Made().StartStock), map);
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = 15,
                Y = 5,
            }
        );
        game.Tick();
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 17,
                Y = 3,
            }
        );
        game.Tick();
        Huts.PlaceAndTick(game, 16, 2);
        var goods = Enumerable.Range(0, 8).Select(place => new Good { Code = place + 1 }).ToList();
        var flagGoods = new Dictionary<(int X, int Y), IReadOnlyList<Good>> { [(17, 3)] = goods };

        var terms = ObjectDraws.Of(map, game.CastleStock.Castle, game.Buildings, flagGoods);

        Assert.IsTrue(terms.OfType<BuildingDraw>().Any(draw => draw.Type == BuildingType.Castle));
        Assert.IsTrue(terms.OfType<BuildingDraw>().Any(draw => draw.Type == BuildingType.Woodcutter));
        Assert.IsTrue(terms.OfType<GoodAtFlag>().Any());
        foreach (var term in terms)
        {
            // The codes 2 to 7 are structures the game draws from its buildings, so the set draws no object for them.
            var drawsNothing = term is MapObjectDraw { Object.Code: < MapObject.FirstTreeCode and not 1 };
            Assert.AreEqual(drawsNothing, term.Ask(source) is null, term.ToString());
        }
    }

    [TestMethod]
    public void TheCheckedInContentSetDrawsEveryObjectCodeFlagGoodAndBuildingTheGameCanName()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/open-settlers"), new HeaderDecoder());

        Assert.IsNotNull(
            source
                .Draw(
                    new MapObjectDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Object = MapObject.Flag,
                        Step = 0,
                    }
                )
                ?.Image
        );
        for (var code = MapObject.FirstTreeCode; code < 128; code++)
        {
            Assert.IsNotNull(
                source
                    .Draw(
                        new MapObjectDraw
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Object = new MapObject { Code = (byte)code },
                            Step = 0,
                        }
                    )
                    ?.Image,
                $"{code}"
            );
        }

        for (var code = 0; code < 32; code++)
        {
            Assert.IsNotNull(
                source
                    .Draw(
                        new GoodAtFlag
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Good = new Good { Code = code },
                        }
                    )
                    ?.Image,
                $"good {code}"
            );
        }

        Assert.IsNotNull(
            source
                .Draw(
                    new BuildingDraw
                    {
                        Row = 0,
                        X = 0,
                        Y = 0,
                        Type = BuildingType.Castle,
                    }
                )
                ?.Image
        );
        foreach (var type in BuildingPlacement.Types)
        {
            Assert.IsNotNull(
                source
                    .Draw(
                        new BuildingDraw
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Type = type,
                        }
                    )
                    ?.Image,
                $"finished {type}"
            );
            Assert.IsNotNull(
                source
                    .Draw(
                        new BuildingDraw
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Type = type,
                            SiteProgress = 0,
                        }
                    )
                    ?.Image,
                $"site of {type}, progress 0"
            );
            Assert.IsNotNull(
                source
                    .Draw(
                        new BuildingDraw
                        {
                            Row = 0,
                            X = 0,
                            Y = 0,
                            Type = type,
                            SiteProgress = 1,
                        }
                    )
                    ?.Image,
                $"site of {type}, progress 1"
            );
        }
    }

    [TestMethod]
    public void TheCheckedInContentSetDrawsEverySettlerTheBuildersEmit()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/open-settlers"), new HeaderDecoder());
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
        var figures = new SettlerFigures(new StepOffsets(SettlerFiguresTests.Sequences()), []);
        var heading = new Heading { Value = 0 };
        // The activities the builder draws (a frame that has a step gives each a figure term) and those it does not.
        Activity[] drawnActivities =
        [
            new Walks { Errand = new ToStorage(), Heading = heading },
            new WalksRoad { Load = null, Heading = heading },
            new WalksRoad
            {
                Load = new Load
                {
                    Good = new Good { Code = 7 },
                    Destination = null,
                },
                Heading = heading,
            },
            new WaitsOutStep
            {
                Then = new Walks { Errand = new ToStorage(), Heading = heading },
            },
            new HandsOver { Load = null, Heading = heading },
            new HandsOver
            {
                Load = new Load
                {
                    Good = new Good { Code = 7 },
                    Destination = null,
                },
                Heading = heading,
            },
            new Door { TimeLeft = 0 },
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = new Vector(0, 0),
                    Purpose = new BackToHut { Carrying = false },
                },
            },
            new Cuts
            {
                Back = new Vector(0, 0),
                SecondVariant = false,
                Count = 0,
                Memory = 0,
            },
            new SearchesHome(),
        ];
        Activity[] otherActivities =
        [
            new WaitsAtFlag
            {
                Flag = 0,
                Direction = 0,
                Approach = heading.Retried,
            },
            new ClaimsNode { Heading = heading, Approach = heading.Retried },
            new WaitsToGoHome { Approach = heading.Retried },
            new WaitsToRejoin { Approach = heading.Retried },
        ];

        // Every job the item's byte 0 can carry (bits 2 to 6): a job the History Edition's tables hold a figure handler for must
        // have a figure in the content set for every activity it is drawn in, and a job without one none, so the two sources draw
        // the same jobs.
        var handlers = SettlerFiguresTests.TablesOf([], [], []);
        var jobsDrawn = 0;
        for (var code = 0; code < 32; code++)
        {
            var job = Job.Of(code);
            Settler Of(Activity activity) => new(job, 3, 4, 3, 0, activity) { Frame = 7 };

            if (!handlers.HasHandler(job))
            {
                foreach (var activity in drawnActivities)
                {
                    Assert.IsTrue(
                        figures.Draws(map, [Of(activity)], 0).All(term => term.Ask(source) is null),
                        $"job {code} {activity}"
                    );
                }

                continue;
            }

            jobsDrawn++;
            foreach (var activity in drawnActivities)
            {
                var terms = figures.Draws(map, [Of(activity)], 0);
                Assert.IsTrue(terms.Any(term => term.Part == SettlerPart.Figure), $"job {code} {activity}");
                foreach (var term in terms)
                {
                    Assert.AreEqual(
                        term.Part == SettlerPart.Figure,
                        term.Ask(source) is not null,
                        $"job {code} {activity}"
                    );
                }
            }

            foreach (var activity in otherActivities)
            {
                foreach (var term in figures.Draws(map, [Of(activity)], 0))
                {
                    Assert.AreEqual(
                        term.Part == SettlerPart.Figure,
                        term.Ask(source) is not null,
                        $"job {code} {activity}"
                    );
                }
            }
        }

        Assert.IsGreaterThanOrEqualTo(3, jobsDrawn);

        // A carrier waiting off the road is drawn by the builder's second path, whose Figure term the source must answer.
        var waiting = figures.Draws(
            map,
            [
                new Settler(
                    new Carrier(),
                    3,
                    4,
                    3,
                    0,
                    new WaitsAtFlag
                    {
                        Flag = 0,
                        Direction = 0,
                        Approach = heading.Retried,
                    }
                )
                {
                    Frame = 7,
                },
            ],
            0
        );
        Assert.IsTrue(waiting.Any(term => term.Part == SettlerPart.Figure));
        Assert.IsTrue(waiting.Where(term => term.Part == SettlerPart.Figure).All(term => term.Ask(source) is not null));
    }

    // A flat image hangs from the triangle's apex whatever the slope, so a node that drops below the apex opens a gap
    // the rows below the triangle must fill. The image's rows below its triangle (Camera.RowSpacing rows) cover the
    // largest drop a node can have.
    [TestMethod]
    public void EveryTerrainImageCoversTheLargestDrop()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/open-settlers"), new HeaderDecoder());
        const int LargestDrop = Node.MaxHeight * TerrainTriangles.HeightStep;

        for (var code = 0; code < OpenSettlersSource.TerrainCodes; code++)
        {
            foreach (var upper in new[] { true, false })
            {
                var image = source.Draw(Triangle(code, upper))!;
                Assert.IsTrue(image.Height - Camera.RowSpacing >= LargestDrop, $"{code} {upper}: skirt {image.Height}");
            }
        }
    }

    [TestMethod]
    public void TheCheckedInContentSetHoldsTheGdignoreThatKeepsGodotFromImportingIt()
    {
        Assert.IsTrue(File.Exists(RepoPath("content/open-settlers/.gdignore")));
    }

    [TestMethod]
    public void APathInASubfolderIsRead()
    {
        using var folder = new ContentFolder();
        folder.Png("terrain/deep.png", "7x1:3");
        folder.Entries["terrain/6/lower"] = ContentFolder.Entry("terrain/deep.png", "0, 0");

        Assert.AreEqual(7, folder.Load().Draw(Triangle(6, false))!.Width);
    }
}

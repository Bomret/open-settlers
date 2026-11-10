using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            Array.Fill(rgba, byte.Parse(text[(colon + 1)..]));

            return new DecodedImage(width, height, rgba);
        }
    }

    static IEnumerable<string> Keys =>
        Enumerable
            .Range(0, 16)
            .SelectMany(code => new[] { $"terrain/{code}/upper", $"terrain/{code}/lower" })
            .Concat(["road/east", "road/south-east", "road/south"]);

    static TerrainTriangle Triangle(int code, bool upper) => new(0, 0, upper, new Terrain(code), 0, 0, 0, 100, 50);

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

        var image = folder.Load().Draw(new RoadStrip(10, 20, direction, 0, 0, new Terrain(5)))!;

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
    public void TheTermsTheSetHasNoArtForYetDrawNothingAndLeaveNoHeadroom()
    {
        using var folder = new ContentFolder();
        var source = folder.Load();
        var settler = new SettlerDraw(
            0,
            0,
            0,
            SettlerPart.Figure,
            Job.Carrier,
            new WalksRoad(null, new Heading(0)),
            null,
            0,
            0,
            0,
            Good.None
        );

        Assert.IsNull(source.Draw(new MapObjectDraw(0, 0, 0, MapObject.Flag, 0)));
        Assert.IsNull(source.Draw(new BuildingDraw(0, 0, 0, BuildingType.Castle)));
        Assert.IsNull(source.Draw(new GoodAtFlag(0, 0, 0, new Good(1))));
        Assert.IsNull(source.Draw(settler));
        Assert.IsNull(source.Draw(new WaterRipple(0, 0, 0, 0, 0)));
        Assert.AreEqual(0, source.ObjectHeadroom);
        Assert.IsNull(ObjectDraws.FlagImage(source));
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

            return new DecodedImage(width, height, new byte[width * height * 4]);
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
                (36, 32),
                (source.Draw(Triangle(code, true))!.Width, source.Draw(Triangle(code, true))!.Height)
            );
            Assert.AreEqual(-20, source.Draw(Triangle(code, false))!.OffsetY);
        }

        foreach (var direction in Enum.GetValues<RoadDirection>())
        {
            Assert.IsNotNull(source.Draw(new RoadStrip(0, 0, direction, 0, 0, new Terrain(5))));
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

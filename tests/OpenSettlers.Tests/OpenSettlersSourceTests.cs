using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The tests of the open settlers source that need the map view, the game, or the History Edition tables besides the source: the
// tests of the source alone are in its test project, in the class of the same name.
[TestClass]
public sealed class OpenSettlersSourceTests
{
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

    // Every term the shared builders can emit, drawn through the checked-in set. A term the set has no entry for fails
    // here, whether a builder gained a term or the manifest lost a key.
    [TestMethod]
    public void TheCheckedInContentSetCoversEveryObjectTermTheBuildersEmit()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/basic"), new HeaderDecoder());
        var map = MapGenerator.Generate(
            MadeProgram.Made(),
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
        var game = GameFactory.Create(
            new RuleTables(classes, MadeProgram.MadeStartStock(), catalog: TestCatalog.StandIn()),
            map
        );
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
        var goods = Enumerable.Range(0, 8).Select(place => TestCatalog.GoodOf(place + 1)).ToList();
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

    // The case that draws every settler the builders emit is in src/Launch.Tests, with the jobs of the History Edition's
    // figure tables (SettlerCases.JobsWithAFigureHandler) given.

    // A flat image hangs from the triangle's apex whatever the slope, so a node that drops below the apex opens a gap
    // the rows below the triangle must fill. The image's rows below its triangle (Camera.RowSpacing rows) cover the
    // largest drop a node can have.
    [TestMethod]
    public void EveryTerrainImageCoversTheLargestDrop()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/basic"), new HeaderDecoder());
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
}

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// ADR 0014 and INV-0010: the map view's shared code names what it draws in game terms. The terrain and road
// classes outside the History Edition source hold no rule table, no archive, and no entry number.
[TestClass]
public sealed class GraphicsSourceBoundaryTests
{
    static string Read(string path)
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return File.ReadAllText(Path.Combine(dir, path));
    }

    [TestMethod]
    [DataRow("src/MapView/TerrainTriangles.cs")]
    [DataRow("src/MapView/TerrainQuads.cs")]
    [DataRow("src/MapView/RoadDraws.cs")]
    [DataRow("src/MapView/ImageLayers.cs")]
    [DataRow("src/MapView/IGraphicsSource.cs")]
    [DataRow("src/MapView/ObjectDraws.cs")]
    [DataRow("src/MapView/ObjectQuads.cs")]
    [DataRow("src/MapView/ObjectTerms.cs")]
    [DataRow("src/MapView/SettlerFigures.cs")]
    [DataRow("src/MapView/MapPicking.cs")]
    [DataRow("src/MapView/ShotCrop.cs")]
    [DataRow("src/MapView/SpriteLayer.cs")]
    [DataRow("src/Viewer/MapViewControl.cs")]
    public void TheSharedTerrainRoadAndObjectCodeReadsNoRulesNoArchiveAndNoEntry(string path)
    {
        var text = Read(path);

        foreach (
            var forbidden in new[]
            {
                "TerrainRules",
                "RoadRules",
                "TerrainGraphics",
                "RoadGraphics",
                "ObjectSprites",
                "ObjectGraphics",
                "SettlerGraphics",
                "SettlerFigureTables",
                "SettlerSprites",
                "IdleCarrierTables",
                "SPAD.PA",
                "MaskLayer",
            }
        )
        {
            Assert.DoesNotContain(forbidden, text, $"{path} mentions {forbidden}");
        }
    }
}

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// ADR 0014 and INV-0010: the map view's shared code names what it draws in game terms. The terrain and road
// classes outside the History Edition source hold no rule table, no archive, and no entry number.
[TestClass]
public sealed class GraphicsSourceBoundaryTests
{
    static string Read(string path) => File.ReadAllText(Path.Combine(RootOf(), path));

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
    [DataRow("src/MapView/ClickDecision.cs")]
    [DataRow("src/MapView/MapKeys.cs")]
    [DataRow("src/MapView/ShotCrop.cs")]
    [DataRow("src/MapView/SpriteLayer.cs")]
    [DataRow("src/MapView/OpenSettlersSource.cs")]
    [DataRow("src/Viewer/MapViewControl.cs")]
    public void TheSharedTerrainRoadAndObjectCodeReadsNoRulesNoArchiveAndNoEntry(string path)
    {
        AssertMentions(path, []);
    }

    // The settlers' step offsets come from the install under every graphics source (ADR 0014, Consequences; INV-0010): the
    // class reads entry 2 of the graphics archive, and only through the one archive-entry helper of TerrainGraphics, which
    // the terrain, road, object, and settler graphics read their entries through too.
    [TestMethod]
    public void TheStepOffsetsReadOnlyTheArchiveEntryOfTheWalkSequences()
    {
        AssertMentions("src/MapView/StepOffsets.cs", ["TerrainGraphics"]);
    }

    [TestMethod]
    public void NoClassReadsAnArchiveEntryButThroughTheOneHelper()
    {
        var folder = Path.GetDirectoryName(Path.Combine(RootOf(), "src/MapView/x"))!;
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("ObjectGraphics.Read(", text, $"{file} reads an entry through a second helper");
            if (Path.GetFileName(file) != "TerrainGraphics.cs")
            {
                // The filter and the message form of an entry read are the helper's; any other copy is a second helper.
                Assert.DoesNotContain(
                    "e is InvalidDataException or ArgumentOutOfRangeException",
                    text,
                    $"{file} filters an entry read itself"
                );
                Assert.DoesNotContain("is unreadable:", text, $"{file} words an entry read failure itself");
            }
        }

        // The positive control: the helper still holds the strings the guard above looks for elsewhere.
        var helper = Read("src/MapView/TerrainGraphics.cs");
        Assert.Contains("internal static T Entry<T>(", helper);
        Assert.Contains("e is InvalidDataException or ArgumentOutOfRangeException", helper);
        Assert.Contains("is unreadable:", helper);
        Assert.DoesNotContain("static T Read<T>(", Read("src/MapView/ObjectGraphics.cs"));
    }

    // The figure handlers and the figure adds are the History Edition's (ADR 0016, INV-0010): the looks of that source and the figure
    // tables that hold them are the one place that names them.
    [TestMethod]
    public void NoClassOfTheMapViewOutsideTheHistoryEditionSourceNamesAFigureHandlerOrFigureAdd()
    {
        string[] historyEdition = ["HistoryEditionLook.cs", "SettlerFigureTables.cs"];
        var folder = Path.GetDirectoryName(Path.Combine(RootOf(), "src/MapView/x"))!;
        var checkedFiles = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            // The looks of the source are the files of its own folder, one for each job.
            if (
                Array.IndexOf(historyEdition, Path.GetFileName(file)) >= 0
                || Path.GetFileName(Path.GetDirectoryName(file)) == "HistoryEditionLooks"
            )
            {
                continue;
            }

            checkedFiles++;
            var text = File.ReadAllText(file);
            foreach (var forbidden in new[] { "HistoryEditionLook", "FigureAdds", "FigureHandler", "FigureBytes" })
            {
                Assert.DoesNotContain(forbidden, text, $"{file} mentions {forbidden}");
            }
        }

        Assert.IsGreaterThan(20, checkedFiles);
    }

    static string RootOf()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return dir;
    }

    static void AssertMentions(string path, string[] excepted)
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
            if (Array.IndexOf(excepted, forbidden) < 0)
            {
                Assert.DoesNotContain(forbidden, text, $"{path} mentions {forbidden}");
            }
        }
    }
}

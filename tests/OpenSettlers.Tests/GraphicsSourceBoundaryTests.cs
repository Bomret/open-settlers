using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    [DataRow("src/Import/IGraphicsSource.cs")]
    [DataRow("src/MapView/ObjectDraws.cs")]
    [DataRow("src/MapView/ObjectQuads.cs")]
    [DataRow("src/Import/IWalkSteps.cs")]
    [DataRow("src/Import/GraphicsLoad.cs")]
    [DataRow("src/Import/ObjectTerms.cs")]
    [DataRow("src/Import/TerrainTriangle.cs")]
    [DataRow("src/Import/RoadStrip.cs")]
    [DataRow("src/MapView/SettlerFigures.cs")]
    [DataRow("src/MapView/MapPicking.cs")]
    [DataRow("src/MapView/ClickDecision.cs")]
    [DataRow("src/MapView/MapKeys.cs")]
    [DataRow("src/MapView/ShotCrop.cs")]
    [DataRow("src/MapView/SpriteLayer.cs")]
    [DataRow("src/Import.OpenSettlers/OpenSettlersSource.cs")]
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
        AssertMentions("src/Import.HistoryEdition/StepOffsets.cs", ["TerrainGraphics"]);
    }

    [TestMethod]
    public void NoClassReadsAnArchiveEntryButThroughTheOneHelper()
    {
        // Every source file under src, so a helper in another folder (src/Adapters, src/Assets) is caught too. The asset check's
        // palette read (src/Import.HistoryEdition/Assets/ArchiveEntryKind.cs, PaletteOf) reads an entry of any archive, not of the
        // map view's graphics archive the helper names, and is the one file besides the helper that holds the filter.
        string[] own =
        [
            "src/Import.HistoryEdition/TerrainGraphics.cs",
            "src/Import.HistoryEdition/Assets/ArchiveEntryKind.cs",
        ];
        var root = RootOf();
        var folder = Path.Combine(root, "src");
        var checkedFiles = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/") || relative.Contains("/bin/"))
            {
                continue;
            }

            checkedFiles++;
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("ObjectGraphics.Read(", text, $"{file} reads an entry through a second helper");
            if (Array.IndexOf(own, relative) < 0)
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

        // The scan reaches the folders outside the map view.
        Assert.IsGreaterThan(
            Directory.GetFiles(Path.Combine(folder, "MapView"), "*.cs", SearchOption.AllDirectories).Length,
            checkedFiles
        );

        // The positive control: the helper still holds the strings the guard above looks for elsewhere.
        var helper = Read("src/Import.HistoryEdition/TerrainGraphics.cs");
        Assert.Contains("internal static T Entry<T>(", helper);
        Assert.Contains("e is InvalidDataException or ArgumentOutOfRangeException", helper);
        Assert.Contains("is unreadable:", helper);
        Assert.DoesNotContain("static T Read<T>(", Read("src/Import.HistoryEdition/ObjectGraphics.cs"));
    }

    // The History Edition assembly is the one place that names its types, the format readers, and the figure handlers and adds
    // (ADR 0016, ADR 0017, INV-0010). The map view and the viewer classes outside the composition root (the launch code and the
    // main scene, `AssetBrowserView`) draw through the ports. The asset browser's views read the install until their move.
    static readonly string[] PortsOnly =
    [
        "src/Viewer/MapViewControl.cs",
        "src/Viewer/PlayDriver.cs",
        "src/Viewer/GodotImageDecoder.cs",
    ];

    // What the shared code may not name: a History Edition type, its namespace, the format readers, and the figure terms.
    static List<string> HistoryEditionNames(string text)
    {
        var found = new List<string>();
        foreach (
            var forbidden in new[]
            {
                "HistoryEditionSource",
                "HistoryEditionLook",
                "Import.HistoryEdition",
                "OpenSettlers.Formats",
                "StepOffsets",
                "FigureAdds",
                "FigureHandler",
                "FigureBytes",
            }
        )
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                found.Add(forbidden);
            }
        }

        if (Regex.IsMatch(text, @"\bInstall\b"))
        {
            found.Add("Install");
        }

        return found;
    }

    [TestMethod]
    public void NoClassOfTheMapViewOrTheViewerOutsideTheCompositionRootNamesAHistoryEditionType()
    {
        var root = RootOf();
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "src/MapView"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
            .Concat(PortsOnly.Select(path => Path.Combine(root, path)))
            .ToList();

        foreach (var file in files)
        {
            Assert.IsEmpty(HistoryEditionNames(File.ReadAllText(file)), $"{file} names a History Edition type");
        }

        Assert.IsGreaterThan(20, files.Count);
    }

    // The positive control: the check fails on a planted violation.
    [TestMethod]
    public void ThePortsOnlyCheckFailsOnAPlantedHistoryEditionType()
    {
        CollectionAssert.AreEqual(
            new[] { "HistoryEditionSource", "Install" },
            HistoryEditionNames("var source = HistoryEditionSource.Load(install: Install.Open());")
        );
        CollectionAssert.AreEqual(
            new[] { "Import.HistoryEdition" },
            HistoryEditionNames("using OpenSettlers.Import.HistoryEdition;")
        );
        Assert.IsEmpty(HistoryEditionNames("GraphicsSourceName.HistoryEdition and the install's folder"));
    }

    // The History Edition assembly holds the looks and the figure tables: the one place that names the figure handlers.
    [TestMethod]
    public void TheHistoryEditionAssemblyHoldsTheLooksAndTheFigureTables()
    {
        var folder = Path.Combine(RootOf(), "src/Import.HistoryEdition");
        Assert.IsTrue(File.Exists(Path.Combine(folder, "HistoryEditionLook.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(folder, "SettlerFigureTables.cs")));
        Assert.IsNotEmpty(Directory.GetFiles(Path.Combine(folder, "HistoryEditionLooks"), "*Look.cs"));
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

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
    [DataRow("src/MapView/BorderDraws.cs")]
    [DataRow("src/MapView/ImageLayers.cs")]
    [DataRow("src/Import/IGraphicsSource.cs")]
    [DataRow("src/MapView/ObjectDraws.cs")]
    [DataRow("src/MapView/ObjectQuads.cs")]
    [DataRow("src/Import/IWalkSteps.cs")]
    [DataRow("src/Import/GraphicsLoad.cs")]
    [DataRow("src/Import/ObjectTerms.cs")]
    [DataRow("src/Import/TerrainTriangle.cs")]
    [DataRow("src/Import/RoadStrip.cs")]
    [DataRow("src/Import/RoadMarker.cs")]
    [DataRow("src/Import/BorderPost.cs")]
    [DataRow("src/MapView/SettlerFigures.cs")]
    [DataRow("src/MapView/MapPicking.cs")]
    [DataRow("src/MapView/RoadMarkerDraws.cs")]
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

    // The settlers' step offsets of the History Edition come from the install (ADR 0014, Consequences; INV-0010): the class
    // reads entry 2 of the graphics archive, and only through the one archive-entry helper of TerrainGraphics, which the
    // terrain, road, object, and settler graphics read their entries through too. The exception holds for the History Edition
    // only: the basic set brings its own walk steps (ADR 0018), and its class reads no archive and names no install.
    [TestMethod]
    public void TheStepOffsetsReadOnlyTheArchiveEntryOfTheWalkSequences()
    {
        AssertMentions("src/Import.HistoryEdition/StepOffsets.cs", ["TerrainGraphics"]);
    }

    [TestMethod]
    public void TheBasicSetsWalkStepsReadNoArchiveEntryAndNameNoInstallOrHistoryEditionType()
    {
        var path = "src/Import.OpenSettlers/OpenSettlersWalkSteps.cs";

        AssertMentions(path, []);
        Assert.IsEmpty(HistoryEditionNames(Read(path)), $"{path} names a History Edition type");
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

    // The History Edition assembly is the one place that names its types, the format readers, and the figure handlers and adds,
    // and the Open Settlers assembly the one place that reads the content set (ADR 0016, ADR 0017, INV-0010, INV-0011). Each
    // source is built only in the composition root. The map view compiles inside the Godot project, which references both
    // source assemblies, so the compiler does not keep it from their public types: this scan does. The map view, the asset check and
    // the asset browser's state (src/Assets), and the viewer classes outside the composition root (the launch code and the main
    // scene, `AssetBrowserView`) read and draw through the ports.
    static readonly string[] PortsOnly =
    [
        "src/Viewer/MapViewControl.cs",
        "src/Viewer/PlayDriver.cs",
        "src/Viewer/GodotImageDecoder.cs",
        "src/Viewer/PreviewView.cs",
        "src/Viewer/PaletteView.cs",
        "src/Viewer/RawImageView.cs",
        "src/Viewer/SoundView.cs",
        "src/Viewer/SpriteView.cs",
        "src/Viewer/TextView.cs",
    ];

    // The folders whose every class reads and draws through the ports.
    static readonly string[] PortsOnlyFolders = ["src/MapView", "src/Assets"];

    // What the shared code may not name: a History Edition type, its namespace, the format readers, and the figure terms.
    static List<string> HistoryEditionNames(string text)
    {
        var found = new List<string>();
        foreach (
            var forbidden in new[]
            {
                "HistoryEditionSource",
                "HistoryEditionLook",
                "HistoryEditionRuleTables",
                "HistoryEditionAssets",
                "JobTableNames",
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

    // What the shared code may not name of the content set: its source and looks, their namespace, and its folder. The look
    // names are the files of the looks folder, so a new look is caught without a change here.
    static List<string> ContentSetNames(string text)
    {
        var looks = Directory
            .GetFiles(Path.Combine(RootOf(), "src/Import.OpenSettlers/OpenSettlersLooks"), "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);
        var found = new List<string>();
        foreach (
            var forbidden in new[] { "OpenSettlersSource", "OpenSettlersLook", "Import.ContentSet", "content/basic" }
        )
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                found.Add(forbidden);
            }
        }

        foreach (var look in looks)
        {
            if (Regex.IsMatch(text, $@"\b{look}\b"))
            {
                found.Add(look!);
            }
        }

        return found;
    }

    // The map view, src/Assets, and the viewer classes outside the composition root; each folder holds classes to scan, so a
    // moved folder does not empty the check.
    static List<string> PortsOnlyFiles()
    {
        var root = RootOf();
        var folders = PortsOnlyFolders
            .Select(folder =>
                Directory
                    .EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
                    .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
                    .ToList()
            )
            .ToList();
        Assert.IsTrue(folders.All(f => f.Count > 0));
        var files = folders.SelectMany(f => f).Concat(PortsOnly.Select(path => Path.Combine(root, path))).ToList();
        Assert.IsGreaterThan(20, files.Count);

        return files;
    }

    [TestMethod]
    public void NoClassOfTheMapViewTheAssetsOrTheViewerOutsideTheCompositionRootNamesAHistoryEditionType()
    {
        foreach (var file in PortsOnlyFiles())
        {
            Assert.IsEmpty(HistoryEditionNames(File.ReadAllText(file)), $"{file} names a History Edition type");
        }
    }

    [TestMethod]
    public void NoClassOfTheMapViewTheAssetsOrTheViewerOutsideTheCompositionRootNamesAContentSetType()
    {
        foreach (var file in PortsOnlyFiles())
        {
            Assert.IsEmpty(ContentSetNames(File.ReadAllText(file)), $"{file} names a content-set type");
        }
    }

    // The positive control of the content-set check: it fails on a planted violation, the look named without its namespace too.
    [TestMethod]
    public void ThePortsOnlyCheckFailsOnAPlantedContentSetType()
    {
        CollectionAssert.AreEqual(
            new[] { "OpenSettlersSource", "content/basic" },
            ContentSetNames("OpenSettlersSource.Load(\"res://content/basic\", decoder)")
        );
        CollectionAssert.AreEqual(
            new[] { "Import.ContentSet" },
            ContentSetNames("using OpenSettlers.Import.ContentSet;")
        );
        CollectionAssert.AreEqual(new[] { "WoodcutterKey" }, ContentSetNames("new WoodcutterKey()"));
        Assert.IsEmpty(ContentSetNames("ContentSetName.Basic and InputEventKey"));
    }

    // The positive control of the History Edition check: it fails on a planted violation.
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
        Assert.IsEmpty(HistoryEditionNames("ContentSetName.HistoryEdition and the install's folder"));
    }

    // A global using leaves the namespace out of the file: the check still finds the public types by name.
    [TestMethod]
    public void ThePortsOnlyCheckFindsAHistoryEditionTypeNamedWithoutItsNamespace()
    {
        CollectionAssert.AreEqual(
            new[] { "HistoryEditionRuleTables", "HistoryEditionAssets", "JobTableNames" },
            HistoryEditionNames("new HistoryEditionRuleTables(i); new HistoryEditionAssets(i); JobTableNames.Jobs")
        );
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
                "BorderRules",
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

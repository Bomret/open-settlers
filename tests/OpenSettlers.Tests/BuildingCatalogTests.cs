using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The building types of the content catalog (ADR 0020): the catalog's refusals, order, and lookups, what the exporters write for a
// type with no original code, the order of commands, the layout's names, and the readout, with made-up entries.
[TestClass]
public sealed class BuildingCatalogTests
{
    // The tie rank is the code, or the hash of the name for a type with none, where the test gives no rank.
    static BuildingEntry Entry(string name, int? code = null, int? rank = null) =>
        new()
        {
            Name = name,
            OriginalCode = code,
            TieRank = rank ?? code ?? (int)Fnv1a.Of(name),
            Size = BuildingSize.Small,
            Needs = new BuildingNeeds { Planks = 1, Stones = 0 },
            StandingDoor = 8,
            ConstructingDoor = 8,
            EarlyBlowStep = 0x1000,
            LateBlowStep = 0x1000,
            SecondFigure = false,
            IsStorage = false,
        };

    [TestMethod]
    public void TheCatalogOrdersItsBuildingTypesByStableNameWhateverOrderTheSourceGives()
    {
        var forward = new ContentCatalog([], [Entry("woodcutter-hut"), Entry("baker"), Entry("castle")]);
        var backward = new ContentCatalog([], [Entry("castle"), Entry("baker"), Entry("woodcutter-hut")]);

        CollectionAssert.AreEqual(
            new[] { "baker", "castle", "woodcutter-hut" },
            forward.Buildings.Select(entry => entry.Name).ToArray()
        );
        CollectionAssert.AreEqual(
            forward.Buildings.Select(entry => entry.Name).ToArray(),
            backward.Buildings.Select(entry => entry.Name).ToArray()
        );
    }

    [TestMethod]
    public void TheCatalogRefusesADuplicateNameADuplicateCodeAndABadNameNamingTheEntry()
    {
        var name = Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentCatalog([], [Entry("farm"), Entry("farm")])
        );
        var code = Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentCatalog([], [Entry("farm", 5), Entry("mill", 5)])
        );
        var bad = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([], [Entry("Pig Farm")]));

        StringAssert.Contains(name.Message, "farm");
        StringAssert.Contains(code.Message, "mill");
        StringAssert.Contains(code.Message, "original code");
        StringAssert.Contains(bad.Message, "Pig Farm");
    }

    [TestMethod]
    public void TheCatalogRefusesTwoTypesWithOneTieRankNamingBoth()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentCatalog([], [Entry("smithy", rank: 3), Entry("mint", rank: 3)])
        );

        Assert.AreEqual("The building type 'smithy' is refused: mint has this tie rank, 3.", error.Message);
    }

    [TestMethod]
    public void NoBuildingIsNeverAnEntryAndATypeTheCatalogLacksIsAnErrorNamingIt()
    {
        var catalog = new ContentCatalog([], [Entry("farm")]);

        Assert.IsNull(catalog.EntryOf(BuildingType.None));
        Assert.IsNull(catalog.EntryOf(BuildingType.Mill));
        Assert.AreEqual("farm", catalog.Require(BuildingType.Farm).Name);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => catalog.Require(BuildingType.Mill));
        StringAssert.Contains(error.Message, "mill");
        StringAssert.Contains(error.Message, "farm");
    }

    [TestMethod]
    public void AnOriginalTypeWritesItsCodeAndATypeWithNoCodeTheWordOfItsNameWithTheLowByteClear()
    {
        var catalog = new ContentCatalog([], [Entry("farm", 0xc), Entry("smithy"), Entry("woodcutter-hut", 2)]);
        var smithy = new BuildingType { Name = "smithy" };

        Assert.AreEqual(0xc, BuildingWords.Of(catalog, BuildingType.Farm));
        Assert.AreEqual(BuildingWords.NoBuilding, BuildingWords.Of(catalog, BuildingType.None));
        var word = BuildingWords.Of(catalog, smithy);
        Assert.AreEqual(0, word & 0xff, "no constructing bit and no original code");
        Assert.IsGreaterThan(0, word, "it never reads as no building");
        Assert.AreEqual((int)(Fnv1a.Of("smithy") & 0x7fffff00), word);
        Assert.IsFalse(BuildingWords.HasCode(catalog, smithy));
        Assert.IsTrue(BuildingWords.HasCode(catalog, BuildingType.Farm));
        BuildingWords.Check(catalog);
    }

    [TestMethod]
    public void TheTypeIndexOfABuildingUnderConstructionAddsTheConstructingBitToTheWordOfNoCodeToo()
    {
        var catalog = new ContentCatalog([], [Entry("farm", 0xc), Entry("smithy")]);
        var smithy = new BuildingType { Name = "smithy" };

        Assert.AreEqual(0xc | 0x20, BuildingEncoder.TypeIndex(new Building(BuildingType.Farm, 1, 1, 0, true), catalog));
        Assert.AreEqual(
            BuildingWords.Of(catalog, smithy) | 0x20,
            BuildingEncoder.TypeIndex(new Building(smithy, 1, 1, 0, true), catalog)
        );
        Assert.AreEqual(-1, BuildingEncoder.TypeIndex(Building.Free(), catalog), "a free slot keeps -1");
    }

    [TestMethod]
    public void TwoCommandsOfOneTickThatDifferOnlyByBuildingComeInTheOrderOfTheirTieRanks()
    {
        // The woodcutter's hut ranks 2 and the baker 0x10, as their codes in the original: the woodcutter's hut comes first, though the
        // baker's stable name comes first.
        Command Place(BuildingType? type) =>
            new()
            {
                Tick = 1,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 4,
                Y = 4,
                Building = type,
            };
        var queue = new CommandQueue(TestCatalog.Default);
        queue.Add(Place(BuildingType.Woodcutter), 0);
        queue.Add(Place(BuildingType.Baker), 0);
        queue.Add(Place(null), 0);

        var taken = new System.Collections.Generic.List<BuildingType?>();
        while (queue.TryTakeDue(1, out var command))
        {
            taken.Add(command.Building);
        }

        CollectionAssert.AreEqual(new BuildingType?[] { null, BuildingType.Woodcutter, BuildingType.Baker }, taken);
    }

    // A game on the made-up tables with a layout that stands a finished woodcutter's hut. With codeless, no building type of the catalog
    // has an original code, as under the basic set; a type named in omit is left out of the catalog.
    static Game Started(bool codeless, string? omit, out string? problem)
    {
        var layout = Layout
            .Read(
                LayoutTests.Text(
                    extra: """
                    "castle": {"x": 10, "y": 10},
                    "flags": [{"x": 14, "y": 11}],
                    "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
                    "buildings": [{"type": "woodcutter-hut", "x": 13, "y": 10}]
                    """
                ),
                "test.jsonc"
            )
            .Layout!;
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var buildings = TestCatalog
            .Buildings(door: 8)
            .Where(entry => entry.Name != omit)
            .Select(entry => codeless ? entry with { OriginalCode = null } : entry);
        var game = GameFactory.Create(
            new RuleTables(classes, TestProgram.MadeStartStock(), catalog: TestCatalog.StandIn(buildings)),
            layout.BuildMap()
        );
        problem = layout.Apply(game);

        return game;
    }

    [TestMethod]
    public void ATypeWithNoCodeStandsExportsItsWordAndShowsItsNameWithNoRawValueBesideIt()
    {
        var game = Started(codeless: true, omit: null, out var problem);

        Assert.IsNull(problem);
        var hut = game.Buildings.Single(building => building.Type == BuildingType.Woodcutter);
        var catalog = game.Tables.Catalog;
        var word = BuildingWords.Of(catalog, hut.Type);
        Assert.AreEqual(
            word,
            game.History.Events.Of(game.History.BuildingId(game.Buildings.ToList().IndexOf(hut)))
                .Last()
                .State.Fields.Single(field => field.Field == "type")
                .Value
        );
        var line = Thing.Of(hut, catalog).Line();
        StringAssert.Contains(line, "woodcutter-hut");
        Assert.IsFalse(line.Contains("type="), "a type with no code shows its name alone");
        var farm = Thing.Of(new Building(BuildingType.Farm, 1, 1, 0), TestCatalog.Default).Line();
        StringAssert.Contains(farm, "type=0x0c");
    }

    [TestMethod]
    public void TheDigestOfATypeWithNoCodeIsTheSameOnEveryRunAndDiffersFromTheOneOfItsOriginalCode()
    {
        var first = Started(codeless: true, omit: null, out _).Digest();
        var again = Started(codeless: true, omit: null, out _).Digest();
        var coded = Started(codeless: false, omit: null, out _).Digest();

        Assert.AreEqual(first, again);
        Assert.AreNotEqual(first, coded);
    }

    [TestMethod]
    public void ARuleThatMeetsABuildingTypeTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        var game = Started(codeless: false, omit: "sawmill", out _);
        var sawmill = new Building(BuildingType.Sawmill, 1, 1, 0);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => game.Tables.DoorSpeed(sawmill));

        StringAssert.Contains(error.Message, "sawmill");
    }

    [TestMethod]
    public void ALayoutNamingATypeTheCatalogLacksIsRefusedWithTheNamesItKnows()
    {
        var game = Started(codeless: false, omit: "woodcutter-hut", out var problem);

        StringAssert.StartsWith(
            problem,
            "test.jsonc: buildings[0]: the building type \"woodcutter-hut\" is no stable name"
        );
        StringAssert.Contains(problem, "use baker, butcher, farm, fisher-hut");
        Assert.IsFalse(problem!.Contains("woodcutter-hut,"));
        Assert.IsFalse(game.Buildings.Any(building => building.Type == BuildingType.Woodcutter));
    }

    [TestMethod]
    public void AScriptSelectorNamesABuildingTypeByItsStableName()
    {
        foreach (var name in new[] { "castle", "woodcutter-hut", "fisher-hut", "sawmill", "storehouse", "pig-farm" })
        {
            var (selector, problem) = ThingSelector.Parse(name);

            Assert.IsNotNull(selector, $"{name}: {problem}");
        }
    }
}

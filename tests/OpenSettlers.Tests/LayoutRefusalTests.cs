using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The refusals of the layout readers (sweep #244 F70-F75). Each fixture holds exactly one fault, because the readers check
// in a fixed order and report one reason. The expected reasons are the wording the readers' documentation states
// (Layout, Layout.Placement): "whole number" means a JSON number System.Text.Json reads as an int.
[TestClass]
public sealed class LayoutRefusalTests
{
    const string Shape = "{\"x\", \"y\"}";
    const string Building =
        "{\"type\": \"woodcutter\", \"stonecutter\", \"forester\", or \"sawmill\", \"x\", \"y\"} (the node of the building, "
        + "finished with its flag south-east of it)";
    const string Settler =
        "{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\", \"y\", \"direction\"} (a flag of the road and "
        + "the direction the road leaves it)";
    const string Good = "{\"x\", \"y\", \"place\": 0 to 7, \"good\": 1 to 25}";
    const string Directions = "directions must be a list of one or more whole numbers from 0 to 5";

    static string[] Problems(string text) => Layout.Read(text, "t.jsonc").Problems.ToArray();

    static string[] Extra(string extra) => Problems(LayoutTests.Text(extra: "\n" + extra));

    static string Road(string directions) => $$""" "roads": [{"x": 11, "y": 11, "directions": {{directions}}}] """;

    [TestMethod]
    [DataRow("[]")]
    [DataRow("[6]")]
    [DataRow("[-1]")]
    [DataRow("[0, 9]")]
    [DataRow("[\"0\"]")]
    [DataRow("[0.5]")]
    [DataRow("3")]
    [DataRow("\"0\"")]
    public void ARoadWithBadDirectionsIsOneProblem(string directions)
    {
        CollectionAssert.AreEqual(new[] { $"t.jsonc: roads[0]: {Directions}" }, Extra(Road(directions)));
    }

    [TestMethod]
    public void ARoadWithNoDirectionsKeyIsOneProblem()
    {
        CollectionAssert.AreEqual(
            new[] { $"t.jsonc: roads[0]: {Directions}" },
            Extra(""" "roads": [{"x": 11, "y": 11}] """)
        );
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("5")]
    public void ARoadDirectionOfZeroOrFiveIsRead(string direction)
    {
        Assert.IsEmpty(Extra(Road($"[{direction}]")));
    }

    [TestMethod]
    [DataRow("\"flags\": 3", "flags: must be a list of " + Shape)]
    [DataRow("\"flags\": {}", "flags: must be a list of " + Shape)]
    [DataRow("\"roads\": 3", "roads: must be a list of {\"x\", \"y\", \"directions\"}")]
    [DataRow("\"flags\": [3]", "flags[0]: must be " + Shape)]
    [DataRow("\"flags\": [[]]", "flags[0]: must be " + Shape)]
    [DataRow("\"roads\": [\"x\"]", "roads[0]: must be {\"x\", \"y\", \"directions\"}")]
    [DataRow("\"flags\": [{\"x\": 1, \"y\": 1, \"z\": 2}]", "flags[0]: unknown key z, an entry is " + Shape)]
    [DataRow(
        "\"flags\": [{\"x\": 1, \"y\": 1, \"directions\": [0]}]",
        "flags[0]: unknown key directions, an entry is " + Shape
    )]
    [DataRow(
        "\"roads\": [{\"x\": 1, \"y\": 1, \"directions\": [0], \"z\": 2}]",
        "roads[0]: unknown key z, an entry is {\"x\", \"y\", \"directions\"}"
    )]
    [DataRow("\"castle\": 3", "castle: must be " + Shape)]
    [DataRow("\"castle\": [10, 10]", "castle: must be " + Shape)]
    [DataRow("\"castle\": {\"x\": 10, \"y\": 10, \"z\": 1}", "castle: unknown key z, an entry is " + Shape)]
    public void AnEntryOrListOfTheWrongKindIsOneProblem(string extra, string problem)
    {
        CollectionAssert.AreEqual(new[] { "t.jsonc: " + problem }, Extra(extra));
    }

    [TestMethod]
    [DataRow("\"flags\": [{\"x\": 1.5, \"y\": 1}]", "flags[0]")]
    [DataRow("\"flags\": [{\"x\": 1, \"y\": \"1\"}]", "flags[0]")]
    [DataRow("\"flags\": [{\"x\": 1}]", "flags[0]")]
    [DataRow("\"castle\": {\"x\": 1, \"y\": null}", "castle")]
    [DataRow("\"roads\": [{\"x\": 1.5, \"y\": 1, \"directions\": [0]}]", "roads[0]")]
    public void AnEntryWithANonWholeXOrYIsOneProblem(string extra, string entry)
    {
        CollectionAssert.AreEqual(new[] { $"t.jsonc: {entry}: x and y must each be a whole number" }, Extra(extra));
    }

    [TestMethod]
    [DataRow("\"buildings\": 3", "buildings: must be a list")]
    [DataRow("\"buildings\": [3]", "buildings[0]: must be " + Building)]
    [DataRow(
        "\"buildings\": [{\"type\": \"sawmill\", \"x\": 1, \"y\": 1, \"z\": 2}]",
        "buildings[0]: unknown key z, a building is " + Building
    )]
    [DataRow(
        "\"buildings\": [{\"type\": \"sawmill\", \"x\": 1.5, \"y\": 1}]",
        "buildings[0]: x and y must each be a whole number"
    )]
    [DataRow(
        "\"buildings\": [{\"type\": \"sawmill\", \"x\": 1, \"y\": \"1\"}]",
        "buildings[0]: x and y must each be a whole number"
    )]
    [DataRow("\"buildings\": [{\"type\": \"sawmill\", \"x\": 1}]", "buildings[0]: x and y must each be a whole number")]
    public void ABuildingEntryOfTheWrongKindIsOneProblem(string extra, string problem)
    {
        CollectionAssert.AreEqual(new[] { "t.jsonc: " + problem }, Extra(extra));
    }

    [TestMethod]
    [DataRow("\"settlers\": 3", "settlers: must be a list")]
    [DataRow("\"settlers\": [3]", "settlers[0]: must be " + Settler)]
    [DataRow(
        "\"settlers\": [{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\": 1, \"y\": 1, \"direction\": 0, \"z\": 2}]",
        "settlers[0]: unknown key z, a settler is " + Settler
    )]
    [DataRow(
        "\"settlers\": [{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\": 1.5, \"y\": 1, \"direction\": 0}]",
        "settlers[0]: x, y, and a direction from 0 to 5 must be whole numbers"
    )]
    [DataRow(
        "\"settlers\": [{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\": 1, \"y\": 1, \"direction\": 0.5}]",
        "settlers[0]: x, y, and a direction from 0 to 5 must be whole numbers"
    )]
    [DataRow(
        "\"settlers\": [{\"job\": \"carrier\", \"state\": \"waiting-on-road\", \"x\": 1, \"y\": 1, \"direction\": -1}]",
        "settlers[0]: x, y, and a direction from 0 to 5 must be whole numbers"
    )]
    [DataRow("\"goods\": 3", "goods: must be a list")]
    [DataRow("\"goods\": [3]", "goods[0]: must be " + Good)]
    [DataRow(
        "\"goods\": [{\"x\": 1, \"y\": 1, \"place\": 0, \"good\": 7, \"z\": 2}]",
        "goods[0]: unknown key z, a good is " + Good
    )]
    [DataRow(
        "\"goods\": [{\"x\": 1.5, \"y\": 1, \"place\": 0, \"good\": 7}]",
        "goods[0]: must be " + Good + ", whole numbers"
    )]
    [DataRow(
        "\"goods\": [{\"x\": 1, \"y\": 1, \"place\": 0.5, \"good\": 7}]",
        "goods[0]: must be " + Good + ", whole numbers"
    )]
    [DataRow(
        "\"goods\": [{\"x\": 1, \"y\": 1, \"place\": 0, \"good\": 7.5}]",
        "goods[0]: must be " + Good + ", whole numbers"
    )]
    [DataRow("\"goods\": [{\"x\": 1, \"y\": 1, \"place\": 0}]", "goods[0]: must be " + Good + ", whole numbers")]
    public void ASettlerOrGoodEntryOfTheWrongKindIsOneProblem(string extra, string problem)
    {
        CollectionAssert.AreEqual(new[] { "t.jsonc: " + problem }, Extra(extra));
    }

    [TestMethod]
    [DataRow("3", "objects: must be a list of {\"x\", \"y\", \"code\"}")]
    [DataRow("{}", "objects: must be a list of {\"x\", \"y\", \"code\"}")]
    [DataRow("\"[]\"", "objects: must be a list of {\"x\", \"y\", \"code\"}")]
    [DataRow("[{\"x\": 1.5, \"y\": 1, \"code\": 8}]", "objects[0]: x, y, and code must each be a whole number")]
    [DataRow("[{\"x\": 1, \"y\": 1.5, \"code\": 8}]", "objects[0]: x, y, and code must each be a whole number")]
    [DataRow("[{\"x\": 1, \"y\": 1, \"code\": 8.5}]", "objects[0]: x, y, and code must each be a whole number")]
    [DataRow("[{\"x\": \"1\", \"y\": 1, \"code\": 8}]", "objects[0]: x, y, and code must each be a whole number")]
    public void AnObjectsEntryOfTheWrongKindIsOneProblem(string objects, string problem)
    {
        CollectionAssert.AreEqual(new[] { "t.jsonc: " + problem }, Problems(LayoutTests.Text(objects: objects)));
    }

    [TestMethod]
    public void ATerrainOrLakesValueThatIsNoListOfStringsIsOneProblemEach()
    {
        const string Reason = "must be a list of strings, one for each row";

        CollectionAssert.AreEqual(
            new[] { $"t.jsonc: terrain: {Reason}" },
            Problems(LayoutTests.Text(terrain: "\"g\""))
        );
        CollectionAssert.AreEqual(new[] { $"t.jsonc: terrain: {Reason}" }, Problems(LayoutTests.Text(terrain: "[3]")));
        CollectionAssert.AreEqual(
            new[] { $"t.jsonc: terrainLower: {Reason}" },
            Problems(LayoutTests.Text(lower: "{}"))
        );
        CollectionAssert.AreEqual(new[] { $"t.jsonc: lakes: {Reason}" }, Problems(LayoutTests.Text(lakes: "3")));
        CollectionAssert.AreEqual(new[] { $"t.jsonc: lakes: {Reason}" }, Problems(LayoutTests.Text(lakes: "[null]")));
    }

    // The whole-number spellings: the readers take a number as whole when System.Text.Json reads it as an int, so only the
    // integer spelling counts (1 and -0). No document defines 1.0 and 1e0 either way; the refusal of both is the readers'
    // current rule, pinned here (the sibling LaunchArgs refuses the command-line size 2.0 alike, LaunchArgsTests). The
    // coordinate of an object is the probe.
    [TestMethod]
    [DataRow("1")]
    [DataRow("-0")]
    public void AWholeNumberSpellingIsRead(string x)
    {
        Assert.IsEmpty(Problems(LayoutTests.Text(objects: $$"""[{"x": {{x}}, "y": 1, "code": 8}]""")));
    }

    [TestMethod]
    [DataRow("1.0")]
    [DataRow("1e0")]
    [DataRow("1.5")]
    [DataRow("1e-1")]
    [DataRow("2147483648")]
    [DataRow("-2147483649")]
    [DataRow("1e10")]
    public void ANumberThatIsNoIntIsRefused(string x)
    {
        CollectionAssert.AreEqual(
            new[] { "t.jsonc: objects[0]: x, y, and code must each be a whole number" },
            Problems(LayoutTests.Text(objects: $$"""[{"x": {{x}}, "y": 1, "code": 8}]"""))
        );
    }
}

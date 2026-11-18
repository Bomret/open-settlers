using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of building-sites.txt are the emulator's output for the original's placement of a building (Building_Place, 0x449a63)
// of each type of M4 after the evaluator judged the node (docs/rules/build-sites.md): the building site's item, the map bytes of the
// node and its flag, and the flag item. The engine places the site by the command PlaceBuilding in the same game and must hold the
// same bytes after each click and after the pass that follows. The rules of the refusals, on made-up tables, are in BuildingSiteTests.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class BuildingSitePlacementTests
{
    [TestMethod]
    public void ABuildingSiteOfEachTypeHoldsTheBytesTheOriginalWrites() =>
        RemovalReplay.Replay(
            "building-sites.txt",
            "sites-four-types",
            (_, _) => [CommandKind.PlaceBuilding],
            line => false
        );

    // The six food types: the farm, fisher, and mill in one game, and the butcher, pig farm, and baker (large) each at the farm's node.
    // The emulator's click places whatever the evaluator judged and refuses nothing: the program's click on a panel's building is not
    // recovered (docs/rules/build-sites.md). The emulator's own words on a refusal are therefore the evaluator's site of a node, kept in
    // food-sites.txt ("food-evaluated"), which the next test sets against the size of each type.
    [TestMethod]
    [DataRow("food-farm-fisher-mill")]
    [DataRow("food-butcher")]
    [DataRow("food-pig-farm")]
    [DataRow("food-baker")]
    public void AFoodSiteHoldsTheBytesTheOriginalWrites(string scenario) =>
        RemovalReplay.Replay("food-sites.txt", scenario, (_, _) => [CommandKind.PlaceBuilding], line => false);

    // On the emulator's game at its pass 40 the evaluator gives eight nodes the state and site of "food-evaluated". The engine's
    // evaluator gives the same, and a food type is placed on a node of site 4 for a large type (the farm, butcher, pig farm, baker)
    // or of site 3 or 4 for a small one (the fisher, mill), and refused, with the game unchanged, on every node of a smaller site.
    [TestMethod]
    public void AFoodTypeIsRefusedOnANodeWhoseEmulatedSiteIsTooSmallForItsSize()
    {
        var scenario = EmulatorFixture.Load("food-sites.txt").Single(candidate => candidate.Name == "food-evaluated");
        var parts = scenario.Command.Split(':');
        var nodes = scenario
            .Lines[0]["classes"]
            .Split(';')
            .Select(entry => entry.Split(':'))
            .Select(entry =>
                (
                    At: entry[0].Split(',').Select(int.Parse).ToArray(),
                    Judged: entry[1].Split('/').Select(int.Parse).ToArray()
                )
            )
            .ToArray();
        var tables = TestProgram.Tables();
        BuildingType[] types =
        [
            BuildingType.Fisher,
            BuildingType.Farm,
            BuildingType.Butcher,
            BuildingType.PigFarm,
            BuildingType.Mill,
            BuildingType.Baker,
        ];

        foreach (var type in types)
        {
            var game = RemovalGame.Started(parts, RemovalGame.MapOf(parts));
            for (var pass = 0; pass < int.Parse(parts[8], CultureInfo.InvariantCulture); pass++)
            {
                game.Tick();
            }

            var needed = tables.SizeOf(type) == BuildingSize.Large ? 4 : 3;
            // The refused nodes first: a placement changes what the evaluator judges around it. Among the nodes that fit, the smallest site
            // first, so a small type is placed on a node of site 3.
            foreach (var node in nodes.OrderBy(node => node.Judged[1] >= needed).ThenBy(node => node.Judged[1]))
            {
                var judged = BuildSite.Evaluate(game.Tables, game.Map, node.At[0], node.At[1], 0, hasCastle: true);
                var where = $"{type.Name} on {node.At[0]},{node.At[1]}";
                Assert.AreEqual(
                    (node.Judged[0], node.Judged[1]),
                    (judged.Node, judged.Site),
                    $"{where}: the evaluator's judgment"
                );
                var digest = game.Digest();
                game.Enqueue(
                    new Command
                    {
                        Tick = game.TickCount,
                        Player = 0,
                        Kind = CommandKind.PlaceBuilding,
                        X = node.At[0],
                        Y = node.At[1],
                        Building = type,
                    }
                );
                game.ApplyCommands();
                if (node.Judged[1] < needed)
                {
                    Assert.AreEqual(digest, game.Digest(), $"{where}: a refused placement changes nothing");
                }
                else
                {
                    Assert.AreNotEqual(digest, game.Digest(), $"{where}: the site fits the type");
                    Assert.AreEqual(
                        needed,
                        node.Judged[1],
                        $"{where}: the type is placed on the smallest site that fits"
                    );
                    break;
                }
            }
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        RemovalTests.Replay(
            "building-sites.txt",
            "sites-four-types",
            (_, _) => [CommandKind.PlaceBuilding],
            line => false
        );
}

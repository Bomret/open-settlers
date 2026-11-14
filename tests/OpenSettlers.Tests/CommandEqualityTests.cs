using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Two commands are equal exactly when their fields are, the road directions element by element (Command).
[TestClass]
public sealed class CommandEqualityTests
{
    static Command Road(int[]? directions) =>
        new()
        {
            Tick = 3,
            Player = 0,
            Kind = CommandKind.BuildRoad,
            X = 5,
            Y = 6,
            Directions = directions,
        };

    [TestMethod]
    public void CommandsWithTheSameFieldsAreEqualWithEqualHashes()
    {
        var one = new Command
        {
            Tick = 3,
            Player = 0,
            Kind = CommandKind.PlaceBuilding,
            X = 5,
            Y = 6,
            Directions = null,
            Building = BuildingType.Woodcutter,
        };
        var other = new Command
        {
            Tick = 3,
            Player = 0,
            Kind = CommandKind.PlaceBuilding,
            X = 5,
            Y = 6,
            Directions = null,
            Building = BuildingType.Woodcutter,
        };

        Assert.AreEqual(one, other);
        Assert.AreEqual(one.GetHashCode(), other.GetHashCode());
    }

    [TestMethod]
    public void NullAndEmptyRoadDirectionsAreEqualWithEqualHashes()
    {
        Assert.AreEqual(Road(null), Road([]));
        Assert.AreEqual(Road(null).GetHashCode(), Road([]).GetHashCode());
    }

    [TestMethod]
    public void DirectionsListedInAnotherOrderAreUnequal()
    {
        Assert.AreNotEqual(Road([0, 1]), Road([1, 0]));
    }

    [TestMethod]
    public void EqualDirectionsInSeparateListsAreEqualWithEqualHashes()
    {
        Assert.AreEqual(Road([0, 1]), Road([0, 1]));
        Assert.AreEqual(Road([0, 1]).GetHashCode(), Road([0, 1]).GetHashCode());
    }

    [TestMethod]
    public void ADifferentBuildingMakesCommandsUnequal()
    {
        var woodcutter = new Command
        {
            Tick = 3,
            Player = 0,
            Kind = CommandKind.PlaceBuilding,
            X = 5,
            Y = 6,
            Directions = null,
            Building = BuildingType.Woodcutter,
        };
        var forester = new Command
        {
            Tick = 3,
            Player = 0,
            Kind = CommandKind.PlaceBuilding,
            X = 5,
            Y = 6,
            Directions = null,
            Building = BuildingType.Forester,
        };

        Assert.AreNotEqual(woodcutter, forester);
        Assert.AreNotEqual(woodcutter, woodcutter with { Building = null });
    }

    [TestMethod]
    public void ADifferentTickPlayerKindOrTileMakesCommandsUnequal()
    {
        var command = Road([0]);

        Assert.AreNotEqual(command, command with { Tick = 4 });
        Assert.AreNotEqual(command, command with { Player = 1 });
        Assert.AreNotEqual(command, command with { Kind = CommandKind.PlaceFlag });
        Assert.AreNotEqual(command, command with { X = 6 });
        Assert.AreNotEqual(command, command with { Y = 7 });
    }
}

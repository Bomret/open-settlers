using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game is a coordinator: it applies commands, runs the tick, and builds the digest (ADR 0013). Everything else a caller
// reaches is a read-only part; the behaviours that change the game are internal to the assemblies that build and drive it.
[TestClass]
public sealed class GameSurfaceTests
{
    [TestMethod]
    public void TheGamePublishesOnlyTheCommandTheTickTheDigestTheSetupOperationsAndTheRemovalQueryAsMethods()
    {
        var methods = typeof(Game)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order();

        string[] expected =
        [
            "ApplyCommands",
            "Digest",
            "Enqueue",
            "PlaceCarrierOnRoad",
            "PlaceFinishedBuilding",
            "PlaceGood",
            "RemovalCommandAt",
            "SetLayoutMode",
            "StandWorkerInFinishedHut",
            "Tick",
        ];

        CollectionAssert.AreEqual(expected, methods.ToArray());
    }

    [TestMethod]
    public void EveryPublicPropertyOfTheGameIsReadOnly()
    {
        var writable = typeof(Game)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name);

        CollectionAssert.AreEqual(System.Array.Empty<string>(), writable.ToArray());
    }

    [TestMethod]
    public void TheGamePublishesOnlyTheCountersAndTheStateItsCallersRead()
    {
        var properties = typeof(Game)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order();
        string[] expected =
        [
            "AppliedCommands",
            "Buildings",
            "CarrierFlagCursor",
            "CastleLeavingCount",
            "CastleStock",
            "CastleWaitingList",
            "Clock",
            "Flags",
            "History",
            "HutPassState",
            "Map",
            "Network",
            "OccupiedMovementSlots",
            "PassPhase",
            "PassScanCursor",
            "RandomState",
            "Revision",
            "RoadPlanner",
            "Settlers",
            "SupplyState",
            "Tables",
            "TickCount",
        ];

        CollectionAssert.AreEqual(expected.Order().ToArray(), properties.ToArray());
    }

    [TestMethod]
    public void NoPublicMemberOfTheGameReturnsOneOfItsBehaviorObjects()
    {
        System.Type[] behaviors =
        [
            typeof(CarrierBehavior),
            typeof(MainPass),
            typeof(CastleRoster),
            typeof(HutWork),
            typeof(SettlerMovement),
            typeof(SettlerSupply),
            typeof(GoodsTransport),
            typeof(Removal),
            typeof(WalkBehavior),
            typeof(WalkHomeBehavior),
            typeof(ActivityRegistry),
            typeof(Causes),
        ];

        var leaking = typeof(Game)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member =>
                member switch
                {
                    PropertyInfo property => (member.Name, Type: property.PropertyType),
                    MethodInfo method => (member.Name, Type: method.ReturnType),
                    _ => (member.Name, Type: typeof(void)),
                }
            )
            .Where(member => behaviors.Contains(member.Type))
            .Select(member => member.Name);

        CollectionAssert.AreEqual(System.Array.Empty<string>(), leaking.ToArray());
    }

    [TestMethod]
    public void TheGameLivesInTheApplicationAssembly()
    {
        Assert.AreEqual("OpenSettlers.Application", typeof(Game).Assembly.GetName().Name);
    }
}

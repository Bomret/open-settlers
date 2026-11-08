using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Each command kind has a handler of its own. The map of the map seed 6359,A0D2,B4FA at size 1 has castle sites at 17,0 and
// 18,0; the tables are made up (no install).
[TestClass]
public sealed class CommandHandlerTests
{
    static Game NewGame()
    {
        var tables = TestProgram.Made();

        return GameFactory.Create(
            tables,
            MapGenerator.Generate(tables, new MapSeed(0x6359, 0xA0D2, 0xB4FA), MapSize.Offered[0])
        );
    }

    static Command Of(int kind, int x, int y, long tick = 0) =>
        new(tick, 0, kind, x, y, kind == CommandKind.BuildRoad ? new[] { 0 } : null);

    static (int X, int Y) NodeWhere(Game game, System.Func<BuildSiteResult, bool> wanted, bool hasCastle)
    {
        for (var y = 0; y < game.Map.Size.Height; y++)
        {
            for (var x = 0; x < game.Map.Size.Width; x++)
            {
                if (wanted(BuildSite.Evaluate(TestProgram.Made(), game.Map, x, y, 0, hasCastle)))
                {
                    return (x, y);
                }
            }
        }

        throw new System.InvalidOperationException("no such node");
    }

    [TestMethod]
    public void TheCommandKindsAreSevenAndDistinct()
    {
        Assert.AreEqual(7, CommandKind.All.Distinct().Count());
    }

    [TestMethod]
    public void EveryCommandKindHasItsOwnHandlerAndNoHandlerHasNoKind()
    {
        var game = NewGame();

        foreach (var kind in CommandKind.All)
        {
            Assert.IsTrue(game.Handlers.TryGetValue(kind, out var handler), $"kind {kind} has no handler");
            Assert.AreEqual(kind, handler!.Kind);
        }

        Assert.AreEqual(CommandKind.All.Count, game.Handlers.Count);
    }

    [TestMethod]
    public void EveryKindButTheCastleIsRefusedBeforeTheCastleStandsAndChangesNothing()
    {
        var game = NewGame();
        var before = game.Digest();

        foreach (var kind in CommandKind.All.Where(kind => kind != CommandKind.PlaceCastle))
        {
            Assert.IsFalse(game.Handlers[kind].Handle(Of(kind, 17, 0)), $"kind {kind} was applied");
        }

        Assert.AreEqual(before, game.Digest());
    }

    [TestMethod]
    public void ThePlaceCastleHandlerRefusesANodeThatIsNoCastleSite()
    {
        var game = NewGame();
        var (x, y) = NodeWhere(game, site => !site.IsCastleSite, hasCastle: false);
        var before = game.Digest();

        Assert.IsFalse(game.Handlers[CommandKind.PlaceCastle].Handle(Of(CommandKind.PlaceCastle, x, y)));
        Assert.IsNull(game.Castle);
        Assert.AreEqual(before, game.Digest());
    }

    [TestMethod]
    public void ThePlaceCastleHandlerPlacesTheCastleOnceAndRefusesASecond()
    {
        var game = NewGame();
        var handler = game.Handlers[CommandKind.PlaceCastle];

        Assert.IsTrue(handler.Handle(Of(CommandKind.PlaceCastle, 17, 0)));
        Assert.AreEqual(17, game.Castle!.Value.X);
        Assert.AreEqual(1, game.Flags.Count);
        Assert.IsFalse(handler.Handle(Of(CommandKind.PlaceCastle, 18, 0)));
        Assert.AreEqual(17, game.Castle!.Value.X);
    }

    [TestMethod]
    public void ThePlaceFlagHandlerPlacesAFlagWhereTheOriginalAllowsAndRefusesElsewhere()
    {
        var game = NewGame();
        game.Handlers[CommandKind.PlaceCastle].Handle(Of(CommandKind.PlaceCastle, 17, 0));
        var handler = game.Handlers[CommandKind.PlaceFlag];
        var (x, y) = NodeWhere(game, site => site.AllowsFlag, hasCastle: true);
        var (blockedX, blockedY) = NodeWhere(game, site => !site.AllowsFlag, hasCastle: true);

        Assert.IsFalse(handler.Handle(Of(CommandKind.PlaceFlag, blockedX, blockedY)));
        Assert.AreEqual(1, game.Flags.Count);
        Assert.IsTrue(handler.Handle(Of(CommandKind.PlaceFlag, x, y)));
        Assert.AreEqual(2, game.Flags.Count);
    }

    [TestMethod]
    public void ACommandForAnotherPlayerOrOutsideTheMapLeavesTheGameUnchanged()
    {
        var game = NewGame();
        game.Enqueue(Of(CommandKind.PlaceCastle, 17, 0));
        game.Tick();
        var before = game.Digest();
        var revision = game.Revision;
        var (x, y) = NodeWhere(game, site => site.AllowsFlag, hasCastle: true);

        game.Enqueue(new Command(game.TickCount, 1, CommandKind.PlaceFlag, x, y));
        game.Enqueue(Of(CommandKind.PlaceFlag, -1, 0, game.TickCount));
        game.Enqueue(Of(CommandKind.PlaceFlag, 0, game.Map.Size.Height, game.TickCount));

        game.ApplyCommands();

        Assert.AreEqual(revision, game.Revision);
        Assert.AreEqual(1, game.Flags.Count);
        Assert.AreEqual(before, game.Digest());
    }

    [TestMethod]
    public void ACommandOfAnUnknownKindIsRefused()
    {
        var game = NewGame();
        game.Enqueue(Of(99, 17, 0));
        game.ApplyCommands();

        Assert.AreEqual(0, game.Revision);
    }
}

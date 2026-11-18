using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The build-site overlay's markers of a game for the view's player (docs/rules/build-sites.md, "The build-site overlay"),
// on layouts/woodcutter.jsonc: a flat meadow with the castle at 10,10.
[TestClass]
public sealed class BuildMarkerDrawsTests
{
    static Layout Woodcutter() =>
        Layout
            .Read(File.ReadAllText(Path.Combine(LayoutFiles.Folder(), "woodcutter.jsonc")), "woodcutter.jsonc")
            .Layout!;

    // The game on the layout's map, the layout's entries applied (the castle stands) or not.
    static Game Game(bool applied)
    {
        var layout = Woodcutter();
        var game = LayoutGames.NewGame(layout);
        if (applied)
        {
            Assert.IsNull(layout.Apply(game));
        }

        return game;
    }

    // The judgment the evaluator gives the node for player 0, with the castle state of the game.
    static BuildMarker? Judged(Game game, int x, int y) =>
        BuildMarkers.Of(
            BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, game.CastleStock.Castle is not null, game.Buildings)
        );

    static void AssertEveryNodeShowsItsJudgment(Game game)
    {
        var draws = BuildMarkerDraws.Of(game, 0).ToDictionary(draw => (draw.Column, draw.Row));
        for (var y = 0; y < game.Map.Size.Height; y++)
        {
            for (var x = 0; x < game.Map.Size.Width; x++)
            {
                Assert.AreEqual(
                    Judged(game, x, y),
                    draws.TryGetValue((x, y), out var draw) ? draw.Marker : null,
                    $"node {x},{y}"
                );
            }
        }
    }

    [TestMethod]
    public void BeforeTheCastleStandsEveryNodeShowsTheJudgmentForAPlayerWithoutACastle()
    {
        var game = Game(applied: false);

        AssertEveryNodeShowsItsJudgment(game);
        Assert.IsTrue(
            BuildMarkerDraws.Of(game, 0).Any(draw => draw.Marker == BuildMarker.Large),
            "castle sites show the large-building marker"
        );
    }

    [TestMethod]
    public void AfterTheCastleStandsMarkersCoverTheCastlesTerritoryOnly()
    {
        var game = Game(applied: true);

        var draws = BuildMarkerDraws.Of(game, 0);

        AssertEveryNodeShowsItsJudgment(game);
        Assert.IsTrue(draws.Count > 0);
        Assert.IsTrue(draws.All(draw => game.Map.At(draw.Column, draw.Row).Owner == Owner.Player(0)));
    }

    [TestMethod]
    public void ANodeThatHoldsAFlagOrTheCastleShowsNoMarker()
    {
        var game = Game(applied: true);

        var draws = BuildMarkerDraws.Of(game, 0);

        Assert.IsFalse(draws.Any(draw => (draw.Column, draw.Row) is (10, 10) or (11, 11) or (14, 11)));
    }

    [TestMethod]
    public void AMarkerStandsAtItsNodeRaisedByTheNodesHeight()
    {
        var game = Game(applied: true);
        var draw = BuildMarkerDraws.Of(game, 0)[0];

        var (x, y) = Camera.NodePosition(draw.Column, draw.Row);

        Assert.AreEqual(x, draw.X);
        Assert.AreEqual(y - game.Map.At(draw.Column, draw.Row).Height * TerrainTriangles.HeightStep, draw.Y);
    }

    [TestMethod]
    public void TheMarkersFollowAFlagPlacedAndRemoved()
    {
        var game = Game(applied: true);
        var before = BuildMarkerDraws.Of(game, 0);
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceFlag,
                X = 8,
                Y = 13,
            }
        );
        game.Tick();

        Assert.AreNotEqual(before.Count, BuildMarkerDraws.Of(game, 0).Count);
        AssertEveryNodeShowsItsJudgment(game);

        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.RemoveFlag,
                X = 8,
                Y = 13,
            }
        );
        game.Tick();

        Assert.AreEqual(before.Count, BuildMarkerDraws.Of(game, 0).Count);
        AssertEveryNodeShowsItsJudgment(game);
    }

    [TestMethod]
    public void ReadingTheMarkersChangesNothingInTheGame()
    {
        var plain = Game(applied: true);
        var watched = Game(applied: true);
        for (var tick = 0; tick < 600; tick++)
        {
            plain.Tick();
            _ = BuildMarkerDraws.Of(watched, 0);
            watched.Tick();
        }

        Assert.AreEqual(plain.Digest(), watched.Digest());
    }
}

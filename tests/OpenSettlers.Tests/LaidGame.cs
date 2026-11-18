using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// A game with a layout's entries applied. It stays with the tests because it names both the layout of the adapters and
/// <see cref="LayoutText"/> of the shared helpers, and no helper project references both.
/// </summary>
static class LaidGame
{
    /// <summary>A layout's entries: a castle, a flag, a road, a hut, a carrier, and a good on the flag.</summary>
    public const string Built = """
        "castle": {"x": 10, "y": 10},
        "flags": [{"x": 14, "y": 11}],
        "roads": [{"x": 11, "y": 11, "directions": [0, 0, 0]}],
        "buildings": [{"type": "woodcutter", "x": 13, "y": 10}],
        "settlers": [{"job": "carrier", "state": "waiting-on-road", "x": 11, "y": 11, "direction": 0}],
        "goods": [{"x": 14, "y": 11, "place": 0, "good": 7}]
        """;

    /// <summary>A game with the layout entries applied (<see cref="Built"/> unless the test gives others).</summary>
    public static Game Laid(string extra = Built)
    {
        var read = Layout.Read(LayoutText.Text(extra: "\n" + extra), "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
        var game = LayoutGames.NewGame(read.Layout!);
        Assert.IsNull(read.Layout!.Apply(game));

        return game;
    }
}

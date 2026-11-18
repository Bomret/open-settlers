using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// Each command kind has a handler of its own, read from the game's handler table: the bench's game, whose handlers are those
// GameFactory gives a game, on made-up tables. The handlers' answers are in the class of the same name in src/Adapters.Tests.
[TestClass]
public sealed class CommandHandlerTests
{
    [TestMethod]
    public void EveryCommandKindHasItsOwnHandlerAndNoHandlerHasNoKind()
    {
        var game = GameBench.Create(MadeProgram.Made(), GameBench.Flat());

        foreach (var kind in CommandKind.All)
        {
            Assert.IsTrue(game.Handlers.TryGetValue(kind, out var handler), $"kind {kind} has no handler");
            Assert.AreEqual(kind, handler!.Kind);
        }

        Assert.AreEqual(CommandKind.All.Count, game.Handlers.Count);
    }
}

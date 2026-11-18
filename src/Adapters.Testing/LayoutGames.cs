using OpenSettlers.Domain.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>A game over a layout's map.</summary>
public static class LayoutGames
{
    /// <summary>A game on the layout's map with the made-up tables of <see cref="MadeProgram.MadeTables"/>: every step takes 100 clock units and the flag's object class is 3.</summary>
    public static Game NewGame(Layout layout) => GameFactory.Create(MadeProgram.MadeTables(), layout.BuildMap());
}

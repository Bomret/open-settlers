namespace OpenSettlers.Simulation;

/// <summary>
/// The port to the game's random generator: each draw is the first word of the generator after one more step, which
/// <c>Rng_Step</c> (<c>0x466ade</c>) and the draw at <c>0x44257a</c> leave. The order of the draws is part of the game
/// (the golden test), so a behaviour draws only where the original does. The adapter holds the generator's state.
/// </summary>
public interface IRandomDraws
{
    /// <summary>Steps the generator and returns its first word.</summary>
    ushort Draw();
}

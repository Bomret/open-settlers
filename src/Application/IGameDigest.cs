namespace OpenSettlers.Simulation;

/// <summary>
/// The port that fingerprints a game: the encoding of the state is the adapter's (ADR 0013), the game only asks for the
/// number.
/// </summary>
public interface IGameDigest
{
    /// <summary>
    /// The fingerprint of the game state the ticks have produced; a command still queued is no part of it. Two games that
    /// ran the same commands from the same start give the same number.
    /// </summary>
    ulong Of(Game game);
}

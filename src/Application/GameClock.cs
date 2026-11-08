namespace OpenSettlers.Simulation;

/// <summary>
/// The game clock in clock units (16 bits, 8 for each tick): the settlers' timers and stamps count in it (ADR 0005). The main
/// pass advances it.
/// </summary>
public sealed class GameClock
{
    /// <summary>The clock units of one pass.</summary>
    public const int Step = 8;

    /// <summary>The clock now.</summary>
    public ushort Now { get; private set; }

    /// <summary>The clock moves on by one pass.</summary>
    public void Advance() => Now = (ushort)(Now + Step);
}

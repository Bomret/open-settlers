namespace OpenSettlers.MapView;

/// <summary>
/// The stand-in readout of the castle's log count in the play view. The original shows stock in its own window, a later
/// slice's; this text only lets a player see a delivery arrive.
/// </summary>
public static class CastleReadout
{
    /// <summary>The text for a castle holding <paramref name="logs"/> logs.</summary>
    public static string Text(int logs) => $"Logs {logs}";
}

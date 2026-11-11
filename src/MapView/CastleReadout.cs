namespace OpenSettlers.MapView;

/// <summary>
/// The stand-in readout of the castle's log, plank, and stone counts in the play view. The original shows stock in its own window,
/// a later slice's; this text only lets a player see a delivery arrive and a site's materials leave.
/// </summary>
public static class CastleReadout
{
    /// <summary>The text for a castle holding <paramref name="logs"/> logs, <paramref name="planks"/> planks, and <paramref name="stones"/> stones.</summary>
    public static string Text(int logs, int planks, int stones) => $"Logs {logs}  Planks {planks}  Stones {stones}";
}

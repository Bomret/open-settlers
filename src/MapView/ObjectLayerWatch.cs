using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Tells the view when the objects it draws changed: a tree cut, a stump gone, a log laid on a flag. The castle, the
/// flags, the roads, and the huts are counted by the view; this watches the object layer and the goods on the flags,
/// which change with no count changing.
/// </summary>
public sealed class ObjectLayerWatch
{
    private ulong? _last;

    /// <summary>True when the object layer or a flag's goods differ from the last call (the first call is a change).</summary>
    public bool Changed(Game game)
    {
        var hash = game.Map.Digest(MapLayer.Object);
        for (var slot = 0; slot < game.Flags.Count; slot++)
        {
            foreach (var good in game.FlagGoods(slot))
            {
                hash = (hash ^ (ulong)good) * 1099511628211UL;
            }
        }

        var changed = _last != hash;
        _last = hash;

        return changed;
    }
}

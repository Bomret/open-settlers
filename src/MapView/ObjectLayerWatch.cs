using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Tells the view when the objects it draws changed: a tree cut, a stump gone, a log laid on a flag. The castle, the
/// flags, the roads, and the huts are counted by the view; this watches the map objects and the goods on the flags,
/// which change with no count changing.
/// </summary>
public sealed class ObjectLayerWatch
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    private ulong? _last;
    private long _tick = -1;

    /// <summary>
    /// True when a map object or a flag's goods differ from the last look (the first call is a change). Only a
    /// tick changes them, so the objects are read once for each tick count and a call at the same count is no change.
    /// </summary>
    public bool Changed(Game game)
    {
        if (_last is not null && game.TickCount == _tick)
        {
            return false;
        }

        _tick = game.TickCount;
        var hash = FnvOffset;
        var map = game.Map;
        for (var index = 0; index < map.Count; index++)
        {
            hash = (hash ^ map.At(index).Object.Code) * FnvPrime;
        }

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

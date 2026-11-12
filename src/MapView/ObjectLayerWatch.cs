using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Tells the view when the objects it draws changed: a tree cut, a stump gone, a log laid on a flag. The castle, the
/// flags, the roads, and the huts are counted by the view; this watches the map objects, the heights, the goods on the
/// flags, and the progress of the building sites, which change with no count changing.
/// </summary>
public sealed class ObjectLayerWatch
{
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
        var hash = Fnv1a.Offset;
        var map = game.Map;
        for (var index = 0; index < map.Count; index++)
        {
            hash = Fnv1a.Add(hash, map.At(index).Object);
            // The digger changes heights with no object changing; the ground and what stands on it are drawn by them.
            hash = Fnv1a.Add(hash, (ulong)map.At(index).Height);
        }

        foreach (var flag in game.Flags)
        {
            foreach (var place in flag.Places)
            {
                hash = Fnv1a.Add(hash, place.Good);
            }
        }

        // A site is drawn by its progress word (SiteStages, ObjectDraws), which the builder's blows move with no object changing.
        // The castle's word moves too but does not change its drawing.
        foreach (var building in game.Buildings)
        {
            if (building.Constructing && building.Type != BuildingType.Castle)
            {
                hash = Fnv1a.Add(hash, building.Progress);
            }
        }

        var changed = _last != hash;
        _last = hash;

        return changed;
    }
}

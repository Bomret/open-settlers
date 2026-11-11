using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The building pass's turn of a building site (<c>0x42d328</c>, <c>0x42d55f</c>, <c>docs/rules/construction-materials.md</c>): a large
/// site that has not started waits until the ground around it is levelled; then the pass keeps the rank of the requests of the site's
/// flag for planks and stones from what the site holds on site and on the way against what it needs, so that it never asks for more
/// than it needs. A site with no worker inside or on the way, whose last request did not fail, asks for its builder (<c>0x42d5fc</c>) and
/// takes the progress word 1. **Not ported:** the digger of a large site, and the branch of the computer player (player byte
/// <c>+0x163</c>).
/// </summary>
/// <param name="map">The map: the heights of the ground around a large site.</param>
/// <param name="flags">The flag table: the site's flag holds its requests.</param>
/// <param name="tables">The rule tables: the size of a type, the rank the planks start from, and the builder's request.</param>
/// <param name="askForWorker">The request of a worker for a building (the castle's), or null where the site asks for none.</param>
public sealed class SitePass(Map map, FlagTable flags, RuleTables tables, Action<Building, WorkerRequest>? askForWorker = null)
{
    // The rank the stones start from: a constant of the routine (0x42d8ff), unlike the planks', which the player word gives.
    private const byte StoneRankStart = 0xff;

    /// <summary>Runs the turn of a building site.</summary>
    public void Turn(Building site)
    {
        if (site.Site is not { } needs)
        {
            return;
        }

        if (tables.SizeOf(site.Type) == BuildingSize.Large && site.Progress == 0)
        {
            // A digger inside or on the way (status bits 6 and 7) leaves the ground to him.
            if (site.WorkerInside || site.WorkerOnTheWay || !Levelled(site, needs.TargetHeight))
            {
                return;
            }
        }

        // 0x42d55f: a site whose bits 2, 6, and 7 are clear (no failed request, no worker inside or on the way) takes the progress word 1
        // and asks for its builder.
        if (!(site.RequestFailed || site.WorkerInside || site.WorkerOnTheWay))
        {
            site.Progress = 1;
            askForWorker?.Invoke(site, tables.BuilderRequest);
        }

        var flag = flags[site.Flag];
        Keep(flag, RequestWord.First, needs.Planks, site.FillLow, tables.PlankRankBase, site.WorkerInside);
        Keep(flag, RequestWord.Second, needs.Stones, site.FillHigh, StoneRankStart, site.WorkerInside);
    }

    // The ground is levelled where the node and its six neighbours all lie at the site's height (0x42d34e to 0x42d490).
    private bool Levelled(Building site, int height)
    {
        if (map.At(site.X, site.Y).Height != height)
        {
            return false;
        }

        for (var direction = 0; direction < Direction.Count; direction++)
        {
            var (dx, dy) = Direction.Steps[direction];
            if (map.At(site.X + dx, site.Y + dy).Height != height)
            {
                return false;
            }
        }

        return true;
    }

    // The rank of one request (0x42d740 to 0x42d7d6 for the planks): the fill byte holds the goods on site in its high nibble and
    // those on the way in its low nibble. A site that holds, with what is on the way, the goods it needs (or 8 or more) asks for no
    // more: its rank is 0. Otherwise the start shifted right by the goods held, again by 2 where no builder is inside, with bit 0
    // cleared.
    private static void Keep(Flag flag, RequestWord word, int needed, int fill, byte start, bool builderInside)
    {
        var held = (fill >> 4) + (fill & 0xf);
        var rank = 0;
        if (held < 8 && held != (needed & 0xff))
        {
            rank = start >> held;
            if (!builderInside)
            {
                rank >>= 2;
            }

            rank &= 0xfe;
        }

        flag.SetRequest(word, flag.RequestOf(word) with { Rank = (byte)rank });
    }
}

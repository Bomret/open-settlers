using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The building pass's turn of a building site (<c>0x42d328</c>, <c>0x42d55f</c>, <c>docs/rules/construction-materials.md</c>): a large
/// site that has not started waits until the ground around it is levelled, and asks for a digger (<c>0x42d52a</c>,
/// <c>docs/rules/digging.md</c>) where none is inside or on the way and its last request did not fail; then the pass keeps the rank of the
/// requests of the site's flag for planks and stones from what the site holds on site and on the way against what it needs, so that it never
/// asks for more than it needs. A site with no worker inside or on the way, whose last request did not fail, asks for its builder
/// (<c>0x42d5fc</c>) and takes the progress word 1. The workers it asks for are those the job rules modules name for each
/// <see cref="SiteStage"/> (<see cref="JobRules.SiteWorker"/>); the pass names no job. **Not ported:** the branch of the computer player
/// (player byte <c>+0x163</c>) and the branch of the digger's request for a player whose byte <c>+0x163</c> has bit 6 set.
/// </summary>
/// <param name="map">The map: the heights of the ground around a large site.</param>
/// <param name="flags">The flag table: the site's flag holds its requests.</param>
/// <param name="tables">The rule tables: the size of a type, the rank the planks start from, and the workers' requests.</param>
/// <param name="modules">The job rules modules, which name the worker of each stage of a site.</param>
/// <param name="askForWorker">The request of a worker for a building (the castle's), or null where the site asks for none.</param>
public sealed class SitePass(
    Map map,
    FlagTable flags,
    RuleTables tables,
    IReadOnlyList<JobRules> modules,
    Action<Building, WorkerRequest>? askForWorker = null
)
{
    // The rank the stones start from: a constant of the routine (0x42d8ff), unlike the planks', which the player word gives.
    const byte StoneRankStart = 0xff;

    /// <summary>Runs the turn of a building site.</summary>
    public void Turn(Building site)
    {
        if (site.Site is not { } needs)
        {
            return;
        }

        if (tables.SizeOf(site.Type) == BuildingSize.Large && site.Progress == 0)
        {
            // A digger inside or on the way (status bits 6 and 7) leaves the ground to him (0x42d33b).
            if (site.WorkerInside || site.WorkerOnTheWay)
            {
                return;
            }

            if (!Levelled(site, needs.TargetHeight))
            {
                // 0x42d4a3: a site whose last request failed (status bit 2) waits for the retry scan to clear the bit; else it asks the
                // castle for a digger (0x42d52a), and the failing request sets the bit (CastleRoster.AskForWorker).
                if (!site.RequestFailed)
                {
                    Ask(site, SiteStage.Levelling);
                }

                return;
            }
        }

        // 0x42d55f: a site whose bits 2, 6, and 7 are clear (no failed request, no worker inside or on the way) takes the progress word 1
        // and asks for its builder.
        if (!(site.RequestFailed || site.WorkerInside || site.WorkerOnTheWay))
        {
            site.Progress = 1;
            Ask(site, SiteStage.Building);
        }

        var flag = flags[site.Flag];
        Keep(flag, RequestWord.First, needs.Planks, site.FillLow, tables.PlankRankBase, site.WorkerInside);
        Keep(flag, RequestWord.Second, needs.Stones, site.FillHigh, StoneRankStart, site.WorkerInside);
    }

    // The site's request for the worker of the stage, none where no module names one. Two modules that name the worker of one stage
    // are refused, so no module's answer depends on the registry's order. The answers read the tables, which a partial table set
    // holds no data for until a site asks, so the refusal comes with the first ask of the stage.
    void Ask(Building site, SiteStage stage)
    {
        JobRules? first = null;
        WorkerRequest? worker = null;
        foreach (var module in modules)
        {
            if (module.SiteWorker(stage, tables) is not { } request)
            {
                continue;
            }

            if (first is not null)
            {
                throw new InvalidOperationException(
                    $"the job rules modules {first.GetType().Name} and {module.GetType().Name} both name the worker of the {stage} stage of a site"
                );
            }

            first = module;
            worker = request;
        }

        if (worker is not null)
        {
            askForWorker?.Invoke(site, worker);
        }
    }

    // The ground is levelled where the node and its six neighbours all lie at the site's height (0x42d34e to 0x42d490).
    bool Levelled(Building site, int height)
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
    static void Keep(Flag flag, RequestWord word, int needed, int fill, byte start, bool builderInside)
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

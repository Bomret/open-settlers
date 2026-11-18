using System;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The FNV-1a 64-bit fingerprint of the game state the ticks have produced (a command still queued is no part of it): the
/// tick, the commands applied, the castle and its stock, the flags, the total of resource 1, the buildings and settlers as
/// the original's items hold them, the goods, the random generator, every map layer, and the settler supply where the game runs
/// it. Each part is written in the
/// original's encoding (ADR 0013), so the digest does not change with the typed representation.
/// </summary>
public sealed class GameDigest : IGameDigest
{
    const ulong FnvOffset = 0xcbf29ce484222325;
    const ulong FnvPrime = 0x100000001b3;

    /// <inheritdoc/>
    public ulong Of(Game game)
    {
        var hash = Mix(Mix(FnvOffset, game.TickCount), (long)Applied(game));
        hash = Mix(hash, game.CastleStock.Castle is { } castle ? castle.X * 65536L + castle.Y : -1);
        MixStock(game, ref hash);

        foreach (var item in FlagEncoder.Items(game.Flags))
        {
            hash = Mix(hash, item);
        }

        hash = Mix(hash, game.Carriers.FlagCursor);

        foreach (var road in game.Network.Roads)
        {
            hash = Mix(
                Mix(Mix(Mix(Mix(hash, road.FromSlot), road.FromDirection), road.ToSlot), road.ToDirection),
                road.Water ? 1 : 0
            );
            foreach (var direction in road.Directions)
            {
                hash = Mix(hash, direction);
            }
        }

        hash = Mix(hash, game.Map.Resource1Total);
        MixSettlers(game, ref hash);
        MixWoodcutting(game, ref hash);

        var layers = MapLayerExporter.Export(game.Map);
        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            hash = Mix(hash, (long)layers.Digest(layer));
        }

        // A game without the supply mixes nothing for it, so its digest stays as it was.
        if (game.Supply is { } supply)
        {
            hash = Mix(hash, supply.Period);
            hash = Mix(hash, supply.Countdown);
            hash = Mix(hash, supply.LastClock);
            hash = Mix(hash, supply.KnightAccumulator);
            hash = Mix(hash, supply.KnightsOwed);
            hash = Mix(hash, supply.Running ? 1 : 0);
            hash = Mix(hash, supply.Cap);
            hash = Mix(hash, supply.MayMake ? 1 : 0);
        }

        return hash;
    }

    // The castle's stock: the count of each good with an original code, in the order of the codes (the storage item's words), then, in
    // the catalog's order of stable names, the hash of the name and the count of each good with no code that the castle holds some of.
    // A good with no code and a count of 0 leaves the digest as it is.
    static void MixStock(Game game, ref ulong hash)
    {
        var stock = game.CastleStock;
        var entries = stock.Catalog.Goods.Select(
            (entry, place) => (entry, count: stock.Stock.Count > place ? stock.Stock[place] : -1)
        );
        foreach (
            var (entry, count) in entries
                .Where(e => e.entry.OriginalCode is not null && e.count >= 0)
                .OrderBy(e => e.entry.OriginalCode)
        )
        {
            hash = Mix(hash, count);
        }

        foreach (var (entry, count) in entries.Where(e => e.entry.OriginalCode is null && e.count > 0))
        {
            hash = Mix(Mix(hash, (long)Fnv1a.Of(entry.Name)), count);
        }
    }

    // The number of jobs the original's list of waiting settlers has an entry for (0x42 + 2 times the job, up to 0x82).
    const int WaitingJobCount = 32;

    // The castle's list of waiting settlers in the original's order: the slot that waits for each job number, then, in the catalog's order
    // of stable names, the hash of the name and the slot of each job with no code that has one waiting. A job with no code and no waiting
    // settler leaves the digest as it is.
    static void MixWaiting(Game game, ref ulong hash)
    {
        var catalog = game.Tables.Catalog;
        for (var code = 0; code < WaitingJobCount; code++)
        {
            var job = Job.Of(code);
            if (job is UnportedJob || catalog.EntryOf(job)?.OriginalCode == code)
            {
                hash = Mix(hash, game.Roster.Waiting(job));
            }
        }

        foreach (var entry in catalog.Jobs.Where(entry => entry.OriginalCode is null))
        {
            if (Job.Named(entry.Name) is { } job && game.Roster.Waiting(job) is var slot and not 0)
            {
                hash = Mix(Mix(hash, (long)Fnv1a.Of(entry.Name)), slot);
            }
        }
    }

    // The commands the game applied, folded one after the other.
    static ulong Applied(Game game)
    {
        var applied = FnvOffset;
        foreach (var command in game.Applied)
        {
            applied = Mix(
                Mix(Mix(Mix(Mix(applied, command.Tick), command.Player), command.Kind), command.X),
                command.Y
            );
            foreach (var direction in command.Directions ?? [])
            {
                applied = Mix(applied, direction);
            }

            if (command.Building is { } type)
            {
                applied = Mix(applied, BuildingWords.Of(game.Tables.Catalog, type));
            }
        }

        return applied;
    }

    static void MixWoodcutting(Game game, ref ulong hash)
    {
        foreach (var word in FlagEncoder.Words(game.Flags))
        {
            hash = Mix(hash, word);
        }

        var random = game.Draws.Generator;
        hash = Mix(Mix(Mix(hash, random.W0), random.W1), random.W2);
        var (stamp, timer, x, y, decay) = game.Hut.ObjectPassState;
        hash = Mix(Mix(Mix(Mix(Mix(hash, stamp), timer), x), y), decay);
        foreach (var good in FlagEncoder.Places(game.Flags, game.Tables.Catalog))
        {
            hash = Mix(hash, good);
        }

        foreach (var request in FlagEncoder.Requests(game.Flags))
        {
            hash = Mix(hash, request);
        }
    }

    static void MixSettlers(Game game, ref ulong hash)
    {
        hash = Mix(
            Mix(Mix(Mix(hash, game.Clock.Now), game.Pass.Phase), game.Pass.ScanCursor),
            game.Roster.LeavingCount
        );
        foreach (var building in game.Buildings)
        {
            foreach (
                var value in new[]
                {
                    BuildingEncoder.TypeIndex(building, game.Tables.Catalog),
                    building.X,
                    building.Y,
                    building.Flag,
                    BuildingEncoder.Status(building),
                    BuildingEncoder.Worker(building),
                    building.Progress,
                    BuildingEncoder.Fill(building),
                    building.Stamp,
                }
            )
            {
                hash = Mix(hash, value);
            }

            if (building.Site is { } site)
            {
                hash = Mix(Mix(Mix(hash, site.Planks), site.Stones), site.TargetHeight);
            }
        }

        foreach (var settler in game.Settlers)
        {
            var raw = SettlerEncoder.Fields(settler, game.Tables.Catalog);
            foreach (
                var value in new[]
                {
                    JobWords.Of(game.Tables.Catalog, settler.Job),
                    settler.Player,
                    settler.Frame,
                    settler.Timer,
                    settler.X,
                    settler.Y,
                    raw.Stamp,
                    raw.State,
                    raw.Mode,
                    raw.Target,
                    raw.Heading,
                    raw.Next,
                }
            )
            {
                hash = Mix(hash, value);
            }
        }

        MixWaiting(game, ref hash);

        foreach (var (index, slot) in game.Movement.Occupied)
        {
            hash = Mix(Mix(hash, index), slot);
        }
    }

    static ulong Mix(ulong hash, long value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
        }

        return hash;
    }
}

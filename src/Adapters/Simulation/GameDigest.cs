using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The FNV-1a 64-bit fingerprint of the game state the ticks have produced (a command still queued is no part of it): the
/// tick, the commands applied, the castle and its stock, the flags, the total of resource 1, the buildings and settlers as
/// the original's items hold them, the goods, the random generator, and every map layer. Each part is written in the
/// original's encoding (ADR 0013), so the digest does not change with the typed representation.
/// </summary>
public sealed class GameDigest : IGameDigest
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    /// <inheritdoc/>
    public ulong Of(Game game)
    {
        var hash = Mix(Mix(FnvOffset, game.TickCount), (long)Applied(game));
        hash = Mix(hash, game.CastleStock.Castle is { } castle ? castle.X * 65536L + castle.Y : -1);
        foreach (var count in game.CastleStock.Stock)
        {
            hash = Mix(hash, count);
        }

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

        return hash;
    }

    // The commands the game applied, folded one after the other.
    private static ulong Applied(Game game)
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
        }

        return applied;
    }

    private static void MixWoodcutting(Game game, ref ulong hash)
    {
        foreach (var word in FlagEncoder.Words(game.Flags))
        {
            hash = Mix(hash, word);
        }

        var random = game.Draws.Generator;
        hash = Mix(Mix(Mix(hash, random.W0), random.W1), random.W2);
        var (stamp, timer, x, y) = game.Woodcutter.ObjectPassState;
        hash = Mix(Mix(Mix(Mix(hash, stamp), timer), x), y);
        foreach (var good in FlagEncoder.Places(game.Flags))
        {
            hash = Mix(hash, good);
        }
    }

    private static void MixSettlers(Game game, ref ulong hash)
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
                    BuildingEncoder.TypeIndex(building),
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
        }

        foreach (var settler in game.Settlers)
        {
            var raw = SettlerEncoder.Fields(settler);
            foreach (
                var value in new[]
                {
                    settler.Job.Code,
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

        foreach (var slot in game.Roster.WaitingList)
        {
            hash = Mix(hash, slot);
        }

        foreach (var (index, slot) in game.Movement.Occupied)
        {
            hash = Mix(Mix(hash, index), slot);
        }
    }

    private static ulong Mix(ulong hash, long value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
        }

        return hash;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Application.Testing;
using OpenSettlers.Domain;
using OpenSettlers.Domain.Testing;
using OpenSettlers.Import.HistoryEdition.Testing;
using OpenSettlers.Simulation;

namespace OpenSettlers.Adapters.Testing;

/// <summary>
/// The game and the map a command of the emulator verb remove names, on the install's tables, and the bytes of the original's
/// building items that the engine's buildings are compared with.
/// </summary>
public static class RemovalGame
{
    static List<int[]> Points(string list) =>
        list.Length == 0 ? [] : [.. list.Split(';').Select(point => point.Split(',').Select(int.Parse).ToArray())];

    /// <summary>The game before the first pass of <c>remove:N:SEED:L:cx,cy:FLAGS:ROADS:HUTS:AT:PASSES:CLICKS[:PUTS]</c>.</summary>
    public static Game Started(string[] parts, Map map, bool handsOutGoods = false)
    {
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(parts[3], CultureInfo.InvariantCulture),
            handsOutGoods: handsOutGoods
        );
        var castle = Points(parts[4]).Single();
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = castle[0],
                Y = castle[1],
            }
        );
        // The flags take their slots in the order the verb lists them, which the order of the commands of one tick (by node) would
        // change: each is applied at once.
        game.ApplyCommands();
        foreach (var flag in Points(parts[5]))
        {
            game.Enqueue(
                new Command
                {
                    Tick = 0,
                    Player = 0,
                    Kind = CommandKind.PlaceFlag,
                    X = flag[0],
                    Y = flag[1],
                }
            );
            game.ApplyCommands();
        }

        foreach (var road in parts[6].Split(';').Where(text => text.Length > 0).Select(text => text.Split(',')))
        {
            game.Enqueue(
                new Command
                {
                    Tick = 0,
                    Player = 0,
                    Kind = CommandKind.BuildRoad,
                    X = int.Parse(road[0], CultureInfo.InvariantCulture),
                    Y = int.Parse(road[1], CultureInfo.InvariantCulture),
                    Directions = [.. road[2].Select(c => c - '0')],
                }
            );
        }

        // The finished huts stand as a layout's finished-building entry leaves them, after the roads.
        game.ApplyCommands();
        foreach (var hut in Points(parts[7]))
        {
            Assert.IsTrue(
                Huts.Place(game, hut[0] - 1, hut[1] - 1, TestCatalog.BuildingOf(hut[2])),
                "the fixture's hut has its site"
            );
        }

        return game;
    }

    /// <summary>The map the command names: its size and seed, generated on the install's tables.</summary>
    public static Map MapOf(string[] parts)
    {
        MapSize.TryFromValue(int.Parse(parts[1], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[2].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();

        return MapGenerator.Generate(
            TestProgram.Tables(),
            new MapSeed
            {
                W0 = seed[0],
                W1 = seed[1],
                W2 = seed[2],
            },
            size!
        );
    }

    /// <summary>A line "fill&lt;slot&gt;+=NN" adds to byte 8 of the building in that slot: a good the original has handed out or delivered.</summary>
    public static void ApplyFills(Game game, Dictionary<string, string> line)
    {
        foreach (var (key, value) in line.Where(pair => pair.Key.StartsWith("fill", StringComparison.Ordinal)))
        {
            var building = game.Buildings[int.Parse(key[4..^1], CultureInfo.InvariantCulture) - 1];
            building.SetFill(FillCounter.Low, building.FillLow + Convert.ToInt32(value, 16));
        }
    }

    /// <summary>
    /// The building's bytes as the item holds them: the node's offset, the type byte, the status, the flag's slot (one more than
    /// the engine's), the two bytes of the fill, the worker, the progress, and the word at 14 that a building taken down keeps its
    /// clock in (a building site holds its target height there, and its needs at 16 and 17). The castle's bytes from 14 hold a
    /// pointer to its storage, which the engine does not keep.
    /// </summary>
    public static string BuildingBytes(Building building, int width)
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes((building.Y * 2 * width + building.X) * 4));
        bytes.AddRange([
            (byte)(BuildingEncoder.TypeIndex(building, TestCatalog.Default) << 2),
            (byte)BuildingEncoder.Status(building),
        ]);
        bytes.AddRange(BitConverter.GetBytes((ushort)(building.Flag + 1)));
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Fill(building)));
        bytes.AddRange(BitConverter.GetBytes((ushort)BuildingEncoder.Worker(building)));
        bytes.AddRange(BitConverter.GetBytes(building.Progress));
        if (building.Site is { } site)
        {
            // A building site holds its target height at 14 and the planks and stones it needs at 16 and 17 (0x449a63); taken down it
            // holds the clock there (0x4617fd).
            bytes.AddRange(BitConverter.GetBytes(building.IsDemolishing ? building.Stamp : (ushort)site.TargetHeight));
            bytes.AddRange([(byte)site.Planks, (byte)site.Stones]);
        }
        else
        {
            bytes.AddRange(BitConverter.GetBytes(building.Stamp));
            bytes.AddRange([0, 0]);
        }

        return Convert.ToHexString([.. bytes]);
    }
}

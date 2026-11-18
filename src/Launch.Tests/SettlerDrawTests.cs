using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The end-to-end check of the settlers' draws against settler-draws.txt (the output of the emulator verb draw, ADR 0008;
// RecordedDraws reads it; the file lives in src/Import.HistoryEdition.Tests, which checks the picks of each term against it):
// the map view's SettlerFigures over the install's StepOffsets and figure tables makes the terms of the recorded settlers, and the
// History Edition source answers each term with an image or none. Through public types only, the terms the source draws must be
// the sprites the original drew, in the order it drew them: one run of terms for each of its records, the part of each sprite (the
// overlay is the one with a palette index), and the position of the figure and of the overlay. The entry numbers and the second
// figure's shift are the picks' (INV-0010 keeps them out of the terms), checked in src/Import.HistoryEdition.Tests.
[TestClass]
public sealed class SettlerDrawTests
{
    const string Fixture = "settler-draws.txt";

    // 0x44ffe7 moves a record's position by 0x10 and 8 before the blit.
    const int OffsetX = 0x10;
    const int OffsetY = 8;

    static readonly string ReferenceSha256 = RecordedDraws.ReferenceSha256(Fixture);

    public static IEnumerable<object[]> Scenarios =>
        RecordedDraws.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    static string Describe(IEnumerable<RecordedSprite> sprites) => string.Join(" ", sprites);

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    [TestCategory(Needs.Install)]
    public void TheEnginesDrawsAreTheOriginalsPassForPass(string name)
    {
        var scenario = RecordedDraws.Load(Fixture).Single(candidate => candidate.Name == name);
        var install = TestProgram.OpenInstall();
        var program = Directory
            .EnumerateFiles(Environment.GetEnvironmentVariable("SIEDLER_DIR")!)
            .First(file => string.Equals(Path.GetFileName(file), Install.Program, StringComparison.OrdinalIgnoreCase));
        if (
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(program)))
            != ReferenceSha256.ToUpperInvariant()
        )
        {
            Assert.Inconclusive(
                "The recordings come from the History Edition's Siedler1.exe (ADR 0004); this install holds another build."
            );
        }

        var tables = TestProgram.Tables();
        var figures = new SettlerFigures(StepOffsets.Load(install), tables.Figures);
        var source = HistoryEditionSource.Load(install, tables);
        // draw:WINDOWS:CX,CY:VERB:N:SEED:...
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[4], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[5].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(
            tables,
            new MapSeed
            {
                W0 = seed[0],
                W1 = seed[1],
                W2 = seed[2],
            },
            size!
        );
        var width = map.Size.Width;

        var known = new SortedDictionary<int, byte[]>();
        foreach (var pass in scenario.Passes)
        {
            foreach (var (slot, (bytes, height)) in pass.Changed)
            {
                known[slot] = bytes;
                var settler = RecordedDraws.FromBytes(bytes, width);
                // The fixture holds the original's height byte: the height in its low 5 bits.
                map.At(settler.X, settler.Y).Height = height & Node.MaxHeight;
            }

            // The settlers as the original's view pass left them, which it drew.
            Check($"{name}: pass {pass.Number}", known);

            // The settlers as they stood before that pass gave them a turn, where the turn only ran the timer down by the
            // clock units since the stamp and set the stamp to the clock: the engine, whose settlers are not given a turn
            // for the view, takes the time left from the clock.
            var before = new SortedDictionary<int, byte[]>(known);
            foreach (var (slot, (timer, stamp)) in pass.Held)
            {
                var ranDown = (ushort)(timer - (ushort)(pass.Clock - stamp));
                if (
                    BitConverter.ToUInt16(known[slot], 2) == ranDown
                    && BitConverter.ToUInt16(known[slot], 8) == pass.Clock
                )
                {
                    var bytes = (byte[])known[slot].Clone();
                    BitConverter.GetBytes(timer).CopyTo(bytes, 2);
                    BitConverter.GetBytes(stamp).CopyTo(bytes, 8);
                    before[slot] = bytes;
                }
            }

            Check($"{name}: pass {pass.Number}, before the view's turn", before);

            void Check(string where, SortedDictionary<int, byte[]> state)
            {
                var settlers = state.Values.Select(bytes => RecordedDraws.FromBytes(bytes, width)).ToList();
                // The draw verb runs the settlers' draw path only (0x42f4f1, 0x44ffe7), not the tile pass, which draws the
                // carriers that wait off their node (states 0x42 to 0x45): those are left out.
                var drawing = settlers.Where(s => s.Job != new Carrier() || s.Activity is not WaitsOffRoad).ToList();

                // Each settler is three terms; the ones the source draws an image for, in the order of the terms, are one run
                // for each settler the source draws at all.
                var runs = figures
                    .Draws(map, drawing, pass.Clock)
                    .Chunk(3)
                    .Select(chunk => chunk.Where(term => source.Draw(term)?.Image is not null).ToArray())
                    .Where(run => run.Length > 0)
                    .ToList();

                Assert.HasCount(
                    pass.Records.Count,
                    runs,
                    $"{where}: the engine draws {runs.Count} settlers, the original {pass.Records.Count} "
                        + $"(settlers {string.Join(",", state.Keys)})"
                );
                for (var i = 0; i < runs.Count; i++)
                {
                    var record = pass.Records[i];
                    var run = runs[i];
                    var bytes = state[record.Slot];
                    var described =
                        $"{where} settler {record.Slot} (state {bytes[10]:X}, frame {bytes[1]:X}): "
                        + $"the original draws {Describe(record.Sprites)}";

                    Assert.IsTrue(
                        run.All(term => term.Frame == bytes[1] && term.Job == Job.Of((bytes[0] >> 2) & 0x1f)),
                        $"{described}, the engine's terms are another settler's"
                    );
                    // The overlay is the sprite with a palette index; the figure comes first and the second figure last.
                    var expected = record
                        .Sprites.Select(
                            (sprite, index) =>
                                sprite.Colour >= 0 ? SettlerPart.Overlay
                                : index == 0 ? SettlerPart.Figure
                                : SettlerPart.SecondFigure
                        )
                        .ToArray();
                    CollectionAssert.AreEqual(expected, run.Select(term => term.Part).ToArray(), described);
                    for (var j = 0; j < run.Length; j++)
                    {
                        if (run[j].Part != SettlerPart.SecondFigure)
                        {
                            Assert.AreEqual(
                                (record.Sprites[j].X, record.Sprites[j].Y),
                                (run[j].X + OffsetX, run[j].Y + OffsetY),
                                $"{described}, the position of the {run[j].Part}"
                            );
                        }
                    }
                }
            }
        }
    }
}

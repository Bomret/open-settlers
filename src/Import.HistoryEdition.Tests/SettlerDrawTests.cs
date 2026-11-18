using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The lines of settler-draws.txt are the output of the emulator verb draw (tools/GhidraDump/emulate.py, EmulateDraw.java,
// ADR 0008): the settler draw path of the original's main loop run after the passes of a game, once for each pass of its
// windows (RecordedDraws reads them). The picks of the History Edition's figure tables, given the settlers' bytes, the
// clock, and the headers of the figures, must be the sprites the original blitted for each settler: the entry, the colour
// index, and, for the second figure, the shift its figure's header gives, seen as the sprite's position relative to the
// settler's first sprite. The placement of the terms (rows, order, absolute positions) is not checked here: the map view
// owns it, and src/Launch.Tests compares it with the same file through public types.
[TestClass]
public sealed class SettlerDrawTests
{
    const string Fixture = "settler-draws.txt";

    // The build the recordings came from: the sha256 of the first "install=" line of the fixture (every recording must
    // name the same one).
    static readonly string ReferenceSha256 = RecordedDraws.ReferenceSha256(Fixture);

    public static IEnumerable<object[]> Scenarios =>
        RecordedDraws.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    // The header bytes 0 and 1 of each figure entry that reads: the shift of its second figure (0x4066d2, 0x4066e4).
    static Dictionary<int, (int X, int Y)> Shifts(Install install)
    {
        var archive = Formats.ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var shifts = new Dictionary<int, (int X, int Y)>();
        for (
            var entry = SettlerFigureTables.FigureBase;
            entry < SettlerFigureTables.OverlayBase && entry < archive.RowCount;
            entry++
        )
        {
            if (archive[entry].State == Formats.EntryState.Absent)
            {
                continue;
            }

            try
            {
                var header = Formats.SpriteFile.Read(archive.Data(archive[entry])).Header;
                shifts[entry] = (header.First, header.Second);
            }
            catch (InvalidDataException)
            {
                // An entry that is no sprite shifts nothing.
            }
        }

        return shifts;
    }

    static string Describe(IEnumerable<RecordedSprite> sprites) => string.Join(" ", sprites);

    [TestMethod]
    public void EveryRecordingNamesTheReferenceInstallItCameFrom()
    {
        foreach (var scenario in RecordedDraws.Load(Fixture))
        {
            var install = scenario.Header.Single(line => line.StartsWith("install=", StringComparison.Ordinal));
            StringAssert.Contains(install, "History Edition", scenario.Name);
            StringAssert.Contains(install, $"sha256={ReferenceSha256}", scenario.Name);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    [TestCategory(Needs.Install)]
    public void TheEnginesDrawsAreTheOriginalsPassForPass(string name)
    {
        var scenario = RecordedDraws.Load(Fixture).Single(candidate => candidate.Name == name);
        var install = TestProgram.OpenInstall();
        if (
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(install.ReadAll(Install.Program)))
            != ReferenceSha256.ToUpperInvariant()
        )
        {
            Assert.Inconclusive(
                "The recordings come from the History Edition's Siedler1.exe (ADR 0004); this install holds another build."
            );
        }

        var tables = TestProgram.Tables();
        var archive = Formats.ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var sequences = archive.Data(archive[2]).ToArray();
        var figureTables = SettlerFigureTables.From(tables, sequences);
        var steps = new StepOffsets(sequences);
        var source = HistoryEditionSource.Load(install, tables);
        var shifts = Shifts(install);
        // draw:WINDOWS:CX,CY:VERB:N:SEED:...
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[4], CultureInfo.InvariantCulture), out var size, out _);
        var map = new Map(size!);
        var width = map.Size.Width;

        var known = new SortedDictionary<int, byte[]>();
        foreach (var pass in scenario.Passes)
        {
            foreach (var (slot, (bytes, _)) in pass.Changed)
            {
                known[slot] = bytes;
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
                foreach (var (slot, bytes) in state)
                {
                    var settler = RecordedDraws.FromBytes(bytes, width);
                    // The draw verb runs the settlers' draw path only (0x42f4f1, 0x44ffe7), not the tile pass, which draws the
                    // carriers that wait off their node (states 0x42 to 0x45): those are left out.
                    if (settler.Job == new Carrier() && settler.Activity is WaitsOffRoad)
                    {
                        continue;
                    }

                    var terms = SettlerTerms.Of(steps, map, [settler], pass.Clock);
                    var picks = SettlerTerms.Picks(figureTables, terms);

                    // The view draws a sprite from the images the source gives: a pick with no image is a failure here, where
                    // the view would draw nothing for it.
                    var unloaded = terms
                        .Where(term => figureTables.Pick(term) is not null && source.Draw(term)?.Image is null)
                        .Select(term => figureTables.Pick(term)!.Value)
                        .ToList();
                    Assert.IsEmpty(
                        unloaded,
                        $"{where}: the engine draws sprites the source has no image for: {string.Join(" ", unloaded.Select(pick => $"{pick.Entry}/{pick.Colour}"))}"
                    );

                    var original = pass.Records.SingleOrDefault(record => record.Slot == slot)?.Sprites ?? [];
                    var describe =
                        $"{where} settler {slot} (state {bytes[10]:X}, frame {bytes[1]:X}): the original draws "
                        + $"{(original.Count == 0 ? "nothing" : Describe(original))}, the engine picks "
                        + $"{(picks.Count == 0 ? "nothing" : string.Join(" ", picks.Select(pick => $"{pick.Entry}/{pick.Colour}")))}";
                    CollectionAssert.AreEqual(
                        original.Select(sprite => (sprite.Entry, sprite.Colour)).ToArray(),
                        picks.Select(pick => (pick.Entry, pick.Colour)).ToArray(),
                        describe
                    );

                    // Each sprite hangs from the first one's position by the offsets of its pick and the shift of its figure.
                    if (picks.Count > 0)
                    {
                        var first = SettlerFigureShift.Of(picks[0], shifts);
                        for (var i = 1; i < picks.Count; i++)
                        {
                            var shift = SettlerFigureShift.Of(picks[i], shifts);
                            Assert.AreEqual(
                                (picks[i].Dx + shift.X - picks[0].Dx - first.X, picks[i].Dy + shift.Y - picks[0].Dy - first.Y),
                                (original[i].X - original[0].X, original[i].Y - original[0].Y),
                                describe
                            );
                        }
                    }
                }
            }
        }
    }
}

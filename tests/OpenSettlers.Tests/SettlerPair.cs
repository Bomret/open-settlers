using System.Collections.Generic;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>A settler sprite as the History Edition's picks give it for a term: the row, entry, colour index, and position.</summary>
internal sealed record Drawn(int Row, int Entry, int Colour, int X, int Y);

/// <summary>
/// The shared placement of the settlers' terms and the History Edition's table picks over them: each term's entry and
/// colour index come from the picks, at the term's position moved by the pick's offsets and, for a second figure, by the
/// shift of its figure.
/// </summary>
internal sealed class SettlerPair(SettlerFigures shared, SettlerFigureTables tables)
{
    /// <summary>The pair over the install's walk sequences and the tables of the exe.</summary>
    public static SettlerPair Of(Assets.Install install, Domain.RuleTables tables)
    {
        var archive = Formats.ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var sequences = archive.Data(archive[2]).ToArray();

        return new SettlerPair(
            new SettlerFigures(new StepOffsets(sequences), tables.FrameRows),
            SettlerFigureTables.From(tables, sequences)
        );
    }

    public SettlerPick? Pick(SettlerDraw term) => tables.Pick(term);

    public List<SettlerDraw> Terms(Map map, IReadOnlyList<Settler> settlers, ushort clock) =>
        shared.Draws(map, settlers, clock);

    public List<Drawn> Draws(
        Map map,
        IReadOnlyList<Settler> settlers,
        ushort clock,
        IReadOnlyDictionary<int, (int X, int Y)>? shifts = null
    )
    {
        var drawn = new List<Drawn>();
        foreach (var term in Terms(map, settlers, clock))
        {
            if (tables.Pick(term) is { } pick)
            {
                var shift =
                    pick.ShiftedBy >= 0 && shifts is not null ? shifts.GetValueOrDefault(pick.ShiftedBy) : default;
                drawn.Add(
                    new Drawn(term.Row, pick.Entry, pick.Colour, term.X + pick.Dx + shift.X, term.Y + pick.Dy + shift.Y)
                );
            }
        }

        return drawn;
    }
}

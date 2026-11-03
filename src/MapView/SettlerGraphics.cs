using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Assets;
using OpenSettlers.Formats;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The sprites of the settlers of one job, as array texture layers like <see cref="ObjectGraphics"/>: the figures, the
/// colour overlays filled with the player's colour (player 0's: the game has one player), and the second figures that the
/// job's walk sequences name, and the <see cref="SettlerFigures"/> that picks them (<c>docs/rules/settlers.md</c>).
/// </summary>
public sealed class SettlerGraphics
{
    // The layers an array texture holds in the view.
    private const int MaxLayers = 2048;

    public required byte[] PaletteRgba { get; init; }

    public required int LayerWidth { get; init; }

    public required int LayerHeight { get; init; }

    public required IReadOnlyList<ObjectImage> Images { get; init; }

    /// <summary>The layer of a sprite entry, by the colour of its fill (-1 for a sprite drawn as it is).</summary>
    public required IReadOnlyDictionary<(int Entry, int Colour), int> Layers { get; init; }

    public required SettlerFigures Figures { get; init; }

    /// <summary>Reads the sprites the sequences of <paramref name="jobs"/> use; sprites past the layer limit are left out.</summary>
    public static SettlerGraphics Load(Install install, ProgramTables tables, params int[] jobs)
    {
        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var palette = PaletteFile.Read(archive.Data(archive[3]).Span);
        var sequences = archive.Data(archive[2]).ToArray();
        var figures = new SettlerFigures(tables.FigureBases, tables.FigureOffsets, sequences, tables.CarriedFigures);
        var images = new List<ObjectImage>();
        var layers = new Dictionary<(int Entry, int Colour), int>();
        foreach (
            var (entry, colour) in jobs.SelectMany(job =>
                figures.Sprites(job, SettlerFigures.ColourOf(0), Game.GoodLog)
            )
        )
        {
            if (images.Count >= MaxLayers || layers.ContainsKey((entry, colour)))
            {
                continue;
            }

            // A sequence names figures for every job; the ones a job has no sprite for lie outside the archive or are absent.
            if (entry < 1 || entry >= archive.RowCount || archive[entry].State == EntryState.Absent)
            {
                continue;
            }

            ObjectImage image;
            try
            {
                image = ObjectImage.From(SpriteFile.Read(archive.Data(archive[entry])));
            }
            catch (InvalidDataException)
            {
                // Likewise an entry that is no sprite: a figure byte of another job's sequences reaches it.
                continue;
            }

            if (colour >= 0)
            {
                image = Fill(image, colour);
            }

            layers[(entry, colour)] = images.Count;
            images.Add(image);
        }

        var width = Math.Max(images.Max(image => image.Width), 1);
        var height = Math.Max(images.Max(image => image.Height), 1);

        return new SettlerGraphics
        {
            PaletteRgba = PaletteTexture.Rgba(palette),
            LayerWidth = width,
            LayerHeight = height,
            Images = [.. images.Select(image => ObjectGraphics.Pad(image, width, height))],
            Layers = layers,
            Figures = figures,
        };
    }

    /// <summary>
    /// A colour overlay drawn in one colour: every pixel takes the colour, and the coverage stays, so the pixels the
    /// overlay leaves transparent stay transparent (0x405880 skips them).
    /// </summary>
    public static ObjectImage Fill(ObjectImage overlay, int colour) =>
        overlay with
        {
            Indices = Array.ConvertAll(overlay.Indices, _ => (byte)colour),
        };
}

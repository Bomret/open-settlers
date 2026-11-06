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
    // The layers an array texture holds in the view. simplicity: a fixed cap, no query of the device. Vulkan guarantees
    // only 256 array layers (maxImageArrayLayers); 2048 is the guaranteed minimum of Direct3D 11 and 12 and what desktop
    // Vulkan and Metal drivers report, and the walk sequences of a job need more than 256. A device below 2048 fails to
    // create the texture; the upgrade path is to cap against RenderingDevice.LimitMaxTextureArrayLayers in the view.
    private const int MaxLayers = 2048;

    public required byte[] PaletteRgba { get; init; }

    public required int LayerWidth { get; init; }

    public required int LayerHeight { get; init; }

    public required IReadOnlyList<ObjectImage> Images { get; init; }

    /// <summary>The layer of a sprite entry, by the colour of its fill (-1 for a sprite drawn as it is).</summary>
    public required IReadOnlyDictionary<(int Entry, int Colour), int> Layers { get; init; }

    public required SettlerFigures Figures { get; init; }

    /// <summary>The header bytes 0 and 1 of each figure entry (1850 to 2499) that loaded: the shift of its second figure.</summary>
    public required IReadOnlyDictionary<int, (int X, int Y)> Shifts { get; init; }

    /// <summary>The layer of the sprite of a draw. A sprite no layer holds is a failure: the load left it out, and the view would never show it.</summary>
    public int LayerOf(SettlerDraw draw) =>
        Layers.TryGetValue((draw.Entry, draw.Colour), out var layer)
            ? layer
            : throw new InvalidOperationException(
                $"No layer holds the settler sprite {draw.Entry}/{draw.Colour}: the load of the settler graphics left it out."
            );

    /// <summary>Reads the sprites the sequences of <paramref name="jobs"/> use; sprites past the layer limit are left out.</summary>
    public static SettlerGraphics Load(Install install, ProgramTables tables, params int[] jobs)
    {
        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var palette = PaletteFile.Read(archive.Data(archive[3]).Span);
        var sequences = archive.Data(archive[2]).ToArray();
        var figures = new SettlerFigures(
            tables.FigureBases,
            tables.FigureOffsets,
            sequences,
            tables.CarriedFigures,
            tables.WoodcutterFigureAdds,
            tables.FrameRows,
            tables.TypeOneHutFigureAdds
        );
        var images = new List<ObjectImage>();
        var layers = new Dictionary<(int Entry, int Colour), int>();
        var shifts = new Dictionary<int, (int X, int Y)>();
        // A carrier carries any good of the table, in the row the good's word picks; the others carry none.
        var goods = Enumerable.Range(0, ProgramTables.CarriedFigureCount);
        foreach (
            var (entry, colour) in jobs.SelectMany(job =>
                (job == Settler.CarrierJob ? goods : [0]).SelectMany(good =>
                    figures.Sprites(job, SettlerFigures.ColourOf(0), good)
                )
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

            Sprite sprite;
            try
            {
                sprite = SpriteFile.Read(archive.Data(archive[entry]));
            }
            catch (InvalidDataException)
            {
                // Likewise an entry that is no sprite: a figure byte of another job's sequences reaches it.
                continue;
            }

            var image = ObjectImage.From(sprite);
            if (colour < 0 && entry < SettlerFigures.OverlayBase)
            {
                shifts[entry] = (sprite.Header.First, sprite.Header.Second);
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
            Shifts = shifts,
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

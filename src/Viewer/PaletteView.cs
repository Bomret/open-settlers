using System.Linq;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.Viewer;

/// <summary>The view of a palette: its 256 colors as a 16 x 16 grid of squares, color 0 at the top left, scaled to fit the pane.</summary>
public sealed class PaletteView : PreviewView
{
    const int Columns = 16;

    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(PalettePreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var palette = ((PalettePreview)preview).Palette;
        var rgba = IndexedImage.ToRgba(Enumerable.Range(0, Palette.ColorCount).Select(i => (byte)i).ToArray(), palette);

        return Picture(preview.Summary, Columns, Palette.ColorCount / Columns, rgba);
    }
}

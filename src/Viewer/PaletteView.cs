using Godot;

namespace OpenSettlers.Viewer;

/// <summary>The view of a palette: its 256 colors as a 16 x 16 grid of squares, color 0 at the top left, scaled to fit the pane.</summary>
public sealed class PaletteView : PreviewView
{
    const int Columns = 16;
    const int ColorCount = 256;

    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(PalettePreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview) =>
        Picture(preview.Summary, Columns, ColorCount / Columns, ((PalettePreview)preview).Rgba);
}

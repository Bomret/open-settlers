using System.Linq;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.Viewer;

/// <summary>The view of a palette: its 256 colors as a 16 x 16 grid of squares, color 0 at the top left.</summary>
public sealed class PaletteView : PreviewView
{
    const int Columns = 16;
    const int Square = 16;

    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(PalettePreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var palette = ((PalettePreview)preview).Palette;
        var rgba = IndexedImage.ToRgba(Enumerable.Range(0, Palette.ColorCount).Select(i => (byte)i).ToArray(), palette);

        var image = Image.CreateFromData(Columns, Palette.ColorCount / Columns, false, Image.Format.Rgba8, rgba);
        var grid = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = new Vector2(Columns * Square, Columns * Square),
        };
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = preview.Summary });
        box.AddChild(grid);

        return box;
    }
}

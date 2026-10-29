using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a raw image: its pixels at their own size, opaque, in the game's palette.</summary>
public sealed class RawImageView : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => RawImageKind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var raw = (RawImagePreview)preview;
        var image = Image.CreateFromData(raw.Width, raw.Height, false, Image.Format.Rgba8, raw.Rgba);
        var picture = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(picture);
        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        box.AddChild(new Label { Text = preview.Summary });
        box.AddChild(scroll);

        return box;
    }
}

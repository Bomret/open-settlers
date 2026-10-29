using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a transparent sprite: its pixels at their own size over a flat background that shows the transparent pixels.</summary>
public sealed class SpriteView : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => SpriteKind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var sprite = (SpritePreview)preview;
        var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, sprite.Rgba);
        var picture = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        var backdrop = new ColorRect
        {
            Color = new Color(0.25f, 0.25f, 0.3f),
            CustomMinimumSize = new Vector2(sprite.Width, sprite.Height),
        };
        backdrop.AddChild(picture);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(backdrop);
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

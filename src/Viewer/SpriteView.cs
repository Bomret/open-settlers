using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a <see cref="SpritePreview"/> (a sprite, or one drawn over color 8): its pixels scaled to fit the pane over a flat background that shows the transparent pixels.</summary>
public sealed class SpriteView : PreviewView
{
    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(SpritePreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var sprite = (SpritePreview)preview;

        return Picture(preview.Summary, sprite.Width, sprite.Height, sprite.Rgba, new Color(0.25f, 0.25f, 0.3f));
    }
}

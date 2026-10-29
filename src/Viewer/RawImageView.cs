using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a raw image: its pixels scaled to fit the pane, opaque, in the game's palette.</summary>
public sealed class RawImageView : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => RawImageKind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var raw = (RawImagePreview)preview;

        return Picture(preview.Summary, raw.Width, raw.Height, raw.Rgba);
    }
}

using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>
/// The view of a <see cref="RawImagePreview"/> (a raw image, a mask, or a fill texture): its RGBA pixels scaled to
/// fit the pane.
/// </summary>
public sealed class RawImageView : PreviewView
{
    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(RawImagePreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var raw = (RawImagePreview)preview;

        return Picture(preview.Summary, raw.Width, raw.Height, raw.Rgba);
    }
}

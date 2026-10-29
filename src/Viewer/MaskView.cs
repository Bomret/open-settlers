using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a mask: its covered pixels in their one color scaled to fit the pane, the rest transparent. The preview is an RGBA image, so it is shown as a raw image is.</summary>
public sealed class MaskView : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => MaskKind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview) => new RawImageView().Create(preview);
}

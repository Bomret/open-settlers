using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a fill texture: its pixels at their own size, opaque, shown as a raw image is.</summary>
public sealed class FillTextureView : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => FillTextureKind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview) => new RawImageView().Create(preview);
}

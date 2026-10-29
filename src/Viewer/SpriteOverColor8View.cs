using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a sprite drawn over color 8: the same picture as a sprite, since both previews are <see cref="SpritePreview"/>.</summary>
public sealed class SpriteOverColor8View : PreviewView
{
    /// <inheritdoc/>
    public override string KindName => SpriteOverColor8Kind.KindName;

    /// <inheritdoc/>
    public override Control Create(Preview preview) => new SpriteView().Create(preview);
}

using Godot;
using OpenSettlers.MapView;

namespace OpenSettlers.Viewer;

/// <summary>
/// Reads the PNG files of a content set with Godot's <c>Image.LoadFromFile</c> (ADR 0015), with no import into Godot
/// resources. Godot's documentation does not say what the call returns for a file it cannot read, so an image that is
/// null or empty both count as undecodable.
/// </summary>
public sealed class GodotImageDecoder : IImageDecoder
{
    /// <inheritdoc />
    public DecodedImage? Decode(string path)
    {
        var image = Image.LoadFromFile(path);
        if (image is null || image.IsEmpty())
        {
            return null;
        }

        image.Convert(Image.Format.Rgba8);

        return new DecodedImage(image.GetWidth(), image.GetHeight(), image.GetData());
    }
}

using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>
/// The images a map view has asked a graphics source for, one array texture layer each, in the order they were first
/// asked for. An image a source returns again keeps its layer.
/// </summary>
public sealed class ImageLayers
{
    readonly Dictionary<ObjectImage, int> _layers = new(ReferenceEqualityComparer.Instance);
    readonly List<ObjectImage> _images = [];

    /// <summary>The images by layer.</summary>
    public IReadOnlyList<ObjectImage> Images => _images;

    /// <summary>The layer of <paramref name="image"/>, added as the next layer where it has none.</summary>
    public int Of(ObjectImage image)
    {
        if (!_layers.TryGetValue(image, out var layer))
        {
            layer = _images.Count;
            _layers[image] = layer;
            _images.Add(image);
        }

        return layer;
    }
}

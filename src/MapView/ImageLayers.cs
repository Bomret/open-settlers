using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenSettlers.MapView;

/// <summary>
/// The images a map view has asked a graphics source for, one array texture layer each, in the order they were first
/// asked for. An image a source returns again keeps its layer.
/// </summary>
public sealed class ImageLayers
{
    // The step a layer's width and height grow by.
    const int SizeStep = 32;

    readonly Dictionary<ObjectImage, int> _layers = new(ReferenceEqualityComparer.Instance);
    readonly List<ObjectImage> _images = [];
    int _width;
    int _height;

    /// <summary>The images by layer.</summary>
    public IReadOnlyList<ObjectImage> Images => _images;

    /// <summary>
    /// The array texture that holds every image: its layer count, the power of two at or above the image count, and its
    /// width and height, the multiples of 32 at or above the widest and the tallest image. It grows in steps, so a view
    /// that builds the texture again where the shape changes builds it a few times while a draw meets its images one by
    /// one. An empty set takes one layer.
    /// </summary>
    public (int Layers, int Width, int Height) Shape =>
        ((int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(_images.Count, 1)), Step(_width), Step(_height));

    /// <summary>The layer of <paramref name="image"/>, added as the next layer where it has none.</summary>
    public int Of(ObjectImage image)
    {
        if (!_layers.TryGetValue(image, out var layer))
        {
            layer = _images.Count;
            _layers[image] = layer;
            _images.Add(image);
            _width = Math.Max(_width, image.Width);
            _height = Math.Max(_height, image.Height);
        }

        return layer;
    }

    static int Step(int size) => (Math.Max(size, 1) + SizeStep - 1) / SizeStep * SizeStep;
}

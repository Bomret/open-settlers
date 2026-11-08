namespace OpenSettlers.MapView;

/// <summary>
/// How a quad tells the sprite shaders (<c>Objects.gdshader</c>, <c>Water.gdshader</c>) its array texture layer. Godot
/// stores a vertex color in 8 bits a channel, so one channel holds layers 0 to 255 only; the layer's low byte goes in red
/// and its high byte in green. The settlers' sprites take more than 256 layers.
/// </summary>
public static class SpriteLayer
{
    /// <summary>The red and green channels of the vertex color that carries <paramref name="layer"/>.</summary>
    public static (float Red, float Green) Channels(int layer) => ((layer & 0xff) / 255f, (layer >> 8) / 255f);

    /// <summary>
    /// The red, green, and blue channels of the vertex color of a quad: <paramref name="layer"/> as
    /// <see cref="Channels"/> splits it, and in blue 1 where the layer is one of the shadow array, else 0.
    /// </summary>
    public static (float Red, float Green, float Blue) Color(int layer, bool shadow)
    {
        var (red, green) = Channels(layer);

        return (red, green, shadow ? 1 : 0);
    }
}

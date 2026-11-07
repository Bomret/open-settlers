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

    /// <summary>The layer the shaders read back from the two channels once stored in 8 bits each.</summary>
    public static int Decode(byte red, byte green) => red + 256 * green;
}

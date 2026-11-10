namespace OpenSettlers.MapView;

/// <summary>
/// How a quad tells <c>Objects.gdshader</c> and <c>Water.gdshader</c> its array texture layer. Godot stores a vertex
/// color in 8 bits a channel, so one channel holds layers 0 to 255 only; the layer's low byte goes in red and its high
/// byte in green. An array holds as many layers as the images a source draws, whatever their number.
/// </summary>
public static class SpriteLayer
{
    /// <summary>The red and green channels of the vertex color that carries <paramref name="layer"/>.</summary>
    public static (float Red, float Green) Channels(int layer) => ((layer & 0xff) / 255f, (layer >> 8) / 255f);
}

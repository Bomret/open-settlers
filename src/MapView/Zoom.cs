namespace OpenSettlers.MapView;

/// <summary>
/// The scale of the map view: screen pixels per map pixel, between <see cref="Min"/> and <see cref="Max"/>. The
/// copies of <see cref="Camera.Copies"/> cover a view of at most two maps each way, so a wider or taller view would
/// show a gap at its edge: <see cref="Fit"/> raises the scale until the view is at most two maps across, past
/// <see cref="Max"/> in a window of more than eight maps at that scale.
/// </summary>
public sealed class Zoom
{
    /// <summary>The smallest scale.</summary>
    public const float Min = 0.25f;

    /// <summary>The largest scale.</summary>
    public const float Max = 4f;

    /// <summary>The factor of one step of the wheel or a key.</summary>
    public const float Step = 1.25f;

    /// <summary>The screen pixels per map pixel, 1 at the start.</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>Multiplies the scale by <c>factor</c>, as a pinch does, and keeps the view inside two maps.</summary>
    public void ByFactor(float factor, Camera camera, float viewWidth, float viewHeight)
    {
        Scale = System.Math.Clamp(Scale * factor, Min, Max);
        Fit(camera, viewWidth, viewHeight);
    }

    /// <summary>Raises the scale until a view of this size in screen pixels covers at most two maps each way.</summary>
    public void Fit(Camera camera, float viewWidth, float viewHeight)
    {
        var least = System.Math.Max(viewWidth / (2f * camera.PeriodX), viewHeight / (2f * camera.PeriodY));
        Scale = System.Math.Max(Scale, least);
    }
}

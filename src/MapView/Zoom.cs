namespace OpenSettlers.MapView;

/// <summary>
/// The scale of the map view: screen pixels per map pixel. It moves in steps of <see cref="Step"/> between
/// <see cref="Min"/> and <see cref="Max"/>. The view draws nine copies of the map around the camera
/// (<see cref="Camera.Copies"/>), so a view wider or taller than two maps would show a gap at its edge:
/// <see cref="Fit"/> raises the scale until the view is at most two maps across.
/// </summary>
public sealed class Zoom
{
    /// <summary>The smallest scale.</summary>
    public const float Min = 0.25f;

    /// <summary>The largest scale.</summary>
    public const float Max = 4f;

    /// <summary>The factor of one step.</summary>
    public const float Step = 1.25f;

    /// <summary>The screen pixels per map pixel, 1 at the start.</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>Zooms in by <c>steps</c> steps, or out for a negative count, and keeps the view inside two maps.</summary>
    public void By(int steps, Camera camera, float viewWidth, float viewHeight) =>
        ByFactor(System.MathF.Pow(Step, steps), camera, viewWidth, viewHeight);

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

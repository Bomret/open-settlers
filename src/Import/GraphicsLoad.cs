namespace OpenSettlers.Import;

/// <summary>
/// A graphics source a host has loaded, with what degraded on the way (ADR 0017): a part of the source that did not load
/// is named by its problem text, and the source draws nothing for that part and still draws the rest.
/// </summary>
public sealed record GraphicsLoad
{
    /// <summary>The source the view draws from.</summary>
    public required IGraphicsSource Source { get; init; }

    /// <summary>The walk steps the source's settler graphics parsed, or null where they did not load or no settler is drawn.</summary>
    public IWalkSteps? Steps { get; init; }

    /// <summary>Why the road graphics did not load, or null.</summary>
    public string? RoadsProblem { get; init; }

    /// <summary>Why the object graphics did not load, or null.</summary>
    public string? ObjectsProblem { get; init; }

    /// <summary>Why the settler graphics did not load, or null.</summary>
    public string? SettlersProblem { get; init; }
}

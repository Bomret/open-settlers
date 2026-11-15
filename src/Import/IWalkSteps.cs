namespace OpenSettlers.Import;

/// <summary>How far a walking settler is moved from its node, in map pixels, at one step of its walk.</summary>
public readonly record struct StepOffset
{
    /// <summary>The horizontal offset in map pixels.</summary>
    public required int Dx { get; init; }

    /// <summary>The vertical offset in map pixels.</summary>
    public required int Dy { get; init; }
}

/// <summary>
/// The walk steps port (ADR 0017): the dx and dy of every step of every walk frame, which the placement of a walking settler
/// reads under every graphics source (<c>docs/rules/settlers.md</c>, "Drawing the walk").
/// </summary>
public interface IWalkSteps
{
    /// <summary>
    /// The step of <paramref name="frame"/> with <paramref name="timeLeft"/> clock units left of it, or null where the frame
    /// has no sequence or the time reaches past the end of the table.
    /// </summary>
    StepOffset? At(int frame, int timeLeft);
}

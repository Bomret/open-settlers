namespace OpenSettlers.Import;

/// <summary>
/// A build marker (<c>CONTEXT.md</c>, "Build-site overlay"): the draw term of the sign the build-site overlay shows on a
/// node for what may be put there. It holds the kind alone, no entry number (INV-0010).
/// </summary>
public enum BuildMarker
{
    /// <summary>A flag may be put there.</summary>
    Flag,

    /// <summary>A mine may be put there.</summary>
    Mine,

    /// <summary>A small building may be put there.</summary>
    Small,

    /// <summary>A large building may be put there.</summary>
    Large,
}

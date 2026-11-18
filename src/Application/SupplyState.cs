namespace OpenSettlers.Simulation;

/// <summary>The values of the settler supply that the digest reads, as they stand at a moment.</summary>
public sealed record SupplyState
{
    /// <summary>The ticks of a period, in clock units.</summary>
    public required ushort Period { get; init; }

    /// <summary>The time left to the next settler.</summary>
    public required ushort Countdown { get; init; }

    /// <summary>The clock value at the last turn.</summary>
    public required ushort LastClock { get; init; }

    /// <summary>The knight accumulator.</summary>
    public required ushort KnightAccumulator { get; init; }

    /// <summary>The knights the supply still owes.</summary>
    public required int KnightsOwed { get; init; }

    /// <summary>Whether the supply runs.</summary>
    public required bool Running { get; init; }

    /// <summary>The most settlers the supply makes.</summary>
    public required int Cap { get; init; }

    /// <summary>Whether the supply may make a settler.</summary>
    public required bool MayMake { get; init; }
}

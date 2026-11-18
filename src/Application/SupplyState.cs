namespace OpenSettlers.Simulation;

/// <summary>The values of the settler supply that the digest reads, as they stand at a moment.</summary>
/// <param name="Period">The ticks of a period, in clock units.</param>
/// <param name="Countdown">The time left to the next settler.</param>
/// <param name="LastClock">The clock value at the last turn.</param>
/// <param name="KnightAccumulator">The knight accumulator.</param>
/// <param name="KnightsOwed">The knights the supply still owes.</param>
/// <param name="Running">Whether the supply runs.</param>
/// <param name="Cap">The most settlers the supply makes.</param>
/// <param name="MayMake">Whether the supply may make a settler.</param>
public sealed record SupplyState(
    ushort Period,
    ushort Countdown,
    ushort LastClock,
    ushort KnightAccumulator,
    int KnightsOwed,
    bool Running,
    int Cap,
    bool MayMake
);

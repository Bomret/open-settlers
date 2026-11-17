namespace OpenSettlers.Domain;

/// <summary>What a settler's free walk along a vector is for (<c>docs/rules/woodcutting.md</c>, <c>docs/rules/settlers.md</c>).</summary>
public abstract record Purpose
{
    private protected Purpose() { }
}

/// <summary>The walk of a woodcutter out to the tree it found; the vector is the way back to the hut's flag.</summary>
public sealed record ToTree : Purpose
{
    /// <summary>The steps of the way back, east and south.</summary>
    public required Vector Back { get; init; }
}

/// <summary>The walk of a stonecutter out to the heap he found; the vector is the way back to the hut's flag.</summary>
public sealed record ToHeap : Purpose
{
    /// <summary>The steps of the way back, east and south.</summary>
    public required Vector Back { get; init; }
}

/// <summary>The walk of a forester out to the node he found to plant on; the vector is the way back to the hut's flag.</summary>
public sealed record ToSpot : Purpose
{
    /// <summary>The steps of the way back, east and south.</summary>
    public required Vector Back { get; init; }
}

/// <summary>The walk of a farmer out to the node he found to sow or harvest; the vector is the way back to the farm's flag.</summary>
public sealed record ToField : Purpose
{
    /// <summary>The steps of the way back, east and south.</summary>
    public required Vector Back { get; init; }
}

/// <summary>The walk of a fisher out to the node he found at a shore; the vector is the way back to the building's flag.</summary>
public sealed record ToWater : Purpose
{
    /// <summary>The steps of the way back, east and south.</summary>
    public required Vector Back { get; init; }
}

/// <summary>The way back to the hut's flag after the work, or at once where the work was gone.</summary>
public sealed record BackToHut : Purpose
{
    /// <summary>Whether the worker carries what he laid down at the flag: the woodcutter's log, the fisher's fish.</summary>
    public required bool Carrying { get; init; }
}

/// <summary>The walk home to the nearest flag of the settler's player, as a settler whose work was taken away takes it.</summary>
public sealed record Home : Purpose;

/// <summary>A free walk: the steps left to take, what they are for, and the walk's memory of its last step.</summary>
public sealed record Trip
{
    /// <summary>The steps left, east and south.</summary>
    public required Vector Remaining { get; init; }

    /// <summary>What the walk is for.</summary>
    public required Purpose Purpose { get; init; }

    /// <summary>
    /// The walk's memory (byte 15): the low three bits name the last step's direction plus one, bit 3 marks the vector used up,
    /// and bits 4 to 7 hold a debt of turns (<c>docs/rules/woodcutting.md</c>).
    /// </summary>
    public int Memory { get; init; } = 0;
}

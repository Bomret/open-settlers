namespace OpenSettlers.Domain;

/// <summary>What a settler's free walk along a vector is for (<c>docs/rules/woodcutting.md</c>, <c>docs/rules/settlers.md</c>).</summary>
public abstract record Purpose
{
    private protected Purpose() { }
}

/// <summary>The walk of a woodcutter out to the tree it found; the vector is the way back to the hut's flag.</summary>
/// <param name="Back">The steps of the way back, east and south.</param>
public sealed record ToTree(Vector Back) : Purpose;

/// <summary>The way back to the hut's flag after the cut, or at once where the tree was gone.</summary>
/// <param name="Carrying">Whether the woodcutter carries the log, which it lays on the flag.</param>
public sealed record BackToHut(bool Carrying) : Purpose;

/// <summary>The walk home to the nearest flag of the settler's player, as a settler whose work was taken away takes it.</summary>
public sealed record Home : Purpose;

/// <summary>A free walk: the steps left to take, what they are for, and the walk's memory of its last step.</summary>
/// <param name="Remaining">The steps left, east and south.</param>
/// <param name="Purpose">What the walk is for.</param>
/// <param name="Memory">
/// The walk's memory (byte 15): the low three bits name the last step's direction plus one, bit 3 marks the vector used up,
/// and bits 4 to 7 hold a debt of turns (<c>docs/rules/woodcutting.md</c>).
/// </param>
public sealed record Trip(Vector Remaining, Purpose Purpose, int Memory = 0);

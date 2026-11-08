namespace OpenSettlers.Domain;

/// <summary>
/// A flag of the player's: the node it stands on. The flag's position in the <see cref="FlagTable"/> is its slot in the
/// original's flag table (<c>Table70</c>, <c>docs/rules/flags.md</c>); the same number is the link the node holds in the
/// map's resource byte. A slot whose flag was taken down (<c>docs/rules/removal.md</c>) is free until a new flag takes it.
/// </summary>
public sealed class Flag
{
    /// <summary>A flag on the node at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public Flag(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The node's column; -1 in a free slot.</summary>
    public int X { get; private set; }

    /// <summary>The node's row; -1 in a free slot.</summary>
    public int Y { get; private set; }

    /// <summary>Whether the slot holds no flag.</summary>
    public bool IsFree => X < 0;

    /// <summary>Takes the flag down: the slot has no node.</summary>
    internal void Release()
    {
        X = -1;
        Y = -1;
    }
}

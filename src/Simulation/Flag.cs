namespace OpenSettlers.Simulation;

/// <summary>
/// A flag of the player's: the node it stands on. The flag's position in <see cref="Game.Flags"/> is its slot in the
/// original's flag table (<c>Table70</c>, <c>docs/rules/flags.md</c>); the same number is the link the node holds in
/// the map's <see cref="MapLayer.Resource"/> byte. A slot whose flag was taken down (<c>docs/rules/removal.md</c>) holds
/// <see cref="Free"/> until a new flag takes it.
/// </summary>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
public readonly record struct Flag(int X, int Y)
{
    /// <summary>The content of a slot no flag holds.</summary>
    public static Flag Free { get; } = new(-1, -1);

    /// <summary>Whether the slot holds no flag.</summary>
    public bool IsFree => X < 0;
}

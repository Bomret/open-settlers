namespace OpenSettlers.Domain;

/// <summary>
/// Whose territory a node lies in. The original keeps the owner in the top 3 bits of a node's height byte: 0 for a node
/// nobody owns and the player's number plus 4 for a node inside a player's territory (<c>docs/rules/castle.md</c>).
/// </summary>
/// <param name="Code">The original's owner value, 0 to 7; kept raw so that the exporters write it back as it was.</param>
public readonly record struct Owner(int Code)
{
    /// <summary>The owner of a node nobody owns.</summary>
    public static Owner Nobody => default;

    /// <summary>True for a node nobody owns.</summary>
    public bool IsNobody => Code == 0;

    /// <summary>The owner a player's territory has; a player's number is 0 to 3.</summary>
    public static Owner Player(int player) => new((player & 3) + 4);
}

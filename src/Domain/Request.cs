namespace OpenSettlers.Domain;

/// <summary>
/// What a flag asks for in one of its request words: the class of goods it asks for and the rank of the request, which the searches
/// compare (<c>docs/rules/construction-materials.md</c>). A rank of 0 asks for nothing: no search takes it. A building site's flag
/// asks for planks and stones, in the class and the request word the good class table gives each.
/// </summary>
public readonly record struct Request
{
    /// <summary>The class the flag asks for, or null where the word asks for none.</summary>
    public required GoodClass? Asks { get; init; }

    /// <summary>The rank of the request: the building pass keeps it from what the site holds.</summary>
    public required byte Rank { get; init; }

    /// <summary>A flag that asks for nothing.</summary>
    public static Request None => default;
}

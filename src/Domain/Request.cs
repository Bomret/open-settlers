namespace OpenSettlers.Domain;

/// <summary>
/// What a flag asks for in one of its request words: the classes it asks for (bit k set asks for the goods of class k) and the
/// rank of the request, which the searches compare (<c>docs/rules/construction-materials.md</c>). A rank of 0 asks for nothing: no
/// search takes it. A building site's flag asks for planks (class 1 of the first word) and stones (class 4 of the second).
/// </summary>
/// <param name="Classes">The byte of classes the flag asks for.</param>
/// <param name="Rank">The rank of the request: the building pass keeps it from what the site holds.</param>
public readonly record struct Request(byte Classes, byte Rank)
{
    /// <summary>A flag that asks for nothing.</summary>
    public static Request None => default;

    /// <summary>Whether the request asks for the goods of the class <paramref name="goodClass"/> (the original shifts the byte by the class taken to 0 to 15).</summary>
    public bool Asks(int goodClass) => (Classes >> (goodClass & 0xf) & 1) != 0;
}

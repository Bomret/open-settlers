namespace OpenSettlers.Domain;

/// <summary>
/// The request a good with no destination looks for (the program's table at <c>0x464117</c>, <c>docs/rules/goods.md</c>): the class
/// of the good and the request word whose flags ask for that class. A good the table gives no class goes to storage at once.
/// </summary>
public readonly record struct GoodClass
{
    /// <summary>The class of the good.</summary>
    public required int Class { get; init; }

    /// <summary>The request word of the flag the class is looked for in.</summary>
    public required RequestWord Word { get; init; }
}

namespace OpenSettlers.Domain;

/// <summary>A kind of good (<c>docs/rules/goods.md</c>), by the number the original gives it: 1 to 26, 7 a log; 0 is no good.</summary>
/// <param name="Code">The good's number, 0 to 31.</param>
public readonly record struct Good(int Code)
{
    /// <summary>No good.</summary>
    public static Good None => default;

    /// <summary>Whether this is no good.</summary>
    public bool IsNone => Code == 0;

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

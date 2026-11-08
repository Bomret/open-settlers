namespace OpenSettlers.Domain;

/// <summary>The FNV-1a 64-bit hash, folded one value at a time.</summary>
public static class Fnv1a
{
    /// <summary>The hash of no values.</summary>
    public const ulong Offset = 0xcbf29ce484222325;

    private const ulong Prime = 0x100000001b3;

    /// <summary>The hash after one more value.</summary>
    public static ulong Add(ulong hash, ulong value) => (hash ^ value) * Prime;
}

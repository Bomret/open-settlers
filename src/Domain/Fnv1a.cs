namespace OpenSettlers.Domain;

/// <summary>The FNV-1a 64-bit hash, folded one value at a time.</summary>
public static class Fnv1a
{
    /// <summary>The hash of no values.</summary>
    public const ulong Offset = 0xcbf29ce484222325;

    const ulong Prime = 0x100000001b3;

    /// <summary>The hash after one more value.</summary>
    public static ulong Add(ulong hash, ulong value) => (hash ^ value) * Prime;

    /// <summary>Mixes a map object into the hash by its code.</summary>
    public static ulong Add(ulong hash, MapObject mapObject) => Add(hash, mapObject.Code);

    /// <summary>Mixes a good into the hash by its number.</summary>
    public static ulong Add(ulong hash, Good good) => Add(hash, (ulong)good.Code);
}

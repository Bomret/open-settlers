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

    /// <summary>
    /// The hash of a text: the FNV-1a of its UTF-8 bytes, one byte at a time. The one definition of the hash of a stable name that the
    /// exporters write for an entry with no original code (ADR 0020).
    /// </summary>
    public static ulong Of(string text)
    {
        var hash = Offset;
        foreach (var value in System.Text.Encoding.UTF8.GetBytes(text))
        {
            hash = Add(hash, value);
        }

        return hash;
    }
}

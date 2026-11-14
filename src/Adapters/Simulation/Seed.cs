using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// The free-game seed: 48 bits held as three unsigned 16-bit words. The free-game screen shows and takes it as
/// four groups of four digits <c>1</c> to <c>8</c>, three bits to a digit (<c>docs/rules/map-start.md</c>).
/// </summary>
public readonly record struct Seed
{
    /// <summary>The first of the seed's three 16-bit words.</summary>
    public required ushort W0 { get; init; }

    /// <summary>The second of the seed's three 16-bit words.</summary>
    public required ushort W1 { get; init; }

    /// <summary>The third of the seed's three 16-bit words.</summary>
    public required ushort W2 { get; init; }

    const int Groups = 4;
    const int DigitsPerGroup = 4;

    /// <summary>
    /// Reads the screen form: 16 digits <c>1</c> to <c>8</c>, as the original takes them, or the same digits as
    /// four groups of four separated by single spaces, as the screen shows them. Returns false with the reason
    /// the text is not a seed.
    /// </summary>
    public static bool TryParse(string? screen, out Seed seed, [NotNullWhen(false)] out string? problem)
    {
        seed = default;
        if (string.IsNullOrEmpty(screen))
        {
            problem = "the seed is empty";
            return false;
        }

        var text = screen is [_, _, _, _, ' ', _, _, _, _, ' ', _, _, _, _, ' ', _, _, _, _]
            ? screen.Replace(" ", "")
            : screen;
        if (text.Length != Groups * DigitsPerGroup)
        {
            problem = $"the seed has {text.Length} characters, not {Groups * DigitsPerGroup} characters";
            return false;
        }

        if (text.AsSpan().IndexOfAnyExceptInRange('1', '8') is var at and >= 0)
        {
            problem = $"'{text[at]}' is not a digit from 1 to 8";
            return false;
        }

        problem = null;
        // The original reads the 16 digits as two 24-bit numbers, first digit highest, and stores each as three
        // bytes with the highest byte first. The words read those bytes lowest byte first.
        var digits = text.Select(c => c - '1').ToArray();
        var high = Combine(digits[..8]);
        var low = Combine(digits[8..]);
        seed = new Seed
        {
            W0 = Word(high >> 16, high >> 8),
            W1 = Word(high, low >> 16),
            W2 = Word(low >> 8, low),
        };
        return true;
    }

    /// <summary>
    /// The groups as the free-game screen shows them. The four groups overlap: the screen reads the words
    /// lowest byte first, so a group holds bits of two neighbouring words.
    /// </summary>
    public string ToScreen()
    {
        var first = (uint)W0 | (uint)W1 << 16;
        var second = (uint)W1 | (uint)W2 << 16;
        int[] groups = [W0 >> 4, (int)(first >> 8 & 0xFFF), (int)(second >> 12 & 0xFFF), W2 & 0xFFF];

        return string.Join(' ', groups.Select(Digits));
    }

    /// <summary>The map seed a free game starts with: each word xor a fixed mask (<c>0x468b3d</c>).</summary>
    public MapSeed ToMapSeed() =>
        new()
        {
            W0 = (ushort)(W0 ^ 0x5A5A),
            W1 = (ushort)(W1 ^ 0xA5A5),
            W2 = (ushort)(W2 ^ 0xC3C3),
        };

    static int Combine(int[] digits) => digits.Aggregate(0, (value, digit) => value << 3 | digit);

    static ushort Word(int low, int high) => (ushort)((low & 0xFF) | (high & 0xFF) << 8);

    static string Digits(int group) =>
        new([
            (char)('1' + (group >> 9 & 7)),
            (char)('1' + (group >> 6 & 7)),
            (char)('1' + (group >> 3 & 7)),
            (char)('1' + (group & 7)),
        ]);
}

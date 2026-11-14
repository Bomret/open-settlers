using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.MapView;

/// <summary>The bits of a key code: one mask per modifier, and the mask of the bits that hold the bare key.</summary>
public readonly record struct KeyMasks
{
    /// <summary>The mask of the Shift modifier.</summary>
    public required long Shift { get; init; }

    /// <summary>The mask of the Ctrl modifier.</summary>
    public required long Ctrl { get; init; }

    /// <summary>The mask of the Alt modifier.</summary>
    public required long Alt { get; init; }

    /// <summary>The mask of the Meta modifier.</summary>
    public required long Meta { get; init; }

    /// <summary>The mask of the bits that hold the bare key.</summary>
    public required long Code { get; init; }
}

/// <summary>A key code split into the bare key and the modifier flags.</summary>
public readonly record struct KeyChord
{
    /// <summary>The bare key.</summary>
    public required long Key { get; init; }

    /// <summary>Whether Shift is held.</summary>
    public required bool Shift { get; init; }

    /// <summary>Whether Ctrl is held.</summary>
    public required bool Ctrl { get; init; }

    /// <summary>Whether Alt is held.</summary>
    public required bool Alt { get; init; }

    /// <summary>Whether Meta is held.</summary>
    public required bool Meta { get; init; }
}

/// <summary>The key handling of a play run that needs no window: the split of a key code (<see cref="Split"/>).</summary>
public static class PlayKeys
{
    /// <summary>
    /// Splits <c>code</c>, the code <c>name</c> resolved to, into the bare key and the flags <c>masks</c> name. A code
    /// with no bare key is no key: the result is the problem to report.
    /// </summary>
    public static string? Split(string name, long code, KeyMasks masks, out KeyChord chord)
    {
        var key = code & masks.Code;
        chord = new KeyChord
        {
            Key = key,
            Shift = (code & masks.Shift) != 0,
            Ctrl = (code & masks.Ctrl) != 0,
            Alt = (code & masks.Alt) != 0,
            Meta = (code & masks.Meta) != 0,
        };

        return key == 0 ? $"'{name}' is no Godot key name" : null;
    }
}

/// <summary>The keys a play run holds down; <see cref="ToPressAgain"/> names those the window released.</summary>
public sealed class HeldKeys
{
    readonly HashSet<long> _keys = [];

    /// <summary>Holds <c>key</c>, the bare key, until a step releases it.</summary>
    public void Hold(long key) => _keys.Add(key);

    /// <summary>A released key is no longer held.</summary>
    public void Release(long key) => _keys.Remove(key);

    /// <summary>The held keys <c>isPressed</c> reports as not pressed, to press again as an echo.</summary>
    public IReadOnlyList<long> ToPressAgain(Func<long, bool> isPressed) => _keys.Where(key => !isPressed(key)).ToList();
}

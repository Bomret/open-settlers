namespace OpenSettlers.MapView;

/// <summary>What a key does in the map view.</summary>
public enum MapKeyAction
{
    /// <summary>The key does nothing.</summary>
    None,

    /// <summary>Leave road mode and drop the road being drawn.</summary>
    LeaveRoadMode,

    /// <summary>Zoom in one step.</summary>
    ZoomIn,

    /// <summary>Zoom out one step.</summary>
    ZoomOut,
}

/// <summary>A key the map view reacts to. The view maps each Godot <c>Key</c> to one value, so a rename breaks its build.</summary>
public enum MapKey
{
    /// <summary>Any key the map view does not use.</summary>
    Other,

    /// <summary>Escape.</summary>
    Escape,

    /// <summary>Plus.</summary>
    Plus,

    /// <summary>Equal, the unshifted key that carries Plus.</summary>
    Equal,

    /// <summary>Keypad plus.</summary>
    KeypadAdd,

    /// <summary>Minus.</summary>
    Minus,

    /// <summary>Keypad minus.</summary>
    KeypadSubtract,
}

/// <summary>The map view's key mapping, kept out of the Godot control so the tests run it.</summary>
public static class MapKeys
{
    /// <summary>
    /// The action of a key event. <paramref name="key"/> is the bare key, without modifiers (Godot's <c>Keycode</c>),
    /// so a held Shift or Ctrl changes nothing. A released key, an echo (a repeat of a held key, which a play run
    /// re-presses after a focus loss), and any key while the seed field has focus do nothing, Escape included.
    /// </summary>
    public static MapKeyAction Action(MapKey key, bool pressed, bool echo, bool seedFocused)
    {
        if (!pressed || echo || seedFocused)
        {
            return MapKeyAction.None;
        }

        return key switch
        {
            MapKey.Escape => MapKeyAction.LeaveRoadMode,
            MapKey.Plus or MapKey.Equal or MapKey.KeypadAdd => MapKeyAction.ZoomIn,
            MapKey.Minus or MapKey.KeypadSubtract => MapKeyAction.ZoomOut,
            _ => MapKeyAction.None,
        };
    }
}

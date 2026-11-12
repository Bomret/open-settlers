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

/// <summary>The map view's key mapping, kept out of the Godot control so the tests run it.</summary>
public static class MapKeys
{
    /// <summary>
    /// The action of a key event. <paramref name="key"/> is the bare key (a Godot <c>Key</c> name such as
    /// <c>Equal</c>, without modifiers), so a held Shift or Ctrl changes nothing. A released key, an echo (a repeat of a
    /// held key, which a play run re-presses after a focus loss), and any key while the seed field has focus do
    /// nothing, Escape included.
    /// </summary>
    public static MapKeyAction Action(string key, bool pressed, bool echo, bool seedFocused)
    {
        if (!pressed || echo || seedFocused)
        {
            return MapKeyAction.None;
        }

        return key switch
        {
            "Escape" => MapKeyAction.LeaveRoadMode,
            "Plus" or "Equal" or "KpAdd" => MapKeyAction.ZoomIn,
            "Minus" or "KpSubtract" => MapKeyAction.ZoomOut,
            _ => MapKeyAction.None,
        };
    }
}

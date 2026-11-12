using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Assets;

/// <summary>
/// The check that two preview views do not draw one <see cref="Preview"/> type. The asset browser looks a view up by
/// exact preview type, so a base type and a derived type are no clash. It takes the view's name and not the view, so
/// it holds no Godot type (INV-0004).
/// </summary>
public static class PreviewClash
{
    /// <summary>
    /// The problem with <c>views</c> that stops the browser: two views of one preview type, naming the type and both
    /// views. Null when every preview type has one view.
    /// </summary>
    public static string? Of(IEnumerable<(string View, Type Preview)> views) =>
        views.GroupBy(v => v.Preview).FirstOrDefault(g => g.Count() > 1) is { } twice
            ? $"two preview views draw {twice.Key.Name}: {twice.ElementAt(0).View} and {twice.ElementAt(1).View}; a preview type needs one view"
            : null;
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>
/// The Godot view of one asset kind's <see cref="Preview"/>. A view is a class with a parameterless constructor
/// in its own file; <see cref="Discover"/> finds it, so adding a view edits no shared list. It lives here because
/// <see cref="AssetKind"/> is Godot-free. A kind without a view shows its preview's summary as text.
/// </summary>
public abstract class PreviewView
{
    /// <summary>The <see cref="AssetKind.Name"/> this view draws.</summary>
    public abstract string KindName { get; }

    /// <summary>Builds the control that shows <c>preview</c>, which is of this view's kind.</summary>
    public abstract Control Create(Preview preview);

    /// <summary>The views by kind name; a later view of the same kind name replaces the earlier one.</summary>
    public static IReadOnlyDictionary<string, PreviewView> Discover() =>
        typeof(PreviewView)
            .Assembly.GetTypes()
            .Where(t =>
                t.IsSubclassOf(typeof(PreviewView)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null
            )
            .Select(t => (PreviewView)Activator.CreateInstance(t)!)
            .GroupBy(v => v.KindName)
            .ToDictionary(g => g.Key, g => g.Last());
}

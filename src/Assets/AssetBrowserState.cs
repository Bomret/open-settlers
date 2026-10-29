using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Launch;

namespace OpenSettlers.Assets;

/// <summary>
/// The asset browser's state and selection flow: the categories (one per asset kind), the assets of the selected
/// kind, and the preview of the selected asset. Godot-free; the view draws it and calls the Select methods.
/// </summary>
public sealed class AssetBrowserState
{
    readonly Install? _install;
    readonly IReadOnlyDictionary<string, AssetKind> _kinds;

    AssetBrowserState(string? message, Install? install, IReadOnlyDictionary<string, AssetKind> kinds)
    {
        Message = message;
        _install = install;
        _kinds = kinds;
        Categories = kinds.Keys.ToList();
    }

    /// <summary>
    /// Set when there is no usable install (the message names <c>--game</c> and <c>SIEDLER_DIR</c>) or when two kinds
    /// share a name: the one message the browser shows.
    /// </summary>
    public string? Message { get; }

    /// <summary>The category names, one per kind, sorted by name; empty when <see cref="Message"/> is set.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>The selected kind's name; null when there are no kinds.</summary>
    public string? SelectedKind { get; private set; }

    /// <summary>The assets of the selected kind in the kind's order; empty when it lists none or could not list them.</summary>
    public IReadOnlyList<Asset> Assets { get; private set; } = [];

    /// <summary>
    /// Set when the selected kind lists no assets ("no unrecovered assets") or could not list them (the reason).
    /// </summary>
    public string? Notice { get; private set; }

    /// <summary>The selected asset; null when the kind lists none.</summary>
    public Asset? SelectedAsset { get; private set; }

    /// <summary>The selected asset's preview; null when it failed to load or none is selected.</summary>
    public Preview? Preview { get; private set; }

    /// <summary>The reason the selected asset failed to load; shown in place of a preview.</summary>
    public string? Failure { get; private set; }

    /// <summary>
    /// Opens the install the launch arguments name and selects the first kind and its first asset. Does not throw:
    /// without a usable install, or with two kinds of one name, the state holds <see cref="Message"/> and no categories.
    /// </summary>
    public static AssetBrowserState Open(LaunchArgs args, IEnumerable<AssetKind> kinds)
    {
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            return new AssetBrowserState(problem, null, new Dictionary<string, AssetKind>());
        }

        var sorted = kinds.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
        if (AssetKind.NameClash(sorted) is { } clash)
        {
            return new AssetBrowserState(clash, null, new Dictionary<string, AssetKind>());
        }

        var state = new AssetBrowserState(null, install, sorted.ToDictionary(k => k.Name));
        if (state.Categories.Count > 0)
        {
            state.SelectKind(state.Categories[0]);
        }

        return state;
    }

    /// <summary>
    /// Selects the kind named <c>name</c>, lists its assets, and selects the first. A name that is no category
    /// changes nothing.
    /// </summary>
    public void SelectKind(string name)
    {
        if (_install == null || !_kinds.TryGetValue(name, out var kind))
        {
            return;
        }

        SelectedKind = name;
        Notice = null;
        try
        {
            Assets = kind.Enumerate(_install).ToList();
            if (Assets.Count == 0)
            {
                Notice = $"no {name} assets";
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Assets = [];
            Notice = $"cannot list the {name} assets: {e.Message}";
        }

        SelectAsset(0);
    }

    /// <summary>
    /// Selects the asset at <c>index</c> of <see cref="Assets"/> and loads its preview; a failure to load becomes
    /// <see cref="Failure"/>. An index outside the list changes nothing, except that index 0 of an empty list
    /// clears the selection.
    /// </summary>
    public void SelectAsset(int index)
    {
        if (Assets.Count == 0 && index == 0)
        {
            SelectedAsset = null;
            Preview = null;
            Failure = null;

            return;
        }

        if (index < 0 || index >= Assets.Count)
        {
            return;
        }

        SelectedAsset = Assets[index];
        try
        {
            Preview = SelectedAsset.Load();
            Failure = null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Preview = null;
            Failure = e.Message;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Assets;

/// <summary>
/// One kind of asset. A kind is a class with a parameterless constructor in its own file; <see cref="Discover"/>
/// finds it, so adding a kind edits no shared list. A kind covers the install files it lists: the asset check
/// reads exactly those, and the browser shows them under the kind's name. The Godot preview of a kind is a
/// <c>PreviewView</c> of the same <see cref="Name"/> in <c>src/Viewer</c>.
/// </summary>
public abstract class AssetKind
{
    /// <summary>The kind's category name in the browser and in the check's report; unique among kinds.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Lists the assets of this kind in the install, in the order the browser shows them (file-number or entry
    /// order). A listed file that is missing, or a kind that finds none of its files, is listed as an asset whose
    /// <see cref="Asset.Load"/> throws with the reason. A file the kind cannot index is likewise one asset of that
    /// file whose <see cref="Asset.Load"/> throws, so the report names the file; <c>Enumerate</c> itself throws only
    /// for a failure that belongs to no file. Does not read the assets' contents.
    /// </summary>
    public abstract IEnumerable<Asset> Enumerate(Install install);

    /// <summary>
    /// One instance of every non-abstract <see cref="AssetKind"/> of this assembly that has a parameterless
    /// constructor, sorted by <see cref="Name"/>.
    /// </summary>
    public static IReadOnlyList<AssetKind> Discover() =>
        typeof(AssetKind)
            .Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(AssetKind)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .Select(t => (AssetKind)Activator.CreateInstance(t)!)
            .OrderBy(k => k.Name, StringComparer.Ordinal)
            .ToList();
}

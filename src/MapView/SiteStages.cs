using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>The layers a building site is drawn in (<c>docs/rules/construction.md</c>), each an object of the original's sprite numbering.</summary>
public enum SiteLayer
{
    /// <summary>The ground of a large site that has not started (object 144, <c>0x44fa69</c>).</summary>
    Ground,

    /// <summary>The base of a site that has started, small or large (object 145, <c>0x44fbf9</c>).</summary>
    Base,

    /// <summary>The scaffold of the building's type (the table at <c>0x44fcfe</c>), which grows with the progress.</summary>
    Stage,

    /// <summary>The finished building of the type (the table at <c>0x44fd16</c>), which grows over the scaffold in the second half.</summary>
    Finished,
}

/// <summary>One layer of a building site's drawing.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Fraction">
/// How much of the layer shows, in 65536ths of its height from the bottom up (<see cref="ObjectImage.Grown"/>), or null for the whole.
/// </param>
public readonly record struct SitePart(SiteLayer Layer, int? Fraction);

/// <summary>
/// The drawing of a building site by its progress word, as the common part of the draw routine for buildings under construction
/// (<c>0x44fa79</c>) does it: a progress of 0 draws the ground of a large site; below <c>0x8000</c> the base and, from twice the word
/// being 3 or more, the type's scaffold grown by twice the word; from <c>0x8000</c> the whole scaffold and the finished building grown by
/// twice the word less <c>0x8000</c>, where that is not 0. Both graphics sources draw the layers in this order.
/// </summary>
public static class SiteStages
{
    /// <summary>
    /// The size of the scaffold the site of <paramref name="type"/> grows (the table at <c>0x44fcfe</c>): large for the sawmill, small
    /// for the woodcutter's, stonecutter's, and forester's huts, and null for a type the view draws none for. Both graphics sources
    /// pick the scaffold's art by this rule.
    /// </summary>
    public static bool? ScaffoldIsLarge(BuildingType type) =>
        type == BuildingType.Sawmill ? true
        : type == BuildingType.Woodcutter || type == BuildingType.Stonecutter || type == BuildingType.Forester ? false
        : null;

    /// <summary>The layers the progress word draws, in the order they are drawn.</summary>
    public static IReadOnlyList<SitePart> Parts(ushort progress)
    {
        if (progress == 0)
        {
            return [new SitePart(SiteLayer.Ground, null)];
        }

        if (progress < 0x8000)
        {
            var fraction = (ushort)(2 * progress);

            return fraction < 3
                ? [new SitePart(SiteLayer.Base, null)]
                : [new SitePart(SiteLayer.Base, null), new SitePart(SiteLayer.Stage, fraction)];
        }

        var rest = (ushort)(2 * (progress - 0x8000));

        return rest == 0
            ? [new SitePart(SiteLayer.Stage, null)]
            : [new SitePart(SiteLayer.Stage, null), new SitePart(SiteLayer.Finished, rest)];
    }

    /// <summary>
    /// The drawing of the site the progress word gives: the layers of <see cref="Parts"/> drawn one over another, each taken from
    /// <paramref name="layer"/> (null for a layer the source has no art for, which draws nothing), the images and the shadows joined. Null
    /// where no layer has art.
    /// </summary>
    public static ObjectDrawing? Compose(ushort progress, Func<SiteLayer, ObjectDrawing?> layer)
    {
        ObjectImage? image = null;
        ObjectImage? shadow = null;
        foreach (var part in Parts(progress))
        {
            if (layer(part.Layer) is not { } drawing)
            {
                continue;
            }

            var shown = part.Fraction is { } fraction ? drawing.Image?.Grown(fraction) : drawing.Image;
            image = Join(image, shown);
            shadow = Join(shadow, drawing.Shadow);
        }

        return image is null && shadow is null ? null : new ObjectDrawing(image, shadow);
    }

    static ObjectImage? Join(ObjectImage? below, ObjectImage? above) =>
        below is null ? above
        : above is null ? below
        : below.Over(above);
}

/// <summary>
/// The drawings of the building sites of a graphics source, made once for each type and progress word (a builder's blows give a few
/// dozen words a building): the layers of the progress word drawn one over another (<see cref="SiteStages.Compose"/>), each layer
/// taken from the source's art.
/// </summary>
/// <param name="layer">The source's art for a layer of the site of a type, or null where it has none.</param>
public sealed class SiteDrawings(Func<BuildingType, SiteLayer, ObjectDrawing?> layer)
{
    readonly Dictionary<(BuildingType Type, ushort Progress), ObjectDrawing?> _made = [];

    /// <summary>The drawing of the site of <paramref name="type"/> at <paramref name="progress"/>, or null where no layer has art.</summary>
    public ObjectDrawing? Of(BuildingType type, ushort progress)
    {
        if (!_made.TryGetValue((type, progress), out var cached))
        {
            cached = SiteStages.Compose(progress, part => layer(type, part));
            _made[(type, progress)] = cached;
        }

        return cached;
    }
}

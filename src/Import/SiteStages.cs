using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Import;

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
public readonly record struct SitePart
{
    /// <summary>The layer.</summary>
    public required SiteLayer Layer { get; init; }

    /// <summary>How much of the layer shows, in 65536ths of its height from the bottom up (<see cref="ObjectImage.Grown"/>), or null for the whole.</summary>
    public required int? Fraction { get; init; }
}

/// <summary>
/// The drawing of a building site by its progress word, as the common part of the draw routine for buildings under construction
/// (<c>0x44fa79</c>) does it: a progress of 0 draws the ground of a large site; below <c>0x8000</c> the base and, from twice the word
/// being 3 or more, the type's scaffold grown by twice the word; from <c>0x8000</c> the whole scaffold and the finished building grown by
/// twice the word less <c>0x8000</c>, where that is not 0. Both graphics sources draw the layers in this order.
/// </summary>
public static class SiteStages
{
    /// <summary>
    /// The size of the scaffold the site of <paramref name="type"/> grows (the table at <c>0x44fcfe</c>): large for the types whose size
    /// is large (the sawmill, the farm, the butcher, the pig farm, the baker), small for the huts (the woodcutter's, stonecutter's, forester's,
    /// fisher's, and mill's), and null for a type the view draws none for. The open-settlers source picks the scaffold's art by this
    /// rule; the History Edition source reads the object of each type from the table.
    /// </summary>
    public static bool? ScaffoldIsLarge(BuildingType type) =>
        type == BuildingType.Sawmill
        || type == BuildingType.Farm
        || type == BuildingType.Butcher
        || type == BuildingType.PigFarm
        || type == BuildingType.Baker
            ? true
        : type == BuildingType.Woodcutter
        || type == BuildingType.Stonecutter
        || type == BuildingType.Forester
        || type == BuildingType.Fisher
        || type == BuildingType.Mill
            ? false
        : null;

    /// <summary>The layers the progress word draws, in the order they are drawn.</summary>
    public static IReadOnlyList<SitePart> Parts(ushort progress)
    {
        if (progress == 0)
        {
            return [new SitePart { Layer = SiteLayer.Ground, Fraction = null }];
        }

        if (progress < 0x8000)
        {
            var fraction = (ushort)(2 * progress);

            return fraction < 3
                ? [new SitePart { Layer = SiteLayer.Base, Fraction = null }]
                :
                [
                    new SitePart { Layer = SiteLayer.Base, Fraction = null },
                    new SitePart { Layer = SiteLayer.Stage, Fraction = fraction },
                ];
        }

        var rest = (ushort)(2 * (progress - 0x8000));

        return rest == 0
            ? [new SitePart { Layer = SiteLayer.Stage, Fraction = null }]
            :
            [
                new SitePart { Layer = SiteLayer.Stage, Fraction = null },
                new SitePart { Layer = SiteLayer.Finished, Fraction = rest },
            ];
    }

    /// <summary>
    /// The layers the progress word of the castle under construction draws (<c>docs/rules/construction.md</c>): the castle's stub
    /// <c>0x44fd2f</c> jumps to <c>0x44fee2</c>, which draws the finished castle grown by the word itself, not doubled, over its whole shadow.
    /// The castle never reaches the site routine <c>0x44fa79</c>, so it draws no ground, base, or scaffold.
    /// </summary>
    public static IReadOnlyList<SitePart> CastleParts(ushort progress) =>
        [new SitePart { Layer = SiteLayer.Finished, Fraction = progress }];

    /// <summary>
    /// The drawing of <paramref name="parts"/> (<see cref="Parts"/> or <see cref="CastleParts"/>) drawn one over another, each layer taken
    /// from <paramref name="layer"/> (null for a layer the source has no art for, which draws nothing), the images and the shadows joined.
    /// Null where no layer has art.
    /// </summary>
    public static ObjectDrawing? Compose(IReadOnlyList<SitePart> parts, Func<SiteLayer, ObjectDrawing?> layer)
    {
        ObjectImage? image = null;
        ObjectImage? shadow = null;
        foreach (var part in parts)
        {
            if (layer(part.Layer) is not { } drawing)
            {
                continue;
            }

            var shown = part.Fraction is { } fraction ? drawing.Image?.Grown(fraction) : drawing.Image;
            image = Join(image, shown);
            shadow = Join(shadow, drawing.Shadow);
        }

        return image is null && shadow is null ? null : new ObjectDrawing { Image = image, Shadow = shadow };
    }

    static ObjectImage? Join(ObjectImage? below, ObjectImage? above) =>
        below is null ? above
        : above is null ? below
        : below.Over(above);
}

/// <summary>
/// The drawings of the building sites of a graphics source, made once for each type and progress word (a builder's blows give a few
/// dozen words a building): the layers of the progress word drawn one over another (<see cref="SiteStages.Parts"/>, and
/// <see cref="SiteStages.CastleParts"/> for the castle under construction), each layer taken from the source's art.
/// </summary>
/// <param name="layer">The source's art for a layer of the site of a type, or null where it has none.</param>
public sealed class SiteDrawings(Func<BuildingType, SiteLayer, ObjectDrawing?> layer)
{
    readonly Dictionary<(BuildingType Type, ushort Progress), ObjectDrawing?> _made = [];

    /// <summary>The drawing of the site of <paramref name="type"/>, or of the castle under construction, at <paramref name="progress"/>, or null where no layer has art.</summary>
    public ObjectDrawing? Of(BuildingType type, ushort progress)
    {
        if (!_made.TryGetValue((type, progress), out var cached))
        {
            var parts = type == BuildingType.Castle ? SiteStages.CastleParts(progress) : SiteStages.Parts(progress);
            cached = SiteStages.Compose(parts, part => layer(type, part));
            _made[(type, progress)] = cached;
        }

        return cached;
    }
}

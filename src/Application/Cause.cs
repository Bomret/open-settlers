using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// Why a change happened (<c>CONTEXT.md</c>, ADR 0011): the player's command that started the chain of changes
/// (<see cref="PlacedKind"/> or <see cref="RazedKind"/>, with the ID of the thing commanded), the rule area that acted on its
/// own where no command started it (<see cref="Settlers"/>, <see cref="Carriers"/>, ...), or <see cref="Layout"/> for the
/// entries of a layout.
/// </summary>
/// <param name="Kind">The name of the cause: <c>placed</c>, <c>razed</c>, a rule area, or <c>layout</c>.</param>
/// <param name="Thing">The thing the command was given for; null for a rule area and for a layout.</param>
public sealed record Cause(string Kind, ThingId? Thing = null)
{
    /// <summary>The kind of a place command.</summary>
    public const string PlacedKind = "placed";

    /// <summary>The kind of a raze command.</summary>
    public const string RazedKind = "razed";

    /// <summary>The kind of a layout's entry.</summary>
    public const string LayoutKind = "layout";

    /// <summary>The rule area of the castle's settlers, the buildings' requests, and the walk to a door (<c>settlers</c>).</summary>
    public static Cause Settlers { get; } = new("settlers");

    /// <summary>The rule area of the roads' carriers (<c>carriers</c>).</summary>
    public static Cause Carriers { get; } = new("carriers");

    /// <summary>The rule area of the woodcutter's work (<c>woodcutting</c>).</summary>
    public static Cause Woodcutting { get; } = new("woodcutting");

    /// <summary>The rule area of the stonecutter's work (<c>stonecutting</c>).</summary>
    public static Cause Stonecutting { get; } = new("stonecutting");

    /// <summary>The rule area of the forester's work (<c>forestry</c>).</summary>
    public static Cause Forestry { get; } = new("forestry");

    /// <summary>The rule area of the walk home to a storage (<c>walk-home</c>).</summary>
    public static Cause WalkHome { get; } = new("walk-home");

    /// <summary>The rule area of the goods on the flags (<c>goods</c>).</summary>
    public static Cause Goods { get; } = new("goods");

    /// <summary>The rule area of taking things down (<c>removal</c>).</summary>
    public static Cause Removal { get; } = new("removal");

    /// <summary>The cause of a layout's entry (<c>layout</c>).</summary>
    public static Cause Layout { get; } = new(LayoutKind);

    /// <summary>Every kind a cause has, for a check of a name.</summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [
        PlacedKind,
        RazedKind,
        "settlers",
        "carriers",
        "woodcutting",
        "stonecutting",
        "walk-home",
        "goods",
        "removal",
        LayoutKind,
        "forestry",
    ];

    /// <summary>The cause of a place command for <paramref name="thing"/>.</summary>
    public static Cause Placed(ThingId thing) => new(PlacedKind, thing);

    /// <summary>The cause of a raze command for <paramref name="thing"/>.</summary>
    public static Cause Razed(ThingId thing) => new(RazedKind, thing);

    /// <summary>Whether a player's command is the cause; only such a cause a settler carries on (<see cref="GameHistory.CarryCause"/>).</summary>
    public bool IsCommand => Kind is PlacedKind or RazedKind;

    /// <summary>The kind, then the thing commanded where there is one: <c>placed building#1.1</c>.</summary>
    public override string ToString() => Thing is { } thing ? $"{Kind} {thing}" : Kind;
}

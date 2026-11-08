using System.Globalization;

namespace OpenSettlers.Simulation;

/// <summary>
/// The name a settler, building, or flag keeps for its whole life (<c>CONTEXT.md</c>, ADR 0011): its kind, its slot, and
/// the count of how often that slot was filled, as in <c>settler#12.3</c>. A slot filled again gives the new thing the next
/// count, so no ID is handed out twice in one game.
/// </summary>
/// <param name="Kind"><see cref="SettlerKind"/>, <see cref="BuildingKind"/>, or <see cref="FlagKind"/>.</param>
/// <param name="Slot">The slot in <see cref="Game.Settlers"/>, <see cref="Game.Buildings"/>, or <see cref="Game.Flags"/>.</param>
/// <param name="Fill">How often the slot was filled, from 1.</param>
public readonly record struct ThingId(string Kind, int Slot, int Fill)
{
    /// <summary>The kind of a settler.</summary>
    public const string SettlerKind = "settler";

    /// <summary>The kind of a building.</summary>
    public const string BuildingKind = "building";

    /// <summary>The kind of a flag.</summary>
    public const string FlagKind = "flag";

    /// <summary>
    /// A map object's ID: its name and its node, as in <c>tree@12,4</c>. A map object does not move, so its node names it;
    /// an event that names one holds this text.
    /// </summary>
    public static string OfObject(string name, int x, int y) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}@{x},{y}");

    /// <summary>The ID as text: <c>&lt;kind&gt;#&lt;slot&gt;.&lt;fill&gt;</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Kind}#{Slot}.{Fill}");
}

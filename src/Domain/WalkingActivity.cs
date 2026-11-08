namespace OpenSettlers.Domain;

/// <summary>What the activities of a settler that walks the roads have in common.</summary>
public static class WalkingActivity
{
    /// <summary>
    /// The heading of a settler that walks the roads (state 2), a carrier on its road (state 3), or one that hands a good over
    /// (state 0xe); null for any other activity.
    /// </summary>
    public static Heading? HeadingOf(Activity activity) =>
        activity switch
        {
            Walks walks => walks.Heading,
            WalksRoad road => road.Heading,
            HandsOver handover => handover.Heading,
            _ => null,
        };

    /// <summary>The good a carrier carries on its road or hands over; null for a carrier with none and for any other activity.</summary>
    public static Load? LoadOf(Activity activity) =>
        activity switch
        {
            WalksRoad road => road.Load,
            HandsOver handover => handover.Load,
            _ => null,
        };

    /// <summary>The activity of a walking settler with the count of its blocked tries, or the mark that it is to go, cleared (byte 15 = 0).</summary>
    public static Activity Cleared(Activity activity) =>
        activity switch
        {
            Walks walks => walks with { Tries = 0 },
            WalksRoad road => road with { Waits = 0, Marked = false },
            HandsOver handover => handover with { Marked = false },
            _ => activity,
        };
}

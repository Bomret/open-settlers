namespace OpenSettlers.MapView;

/// <summary>The line the map view shows over the map: why part of it is not drawn, or empty.</summary>
public static class ViewMessage
{
    /// <summary>
    /// The first problem, in this order: the graphics (no map is drawn), the map view's state, the objects, the roads,
    /// and the settlers. Empty where there is none.
    /// </summary>
    public static string Of(string? graphics, string? state, string? objects, string? roads, string? settlers) =>
        graphics is not null ? $"Cannot draw the map: {graphics}"
        : state is not null ? state
        : objects is not null ? $"Cannot draw the objects: {objects}"
        : roads is not null ? $"Cannot draw the roads: {roads}"
        : settlers is not null ? $"Cannot draw the settlers: {settlers}"
        : "";
}

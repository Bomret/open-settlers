namespace OpenSettlers.Simulation;

/// <summary>
/// The exporter of the typed building: it writes the original's status byte, worker word, fill word, and type index (the
/// building table's item, <c>Table18</c>) for the digest, the event snapshots, and the readouts (ADR 0013).
/// </summary>
public static class BuildingEncoder
{
    const int RequestFailedBit = 0x4;
    const int DemolishingBit = 0x20;
    const int WorkerInsideBit = 0x40;
    const int WorkerOnTheWayBit = 0x80;
    const int ConstructingBit = 0x20;

    /// <summary>The status byte (byte 5): the building's flags and the bits with no recovered meaning.</summary>
    public static int Status(Building building) =>
        building.OtherStatus
        | (building.RequestFailed ? RequestFailedBit : 0)
        | (building.IsDemolishing ? DemolishingBit : 0)
        | (building.WorkerInside ? WorkerInsideBit : 0)
        | (building.WorkerOnTheWay ? WorkerOnTheWayBit : 0);

    /// <summary>The word at 10: the slot of the worker inside, or the countdown of a building that burns.</summary>
    public static int Worker(Building building) => building.IsDemolishing ? building.BurnLeft : building.Worker;

    /// <summary>The fill word (bytes 8 and 9): the counters of the goods the building takes in.</summary>
    public static int Fill(Building building) => building.FillLow | (building.FillHigh << 8);

    /// <summary>The type as the tables of the walk index it: the type, plus 0x20 while the building is built.</summary>
    public static int TypeIndex(Building building) =>
        building.Type.Code | (building.Constructing ? ConstructingBit : 0);
}

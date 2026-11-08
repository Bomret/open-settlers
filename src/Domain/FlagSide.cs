namespace OpenSettlers.Domain;

/// <summary>
/// One of the six sides of a flag, the side a road leaves it by in a direction. A side with no road keeps the values the
/// last road left in it until a new road takes the side (the original's flag item keeps its bytes).
/// </summary>
public sealed class FlagSide
{
    /// <summary>The length class of the road, written at both its ends.</summary>
    public LengthClass Length { get; internal set; }

    /// <summary>Whether a carrier is on the way to the road.</summary>
    public bool CarrierOnTheWay { get; internal set; }

    /// <summary>The number of carriers on the road.</summary>
    public int Carriers { get; internal set; }

    /// <summary>Whether the flag's last turn saw a carrier on the road (the road's bit of the flag's status).</summary>
    public bool HoldsCarrier { get; internal set; }

    /// <summary>The direction in which the road leaves the flag at its far end.</summary>
    public Direction FarDirection { get; internal set; }

    /// <summary>The place of the good that waits to go along the road, or null where none waits.</summary>
    public int? WaitingPlace { get; internal set; }

    /// <summary>
    /// Gives the side a new length class: the carriers on the road count again from none, and whether a carrier is on the
    /// way is as given.
    /// </summary>
    public void SetLength(LengthClass length, bool carrierOnTheWay)
    {
        Length = length;
        CarrierOnTheWay = carrierOnTheWay;
        Carriers = 0;
    }
}

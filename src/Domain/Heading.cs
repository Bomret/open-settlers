namespace OpenSettlers.Domain;

/// <summary>
/// The direction a walking settler would step back to the node it came from. A step that was blocked is tried again at the
/// settler's next turn and marks it by the direction less 6 (<see cref="Retried"/>), so a heading is a direction, or a
/// direction a retry marker has gone over.
/// </summary>
/// <param name="Value">The direction, 0 to 5, or the direction less 6 for a step tried again.</param>
public readonly record struct Heading(int Value)
{
    /// <summary>The heading of a settler that came by the node in <paramref name="direction"/> from the one it stands on.</summary>
    public static Heading Facing(Direction direction) => new(direction.Value);

    /// <summary>Whether the step is tried again at the next turn.</summary>
    public bool IsRetried => Value < 0;

    /// <summary>The direction the heading names, with a retry marker taken off.</summary>
    public Direction Direction => new(Value < 0 ? Value + Direction.Count : Value);

    /// <summary>The heading marked for a step tried again: the direction less 6.</summary>
    public Heading Retried => new(Value - Direction.Count);

    /// <summary>
    /// The heading a settler takes from the retried heading it kept in the low byte of its stamp, with 6 added back (the
    /// carrier that waits next to a flag turns back along the road it came by: <c>0x42ff81</c>).
    /// </summary>
    public Heading Unmarked => new((byte)((byte)Value + Direction.Count));

    /// <summary>
    /// The heading opposite the one a settler kept as a retried heading in the low byte of its stamp: the carrier that waits
    /// next to the flag at the far end of its road turns round (<c>0x42ff81</c>; checked against the emulator's fetches in
    /// <c>road-splits.txt</c>, where the carrier came by 0 and by 3).
    /// </summary>
    public Heading Turned
    {
        get
        {
            var turned = (sbyte)Value + Direction.Count / 2;

            return new((byte)(turned < 0 ? turned + Direction.Count : turned));
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

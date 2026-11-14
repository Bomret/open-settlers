namespace OpenSettlers.Simulation;

/// <summary>
/// One step of a settler on a road (the step routines at <c>0x43a64c</c> to <c>0x43ac8b</c>), shared by the walk to an errand and
/// the carriers' walk. A step east, south-east, or south onto a settler that comes the other way and is blocked passes it.
/// </summary>
public sealed class RoadStep(SettlerMovement movement, SettlerTable settlers, GameHistory history, Causes causes)
{
    // The activity of a walking settler with a new heading.
    static Activity WithHeading(Activity activity, Heading heading) =>
        activity switch
        {
            Walks walks => walks with { Heading = heading },
            WalksRoad road => road with { Heading = heading },
            _ => activity,
        };

    // A step clears byte 15 of a settler in state 2 (0x43a64c); the carrier's step in state 3 (0x43b788 and its siblings)
    // leaves it, so the carrier's count of turns at the end of its road and its mark to go outlast its steps.
    static Activity Cleared(Activity activity) => activity is Walks ? WalkingActivity.Cleared(activity) : activity;

    /// <summary>One step on a road; false where the settler waits out the rest of the step's time or the step is blocked.</summary>
    public bool Step(int slot, Settler settler, int direction)
    {
        var tables = movement.Tables;
        var here = movement.Map.Index(settler.X, settler.Y);
        var (x, y, next) = movement.Neighbour(settler, direction);
        int shape;
        var other = movement.Occupant(next);
        var forward = Heading.Facing(new Direction(direction).Opposite);
        if (other == 0)
        {
            shape = 4 + 9 * direction + movement.Height(next) - movement.Height(here);
            settler.Activity = Cleared(WithHeading(settler.Activity, forward));
            movement.Move(slot, settler, x, y, next);
        }
        else if (
            direction <= 2
            && settlers[other] is { Activity: Walks or WalksRoad } passer
            && WalkingActivity.HeadingOf(passer.Activity) == forward.Retried
        )
        {
            // A step east, south-east, or south onto a settler that walks (state 2 or 3) and is blocked on this very node (its
            // heading holds the opposite direction less 6): the two pass, each taking the node of the other (0x43a6c1 and its
            // siblings; the steps west, north-west, and north have no such branch). The settler goes on with the shape of its
            // swap step (the shapes 4 + 9 * (direction + 6) and up), the other on with the one of its opposite step, and its
            // timer is that step's time.
            var rise = movement.Height(next) - movement.Height(here);
            var back = 4 + 9 * ((direction + 3) % 6) - rise;
            passer.X = settler.X;
            passer.Y = settler.Y;
            movement.Occupy(here, other);
            passer.Activity = WithHeading(passer.Activity, Heading.Facing(direction));
            passer.Frame = (byte)back;
            passer.Timer = tables.StepTime(back);
            history.RecordSettler(
                other,
                EventType.Stepped,
                causes.OfSettler(other),
                history.SettlerId(slot).ToString()
            );
            shape = 4 + 9 * (direction + 6) + rise;
            settler.Activity = WithHeading(settler.Activity, forward);
            settler.X = x;
            settler.Y = y;
            movement.Occupy(next, slot);
        }
        else
        {
            // The settler there waits, or waits for this node. The settler marks the direction it tried in its heading
            // (the direction less 6) and tries it again when its timer of 0x7f has run out (0x43a791).
            settler.Timer = 0x7f;
            settler.Frame = (byte)(0x51 + direction);
            settler.Activity = WithHeading(settler.Activity, Heading.Facing(direction).Retried);
            history.RecordSettler(slot, EventType.Stepped, causes.OfSettler(slot));

            return false;
        }

        settler.Activity = Cleared(settler.Activity);
        var old = settler.Timer;
        settler.Timer = (ushort)(old + tables.StepTime(shape));
        var finished = settler.Timer < old;
        if (finished)
        {
            settler.Frame = (byte)shape;
        }

        history.RecordSettler(slot, EventType.Stepped, causes.OfSettler(slot));

        return !finished;
    }
}

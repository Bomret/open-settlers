using System;
using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

// Taking down a flag, a road, or a building (docs/rules/removal.md): the click routines of the original, the check 0x44b6e6 that
// says what a node is, the flag's removal 0x462153, the road's take-down 0x462e1d with 0x463204, the building's 0x4617fd with the
// release of its goods 0x4312ed, and the states the settlers take (0x19 and 0x1c walk home, 0x44 and 0x45 for a carrier that
// waited); and the split of a road by a flag put on one of its nodes (docs/rules/roads.md#a-flag-on-a-road): Road_SplitAtFlag
// 0x4662e2 with the walk Road_GatherSettlers 0x463a40 (Gather) and the join of the flag to each end flag, Road_LinkHalf 0x466719.
// The split is the inverse of the join of a flag taken down (JoinRoads). **Not ported:** what the player's own block holds (the
// per-player request words, the counts of the buildings), the storage item's request fields (0x4312ed's last loop), the
// territory a military building or the castle held, the boatmen of the water roads, and the click on the castle (the check gives
// it class 0, so the click is refused).

/// <summary>
/// The removal: the click class of a node (the remove tool's command for it is the game's),the take-down of a flag, a building, or a road, the dismissal and the rejoining of the
/// settlers on a road that goes, the count of the goods that are lost, the burning of a building taken down, and the split of a
/// road by a flag. It handles the activities <see cref="WaitsToGoHome"/> and <see cref="WaitsToRejoin"/>.
/// </summary>
/// <param name="movement">The settlers' ground: the node of each settler, the timer, the change of activity.</param>
/// <param name="settlers">The settler table.</param>
/// <param name="buildings">The building table.</param>
/// <param name="flags">The flag table.</param>
/// <param name="roads">The roads between the flags.</param>
/// <param name="history">The history the removal records its changes in (no rule reads it).</param>
/// <param name="carriers">The carriers' side of a road that goes or is split.</param>
/// <param name="goods">The rank of a good among those that wait for a road.</param>
/// <param name="beginWalkHome">The settler in the slot is on the walk home.</param>
/// <param name="hasCastle">Whether the castle is placed: the click on a node is refused before.</param>
/// <param name="forgetPlacement">The building's thing no longer carries the cause of the command that placed it.</param>
public sealed class Removal(
    SettlerMovement movement,
    SettlerTable settlers,
    BuildingTable buildings,
    FlagTable flags,
    RoadNetwork roads,
    GameHistory history,
    IRoadCarriers carriers,
    ITransportPriorities goods,
    Action<int> beginWalkHome,
    Func<bool> hasCastle,
    Action<ThingId> forgetPlacement
)
{
    // The command being applied that takes things down, joins roads, or splits one, for the cause of the events its changes record
    // (ADR 0011): the raze command, or the flag placement that splits a road (a layout's entry has the layout cause); null between
    // commands. Never read to decide a change (INV-0007).
    private Cause? _raze;

    // The raze cause of each building taken down, for its burn and free events ticks later.
    private readonly Dictionary<ThingId, Cause> _razedBuildings = [];

    private Map Map => movement.Map;

    private Cause RazeCause => _raze ?? Cause.Removal;

    /// <summary>Registers the behaviour of the activities of the carriers that a road taken down or joined leaves waiting.</summary>
    public void Register(ActivityRegistry registry)
    {
        registry.Register<WaitsToGoHome>((slot, settler, _) => Dismissed(slot, settler));
        registry.Register<WaitsToRejoin>((slot, settler, _) => Rejoined(slot, settler));
    }

    /// <summary>
    /// What the original's check <c>0x44b6e6</c> says a click on the node is, for the classes the removal acts on: 1 a flag the
    /// click refuses, 2 a flag it takes down, 3 a building, 4 a node of a road that is no flag; 0 for every other node (the check
    /// gives 5 to 7 for nodes where something may be built).
    /// </summary>
    public int ClickClass(int x, int y) => ClickClass(Map.Index(x, y));

    /// <summary>
    /// The command a click of the remove tool on the node asks for: <see cref="CommandKind.RemoveFlag"/> for a flag the original
    /// takes down, <see cref="CommandKind.RemoveBuilding"/> for a building that stands, <see cref="CommandKind.RemoveRoad"/>
    /// for a node of a road, and null where the original's check refuses the click.
    /// </summary>
    public int? CommandAt(int x, int y) =>
        ClickClass(x, y) switch
        {
            2 => CommandKind.RemoveFlag,
            3 => CommandKind.RemoveBuilding,
            4 => CommandKind.RemoveRoad,
            _ => null,
        };

    private int ClickClass(int index)
    {
        if (!hasCastle() || Map.At(index).Owner != Owner.Player(0))
        {
            return 0;
        }

        var nodeRoads = Map.At(index).Roads;
        var objectClass = movement.Tables.ClassOf(Map.At(index).Object);
        if (objectClass.IsFlag)
        {
            var (x, y) = Position(index);
            var (dx, dy) = Direction.Steps[4];
            // A flag with a building at its north-west (the road mark in direction 4) is refused; so is a flag with roads
            // unless it has two, each a road of land, to different flags.
            if (nodeRoads.Has(4) && movement.Tables.ClassOf(Map.At(x + dx, y + dy).Object).IsBuilding)
            {
                return 1;
            }

            if (!nodeRoads.IsEmpty)
            {
                var slot = Map.At(index).LinkedSlot;
                var count = 0;
                var far = -1;
                for (var direction = 5; direction >= 0; direction--)
                {
                    if (roads.RoadAt(slot, direction) is not { } road)
                    {
                        continue;
                    }

                    var other = RoadNetwork.FarEnd(road, slot, direction).Flag;
                    if (road.Water || (far >= 0 && far == other))
                    {
                        return 1;
                    }

                    count++;
                    far = other;
                }

                if (count != 2)
                {
                    return 1;
                }
            }

            return 2;
        }

        if (objectClass.IsBuilding)
        {
            return !objectClass.IsCastle && movement.BuildingAt(index) is { IsDemolishing: false } ? 3 : 0;
        }

        return !nodeRoads.IsEmpty && !nodeRoads.IsOnly(1) && !nodeRoads.IsOnly(4) ? 4 : 0;
    }

    private (int X, int Y) Position(int index) => (index % Map.Size.Width, index / Map.Size.Width);

    // The node a step in a direction leads to.
    private int NodeAfter(int index, int direction)
    {
        var (x, y) = Position(index);
        var (dx, dy) = Direction.Steps[direction];

        return Map.Index(x + dx, y + dy);
    }

    // The direction of a settler's heading as the walk reads it: a negative heading marks a blocked step and gives the direction
    // less 6.
    private static int Back(Settler settler) => WalkingActivity.HeadingOf(settler.Activity)?.Direction ?? 0;

    // The errand of a settler whose target is a flag (the states 2, 15, and 5 or 7 on their way to state 2): 0x4312ed and
    // 0x462153 send them home when what they walk to is gone. The original also tests state 1, a settler that waits in the
    // castle; its target is 0 there, and no flag has slot 0 (the dummy of the flag table), so the test never hits. The engine's
    // slot 0 is the castle's flag, so the test is left out. Null for a settler that goes on no errand.
    private static Errand? HeadsTo(Settler settler) => settler.Activity.GoingTo;

    // Whether the settler walks to the road that leaves the flag in the direction.
    private static bool HeadsToRoad(Settler settler, int flag, int direction) =>
        HeadsTo(settler) is ToRoad road && road.At == flag && road.Direction == direction;

    // The settler's walk is turned home: it goes on to a storage whatever it was walking to.
    private void SendHome(int walker, Settler settler)
    {
        settler.Activity = settler.Activity.Redirected(new ToStorage());
        beginWalkHome(walker);
    }

    // The cause the building at the slot was taken down with, removal for a building no raze took down.
    private Cause RazedCauseOf(int slot) => _razedBuildings.GetValueOrDefault(history.BuildingId(slot), Cause.Removal);

    // Records the settler's event: it carries the raze command's cause on until it idles (History.CarryCause); a change ticks after the
    // command takes the cause the settler already carries, else removal.
    private void Touch(int slot, string type, string? other = null)
    {
        if (_raze is { IsCommand: true } command)
        {
            history.CarryCause(slot, command);
        }

        history.RecordSettler(slot, type, RazeCause, other);
    }

    private void TouchFlag(int slot, string type, string? other = null) =>
        history.RecordFlag(slot, type, RazeCause, other);

    private void TouchBuilding(Building building, string type) =>
        history.RecordBuilding(buildings.SlotOf(building), type, RazeCause);

    /// <summary>The flag on the node is taken down where the click allows it; false (and nothing changed) where it does not.</summary>
    public bool RemoveFlagAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 2)
        {
            return false;
        }

        _raze = Cause.Razed(history.FlagId(Map.At(index).LinkedSlot));
        TakeFlagDown(index);
        _raze = null;

        return true;
    }

    /// <summary>The building on the node is taken down where the click allows it; false (and nothing changed) where it does not.</summary>
    public bool RemoveBuildingAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 3 || movement.BuildingAt(index) is not { } building)
        {
            return false;
        }

        _raze = Cause.Razed(history.BuildingId(buildings.SlotOf(building)));
        TakeBuildingDown(building);
        _raze = null;

        return true;
    }

    /// <summary>The road through the node is taken down where the click allows it; false (and nothing changed) where it does not.</summary>
    public bool RemoveRoadAt(int x, int y)
    {
        var index = Map.Index(x, y);
        if (ClickClass(index) != 4)
        {
            return false;
        }

        var nodeRoads = Map.At(index).Roads;
        if (RoadThrough(index) is not { } road || nodeRoads.Count < 2)
        {
            return false;
        }

        var (first, second) = RoadBits(nodeRoads);

        // A road has no ID: its cause names the flag it leaves from.
        _raze = Cause.Razed(history.FlagId(road.FromSlot));
        TakeRoadDown(index, first);
        TakeRoadDown(index, second);
        roads.Remove(road);
        _raze = null;
        carriers.ForgetCause(road);

        return true;
    }

    // The take-down and the split walk from a node along its first two road bits (0x462e1d, 0x4662e2); a second bit found at 4
    // with bit 5 set is taken for 5.
    private static (int First, int Second) RoadBits(RoadSet nodeRoads)
    {
        var first = nodeRoads.First;
        var second = first + 1;
        while (!nodeRoads.Has(second))
        {
            second++;
        }

        return (first, second == 4 && nodeRoads.Has(5) ? 5 : second);
    }

    // The road that passes a node that holds no flag.
    private Road? RoadThrough(int index)
    {
        foreach (var road in roads.Roads)
        {
            var node = Map.Index(flags[road.FromSlot].X, flags[road.FromSlot].Y);
            for (var step = 0; step < road.Directions.Count - 1; step++)
            {
                node = NodeAfter(node, road.Directions[step]);
                if (node == index)
                {
                    return road;
                }
            }
        }

        return null;
    }

    // 0x463204: the road's bits are taken from the node on in one direction until the flag at its end; the settlers on the way
    // are dismissed, and the flag at the end loses its road (EndOfRoad).
    private void TakeRoadDown(int index, int direction)
    {
        DismissWaiting(index);
        var standing = movement.Occupant(index);
        if (standing != 0 && settlers[standing].Activity is Walks or WalksRoad && Back(settlers[standing]) == direction)
        {
            Dismiss(standing);
        }

        Map.At(index).Roads = Map.At(index).Roads.Without(direction);
        while (true)
        {
            index = NodeAfter(index, direction);
            var back = (direction + 3) % 6;
            Map.At(index).Roads = Map.At(index).Roads.Without(back);
            var rest = Map.At(index).Roads;
            if (Map.At(index).FlagMark)
            {
                EndOfRoad(index, back);

                return;
            }

            if (rest.IsEmpty)
            {
                return;
            }

            direction = rest.First;
            Map.At(index).Roads = rest.Without(direction);
            DismissWaiting(index);
            standing = movement.Occupant(index);
            if (standing != 0 && settlers[standing].Activity is Walks or WalksRoad)
            {
                Dismiss(standing);
            }
        }
    }

    // The flag at the end of a road that was taken down (0x463204 from 0x46358c): a settler on the flag that came along the road
    // is dismissed; the flag's item loses the road's carrier bits, the settlers on their way to the road's end go home, and the
    // goods that were to go along the road lose that direction.
    private void EndOfRoad(int index, int back)
    {
        var slot = Map.At(index).LinkedSlot;
        var standing = movement.Occupant(index);
        if (
            standing != 0
            && settlers[standing].Activity is HandsOver or WalksRoad or Walks
            && Back(settlers[standing]) == back
        )
        {
            Dismiss(standing);
        }

        var side = flags[slot].Side(back);
        var carrierOnTheWay = side.CarrierOnTheWay;
        side.CarrierOnTheWay = false;
        side.HoldsCarrier = false;
        if (carrierOnTheWay)
        {
            for (var walker = 0; walker < settlers.Count; walker++)
            {
                if (HeadsToRoad(settlers[walker], slot, back))
                {
                    SendHome(walker, settlers[walker]);
                    Touch(walker, EventType.EndOfRoad, history.FlagId(slot).ToString());
                }
            }
        }

        side.WaitingPlace = null;
        for (var place = Flag.PlaceCount - 1; place >= 0; place--)
        {
            var good = flags[slot].Places[place];
            if (!good.IsEmpty && good.Way == back)
            {
                good.Way = null;
                flags[slot].NeedsGoodsTurn = true;
            }
        }

        TouchFlag(slot, EventType.EndOfRoad);
    }

    // 0x46385b (the boatman's state 0x1a is not ported): a settler on a road that goes is dismissed. One on its way to a building
    // at a flag frees the building's wait for a worker; one on its way to a road frees the road's carrier-on-the-way bits; one
    // that carries a good counts it lost. It then looks for the nearest flag on the way home (state 0x19).
    // **Inferred:** the carrier dismissed while it carries a good is read from the code; no emulator or debugger run reaches
    // this. Open lead in docs/milestones/m3-first-loop.md (a carrier carrying a good on a road that goes).
    private void Dismiss(int slot)
    {
        var settler = settlers[slot];
        if (settler.Activity is Walks walks)
        {
            if (walks.Errand is ToBuilding toBuilding)
            {
                if (buildings.OfFlag(toBuilding.At) is { } building)
                {
                    var onTheWay = building.WorkerOnTheWay;
                    building.WorkerOnTheWay = false;
                    if (!onTheWay && building.FillLow != 0xff)
                    {
                        building.SetFill(FillCounter.Low, building.FillLow - 1);
                    }

                    TouchBuilding(building, EventType.Dismiss);
                }
            }
            else if (walks.Errand is ToRoad toRoad)
            {
                flags[toRoad.At].Side(toRoad.Direction).CarrierOnTheWay = false;
                TouchFlag(toRoad.At, EventType.Dismiss);
                if (roads.RoadAt(toRoad.At, toRoad.Direction) is { } road)
                {
                    var (far, farDirection) = RoadNetwork.FarEnd(road, toRoad.At, toRoad.Direction);
                    flags[far].Side(farDirection).CarrierOnTheWay = false;
                    TouchFlag(far, EventType.Dismiss);
                }
            }
        }
        else if (WalkingActivity.LoadOf(settler.Activity) is { } load)
        {
            CountLost(load.Good, load.Destination);
        }

        movement.Become(settler, new SearchesHome());
        beginWalkHome(slot);
        Touch(slot, EventType.Dismiss);
    }

    // 0x463ef9: the carriers that wait at a node next to a flag (the states 0x42, 0x43, 0x45) are told to go home (state 0x44).
    private void DismissWaiting(int index)
    {
        for (var slot = 0; slot < settlers.Count; slot++)
        {
            var settler = settlers[slot];
            if (Map.Index(settler.X, settler.Y) != index)
            {
                continue;
            }

            if (settler.Activity is WaitsToGoHome)
            {
                return;
            }

            if (settler.Activity is WaitsOffRoad waiting)
            {
                movement.Become(settler, new WaitsToGoHome(waiting.Approach));
                Touch(slot, EventType.DismissWaiting);

                return;
            }
        }
    }

    // 0x464008: a carrier that waits at a node next to a flag is taken with the settlers on the road (state 0x45); its slot, or -1.
    private int TakeWaiting(int index)
    {
        for (var slot = 0; slot < settlers.Count; slot++)
        {
            var settler = settlers[slot];
            if (Map.Index(settler.X, settler.Y) != index)
            {
                continue;
            }

            switch (settler.Activity)
            {
                case WaitsAtFlag or ClaimsNode:
                    movement.Become(settler, new WaitsToRejoin(((WaitsOffRoad)settler.Activity).Approach));
                    Touch(slot, EventType.TakeWaiting);

                    return slot;
                case WaitsToRejoin:
                    return slot;
                case WaitsToGoHome:
                    return -1;
            }
        }

        return -1;
    }

    // State 0x44 (0x43007b): a dismissed carrier that waited takes the node it stands on where no settler stands and goes
    // home (state 0x19); else it tries again at its next turn.
    private void Dismissed(int slot, Settler settler)
    {
        var node = Map.Index(settler.X, settler.Y);
        if (movement.Occupant(node) != 0)
        {
            return;
        }

        movement.Occupy(node, slot);
        settler.Stamp = movement.Clock;
        settler.Timer = 0;
        movement.Become(settler, new SearchesHome());
        beginWalkHome(slot);
        Touch(slot, EventType.Dismissed);
    }

    // State 0x45 (0x4300f1): a carrier that waited at the end of a road that was joined takes state 0x43 with the highest road
    // direction of its node as its heading, and runs that state at once.
    private void Rejoined(int slot, Settler settler)
    {
        var approach = ((WaitsToRejoin)settler.Activity).Approach;
        movement.Become(settler, new ClaimsNode(Heading.Facing(Map.At(settler.X, settler.Y).Roads.Last), approach));
        Touch(slot, EventType.Rejoined);
        carriers.Claim(slot, settler);
    }

    // 0x462c8d: a good that is lost (carried by a settler who is dismissed, or lying on a flag that is taken down) is counted back.
    // The goods 14 and 15 count against the total of resource 1; a good that has a kind counts against the byte of the fill of
    // the building at its destination flag, except the buildings with a storage. **Not ported:** a destination flag with no
    // building, where the original reads a null pointer.
    private void CountLost(Good good, int? destination)
    {
        if (good.CountsAgainstResource1)
        {
            Map.Resource1Total--;
        }

        if (
            destination is { } flag
            && buildings.CountOneLess(flag, movement.Tables.FillCounterOf(good)) is { } building
        )
        {
            TouchBuilding(building, EventType.CountLost);
        }
    }

    private sealed record Walked(int Steps, List<int> Settlers, int Far, int FarDirection);

    // 0x463a40: the walk along the road that leaves the flag in a direction, to the flag at its end. It counts the steps, takes
    // the settlers on the road (a carrier in state 3 that is not marked to go, at the first flag and at the far flag only when
    // it came along the road; a carrier that waits next to a flag takes state 0x45), and gives the far flag and the direction
    // the road leaves it in.
    private Walked Gather(int index, int direction)
    {
        var gathered = new List<int>();
        DismissWaiting(index);
        var standing = movement.Occupant(index);
        if (
            standing != 0
            && settlers[standing] is { Activity: WalksRoad { Marked: false } } first
            && Back(first) == direction
        )
        {
            first.Activity = WalkingActivity.Cleared(first.Activity);
            Touch(standing, EventType.Gather);
            gathered.Add(standing);
        }

        var steps = 0;
        while (true)
        {
            steps++;
            index = NodeAfter(index, direction);
            var back = (direction + 3) % 6;
            var rest = Map.At(index).Roads.Without(back);
            if (Map.At(index).FlagMark)
            {
                standing = movement.Occupant(index);
                if (
                    standing != 0
                    && settlers[standing] is { Activity: HandsOver or WalksRoad { Marked: false } } last
                    && Back(last) == back
                )
                {
                    last.Activity = WalkingActivity.Cleared(last.Activity);
                    Touch(standing, EventType.Gather);
                    gathered.Add(standing);
                }

                return new Walked(steps, gathered, Map.At(index).LinkedSlot, back);
            }

            direction = rest.First;
            if (TakeWaiting(index) is var waiting and >= 0)
            {
                gathered.Add(waiting);
            }

            standing = movement.Occupant(index);
            if (standing != 0 && settlers[standing] is { Activity: WalksRoad { Marked: false } } passing)
            {
                passing.Activity = WalkingActivity.Cleared(passing.Activity);
                Touch(standing, EventType.Gather);
                gathered.Add(standing);
            }
        }
    }

    // The directions of a road walked from one of its flags, the road leaving that flag in a direction.
    private static List<Direction> PathFrom(Road road, int flag, int direction) =>
        road.FromSlot == flag && road.FromDirection == direction
            ? [.. road.Directions]
            : [.. road.Directions.Reverse().Select(step => step.Opposite)];

    /// <summary>
    /// 0x462153: the flag on the node is taken down with no check of the click (public for the test of the join of two water
    /// roads, whose flag the original's click refuses). A flag with roads (the check allows two) joins its two roads into one
    /// between the flags at their far ends; the flag then goes as a flag with no road does: the node loses its flag mark, object,
    /// and link, the settlers on their way to it go home, the goods that lie on it are counted lost, and its slot is free.
    /// </summary>
    public void TakeFlagDown(int index)
    {
        var slot = Map.At(index).LinkedSlot;
        var standing = movement.Occupant(index);
        if (standing != 0)
        {
            var settler = settlers[standing];
            if (settler.Activity is WaitsOutStep or StepsOntoFlag)
            {
                // The step's wait ends in the search for a flag home instead of the activity it was for.
                settler.Activity = settler.Activity switch
                {
                    WaitsOutStep wait => wait with { Then = movement.HomeSearchAfterStep(wait.Then) },
                    StepsOntoFlag step => step with { Then = movement.HomeSearchAfterStep(step.Then) },
                    var other => other,
                };
                Touch(standing, EventType.TookFlagDown);
            }
            else if (settler.Activity is Walks && Map.At(index).Roads.IsEmpty)
            {
                movement.Become(settler, movement.HomeSearchFrom(settler.Activity));
                beginWalkHome(standing);
                Touch(standing, EventType.TookFlagDown);
            }
        }

        if (!Map.At(index).Roads.IsEmpty)
        {
            JoinRoads(index, slot);
        }

        Map.At(index).FlagMark = false;
        for (var walker = 0; walker < settlers.Count; walker++)
        {
            if (HeadsTo(settlers[walker])?.Flag == slot)
            {
                SendHome(walker, settlers[walker]);
                Touch(walker, EventType.TookFlagDown, history.FlagId(slot).ToString());
            }
        }

        Map.At(index).Object = MapObject.None;
        Map.At(index).ClearLink();
        for (var place = 7; place >= 0; place--)
        {
            var good = flags[slot].Places[place];
            if (!good.IsEmpty)
            {
                CountLost(good.Good, good.Destination);
            }
        }

        // The event that ends the flag, recorded while the slot still holds it. Both events carry the same state: the spec records
        // one event per rule change, and the second one is the end of the slot.
        TouchFlag(slot, EventType.TookFlagDown);
        TouchFlag(slot, EventType.FreeFlagSlot);
        // Frees the flag's slot (0x45d974): the item keeps its values until a new flag takes the slot; the table's count falls
        // past the free slots at its end.
        flags.Free(slot);
    }

    // The settlers on a road count on it up to the capacity of its length class (the table 0x462acd, less one for a road that
    // a carrier is on its way to, in the split): the status bit is set at both ends and the number goes into the low nibble of
    // both road bytes; a capacity below 0 counts as 0 (the all-zero table RuleTables falls back to with no install, less
    // one; the original's table has no 0); the settlers beyond the capacity, taken from the start of the list, are marked to
    // go (the next state 0xff, a waiting carrier takes state 0x44) and a good they carry is lost. A road with no
    // settler keeps its bytes.
    private void CountCarriers(List<int> onRoad, (int Flag, int Direction) a, (int Flag, int Direction) b, int capacity)
    {
        if (onRoad.Count == 0)
        {
            return;
        }

        flags[a.Flag].Side(a.Direction).HoldsCarrier = true;
        flags[b.Flag].Side(b.Direction).HoldsCarrier = true;
        var kept = onRoad.Count;
        if (capacity < onRoad.Count)
        {
            kept = Math.Max(capacity, 0);
            var excess = onRoad.Count - kept;
            foreach (var carrierSlot in onRoad)
            {
                var carrier = settlers[carrierSlot];
                if (carrier.Activity is WaitsToRejoin rejoining)
                {
                    movement.Become(carrier, new WaitsToGoHome(rejoining.Approach));
                }
                else
                {
                    // The carrier is marked to go and drops its good; its item keeps the good's destination (the residue).
                    var load = WalkingActivity.LoadOf(carrier.Activity);
                    movement.Become(
                        carrier,
                        carrier.Activity switch
                        {
                            WalksRoad road => road with { Marked = true, Waits = 0, Load = null },
                            HandsOver hands => hands with { Marked = true, Load = null },
                            var other => other,
                        }
                    );
                    if (load is { } lost)
                    {
                        CountLost(lost.Good, lost.Destination);
                    }
                }

                if (--excess == 0)
                {
                    break;
                }
            }
        }

        flags[a.Flag].Side(a.Direction).Carriers = kept;
        flags[b.Flag].Side(b.Direction).Carriers = kept;
    }

    // The join of the two roads of a flag (0x462153): one road of the summed length between the flags at the far ends. The
    // settlers on the old roads count on the new one (CountCarriers). The new road is a water road where both old roads are
    // (the take-down writes no land bit at the far flags, so each far flag keeps the bit of its own road, and the original's
    // click only takes down a flag whose roads are land; the engine keeps one kind for the road).
    // **Inferred:** the case of more settlers than the length class holds (the mark 0xff) is read from the code; no emulator or
    // debugger run reaches it. Open lead in docs/milestones/m3-first-loop.md (more settlers on a joined road than its class holds).
    private void JoinRoads(int index, int slot)
    {
        // **Inferred:** bit 4 with no road there is the mark of a hut standing at the flag (PlaceBuilding), no road bit. Nothing
        // shows that the original (0x462153) masks it so: its click refuses a flag with a hut (ClickClass) and a hut's removal
        // clears the mark first, so only a direct TakeFlagDown reaches this. A guard for a state the original's input never reaches.
        var nodeRoads = roads.RoadAt(slot, 4) is null ? Map.At(index).Roads.Without(4) : Map.At(index).Roads;
        if (nodeRoads.IsEmpty)
        {
            return;
        }

        var first = nodeRoads.First;
        var last = nodeRoads.Last;

        if (roads.RoadAt(slot, first) is not { } roadA || roads.RoadAt(slot, last) is not { } roadB)
        {
            return;
        }

        var a = Gather(index, first);
        var b = Gather(index, last);
        var path = PathFrom(roadA, slot, first).AsEnumerable().Reverse().Select(step => step.Opposite).ToList();
        path.AddRange(PathFrom(roadB, slot, last));
        roads.Remove(roadA);
        roads.Remove(roadB);
        carriers.ForgetCause(roadA);
        carriers.ForgetCause(roadB);
        roads.Add(new Road(a.Far, a.FarDirection, b.Far, b.FarDirection, path, roadA.Water && roadB.Water));

        var sideA = flags[a.Far].Side(a.FarDirection);
        var sideB = flags[b.Far].Side(b.FarDirection);
        sideA.FarDirection = b.FarDirection;
        sideB.FarDirection = a.FarDirection;
        sideA.HoldsCarrier = false;
        sideB.HoldsCarrier = false;
        var length = LengthClass.OfSteps(path.Count);
        sideA.SetLength(length, carrierOnTheWay: false);
        sideB.SetLength(length, carrierOnTheWay: false);
        var onRoad = a.Settlers.Concat(b.Settlers).ToList();
        CountCarriers(
            onRoad,
            (a.Far, a.FarDirection),
            (b.Far, b.FarDirection),
            movement.Tables.RoadCapacity(length.Value)
        );

        // The flags at the ends of the new road, naming each other, and the carriers now on it, naming its two flags.
        TouchFlag(a.Far, EventType.JoinRoads, history.FlagId(b.Far).ToString());
        TouchFlag(b.Far, EventType.JoinRoads, history.FlagId(a.Far).ToString());
        foreach (var carrierSlot in onRoad)
        {
            Touch(carrierSlot, EventType.JoinRoads, $"{history.FlagId(a.Far)} {history.FlagId(b.Far)}");
        }

        for (var walker = 0; walker < settlers.Count; walker++)
        {
            var settler = settlers[walker];
            if (HeadsToRoad(settler, a.Far, a.FarDirection) || HeadsToRoad(settler, b.Far, b.FarDirection))
            {
                SendHome(walker, settler);
                Touch(walker, EventType.JoinRoads);
            }
        }
    }

    // 0x4617fd for a building that is no military building and not the castle: the building is marked taken down (it burns for
    // 0x7ff clock units, Burn), its node and its flag lose the marks of the building, its worker walks home, the goods that wait
    // for it lose their destination (ReleaseGoodsFor), and its flag goes where no road leaves it.
    private void TakeBuildingDown(Building building)
    {
        var flag = flags[building.Flag];
        var flagNode = Map.Index(flag.X, flag.Y);
        building.IsDemolishing = true;
        ref var hutNode = ref Map.At(building.X, building.Y);
        hutNode.Roads = hutNode.Roads.Without(1);
        Map.At(flagNode).Roads = Map.At(flagNode).Roads.Without(4);
        building.OtherStatus &= ~0x18;
        building.FillLow = 0;
        building.FillHigh = 0;
        if (building.Site is { } site)
        {
            // The byte of the planks a site needs (+0x10) is cleared with the fills; the stones it needs stay, and nothing reads them.
            building.Site = site with
            {
                Planks = 0,
            };
        }

        // The flag's request words lose their classes (0x4617fd clears the bytes at flag +0x42 and +0x44; the ranks stay), so no
        // search finds the flag any more.
        foreach (var word in new[] { RequestWord.First, RequestWord.Second })
        {
            flag.SetRequest(word, flag.RequestOf(word) with { Asks = null });
        }

        var worker = building.Worker;
        building.Worker = 0;
        building.BurnLeft = 0x7ff;
        building.Stamp = movement.Clock;
        var wasWorking = building.WorkerInside;
        building.WorkerInside = false;
        _razedBuildings[history.BuildingId(buildings.SlotOf(building))] = RazeCause;
        TouchBuilding(building, EventType.TookBuildingDown);
        if (wasWorking)
        {
            SendWorkerHome(worker);
        }

        ReleaseGoodsFor(building.Flag);
        if (Map.At(flagNode).Roads.IsEmpty && Map.At(flagNode).Object.Kind == MapObjectKind.Flag)
        {
            TakeFlagDown(flagNode);
        }
    }

    // The worker inside a building that is taken down: a settler of the job 4 loses its job; it walks home from where it stands
    // (state 0x19 where it holds its node, else state 0x1c to claim it).
    private void SendWorkerHome(int slot)
    {
        var settler = settlers[slot];
        var residue = movement.ResidueOf(settler.Activity);
        if (settler.Job == Job.CastleSettler)
        {
            // The settler keeps its identity and takes the carrier's job; the mode byte of its item is cleared.
            settler.Job = Job.Carrier;
            residue = movement.ResidueOf(settler.Activity) with { Mode = 0 };
        }

        settler.Timer = 0;
        if (movement.Occupant(Map.Index(settler.X, settler.Y)) == slot)
        {
            movement.Become(settler, new SearchesHome(), residue);
            beginWalkHome(slot);
        }
        else
        {
            movement.Become(settler, new ClaimsHomeNode(), residue);
        }

        Touch(slot, EventType.SendWorkerHome);
    }

    // 0x4312ed: what waits for the building at a flag is released. A settler on its way to the flag (the mode below 0) goes home;
    // a carrier that carries a good there loses the destination; a good on any flag that was to go to the flag loses its
    // destination and its direction, and the flag gets its turn again. **Not ported:** the loop over the storage items.
    private void ReleaseGoodsFor(int flag)
    {
        for (var walker = 0; walker < settlers.Count; walker++)
        {
            var settler = settlers[walker];
            if (HeadsTo(settler) is (ToBuilding or ToStorage) and { } errand && errand.Flag == flag)
            {
                SendHome(walker, settler);
                Touch(walker, EventType.ReleaseGoodsFor, history.FlagId(flag).ToString());
            }
            else if (
                settler.Activity is WalksRoad { Load: { Destination: { } destination } load } road
                && destination == flag
            )
            {
                settler.Activity = road with { Load = load with { Destination = null } };
                Touch(walker, EventType.ReleaseGoodsFor, history.FlagId(flag).ToString());
            }
        }

        for (var holder = 0; holder < flags.Count; holder++)
        {
            var released = false;
            for (var place = 7; place >= 0; place--)
            {
                var good = flags[holder].Places[place];
                if (good.IsEmpty || good.Destination != flag)
                {
                    continue;
                }

                released = true;
                good.Destination = null;
                flags[holder].NeedsGoodsTurn = true;
                if (good.Way is not { } way)
                {
                    continue;
                }

                good.Way = null;
                var best = 0;
                var bestPlace = -1;
                for (var other = 0; other < Flag.PlaceCount; other++)
                {
                    var candidate = flags[holder].Places[other];
                    if (!candidate.IsEmpty && candidate.Way == way && best < goods.TransportPriority(candidate.Good))
                    {
                        best = goods.TransportPriority(candidate.Good);
                        bestPlace = other;
                    }
                }

                flags[holder].Side(way).WaitingPlace = bestPlace >= 0 ? bestPlace : null;
            }

            if (released)
            {
                TouchFlag(holder, EventType.ReleaseGoodsFor, history.FlagId(flag).ToString());
            }
        }
    }

    /// <summary>
    /// The building pass for a building that burns (0x42cb2d): the time since its last turn comes off the countdown of 0x7ff in
    /// its worker word, and when that wraps the building is gone.
    /// </summary>
    public void Burn(int slot, Building building)
    {
        var elapsed = (ushort)(movement.Clock - building.Stamp);
        building.Stamp = movement.Clock;
        var left = building.BurnLeft;
        building.BurnLeft = (ushort)(left - elapsed);
        history.RecordBuilding(slot, EventType.Burn, RazedCauseOf(slot));
        if (left < elapsed)
        {
            FreeBuilding(slot);
        }
    }

    // 0x42cf9d for a building without a territory: the node loses its lake mark, object, and link, and the slot is free; the
    // table's count falls past the free slots at its end.
    private void FreeBuilding(int slot)
    {
        var building = buildings[slot];
        var node = Map.Index(building.X, building.Y);
        ref var freed = ref Map.At(node);
        freed.LakeMark = false;
        freed.Object = MapObject.None;
        freed.ClearLink();
        // The event that ends the building, recorded while the slot still holds it.
        history.RecordBuilding(slot, EventType.FreeBuilding, RazedCauseOf(slot));
        _razedBuildings.Remove(history.BuildingId(slot));
        forgetPlacement(history.BuildingId(slot));
        buildings.Free(slot);
    }

    /// <summary>
    /// The flag in the slot has just been put on the node of a road: the road becomes two roads that meet at the flag, and the
    /// settlers on it go to the half they stand on or walk to (Gather and the carrier on its way). The half that holds no
    /// carrier asks the castle for one at the next flag pass of its flags, with a request that takes the cause. A node that holds
    /// no road changes nothing.
    /// </summary>
    public void SplitRoad(int index, int slot, Cause cause)
    {
        if (RoadThrough(index) is not { } road)
        {
            return;
        }

        _raze = cause;
        var (first, second) = RoadBits(Map.At(index).Roads);
        var a = Gather(index, first);
        var b = Gather(index, second);

        // A carrier on its way to the road (marked at the end reached along the second direction) keeps the half
        // at the end flag it targets; the other end loses the mark. **Inferred:** with no such settler the end reached along the
        // second direction loses it.
        if (flags[b.Far].Side(b.FarDirection).CarrierOnTheWay)
        {
            var onItsWay = settlers.FirstOrDefault(settler =>
                HeadsToRoad(settler, a.Far, a.FarDirection) || HeadsToRoad(settler, b.Far, b.FarDirection)
            );
            var loser = onItsWay is not null && HeadsToRoad(onItsWay, b.Far, b.FarDirection) ? a : b;
            flags[loser.Far].Side(loser.FarDirection).CarrierOnTheWay = false;
        }

        // The half on the side of the road's start, and the steps the road took before the node.
        var fromSide = a.Far == road.FromSlot && a.FarDirection == road.FromDirection;
        var (toStart, toEnd) = fromSide ? (a, b) : (b, a);
        var (startDirection, endDirection) = fromSide ? (first, second) : (second, first);
        var steps = toStart.Steps;
        roads.Remove(road);
        var halves = new[]
        {
            new Road(
                road.FromSlot,
                road.FromDirection,
                slot,
                startDirection,
                [.. road.Directions.Take(steps)],
                road.Water
            ),
            new Road(slot, endDirection, road.ToSlot, road.ToDirection, [.. road.Directions.Skip(steps)], road.Water),
        };
        foreach (var half in halves)
        {
            roads.Add(half);
        }

        LinkHalf(slot, first, a);
        LinkHalf(slot, second, b);

        // A half that holds no carrier and none on its way is asked for at the next flag pass, with the cause of this command.
        carriers.ForgetCause(road);
        // The cause only attributes the request (INV-0007): a request is made with or without it.
        if (cause.IsCommand)
        {
            foreach (var (half, direction) in new[] { (halves[0], startDirection), (halves[1], endDirection) })
            {
                if (flags[slot].Side(direction) is { CarrierOnTheWay: false, Carriers: 0 })
                {
                    carriers.CarryCause(half, cause);
                }
            }
        }

        // The new flag, naming the far flags of the halves; each far flag, naming the new flag; the carriers on a half, naming
        // its two flags.
        TouchFlag(slot, EventType.SplitRoads, $"{history.FlagId(a.Far)} {history.FlagId(b.Far)}");
        foreach (var half in new[] { a, b })
        {
            TouchFlag(half.Far, EventType.SplitRoads, history.FlagId(slot).ToString());
            foreach (var carrierSlot in half.Settlers)
            {
                Touch(carrierSlot, EventType.SplitRoads, $"{history.FlagId(half.Far)} {history.FlagId(slot)}");
            }
        }

        _raze = null;
    }

    // 0x466719: the new flag's end of a half and the far flag's end. The new flag's end is cleared and takes the mark of a
    // carrier on its way from the far flag's end, which keeps only that mark; the half's length class goes into both; the
    // directions of the two ends name each other; the carriers on the half count on it (CountCarriers) up to the capacity of
    // its length class, one less where a carrier is on its way to it.
    private void LinkHalf(int flag, int direction, Walked half)
    {
        var (far, farDirection) = (half.Far, half.FarDirection);
        var side = flags[flag].Side(direction);
        var farSide = flags[far].Side(farDirection);
        var onItsWay = farSide.CarrierOnTheWay;
        var length = LengthClass.OfSteps(half.Steps);
        side.SetLength(length, onItsWay);
        farSide.SetLength(length, onItsWay);
        side.FarDirection = farDirection;
        farSide.FarDirection = direction;
        side.HoldsCarrier = false;
        farSide.HoldsCarrier = false;
        CountCarriers(
            half.Settlers,
            (flag, direction),
            (far, farDirection),
            movement.Tables.RoadCapacity(length.Value) - (onItsWay ? 1 : 0)
        );
    }
}

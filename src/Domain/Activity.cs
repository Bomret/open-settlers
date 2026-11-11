namespace OpenSettlers.Domain;

/// <summary>
/// What a settler does at one moment, with exactly the data that doing it needs: waiting in the castle, walking along a
/// road, searching for a tree, walking home. A settler has one activity at a time and leaves it for the next. Each
/// activity type keeps the <see cref="Code"/> of the original's settler state it stands for, so the exporters, the name table,
/// and the readouts show it raw (ADR 0013, INV-0006).
/// </summary>
public abstract record Activity
{
    private protected Activity() { }

    /// <summary>The code of the original's settler state (byte 10 of the settler's item) that the activity stands for.</summary>
    public abstract int Code { get; }

    /// <summary>
    /// The bytes of the original's item that the activity does not use, kept as they were so that they are exported again
    /// exactly (<see cref="Domain.Residue"/>). No rule reads or writes them.
    /// </summary>
    public Residue Residue { get; init; }

    /// <summary>Where the activity takes the settler along the roads, or null where it takes it nowhere along them.</summary>
    public virtual Errand? GoingTo => null;

    /// <summary>The same activity going to <paramref name="errand"/> instead; an activity that goes nowhere is unchanged.</summary>
    public virtual Activity Redirected(Errand errand) => this;

    /// <summary>The same activity with <paramref name="residue"/> as the bytes it leaves unused.</summary>
    public Activity Carrying(Residue residue) => this with { Residue = residue };
}

/// <summary>State 1: a settler waits in the castle; each turn it enters its slot in the castle's list of waiting settlers.</summary>
/// <param name="Storage">The slot of the storage the settler waits in; the castle is 0.</param>
public sealed record WaitsInCastle(int Storage = 0) : Activity
{
    /// <inheritdoc />
    public override int Code => 1;
}

/// <summary>An activity that takes the settler along the roads to an errand: a walk, or the way out of the castle to it.</summary>
/// <param name="Errand">Where the activity leads.</param>
public abstract record HasErrand(Errand Errand) : Activity
{
    /// <inheritdoc />
    public override Errand? GoingTo => Errand;

    /// <inheritdoc />
    public override Activity Redirected(Errand errand) => this with { Errand = errand };
}

/// <summary>State 2: the settler walks the roads to <see cref="Errand"/>.</summary>
/// <param name="Errand">Where the walk leads.</param>
/// <param name="Heading">The direction back to the node the settler came from.</param>
/// <param name="Tries">The turns spent on a blocked step (byte 15), counted from 0.</param>
public sealed record Walks(Errand Errand, Heading Heading, int Tries = 0) : HasErrand(Errand)
{
    /// <inheritdoc />
    public override int Code => 2;
}

/// <summary>State 3: a carrier walks its road, with a good in its hands or none.</summary>
/// <param name="Load">The good the carrier carries, or null.</param>
/// <param name="Heading">The direction back to the node the carrier came from.</param>
/// <param name="Waits">The turns the carrier has spent at the end of a road that holds two carriers.</param>
/// <param name="Marked">Whether the carrier is marked to go: its road was taken down or joined.</param>
public sealed record WalksRoad(Load? Load, Heading Heading, int Waits = 0, bool Marked = false) : Activity
{
    /// <inheritdoc />
    public override int Code => 3;
}

/// <summary>State 4: the settler has stepped through a door and waits out what the door's speed leaves of the step.</summary>
/// <param name="TimeLeft">The time the door step has left, where the settler goes on.</param>
/// <param name="EnteringStorage">Whether the settler came home to a storage and enters it (the walk home).</param>
public sealed record Door(int TimeLeft, bool EnteringStorage = false) : Activity
{
    /// <inheritdoc />
    public override int Code => 4;
}

/// <summary>An activity of a step whose time runs out before the settler takes up <see cref="Then"/>.</summary>
/// <param name="Then">The activity the settler takes when the step's time has run out.</param>
public abstract record StepsBefore(Activity Then) : Activity
{
    /// <inheritdoc />
    public override Errand? GoingTo => Then.GoingTo;

    /// <inheritdoc />
    public override Activity Redirected(Errand errand) => this with { Then = Then.Redirected(errand) };
}

/// <summary>State 5: the settler waits out a step and then takes up <see cref="StepsBefore.Then"/>.</summary>
/// <param name="Then">The activity the settler takes when the step's time has run out.</param>
public sealed record WaitsOutStep(Activity Then) : StepsBefore(Then)
{
    /// <inheritdoc />
    public override int Code => 5;
}

/// <summary>State 6: the settler stands on the flag of the building it walks into and waits for the node of the door.</summary>
/// <param name="Storage">Whether the building is a storage the settler walks home to.</param>
public sealed record EntersBuilding(bool Storage = false) : Activity
{
    /// <inheritdoc />
    public override int Code => 6;
}

/// <summary>State 7: the settler steps from a hut onto its flag, and then takes up <see cref="StepsBefore.Then"/>.</summary>
/// <param name="Then">The activity the settler takes when the step's time has run out.</param>
public sealed record StepsOntoFlag(Activity Then) : StepsBefore(Then)
{
    /// <inheritdoc />
    public override int Code => 7;
}

/// <summary>
/// State 9: the builder works in the building site he stands in (<c>0x43ec82</c>, <c>docs/rules/construction.md</c>): he takes the site's
/// materials one after the other in the order of the type's order word, and for each makes eight blows that add a step to the site's
/// progress word.
/// </summary>
/// <param name="Site">The slot of the building site in the building table (the word at 12).</param>
/// <param name="Blowing">Whether he is making the blows of a material (byte 11 is 0xff) and not looking for the next one (byte 11 is 1).</param>
/// <param name="Materials">The materials he has taken so far, the index of the next in the order (the low four bits of byte 14).</param>
/// <param name="SecondFigure">Whether his figure is the second one the types 10, 17, 19, and 22 take (bit 7 of byte 14, the frames 0x64 and 0x6a to 0x6d).</param>
/// <param name="Blows">The blows left of the material (byte 15).</param>
public sealed record Builds(int Site, bool Blowing, int Materials, bool SecondFigure, int Blows) : Activity
{
    /// <inheritdoc />
    public override int Code => 9;
}

/// <summary>
/// State 0x4a: the builder has finished the building and waits at its door until the node of the flag is free, then steps onto the flag
/// to go home (<c>0x43e008</c>).
/// </summary>
public sealed record LeavesSite : Activity
{
    /// <inheritdoc />
    public override int Code => 0x4a;
}

/// <summary>State 10: the settler stands by while the castle is built.</summary>
public sealed record BuildsCastle : Activity
{
    /// <inheritdoc />
    public override int Code => 0xa;
}

/// <summary>State 12: the settler has finished standing by at the castle. The original's next steps are not ported.</summary>
public sealed record BuiltCastle : Activity
{
    /// <inheritdoc />
    public override int Code => 0xc;
}

/// <summary>State 14: a carrier hands the good it carries over to the building at its flag.</summary>
/// <param name="Load">The good still in the carrier's hands, or null once it is handed over.</param>
/// <param name="Heading">The direction back to the node the carrier came from.</param>
/// <param name="Marked">Whether the carrier is in the second half of the handover, or is marked to go.</param>
public sealed record HandsOver(Load? Load, Heading Heading, bool Marked = false) : Activity
{
    /// <inheritdoc />
    public override int Code => 0xe;
}

/// <summary>State 15: the settler leaves the castle by its door onto the castle's flag.</summary>
/// <param name="Errand">Where the settler walks to from the flag.</param>
public sealed record Leaving(Errand Errand) : HasErrand(Errand)
{
    /// <inheritdoc />
    public override int Code => 0xf;
}

/// <summary>State 16, or state 22 for the stonecutter's walk: the settler walks free along a vector.</summary>
/// <param name="Trip">The walk.</param>
/// <param name="EyesHeaps">
/// Whether the stonecutter looks for a stone heap beside his node at each turn of the walk (state 22, <c>0x436a84</c>): he
/// stops where one stands north-west of him. The walk of every other settler, and the stonecutter's way back, is state 16.
/// </param>
public sealed record WalksFree(Trip Trip, bool EyesHeaps = false) : Activity
{
    /// <inheritdoc />
    public override int Code => EyesHeaps ? 0x16 : 0x10;
}

/// <summary>State 17: the woodcutter cuts the tree it stands on, one falling step at a time.</summary>
/// <param name="Back">The steps of the way back to the hut's flag.</param>
/// <param name="SecondVariant">Whether the tree takes the second cutting variant.</param>
/// <param name="Count">The falling steps done, 0 to 5.</param>
/// <param name="Memory">The memory the free walk to the tree left.</param>
public sealed record Cuts(Vector Back, bool SecondVariant, int Count, int Memory) : Activity
{
    /// <inheritdoc />
    public override int Code => 0x11;
}

/// <summary>State 18: the woodcutter inside its hut looks for a tree.</summary>
public sealed record LooksForTree : Activity
{
    /// <inheritdoc />
    public override int Code => 0x12;
}

/// <summary>State 21: the stonecutter inside his hut looks for a stone heap.</summary>
public sealed record LooksForStone : Activity
{
    /// <inheritdoc />
    public override int Code => 0x15;
}

/// <summary>
/// State 23: the stonecutter stands on the node of the heap he cuts, steps back onto the node he came by, and takes the stone
/// with him (<c>0x435c5c</c>).
/// </summary>
/// <param name="Back">The steps of the way back to the hut's flag, east and south (bytes 11 and 12).</param>
/// <param name="Phase">The part of the cut: 0 the step onto the heap, 1 the stroke, 2 the step back (byte 13).</param>
/// <param name="Quarter">A quarter of the step onto the heap's time, the point where the stroke begins (the word at 14).</param>
public sealed record CutsStone(Vector Back, int Phase, int Quarter) : Activity
{
    /// <inheritdoc />
    public override int Code => 0x17;
}

/// <summary>State 25: the settler looks for the nearest flag on the way home.</summary>
/// <param name="FarEndFirst">Whether the search starts from the far end of its table of offsets.</param>
/// <param name="FarEndMark">
/// The byte 11 the search starts at the far end by: the byte the walk before it left, kept as it was (not 0), 1 where a rule
/// starts the search. Its value is of no meaning while <paramref name="FarEndFirst"/> is false.
/// </param>
public sealed record SearchesHome(bool FarEndFirst = false, int FarEndMark = 1) : Activity
{
    /// <inheritdoc />
    public override int Code => 0x19;
}

/// <summary>State 28: the settler claims the node it stands on to start the way home.</summary>
public sealed record ClaimsHomeNode : Activity
{
    /// <inheritdoc />
    public override int Code => 0x1c;
}

/// <summary>State 53: a knight walks free along a vector (the knights' own walk is not ported).</summary>
/// <param name="Trip">The walk.</param>
public sealed record KnightWalks(Trip Trip) : Activity
{
    /// <inheritdoc />
    public override int Code => 0x35;
}

/// <summary>
/// A state code the engine has no name for, kept as it is: no rule takes a settler into it and its turn changes nothing. The
/// original's branches that reach the state 0x49 are not ported, and a settler a layout or a snapshot places in
/// another state keeps it raw (INV-0006).
/// </summary>
/// <param name="StateCode">The code of the original's state.</param>
public sealed record RawState(int StateCode) : Activity
{
    /// <inheritdoc />
    public override int Code => StateCode;
}

/// <summary>
/// The activities of a carrier that stands next to the flag at the end of its road, off its node (states 0x42 to 0x45). The
/// carrier keeps the heading it stopped with, marked as a step tried again.
/// </summary>
/// <param name="Approach">The heading the carrier held when it stopped, marked as a step tried again.</param>
public abstract record WaitsOffRoad(Heading Approach) : Activity;

/// <summary>State 66: a carrier waits next to a flag at the end of its road, off its node.</summary>
/// <param name="Flag">The slot of the flag the carrier waits next to.</param>
/// <param name="Direction">The direction in which the road leaves that flag.</param>
/// <param name="Approach">The heading the carrier held when it stopped, marked as a step tried again.</param>
public sealed record WaitsAtFlag(int Flag, Direction Direction, Heading Approach) : WaitsOffRoad(Approach)
{
    /// <inheritdoc />
    public override int Code => 0x42;
}

/// <summary>State 67: a carrier that waits next to a flag claims its node to go on with a good to fetch.</summary>
/// <param name="Heading">The direction back to the node the carrier came from.</param>
/// <param name="Approach">The heading the carrier held when it stopped, marked as a step tried again.</param>
public sealed record ClaimsNode(Heading Heading, Heading Approach) : WaitsOffRoad(Approach)
{
    /// <inheritdoc />
    public override int Code => 0x43;
}

/// <summary>State 68: a carrier that waited next to a flag is told to go home because its road went.</summary>
/// <param name="Approach">The heading the carrier held when it stopped, marked as a step tried again.</param>
public sealed record WaitsToGoHome(Heading Approach) : WaitsOffRoad(Approach)
{
    /// <inheritdoc />
    public override int Code => 0x44;
}

/// <summary>State 69: a carrier that waited next to a flag is taken along when its road is joined.</summary>
/// <param name="Approach">The heading the carrier held when it stopped, marked as a step tried again.</param>
public sealed record WaitsToRejoin(Heading Approach) : WaitsOffRoad(Approach)
{
    /// <inheritdoc />
    public override int Code => 0x45;
}

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

    /// <summary>
    /// Whether the view draws a settler in this activity, by its frame and the time left of its step (<c>docs/rules/settlers.md</c>,
    /// "Drawing the walk"). Such a settler takes one more turn every pass where the game gives the settlers in view their turns
    /// (<c>MainPass.EveryPassTurns</c>), so its figure does not stand still between its slice turns. An activity the original draws
    /// overrides this; the others are not drawn.
    /// </summary>
    public virtual bool IsDrawn => false;

    /// <summary>
    /// Whether this is one of the activities the play script's <c>fast-forward &lt;job&gt; carrying</c> waits for: a carrier on its
    /// road with a good in its hands, a settler on its free walk back to its hut with the good it took, or the sawmiller with his
    /// plank in the sawmill's door and on the step out of it. The activities where a settler hands a good over or puts it down are
    /// not among them.
    /// </summary>
    public virtual bool Carries => false;

    /// <summary>Where the activity takes the settler along the roads, or null where it takes it nowhere along them.</summary>
    public virtual Errand? GoingTo => null;

    /// <summary>The same activity going to <paramref name="errand"/> instead; an activity that goes nowhere is unchanged.</summary>
    public virtual Activity Redirected(Errand errand) => this;

    /// <summary>The same activity with <paramref name="residue"/> as the bytes it leaves unused.</summary>
    public Activity Carrying(Residue residue) => this with { Residue = residue };
}

/// <summary>State 1: a settler waits in the castle; each turn it enters its slot in the castle's list of waiting settlers.</summary>
public sealed record WaitsInCastle : Activity
{
    /// <summary>The slot of the storage the settler waits in; the castle is 0.</summary>
    public int Storage { get; init; } = 0;

    /// <inheritdoc />
    public override int Code => 1;
}

/// <summary>An activity that takes the settler along the roads to an errand: a walk, or the way out of the castle to it.</summary>
public abstract record HasErrand : Activity
{
    /// <summary>Where the activity leads.</summary>
    public required Errand Errand { get; init; }

    /// <inheritdoc />
    public override Errand? GoingTo => Errand;

    /// <inheritdoc />
    public override Activity Redirected(Errand errand) => this with { Errand = errand };
}

/// <summary>State 2: the settler walks the roads to <see cref="Errand"/>.</summary>
public sealed record Walks : HasErrand
{
    /// <summary>The direction back to the node the settler came from.</summary>
    public required Heading Heading { get; init; }

    /// <summary>The turns spent on a blocked step (byte 15), counted from 0.</summary>
    public int Tries { get; init; } = 0;

    /// <inheritdoc />
    public override int Code => 2;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>State 3: a carrier walks its road, with a good in its hands or none.</summary>
public sealed record WalksRoad : Activity
{
    /// <summary>The good the carrier carries, or null.</summary>
    public required Load? Load { get; init; }

    /// <summary>The direction back to the node the carrier came from.</summary>
    public required Heading Heading { get; init; }

    /// <summary>The turns the carrier has spent at the end of a road that holds two carriers.</summary>
    public int Waits { get; init; } = 0;

    /// <summary>Whether the carrier is marked to go: its road was taken down or joined.</summary>
    public bool Marked { get; init; } = false;

    /// <inheritdoc />
    public override int Code => 3;

    /// <inheritdoc />
    public override bool IsDrawn => true;

    /// <inheritdoc />
    public override bool Carries => Load is { Good.IsNone: false };
}

/// <summary>State 4: the settler has stepped through a door and waits out what the door's speed leaves of the step.</summary>
public sealed record Door : Activity
{
    /// <summary>The time the door step has left, where the settler goes on.</summary>
    public required int TimeLeft { get; init; }

    /// <summary>Whether the settler came home to a storage and enters it (the walk home).</summary>
    public bool EnteringStorage { get; init; } = false;

    /// <inheritdoc />
    public override int Code => 4;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>An activity of a step whose time runs out before the settler takes up <see cref="Then"/>.</summary>
public abstract record StepsBefore : Activity
{
    /// <summary>The activity the settler takes when the step's time has run out.</summary>
    public required Activity Then { get; init; }

    /// <inheritdoc />
    public override Errand? GoingTo => Then.GoingTo;

    /// <inheritdoc />
    public override Activity Redirected(Errand errand) => this with { Then = Then.Redirected(errand) };
}

/// <summary>State 5: the settler waits out a step and then takes up <see cref="StepsBefore.Then"/>.</summary>
public sealed record WaitsOutStep : StepsBefore
{
    /// <inheritdoc />
    public override int Code => 5;

    /// <inheritdoc />
    public override bool IsDrawn => true;

    /// <summary>The sawmiller's step out of the door holds the plank he lays down after it.</summary>
    public override bool Carries => Then is PutsDownGood;
}

/// <summary>State 6: the settler stands on the flag of the building it walks into and waits for the node of the door.</summary>
public sealed record EntersBuilding : Activity
{
    /// <summary>Whether the building is a storage the settler walks home to.</summary>
    public bool Storage { get; init; } = false;

    /// <inheritdoc />
    public override int Code => 6;
}

/// <summary>State 7: the settler steps from a hut onto its flag, and then takes up <see cref="StepsBefore.Then"/>.</summary>
public sealed record StepsOntoFlag : StepsBefore
{
    /// <inheritdoc />
    public override int Code => 7;
}

/// <summary>
/// State 9: the builder works in the building site he stands in (<c>0x43ec82</c>, <c>docs/rules/construction.md</c>): he takes the site's
/// materials one after the other in the order of the type's order word, and for each makes eight blows that add a step to the site's
/// progress word.
/// </summary>
public sealed record Builds : Activity
{
    /// <summary>The slot of the building site in the building table (the word at 12).</summary>
    public required int Site { get; init; }

    /// <summary>Whether he is making the blows of a material (byte 11 is 0xff) and not looking for the next one (byte 11 is 1).</summary>
    public required bool Blowing { get; init; }

    /// <summary>The materials he has taken so far, the index of the next in the order (the low four bits of byte 14).</summary>
    public required int Materials { get; init; }

    /// <summary>Whether his figure is the second one the types 10, 17, 19, and 22 take (bit 7 of byte 14, the frames 0x64 and 0x6a to 0x6d).</summary>
    public required bool SecondFigure { get; init; }

    /// <summary>The blows left of the material (byte 15).</summary>
    public required int Blows { get; init; }

    /// <inheritdoc />
    public override int Code => 9;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>
/// State 8: the digger works in the ground of the large site he entered (<c>0x43e45e</c>, <c>docs/rules/digging.md</c>). He makes sixteen
/// passes, the first with <see cref="Pass"/> 15 and the last with 0, over the site's node and its six neighbours; in each he visits the
/// nodes whose height is the pass's, walks onto each, and changes its height by one toward the site's height, lowering in the odd passes and
/// raising in the even ones.
/// </summary>
public sealed record Digs : Activity
{
    /// <summary>The pass he is in, 15 down to 0 (byte 11): the height the pass names is the target's plus a delta the pass number gives.</summary>
    public required int Pass { get; init; }

    /// <summary>The site's height (byte 12).</summary>
    public required int Target { get; init; }

    /// <summary>The node of the pass he works at, 6 (east) down to 0 (the site's node) (byte 13); direction 6 - <see cref="Node"/> leads from the site to it.</summary>
    public required int Node { get; init; }

    /// <summary>Where he is in the work of the node (byte 14): 0 the wait for a busy node, 1 the next node, 2 the dig, 3 the step onto the node.</summary>
    public required int Phase { get; init; }

    /// <inheritdoc />
    public override int Code => 8;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>
/// State 0x18: the sawmiller works in the sawmill he stands in (<c>0x4359fe</c>, <c>docs/rules/sawmilling.md</c>). Without a log on site he waits
/// for one; with one, he takes it out of the building's fill and saws for 0x93f clock units, then carries a plank out to the flag.
/// </summary>
public sealed record Saws : Activity
{
    /// <summary>Whether he saws a log (byte 11 is 0xff) and not waits for one (byte 11 is 0).</summary>
    public required bool Sawing { get; init; }

    /// <inheritdoc />
    public override int Code => 0x18;

    /// <summary>Drawn while he saws; waiting for a log he is not drawn.</summary>
    public override bool IsDrawn => Sawing;
}

/// <summary>
/// State 0xb: the sawmiller waits at the sawmill's door with the plank in his hands until the node of the flag is free and the flag has a
/// free place for it (<c>0x43e1d0</c>, <c>0x43e228</c>), then steps onto the flag and lays the plank down.
/// </summary>
public sealed record StepsOutWithGood : Activity
{
    /// <summary>The good he carries out (byte 11), the plank.</summary>
    public required Good Good { get; init; }

    /// <inheritdoc />
    public override int Code => 0xb;

    /// <inheritdoc />
    public override bool Carries => true;
}

/// <summary>State 0xd: the settler stands on the flag with a good in his hands and lays it on the flag (<c>0x43c6aa</c>), then enters the building.</summary>
public sealed record PutsDownGood : Activity
{
    /// <summary>The good he lays down (byte 11).</summary>
    public required Good Good { get; init; }

    /// <inheritdoc />
    public override int Code => 0xd;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>
/// State 0x4a: the builder has finished the building and waits at its door until the node of the flag is free, then steps onto the flag
/// to go home (<c>0x43e008</c>).
/// </summary>
public sealed record LeavesSite : Activity
{
    /// <inheritdoc />
    public override int Code => 0x4a;

    /// <inheritdoc />
    public override bool IsDrawn => true;
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
public sealed record HandsOver : Activity
{
    /// <summary>The good still in the carrier's hands, or null once it is handed over.</summary>
    public required Load? Load { get; init; }

    /// <summary>The direction back to the node the carrier came from.</summary>
    public required Heading Heading { get; init; }

    /// <summary>Whether the carrier is in the second half of the handover, or is marked to go.</summary>
    public bool Marked { get; init; } = false;

    /// <inheritdoc />
    public override int Code => 0xe;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>State 15: the settler leaves the castle by its door onto the castle's flag.</summary>
public sealed record Leaving : HasErrand
{
    /// <inheritdoc />
    public override int Code => 0xf;
}

/// <summary>State 16, or state 22 for the stonecutter's walk: the settler walks free along a vector.</summary>
public sealed record WalksFree : Activity
{
    /// <summary>The walk.</summary>
    public required Trip Trip { get; init; }

    /// <summary>
    /// Whether the stonecutter looks for a stone heap beside his node at each turn of the walk (state 22, <c>0x436a84</c>): he
    /// stops where one stands north-west of him. The walk of every other settler, and the stonecutter's way back, is state 16.
    /// </summary>
    public bool EyesHeaps { get; init; } = false;

    /// <inheritdoc />
    public override int Code => EyesHeaps ? 0x16 : 0x10;

    /// <inheritdoc />
    public override bool IsDrawn => true;

    /// <inheritdoc />
    public override bool Carries => Trip.Purpose is BackToHut { Carrying: true };
}

/// <summary>State 17: the woodcutter cuts the tree it stands on, one falling step at a time.</summary>
public sealed record Cuts : Activity
{
    /// <summary>The steps of the way back to the hut's flag.</summary>
    public required Vector Back { get; init; }

    /// <summary>Whether the tree takes the second cutting variant.</summary>
    public required bool SecondVariant { get; init; }

    /// <summary>The falling steps done, 0 to 5.</summary>
    public required int Count { get; init; }

    /// <summary>The memory the free walk to the tree left.</summary>
    public required int Memory { get; init; }

    /// <inheritdoc />
    public override int Code => 0x11;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>State 18: the woodcutter inside its hut looks for a tree.</summary>
public sealed record LooksForTree : Activity
{
    /// <inheritdoc />
    public override int Code => 0x12;
}

/// <summary>State 19: the forester inside his hut looks for a free meadow node to plant on.</summary>
public sealed record LooksForSpot : Activity
{
    /// <inheritdoc />
    public override int Code => 0x13;
}

/// <summary>State 20: the forester stands on the node he plants on, waits, and puts the sapling down (<c>0x4389bf</c>).</summary>
public sealed record Plants : Activity
{
    /// <summary>The steps of the way back to the hut's flag, east and south (bytes 11 and 12).</summary>
    public required Vector Back { get; init; }

    /// <summary>Whether the sapling is down and he waits to turn back (byte 14 not 0).</summary>
    public required bool Planted { get; init; }

    /// <summary>The memory the free walk to the node left (byte 15).</summary>
    public required int Memory { get; init; }

    /// <inheritdoc />
    public override int Code => 0x14;

    /// <inheritdoc />
    public override bool IsDrawn => true;
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
public sealed record CutsStone : Activity
{
    /// <summary>The steps of the way back to the hut's flag, east and south (bytes 11 and 12).</summary>
    public required Vector Back { get; init; }

    /// <summary>The part of the cut: 0 the step onto the heap, 1 the stroke, 2 the step back (byte 13).</summary>
    public required int Phase { get; init; }

    /// <summary>A quarter of the step onto the heap's time, the point where the stroke begins (the word at 14).</summary>
    public required int Quarter { get; init; }

    /// <inheritdoc />
    public override int Code => 0x17;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>State 25: the settler looks for the nearest flag on the way home.</summary>
public sealed record SearchesHome : Activity
{
    /// <summary>Whether the search starts from the far end of its table of offsets.</summary>
    public bool FarEndFirst { get; init; } = false;

    /// <summary>
    /// The byte 11 the search starts at the far end by: the byte the walk before it left, kept as it was (not 0), 1 where a rule
    /// starts the search. Its value is of no meaning while <paramref name="FarEndFirst"/> is false.
    /// </summary>
    public int FarEndMark { get; init; } = 1;

    /// <inheritdoc />
    public override int Code => 0x19;

    /// <inheritdoc />
    public override bool IsDrawn => true;
}

/// <summary>State 28: the settler claims the node it stands on to start the way home.</summary>
public sealed record ClaimsHomeNode : Activity
{
    /// <inheritdoc />
    public override int Code => 0x1c;
}

/// <summary>State 53: a knight walks free along a vector (the knights' own walk is not ported).</summary>
public sealed record KnightWalks : Activity
{
    /// <summary>The walk.</summary>
    public required Trip Trip { get; init; }

    /// <inheritdoc />
    public override int Code => 0x35;
}

/// <summary>
/// A state code the engine has no name for, kept as it is: no rule takes a settler into it and its turn changes nothing. The
/// original's branches that reach the state 0x49 are not ported, and a settler a layout or a snapshot places in
/// another state keeps it raw (INV-0006).
/// </summary>
public sealed record RawState : Activity
{
    /// <summary>The code of the original's state.</summary>
    public required int StateCode { get; init; }

    /// <inheritdoc />
    public override int Code => StateCode;
}

/// <summary>
/// The activities of a carrier that stands next to the flag at the end of its road, off its node (states 0x42 to 0x45). The
/// carrier keeps the heading it stopped with, marked as a step tried again.
/// </summary>
public abstract record WaitsOffRoad : Activity
{
    /// <summary>The heading the carrier held when it stopped, marked as a step tried again.</summary>
    public required Heading Approach { get; init; }
}

/// <summary>State 66: a carrier waits next to a flag at the end of its road, off its node.</summary>
public sealed record WaitsAtFlag : WaitsOffRoad
{
    /// <summary>The slot of the flag the carrier waits next to.</summary>
    public required int Flag { get; init; }

    /// <summary>The direction in which the road leaves that flag.</summary>
    public required Direction Direction { get; init; }

    /// <inheritdoc />
    public override int Code => 0x42;
}

/// <summary>State 67: a carrier that waits next to a flag claims its node to go on with a good to fetch.</summary>
public sealed record ClaimsNode : WaitsOffRoad
{
    /// <summary>The direction back to the node the carrier came from.</summary>
    public required Heading Heading { get; init; }

    /// <inheritdoc />
    public override int Code => 0x43;
}

/// <summary>State 68: a carrier that waited next to a flag is told to go home because its road went.</summary>
public sealed record WaitsToGoHome : WaitsOffRoad
{
    /// <inheritdoc />
    public override int Code => 0x44;
}

/// <summary>State 69: a carrier that waited next to a flag is taken along when its road is joined.</summary>
public sealed record WaitsToRejoin : WaitsOffRoad
{
    /// <inheritdoc />
    public override int Code => 0x45;
}

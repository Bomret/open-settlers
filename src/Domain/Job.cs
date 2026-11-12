namespace OpenSettlers.Domain;

/// <summary>
/// What a settler is employed as, by the number the original gives the job (<c>docs/rules/settlers.md</c>). A settler
/// keeps its identity when its job changes: a settler without a job becomes a carrier when it takes a road. A job is a type
/// (ADR 0016): each ported job is a sealed record of its own that declares its number, a <see cref="Knight"/> holds the
/// numbers 22 to 26, and an <see cref="UnportedJob"/> holds any other number. <see cref="Of"/>, generated from the job types,
/// finds the job of a number.
/// </summary>
public abstract partial record Job
{
    private protected Job() { }

    /// <summary>The job's number: byte 0 of the settler's item, bits 2 to 6.</summary>
    public abstract int Code { get; }

    /// <summary>The number of the carrier's job, for a <c>case</c> label.</summary>
    public const int CarrierCode = 0;

    /// <summary>The number of the woodcutter's job, for a <c>case</c> label.</summary>
    public const int WoodcutterCode = 5;

    /// <summary>The carrier, who takes a road and waits on it (<c>docs/rules/carriers.md</c>).</summary>
    public static Job Carrier => new Carrier();

    /// <summary>The number of the digger's job, for a <c>case</c> label.</summary>
    public const int DiggerCode = 2;

    /// <summary>
    /// The digger, who levels the ground of a large building site before its builder starts (<c>docs/rules/digging.md</c>). The program's
    /// request for him (<c>0x42d52a</c>, in the large site's handler <c>0x42d328</c>) names job 2 and the stock word of the tool good 16.
    /// </summary>
    public static Job Digger => new Digger();

    /// <summary>The number of the builder's job, for a <c>case</c> label.</summary>
    public const int BuilderCode = 3;

    /// <summary>
    /// The builder, who walks out to a building site and builds it from the planks and stones on site (<c>docs/rules/construction.md</c>).
    /// The program's request for it (<c>0x42d5fc</c>) names job 3 and the stock word of the tool good 17.
    /// </summary>
    public static Job Builder => new Builder();

    /// <summary>
    /// The castle's own first settler, who stands by while the castle is built (state 10, <c>0x43f17a</c>) and carries the goods the castle
    /// hands out onto its flag. No building asks for it: only the castle's placement creates it (<c>docs/rules/castle.md</c>).
    /// </summary>
    public static Job CastleSettler => new CastleSettler();

    /// <summary>The woodcutter, who cuts trees and carries the logs to his hut's flag (<c>docs/rules/woodcutting.md</c>).</summary>
    public static Job Woodcutter => new Woodcutter();

    /// <summary>The number of the stonecutter's job, for a <c>case</c> label.</summary>
    public const int StonecutterCode = 7;

    /// <summary>The stonecutter, who cuts stone heaps and carries the stones to his hut's flag (<c>docs/rules/stonecutting.md</c>).</summary>
    public static Job Stonecutter => new Stonecutter();

    /// <summary>The number of the sawmiller's job, for a <c>case</c> label.</summary>
    public const int SawmillerCode = 6;

    /// <summary>
    /// The sawmiller, who saws the logs the sawmill holds into planks and carries each plank out to the flag
    /// (<c>docs/rules/sawmilling.md</c>). The handler of the building type 0x11 (<c>0x42da0b</c>) asks for job 6 with the stock word of the tool good 22,
    /// and his work puts the good 8 on the flag.
    /// </summary>
    public static Job Sawmiller => new Sawmiller();

    /// <summary>The number of the forester's job, for a <c>case</c> label.</summary>
    public const int ForesterCode = 8;

    /// <summary>The forester, who plants saplings on free meadow nodes around his hut (<c>docs/rules/forestry.md</c>).</summary>
    public static Job Forester => new Forester();

    /// <summary>A settler without a job yet.</summary>
    public static Job Jobless => new Jobless();

    /// <summary>The job of the settler of the type 1 hut, whose work looks for a free node by the water.</summary>
    public static Job TypeOneHut => new TypeOneHut();

    /// <summary>The first job of the knights; the knights are the jobs 22 to 26.</summary>
    internal const int FirstKnight = 22;

    internal const int LastKnight = 26;

    /// <summary>Whether this is one of the knights' jobs.</summary>
    public bool IsKnight => Code is >= FirstKnight and <= LastKnight;

    /// <summary>Whether this is a job of a knight or of one of the jobs above (the castle's own handlers are not ported).</summary>
    public bool IsKnightOrAbove => Code >= FirstKnight;

    /// <summary>
    /// Whether the settler arrives at the end of a free walk home as the generic arrival of <c>0x4385ab</c> has it whatever
    /// the vector (the table at <c>0x437dfb</c>): all but the woodcutter, the jobs 7, 8, 11 (the settler of the type 1 hut, <c>docs/rules/settlers.md</c>), 14, and
    /// 20, and the knights. Those jobs' own branches hand over to it where the walk is a walk home. A job type that has a branch of its own overrides it.
    /// </summary>
    public virtual bool ArrivesHomeGenerically => true;

    /// <summary>The level of a knight's job: 0 for the first.</summary>
    public int KnightLevel => Code - FirstKnight;

    /// <summary>The job a knight takes when it is made a knight of the next level.</summary>
    public Job Promoted => Of(Code + 1);

    /// <summary>The job's number alone.</summary>
    public sealed override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

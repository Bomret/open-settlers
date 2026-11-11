namespace OpenSettlers.Domain;

/// <summary>
/// What a settler is employed as, by the number the original gives the job (<c>docs/rules/settlers.md</c>). A settler
/// keeps its identity when its job changes: a settler without a job becomes a carrier when it takes a road.
/// </summary>
/// <param name="Code">The job's number: byte 0 of the settler's item, bits 2 to 6.</param>
public readonly record struct Job(int Code)
{
    /// <summary>The number of the carrier's job, for a <c>case</c> label.</summary>
    public const int CarrierCode = 0;

    /// <summary>The number of the woodcutter's job, for a <c>case</c> label.</summary>
    public const int WoodcutterCode = 5;

    /// <summary>The carrier, who takes a road and waits on it (<c>docs/rules/carriers.md</c>).</summary>
    public static Job Carrier => new(CarrierCode);

    /// <summary>The number of the builder's job, for a <c>case</c> label.</summary>
    public const int BuilderCode = 3;

    /// <summary>
    /// The builder, who walks out to a building site and builds it from the planks and stones on site (<c>docs/rules/construction.md</c>).
    /// The program's request for it (<c>0x42d5fc</c>) names job 3 and the stock word of the tool good 17.
    /// </summary>
    public static Job Builder => new(BuilderCode);

    /// <summary>
    /// The castle's own first settler, who stands by while the castle is built (state 10, <c>0x43f17a</c>) and carries the goods the castle
    /// hands out onto its flag. No building asks for it: only the castle's placement creates it (<c>docs/rules/castle.md</c>).
    /// </summary>
    public static Job CastleSettler => new(4);

    /// <summary>The woodcutter, who cuts trees and carries the logs to his hut's flag (<c>docs/rules/woodcutting.md</c>).</summary>
    public static Job Woodcutter => new(WoodcutterCode);

    /// <summary>The number of the stonecutter's job, for a <c>case</c> label.</summary>
    public const int StonecutterCode = 7;

    /// <summary>The stonecutter, who cuts stone heaps and carries the stones to his hut's flag (<c>docs/rules/stonecutting.md</c>).</summary>
    public static Job Stonecutter => new(StonecutterCode);

    /// <summary>The number of the forester's job, for a <c>case</c> label.</summary>
    public const int ForesterCode = 8;

    /// <summary>The forester, who plants saplings on free meadow nodes around his hut (<c>docs/rules/forestry.md</c>).</summary>
    public static Job Forester => new(ForesterCode);

    /// <summary>A settler without a job yet.</summary>
    public static Job Jobless => new(21);

    /// <summary>The job of the settler of the type 1 hut, whose work looks for a free node by the water.</summary>
    public static Job TypeOneHut => new(RuleTables.TypeOneHutJob);

    /// <summary>The first job of the knights; the knights are the jobs 22 to 26.</summary>
    private const int FirstKnight = 22;

    private const int LastKnight = 26;

    /// <summary>Whether this is one of the knights' jobs.</summary>
    public bool IsKnight => Code is >= FirstKnight and <= LastKnight;

    /// <summary>Whether this is a job of a knight or of one of the jobs above (the castle's own handlers are not ported).</summary>
    public bool IsKnightOrAbove => Code >= FirstKnight;

    /// <summary>
    /// Whether the settler arrives at the end of a free walk home as the generic arrival of <c>0x4385ab</c> has it whatever
    /// the vector (the table at <c>0x437dfb</c>): all but the woodcutter, the jobs 7, 8, 11 (the settler of the type 1 hut, <c>docs/rules/settlers.md</c>), 14, and
    /// 20, and the knights. Those jobs' own branches hand over to it where the walk is a walk home.
    /// </summary>
    public bool ArrivesHomeGenerically =>
        Code is not (WoodcutterCode or StonecutterCode or ForesterCode or 11 or 14 or 20) && !IsKnightOrAbove;

    /// <summary>The level of a knight's job: 0 for the first.</summary>
    public int KnightLevel => Code - FirstKnight;

    /// <summary>The job a knight takes when it is made a knight of the next level.</summary>
    public Job Promoted => new(Code + 1);

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

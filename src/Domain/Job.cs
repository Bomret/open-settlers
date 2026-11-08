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

    /// <summary>The builder, who stands by while the castle is built.</summary>
    public static Job Builder => new(4);

    /// <summary>The woodcutter, who cuts trees and carries the logs to his hut's flag (<c>docs/rules/woodcutting.md</c>).</summary>
    public static Job Woodcutter => new(WoodcutterCode);

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
    /// the vector (the table at <c>0x437dfb</c>): all but the woodcutter, the settler of the type 1 hut, the jobs 7, 8, 14, and
    /// 20, and the knights. Those jobs' own branches hand over to it where the walk is a walk home.
    /// </summary>
    public bool ArrivesHomeGenerically => Code is not (WoodcutterCode or 7 or 8 or 11 or 14 or 20) && !IsKnightOrAbove;

    /// <summary>The level of a knight's job: 0 for the first.</summary>
    public int KnightLevel => Code - FirstKnight;

    /// <summary>The job a knight takes when it is made a knight of the next level.</summary>
    public Job Promoted => new(Code + 1);

    /// <inheritdoc />
    public override string ToString() => Code.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

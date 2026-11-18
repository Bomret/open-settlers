using System;

namespace OpenSettlers.Domain;

/// <summary>A knight, whose level is the offset of its number from the first knight's: the jobs 22 to 26.</summary>
/// <param name="Level">The level: 0 for the first knight.</param>
[JobName(
    "knight-level-0",
    "Knight of the first level, job 22; its idle turn 0x43910b draws for promotion to job 23 (limit 4000); inferred name, the program names no rank; docs/rules/settlers.md",
    Level = 0
)]
[JobName(
    "knight-level-1",
    "Knight of the second level, job 23; its idle turn 0x4391eb draws for promotion to job 24 (limit 2000); inferred name, the program names no rank; docs/rules/settlers.md",
    Level = 1
)]
[JobName(
    "knight-level-2",
    "Knight of the third level, job 24; its idle turn 0x4392cb draws for promotion to job 25 (limit 1000); inferred name, the program names no rank; docs/rules/settlers.md",
    Level = 2
)]
[JobName(
    "knight-level-3",
    "Knight of the fourth level, job 25; its idle turn 0x4393ab draws for promotion to job 26 (limit 500); inferred name, the program names no rank; docs/rules/settlers.md",
    Level = 3
)]
[JobName(
    "knight-level-4",
    "Knight of the last level, job 26, which only waits (no promotion routine follows 0x4393ab); inferred name, the program names no rank; docs/rules/settlers.md",
    Level = 4
)]
public sealed record Knight(int Level) : Job
{
    /// <summary>The number of the first knight's job.</summary>
    public const int Number = 22;

    /// <summary>The number of the last knight's job.</summary>
    public const int LastNumber = 26;

    /// <summary>The level of the last knight; the level attributes above name each level up to it.</summary>
    public const int LastLevel = 4;

    /// <summary>The level: 0 for the first knight. A level above <see cref="LastLevel"/> is refused.</summary>
    public int Level { get; } =
        Level is >= 0 and <= LastLevel
            ? Level
            : throw new ArgumentException($"The level {Level} is no knight's.", nameof(Level));

    /// <summary>Whether a knight of a higher level exists, so that this one may be promoted.</summary>
    public bool CanBePromoted => Level < LastLevel;

    /// <summary>The knight of the next level; throws for the last level.</summary>
    public Knight Promoted => new(Level + 1);

    /// <inheritdoc />
    public override int? OriginalCode => Number + Level;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}

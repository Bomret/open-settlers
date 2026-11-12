using System;

namespace OpenSettlers.Domain;

/// <summary>A knight, whose level is the offset of its number from the first knight's: the jobs 22 to 26.</summary>
/// <param name="Level">The level: 0 for the first knight.</param>
public sealed record Knight(int Level) : Job
{
    /// <summary>The number of the first knight's job.</summary>
    public const int Number = FirstKnight;

    /// <summary>The number of the last knight's job.</summary>
    public const int LastNumber = LastKnight;

    /// <summary>The level: 0 for the first knight. A level whose number is no knight's is refused.</summary>
    public int Level { get; } =
        Level is >= 0 and <= LastNumber - Number
            ? Level
            : throw new ArgumentException($"The level {Level} is no knight's.", nameof(Level));

    /// <inheritdoc />
    public override int Code => Number + Level;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;
}

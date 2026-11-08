namespace OpenSettlers.Domain;

/// <summary>
/// The class the rules give an object code: 0 nothing, 1 and 2 the generated trees and stones (a class above 1 is a large
/// object), 3 a flag, 4 to 6 buildings with 6 the castle (<c>docs/rules/build-sites.md</c>). The program's class table
/// assigns one to each object code; <see cref="RuleTables.ClassOf"/> reads it.
/// </summary>
/// <param name="Value">The class number, 0 to 6.</param>
public readonly record struct ObjectClass(int Value)
{
    /// <summary>The class of a class number, for the tables a test builds from numbers.</summary>
    public static implicit operator ObjectClass(int value) => new(value);

    /// <summary>Class 0: the object does not occupy its node for the rules.</summary>
    public bool IsEmpty => Value == 0;

    /// <summary>A class above 1: a large object, which the object overlap pass keeps only where no neighbour carries a mark.</summary>
    public bool IsLarge => Value > 1;

    /// <summary>Class 2: a stone.</summary>
    public bool IsStone => Value == 2;

    /// <summary>Class 3: a flag.</summary>
    public bool IsFlag => Value == 3;

    /// <summary>A class of 3 or more: a flag or a building.</summary>
    public bool IsFlagOrBuilding => Value >= 3;

    /// <summary>A class above 3: a building.</summary>
    public bool IsBuilding => Value > 3;

    /// <summary>Class 4: a building of the smallest size.</summary>
    public bool IsSmallBuilding => Value == 4;

    /// <summary>A class above 4: a building of the larger sizes, the castle included.</summary>
    public bool IsLargeBuilding => Value > 4;

    /// <summary>Class 6: the castle.</summary>
    public bool IsCastle => Value == 6;
}

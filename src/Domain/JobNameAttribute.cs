using System;

namespace OpenSettlers.Domain;

/// <summary>
/// Declares the stable name of a job with its evidence (ADR 0020, INV-0006), on the job's type. A job type that spans several numbers
/// (<see cref="Knight"/>) declares one for each <see cref="Level"/>. The job generator reads it, refuses a job with no name, a name outside
/// lowercase letters, digits, and hyphens, and two jobs with one name, and writes <see cref="Job.Name"/>.
/// </summary>
/// <param name="name">The stable name.</param>
/// <param name="evidence">Where the name and the job come from: an address of the program, or a document that says the name is inferred.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class JobNameAttribute(string name, string evidence) : Attribute
{
    /// <summary>The stable name.</summary>
    public string Name { get; } = name;

    /// <summary>The evidence for the name.</summary>
    public string Evidence { get; } = evidence;

    /// <summary>The level of a job that spans several numbers that the name is for: 0 for the first; -1 for a job of one number.</summary>
    public int Level { get; init; } = -1;
}

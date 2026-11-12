using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// What the importer and the name table ask of a <see cref="JobTables{TJob}"/> module without knowing its job type. The generated
/// <c>JobTablesRegistry</c> holds every module as one.
/// </summary>
internal interface IJobTables
{
    /// <summary>The job the module is for.</summary>
    Job Job { get; }

    /// <summary>
    /// Reads the job's data from the bytes of <c>Siedler1.exe</c> into <paramref name="data"/>, one entry for each record the job owns
    /// (<see cref="RuleTables.Get{T}"/> serves it by job and type). Throws <see cref="System.IO.InvalidDataException"/> where the
    /// program does not hold the job as expected.
    /// </summary>
    void Read(byte[] exe, ICollection<(Job Job, object Data)> data);
}

/// <summary>
/// The tables module of the job <typeparamref name="TJob"/> (ADR 0016, glossary: Job module): what the importer reads from the
/// program for the job, and the names the name table gives the job and its own states. A module derives from this type, has a
/// parameterless constructor, and states its names with <see cref="JobNameAttribute"/> and <see cref="StateNameAttribute"/>, which
/// the generator reads at build time: it writes <c>JobTablesRegistry</c> from the modules it finds, and fails the build for a
/// module with no constructor, a second module for one job, two modules that name one job or one state, a module that names one
/// state twice, and a module that names a state of <see cref="CoreStateCodesAttribute"/>. A job that owns no
/// data and no names needs no module.
/// </summary>
/// <typeparam name="TJob">The job the module is for.</typeparam>
public abstract class JobTables<TJob> : IJobTables
    where TJob : Job, new()
{
    /// <summary>The job the module is for.</summary>
    public Job Job { get; } = new TJob();

    /// <inheritdoc />
    public virtual void Read(byte[] exe, ICollection<(Job Job, object Data)> data) { }
}

/// <summary>The name of the module's job and the evidence for it (INV-0006). The generator reads it at build time.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JobNameAttribute(string name, string evidence) : Attribute
{
    /// <summary>The name.</summary>
    public string Name { get; } = name;

    /// <summary>The evidence for the name.</summary>
    public string Evidence { get; } = evidence;
}

/// <summary>
/// The codes of the states <see cref="NameTable"/> names itself, the ones that belong to no one job. The generator reads it at build
/// time and fails the build for a module that names one of them, which <see cref="NameTable"/> could not hold twice.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CoreStateCodesAttribute(params int[] codes) : Attribute
{
    /// <summary>The codes.</summary>
    public int[] Codes { get; } = codes;
}

/// <summary>The name of a settler state the module's job owns and the evidence for it (INV-0006). The generator reads it at build time.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class StateNameAttribute(int code, string name, string evidence) : Attribute
{
    /// <summary>The state's code.</summary>
    public int Code { get; } = code;

    /// <summary>The name.</summary>
    public string Name { get; } = name;

    /// <summary>The evidence for the name.</summary>
    public string Evidence { get; } = evidence;
}

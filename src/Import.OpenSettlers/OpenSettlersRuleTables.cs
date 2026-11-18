using System;
using System.IO;

namespace OpenSettlers.Import.ContentSet;

/// <summary>
/// The rule tables port of the basic content set (ADR 0018): the stand-in rules in the hand-written JSONC files of the set's folder,
/// encoded into <see cref="RuleTables"/>. The files hold values of the project's own making in readable terms (directions, sizes,
/// materials, speeds), each with its reason beside it, and none is read from the install (INV-0001).
/// </summary>
/// <param name="folder">The content set's folder.</param>
public sealed class OpenSettlersRuleTables(string folder) : IRuleTablesSource
{
    /// <summary>The file of the world's rules, in the content set's folder: object classes, step times, scan windows, frame rows, road capacities, and walk rows.</summary>
    public const string WorldFile = "rules/world.jsonc";

    /// <summary>The file of the goods' rules: counting, the handover's counter, resource-1 counting, priorities, requests, the distribution orders, the start stock, and the request ranks.</summary>
    public const string GoodsFile = "rules/goods.jsonc";

    /// <summary>The file of the buildings' rules: size, needs, door speeds, the builder's materials and blows, and the workers.</summary>
    public const string BuildingsFile = "rules/buildings.jsonc";

    /// <summary>The file of the data of the jobs the simulation reads: the builder, the digger, and the pig farmer.</summary>
    public const string JobsFile = "rules/jobs.jsonc";

    /// <summary>The rule files of a content set, by their path under its folder.</summary>
    public static readonly string[] Files = [WorldFile, GoodsFile, BuildingsFile, JobsFile];

    /// <summary>
    /// Reads the rule files and encodes them into the tables. Throws an <see cref="InvalidDataException"/> of the form
    /// <c>&lt;file&gt;: &lt;key&gt;: &lt;reason&gt;</c> for the first fault of a file: malformed JSONC, a missing table, a missing
    /// job's data, a wrong count, a value outside what its table allows, or a duplicate key. An <see cref="IOException"/> or an
    /// <see cref="UnauthorizedAccessException"/> reports a file that cannot be read. Any other exception is a fault of the reader and
    /// passes through as it is.
    /// </summary>
    public RuleTables Read() => StandInRulesReader.Read(folder).ToRuleTables();
}

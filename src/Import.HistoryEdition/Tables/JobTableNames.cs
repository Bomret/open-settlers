using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The names the jobs' tables modules state, as the name table of the adapters reads them: the registry itself stays
/// internal to this assembly (INV-0011).
/// </summary>
public static class JobTableNames
{
    /// <summary>The job names the modules state: the job's number, the name, and the evidence.</summary>
    public static IReadOnlyList<(int Code, string Name, string Evidence)> Jobs => JobTablesRegistry.JobNames;

    /// <summary>The state names the modules state: the state's code, the name, and the evidence.</summary>
    public static IReadOnlyList<(int Code, string Name, string Evidence)> States => JobTablesRegistry.StateNames;

    /// <summary>The evidence of a state whose name rests on the code of the Activity record alone.</summary>
    public const string StateEvidence = TableEvidence.State;
}

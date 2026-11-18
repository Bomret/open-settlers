using System.Collections.Generic;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>
/// The state names the jobs' tables modules state, as the name table of the adapters reads them: the registry itself stays
/// internal to this assembly (INV-0011). The jobs' own names are on the job types of the domain.
/// </summary>
public static class JobTableNames
{
    /// <summary>The state names the modules state: the state's code, the name, and the evidence.</summary>
    public static IReadOnlyList<(int Code, string Name, string Evidence)> States => JobTablesRegistry.StateNames;

    /// <summary>The evidence of a state whose name rests on the code of the Activity record alone.</summary>
    public const string StateEvidence = TableEvidence.State;
}

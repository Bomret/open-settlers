using System;
using System.Globalization;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// How the exporters write a job (ADR 0020, INV-0008, INV-0014): the original's code where the job's entry has one, and for an entry with
/// no code the exporter word of its stable name (<see cref="ExporterWords"/>): the FNV-1a hash with the low byte cleared, so it never
/// equals a job number of the original (0 to 31). An <see cref="UnportedJob"/> has no entry and no name: it writes its raw number.
/// </summary>
public static class JobWords
{
    /// <summary>
    /// The number an exporter writes for <paramref name="job"/>: the original's code of its entry, the raw number of an unported job, or
    /// the word of the hash of its stable name for an entry with no code. Throws for a job the catalog lacks, naming it.
    /// </summary>
    public static int Of(ContentCatalog catalog, Job job)
    {
        if (job is UnportedJob unported)
        {
            return unported.Raw;
        }

        var entry = catalog.Require(job);

        return entry.OriginalCode ?? ExporterWords.Of(entry.Name);
    }

    /// <summary>
    /// Whether <paramref name="job"/> has a number of the original, which a readout shows beside its name: a job with no original code
    /// shows its name alone (INV-0006).
    /// </summary>
    public static bool HasCode(ContentCatalog catalog, Job job) =>
        job is UnportedJob || catalog.Require(job).OriginalCode is not null;
}

using System;
using System.Globalization;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// How the exporters write a job (ADR 0020, INV-0008, INV-0014): the original's code where the job's entry has one, and for an entry with
/// no code the word <see cref="GoodWords"/> writes for a good: the FNV-1a hash of its stable name with the low byte cleared, so it never
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

        return entry.OriginalCode ?? GoodWords.WordOf(entry.Name);
    }

    /// <summary>
    /// Whether <paramref name="job"/> has a number of the original, which a readout shows beside its name: a job with no original code
    /// shows its name alone (INV-0006).
    /// </summary>
    public static bool HasCode(ContentCatalog catalog, Job job) =>
        job is UnportedJob || catalog.Require(job).OriginalCode is not null;

    /// <summary>The text a readout shows for the job's number: <see cref="Of"/> where the job has a code, else its stable name.</summary>
    public static string Text(ContentCatalog catalog, Job job) =>
        HasCode(catalog, job) ? Of(catalog, job).ToString(CultureInfo.InvariantCulture) : job.Name;

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> that names both jobs where two jobs of the catalog with no original code have one word,
    /// which a settler's item and the digest could not tell apart.
    /// </summary>
    public static void Check(ContentCatalog catalog) =>
        GoodWords.CheckCodeless(
            "jobs",
            catalog.Jobs.Where(entry => entry.OriginalCode is null).Select(entry => entry.Name),
            nameof(catalog)
        );
}

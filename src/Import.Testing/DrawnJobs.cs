using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Import.Testing;

/// <summary>
/// The jobs every graphics source draws a settler for (ADR 0016, INV-0010). Each source's test project checks that its
/// looks hold exactly these jobs, and the repository's cross-source check that every job with rules is among them.
/// </summary>
public static class DrawnJobs
{
    /// <summary>The jobs, one instance each.</summary>
    public static IReadOnlyList<Job> All { get; } =
    [
        new Carrier(),
        new Woodcutter(),
        new Stonecutter(),
        new Fisher(),
        new Builder(),
        new Forester(),
        new Digger(),
        new Sawmiller(),
        new Miller(),
        new Baker(),
        new Farmer(),
        new Butcher(),
        new PigFarmer(),
    ];
}

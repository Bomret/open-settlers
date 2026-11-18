using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// The engine's list of the goods, building types, and jobs a game knows, one <see cref="GoodEntry"/>, <see cref="BuildingEntry"/>, or
/// <see cref="JobEntry"/> each (ADR 0020). A content set's source builds it, and <see cref="RuleTables"/> holds it. It orders its entries of each kind by stable name,
/// ordinal, whatever order the source gives them in, so a game plays the same on every run (INV-0002). "No good" and "no building" are
/// never entries.
/// </summary>
public sealed class ContentCatalog
{
    /// <summary>The number of request classes a request word holds: a word is one byte of classes.</summary>
    const int ClassesPerWord = 8;

    readonly Dictionary<string, GoodEntry> _byName;
    readonly Dictionary<string, BuildingEntry> _buildingsByName = [];
    readonly Dictionary<string, JobEntry> _jobsByName = [];

    /// <summary>
    /// Creates the catalog from its goods. Throws an <see cref="ArgumentException"/> that names the entry and builds nothing for a duplicate
    /// stable name, a duplicate original code, a stable name outside lowercase letters, digits, and hyphens, a transport rank below 1 or
    /// one that another good holds, and a request class that does not fit its request word (0 to 7).
    /// </summary>
    /// <param name="goods">The goods, in any order.</param>
    /// <param name="buildings">
    /// The building types, in any order. The catalog refuses, naming the entry, a duplicate stable name, a duplicate original code, a
    /// duplicate tie rank, and a stable name outside lowercase letters, digits, and hyphens.
    /// </param>
    /// <param name="jobs">
    /// The jobs, in any order. The catalog refuses, naming the entry, a duplicate stable name, a duplicate original code, and a stable name
    /// outside lowercase letters, digits, and hyphens.
    /// </param>
    public ContentCatalog(
        IEnumerable<GoodEntry> goods,
        IEnumerable<BuildingEntry>? buildings = null,
        IEnumerable<JobEntry>? jobs = null
    )
    {
        var orderedJobs = (jobs ?? []).OrderBy(job => job.Name, StringComparer.Ordinal).ToList();
        Dictionary<int, JobEntry> jobsByCode = [];
        foreach (var job in orderedJobs)
        {
            if (job.Name.Length == 0 || !job.Name.All(IsNameCharacter))
            {
                throw Refuse(job, "its stable name holds anything other than lowercase letters, digits, and hyphens");
            }

            if (!_jobsByName.TryAdd(job.Name, job))
            {
                throw Refuse(job, "another job has this stable name");
            }

            if (job.OriginalCode is { } jobCode && !jobsByCode.TryAdd(jobCode, job))
            {
                throw Refuse(job, $"{jobsByCode[jobCode].Name} has this original code, {jobCode}");
            }
        }

        Jobs = [.. orderedJobs];

        var orderedBuildings = (buildings ?? []).OrderBy(building => building.Name, StringComparer.Ordinal).ToList();
        Dictionary<int, BuildingEntry> buildingsByCode = [];
        Dictionary<int, string> buildingsByRank = [];
        foreach (var building in orderedBuildings)
        {
            if (building.Name.Length == 0 || !building.Name.All(IsNameCharacter))
            {
                throw Refuse(
                    building,
                    "its stable name holds anything other than lowercase letters, digits, and hyphens"
                );
            }

            if (!_buildingsByName.TryAdd(building.Name, building))
            {
                throw Refuse(building, "another building type has this stable name");
            }

            if (building.OriginalCode is { } buildingCode && !buildingsByCode.TryAdd(buildingCode, building))
            {
                throw Refuse(building, $"{buildingsByCode[buildingCode].Name} has this original code, {buildingCode}");
            }

            if (!buildingsByRank.TryAdd(building.TieRank, building.Name))
            {
                throw Refuse(building, $"{buildingsByRank[building.TieRank]} has this tie rank, {building.TieRank}");
            }
        }

        Buildings = [.. orderedBuildings];

        var ordered = goods.OrderBy(good => good.Name, StringComparer.Ordinal).ToList();
        _byName = [];
        Dictionary<int, GoodEntry> byCode = [];
        var byRank = new Dictionary<int, string>();
        foreach (var good in ordered)
        {
            if (good.Name.Length == 0 || !good.Name.All(IsNameCharacter))
            {
                throw Refuse(good, "its stable name holds anything other than lowercase letters, digits, and hyphens");
            }

            if (!_byName.TryAdd(good.Name, good))
            {
                throw Refuse(good, "another good has this stable name");
            }

            if (good.OriginalCode is { } code && !byCode.TryAdd(code, good))
            {
                throw Refuse(good, $"{byCode[code].Name} has this original code, {code}");
            }

            if (good.TransportRank < 1)
            {
                throw Refuse(good, $"its transport rank {good.TransportRank} is below 1");
            }

            if (!byRank.TryAdd(good.TransportRank, good.Name))
            {
                throw Refuse(good, $"{byRank[good.TransportRank]} has this transport rank, {good.TransportRank}");
            }

            if (good.Request is { } request && request.Class is < 0 or >= ClassesPerWord)
            {
                throw Refuse(
                    good,
                    $"its request class {request.Class} does not fit a request word, which holds the classes 0 to {ClassesPerWord - 1}"
                );
            }
        }

        Goods = [.. ordered];
    }

    /// <summary>A catalog with no good.</summary>
    public static ContentCatalog Empty { get; } = new([]);

    /// <summary>The goods in ordinal order of their stable names.</summary>
    public ImmutableArray<GoodEntry> Goods { get; }

    /// <summary>The building types in ordinal order of their stable names.</summary>
    public ImmutableArray<BuildingEntry> Buildings { get; }

    /// <summary>The jobs in ordinal order of their stable names.</summary>
    public ImmutableArray<JobEntry> Jobs { get; }

    /// <summary>
    /// The entry of <paramref name="job"/>, the one with its stable name; null for a job the catalog lacks and for an
    /// <see cref="UnportedJob"/>, which has no name.
    /// </summary>
    public JobEntry? EntryOf(Job job) => job.Name.Length == 0 ? null : _jobsByName.GetValueOrDefault(job.Name);

    /// <summary>
    /// The entry of <paramref name="job"/>. Throws an <see cref="InvalidOperationException"/> that names the job where the catalog lacks
    /// it: a rule that meets a job its game's catalog lacks stops there.
    /// </summary>
    public JobEntry Require(Job job) =>
        EntryOf(job)
        ?? throw new InvalidOperationException(
            $"The job '{job}' is no entry of the content catalog, which holds: {string.Join(", ", Jobs.Select(entry => entry.Name))}."
        );

    /// <summary>
    /// The entry of <paramref name="type"/>, the one with its stable name; null for the type of no building and for a type the catalog lacks.
    /// </summary>
    public BuildingEntry? EntryOf(BuildingType type) =>
        type.IsNone ? null : _buildingsByName.GetValueOrDefault(type.Name);

    /// <summary>
    /// The entry of <paramref name="type"/>. Throws an <see cref="InvalidOperationException"/> that names the type where the catalog lacks
    /// it: a rule that meets a building type its game's catalog lacks stops there. The type of no building is no entry and throws too.
    /// </summary>
    public BuildingEntry Require(BuildingType type) =>
        EntryOf(type)
        ?? throw new InvalidOperationException(
            $"The building type '{type.Name}' is no entry of the content catalog, which holds: {string.Join(", ", Buildings.Select(entry => entry.Name))}."
        );

    /// <summary>
    /// The entry of <paramref name="good"/>, the one with its stable name; null for no good and for a good the catalog lacks.
    /// </summary>
    public GoodEntry? EntryOf(Good good) => good.IsNone ? null : _byName.GetValueOrDefault(good.Name);

    /// <summary>
    /// The entry of <paramref name="good"/>. Throws an <see cref="InvalidOperationException"/> that names the good where the catalog
    /// lacks it: a rule that meets a good its game's catalog lacks stops there. No good is no entry and throws too.
    /// </summary>
    public GoodEntry Require(Good good) =>
        EntryOf(good)
        ?? throw new InvalidOperationException(
            $"The good '{good.Name}' is no entry of the content catalog, which holds: {string.Join(", ", Goods.Select(entry => entry.Name))}."
        );

    static bool IsNameCharacter(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';

    static ArgumentException Refuse(GoodEntry good, string reason) =>
        new($"The good '{good.Name}' is refused: {reason}.");

    static ArgumentException Refuse(JobEntry job, string reason) => new($"The job '{job.Name}' is refused: {reason}.");

    static ArgumentException Refuse(BuildingEntry building, string reason) =>
        new($"The building type '{building.Name}' is refused: {reason}.");
}

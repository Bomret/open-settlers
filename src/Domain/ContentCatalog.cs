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

    readonly Dictionary<string, GoodEntry> _byName = [];
    readonly Dictionary<string, BuildingEntry> _buildingsByName = [];
    readonly Dictionary<string, JobEntry> _jobsByName = [];

    /// <summary>
    /// Creates the catalog from its entries. Throws an <see cref="ArgumentException"/> that names the entry and builds nothing where two
    /// entries of one kind share a stable name or an original code, a stable name holds anything other than lowercase letters, digits,
    /// and hyphens, two building types share a tie rank, a good's transport rank is below 1 or another good holds it, or a good's
    /// request class does not fit its request word (0 to 7).
    /// </summary>
    /// <param name="goods">The goods, in any order.</param>
    /// <param name="buildings">The building types, in any order.</param>
    /// <param name="jobs">The jobs, in any order.</param>
    public ContentCatalog(
        IEnumerable<GoodEntry> goods,
        IEnumerable<BuildingEntry>? buildings = null,
        IEnumerable<JobEntry>? jobs = null
    )
    {
        Jobs = Checked(jobs ?? [], "job", job => job.Name, job => job.OriginalCode, _jobsByName);

        Dictionary<int, string> buildingsByRank = [];
        Buildings = Checked(
            buildings ?? [],
            "building type",
            building => building.Name,
            building => building.OriginalCode,
            _buildingsByName,
            building =>
            {
                if (!buildingsByRank.TryAdd(building.TieRank, building.Name))
                {
                    throw Refuse(
                        "building type",
                        building.Name,
                        $"{buildingsByRank[building.TieRank]} has this tie rank, {building.TieRank}"
                    );
                }
            }
        );

        Dictionary<int, string> byRank = [];
        Goods = Checked(
            goods,
            "good",
            good => good.Name,
            good => good.OriginalCode,
            _byName,
            good =>
            {
                if (good.TransportRank < 1)
                {
                    throw Refuse("good", good.Name, $"its transport rank {good.TransportRank} is below 1");
                }

                if (!byRank.TryAdd(good.TransportRank, good.Name))
                {
                    throw Refuse(
                        "good",
                        good.Name,
                        $"{byRank[good.TransportRank]} has this transport rank, {good.TransportRank}"
                    );
                }

                if (good.Request is { } request && request.Class is < 0 or >= ClassesPerWord)
                {
                    throw Refuse(
                        "good",
                        good.Name,
                        $"its request class {request.Class} does not fit a request word, which holds the classes 0 to {ClassesPerWord - 1}"
                    );
                }
            }
        );
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

    // The entries of one kind in ordinal order of their names, each added to byName. Refuses, naming the entry, a name outside lowercase
    // letters, digits, and hyphens, a duplicate name, and a duplicate code, then runs the kind's own check on the entry.
    static ImmutableArray<T> Checked<T>(
        IEnumerable<T> entries,
        string kind,
        Func<T, string> name,
        Func<T, int?> code,
        Dictionary<string, T> byName,
        Action<T>? check = null
    )
    {
        var ordered = entries.OrderBy(name, StringComparer.Ordinal).ToImmutableArray();
        Dictionary<int, string> byCode = [];
        foreach (var entry in ordered)
        {
            var entryName = name(entry);
            if (entryName.Length == 0 || !entryName.All(IsNameCharacter))
            {
                throw Refuse(
                    kind,
                    entryName,
                    "its stable name holds anything other than lowercase letters, digits, and hyphens"
                );
            }

            if (!byName.TryAdd(entryName, entry))
            {
                throw Refuse(kind, entryName, $"another {kind} has this stable name");
            }

            if (code(entry) is { } value && !byCode.TryAdd(value, entryName))
            {
                throw Refuse(kind, entryName, $"{byCode[value]} has this original code, {value}");
            }

            check?.Invoke(entry);
        }

        return ordered;
    }

    static bool IsNameCharacter(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';

    static ArgumentException Refuse(string kind, string name, string reason) =>
        new($"The {kind} '{name}' is refused: {reason}.");
}

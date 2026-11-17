using System;
using System.IO;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The map view's model: the seed and size last applied, the map they generated, and the reason the last change was
/// refused. A valid change replaces the map; an invalid seed or size leaves the last map and the last applied seed
/// and size in place and sets <see cref="Problem"/>. Without the rule tables no map generates.
/// </summary>
public sealed class MapViewState
{
    /// <summary>The seed the view opens with, in the original's screen form.</summary>
    public const string DefaultSeed = "1234567812345678";

    readonly RuleTables? _tables;
    readonly string? _tablesProblem;
    bool _layout;

    MapViewState(RuleTables? tables, string? tablesProblem, string seedText, int sizeValue)
    {
        _tables = tables;
        _tablesProblem = tablesProblem;
        SeedText = seedText;
        SizeValue = sizeValue;
    }

    /// <summary>The seed last applied, as typed.</summary>
    public string SeedText { get; private set; }

    /// <summary>The seed last applied, parsed; compared so the same seed typed in another form does not regenerate.</summary>
    Seed _applied;

    /// <summary>The size value last applied.</summary>
    public int SizeValue { get; private set; }

    /// <summary>The start stock level of the human player of a game on the applied seed (<see cref="StartStock.LevelOf"/>).</summary>
    public int StockLevel => StartStock.LevelOf(_applied);

    /// <summary>The map of <see cref="SeedText"/> and <see cref="SizeValue"/>; null when the opening seed or size was refused.</summary>
    public Map? Map { get; private set; }

    /// <summary>Why the last change was refused; null after a change that was accepted.</summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// Reads the rule tables from the port. Where the port cannot read them (an <see cref="IOException"/>,
    /// <see cref="InvalidDataException"/>, or <see cref="UnauthorizedAccessException"/>) the result is null and
    /// <paramref name="problem"/> is the failure's message; otherwise <paramref name="problem"/> is null.
    /// </summary>
    public static RuleTables? ReadTables(IRuleTablesSource source, out string? problem)
    {
        try
        {
            problem = null;

            return source.Read();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            problem = e.Message;

            return null;
        }
    }

    /// <summary>
    /// Opens the state on the rule tables, a seed, and a size, generating their map unless one is refused.
    /// Null <paramref name="tables"/> generate no map, and <see cref="Problem"/> names
    /// <paramref name="tablesProblem"/>, why they could not be read.
    /// </summary>
    public static MapViewState Open(RuleTables? tables, string seedText, int sizeValue, string? tablesProblem = null)
    {
        var state = new MapViewState(tables, tablesProblem, seedText, sizeValue);
        state.Apply(seedText, sizeValue);

        return state;
    }

    /// <summary>
    /// Opens the state on the map a layout draws. The seed and size are the layout's, and the state refuses every
    /// change of them (<see cref="Apply"/>): the game runs on the layout's map.
    /// Where <paramref name="tablesProblem"/> says the rule tables could not be read, no map is held and
    /// <see cref="Problem"/> names it.
    /// </summary>
    public static MapViewState OpenLayout(Layout layout, string? tablesProblem = null)
    {
        return new MapViewState(null, tablesProblem, layout.Seed.ToScreen(), layout.Size.Value)
        {
            _applied = layout.Seed,
            _layout = true,
            Map = tablesProblem is null ? layout.BuildMap() : null,
            Problem = tablesProblem is null
                ? null
                : $"Cannot start the game, the rule tables are unreadable: {tablesProblem}",
        };
    }

    /// <summary>
    /// Applies a typed seed and a size. Returns true when a new map was generated, false when the change was refused
    /// (see <see cref="Problem"/>) or the seed, in any screen form, and size are the ones already applied.
    /// </summary>
    public bool Apply(string seedText, int sizeValue)
    {
        if (_layout)
        {
            Problem = "The game runs on a layout: its seed and size cannot change.";

            return false;
        }

        if (_tables is null)
        {
            // The problem names the file and key (the basic set's rule files) or the install's entry, so it names no source.
            Problem = $"Cannot generate the map, the rule tables are unreadable: {_tablesProblem}";

            return false;
        }

        if (!Seed.TryParse(seedText, out var seed, out var problem))
        {
            Problem = problem;

            return false;
        }

        if (Map is not null && seed == _applied && sizeValue == SizeValue)
        {
            Problem = null;

            return false;
        }

        try
        {
            if (!MapGenerator.TryGenerate(_tables, seed.ToMapSeed(), sizeValue, null, out var map, out problem))
            {
                Problem = problem;

                return false;
            }

            Map = map;
        }
        catch (System.InvalidOperationException e)
        {
            // MapGenerator refuses a seed whose map holds no land mass (the original scans without end).
            Problem = $"no map for this seed: {e.Message}";

            return false;
        }

        SeedText = seedText;
        _applied = seed;
        SizeValue = sizeValue;
        Problem = null;

        return true;
    }
}

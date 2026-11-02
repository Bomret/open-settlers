using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The map view's model: the seed and size last applied, the map they generated, and the reason the last change was
/// refused. A valid change replaces the map; an invalid seed or size leaves the last map and the last applied seed
/// and size in place and sets <see cref="Problem"/>.
/// </summary>
public sealed class MapViewState
{
    /// <summary>The seed the view opens with, in the original's screen form.</summary>
    public const string DefaultSeed = "1234567812345678";

    MapViewState(string seedText, int sizeValue)
    {
        SeedText = seedText;
        SizeValue = sizeValue;
    }

    /// <summary>The seed last applied, as typed.</summary>
    public string SeedText { get; private set; }

    /// <summary>The size value last applied.</summary>
    public int SizeValue { get; private set; }

    /// <summary>The map of <see cref="SeedText"/> and <see cref="SizeValue"/>; null when the opening seed or size was refused.</summary>
    public Map? Map { get; private set; }

    /// <summary>Why the last change was refused; null after a change that was accepted.</summary>
    public string? Problem { get; private set; }

    /// <summary>Opens the state on a seed and size, generating their map unless one is refused.</summary>
    public static MapViewState Open(string seedText, int sizeValue)
    {
        var state = new MapViewState(seedText, sizeValue);
        state.Apply(seedText, sizeValue, force: true);

        return state;
    }

    /// <summary>
    /// Applies a typed seed and a size. Returns true when a new map was generated, false when the change was refused
    /// (see <see cref="Problem"/>) or the seed and size are the ones already applied.
    /// </summary>
    public bool Apply(string seedText, int sizeValue) => Apply(seedText, sizeValue, force: false);

    bool Apply(string seedText, int sizeValue, bool force)
    {
        if (!Seed.TryParse(seedText, out var seed, out var problem))
        {
            Problem = problem;

            return false;
        }

        if (!force && Map is not null && seedText == SeedText && sizeValue == SizeValue)
        {
            Problem = null;

            return false;
        }

        try
        {
            if (!MapGenerator.TryGenerate(seed.ToMapSeed(), sizeValue, null, out var map, out problem))
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
        SizeValue = sizeValue;
        Problem = null;

        return true;
    }
}

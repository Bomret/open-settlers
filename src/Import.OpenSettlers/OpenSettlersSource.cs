using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OpenSettlers.Domain;

namespace OpenSettlers.Import.ContentSet;

/// <summary>
/// The graphics source of the repository's own content set <c>content/basic/</c> (ADR 0014, ADR 0015). The set
/// holds PNG images and one JSON manifest, <see cref="ManifestFile"/>: an object whose keys name draw terms and whose
/// values are <c>{ "image": "&lt;path&gt;", "anchor": [x, y] }</c>. The path is relative to the set's folder, and the
/// anchor is the offset of the image's top left corner from the term's position. The keys: <see cref="TerrainKey"/> for
/// each terrain code and triangle, <see cref="RoadKey"/> for each road direction, <see cref="FlagKey"/> and
/// <see cref="ObjectKey"/> for the flag and the object codes 8 to 127, <see cref="CastleKey"/> and
/// <see cref="WoodcutterKey"/> for the buildings, <see cref="GoodKey"/> for each of the 32 goods, <see cref="MarkerKey"/>
/// for each of the four build markers, <c>road-marker/end</c>, <c>road-marker/blocked</c>, <c>road-marker/way-back</c>, and
/// <c>road-marker/slope/</c> and the height difference for each slope from -4 to 4, <see cref="BorderPoleKey"/> and
/// <see cref="BorderWaterKey"/> for the border posts, <see cref="SettlerKey"/> for each job and kind of activity of a
/// settler, and <see cref="FlameKey"/> for each flame size and phase. The keys <see cref="SmallFlamesKey"/> and <see cref="LargeFlamesKey"/>
/// for the finished small and large buildings and <see cref="EarlySiteFlamesKey"/> hold no image but <c>{ "flames": [{ "size": "small" | "large", "at": [x, y] }, …] }</c>,
/// the flames of a burning building in the order they are drawn, each at its offset from the building (ADR 0021). The art is flat: one image per terrain code
/// and triangle, one strip per direction, whatever the slope, one border post on land and one in water, whatever the slope,
/// one frame per object, and one figure per job and kind of
/// activity, whatever the settler's heading, step, or frame. A settler's pixels in <see cref="MarkColour"/> show the
/// player's colour. The source draws no shadow, no water ripple, and no object of the codes 2 to 7. Every key above must
/// be in the manifest.
/// </summary>
public sealed class OpenSettlersSource : IGraphicsSource
{
    /// <summary>The manifest's file name in the content set's folder.</summary>
    public const string ManifestFile = "manifest.json";

    /// <summary>The number of terrain codes a triangle can hold, 0 to 15.</summary>
    public const int TerrainCodes = 16;

    /// <summary>The lowest object code with art besides the flag; codes 2 to 7 have none.</summary>
    public const int FirstObjectCode = 8;

    /// <summary>The highest object code.</summary>
    public const int LastObjectCode = 127;

    readonly ObjectImage[] _terrain;
    readonly ObjectImage[] _roads;
    readonly ObjectDrawing _flag;
    readonly ObjectDrawing[] _objects;
    readonly IReadOnlyDictionary<string, ObjectDrawing> _buildings;
    readonly ObjectDrawing[] _goods;
    readonly ObjectImage[] _settlers;
    readonly ObjectImage[] _markers;
    readonly ObjectImage[] _flameImages;
    readonly IReadOnlyDictionary<BuildingType, IReadOnlyList<Flame>> _flames;
    readonly IReadOnlyList<Flame> _earlySiteFlames;
    readonly IReadOnlyDictionary<RoadMarker, ObjectImage> _roadMarkers;
    readonly ObjectImage _pole;
    readonly ObjectImage _waterPost;
    readonly int _headroom;

    // The figure of a job and kind in a player's colour, one instance per job, kind, and colour.
    readonly Dictionary<(int Frame, int Colour), ObjectDrawing> _marked = [];

    // The drawings of the building sites by type and progress word.
    readonly SiteDrawings _sites;

    OpenSettlersSource(
        ObjectImage[] terrain,
        ObjectImage[] roads,
        ObjectDrawing flag,
        ObjectDrawing[] objects,
        IReadOnlyDictionary<string, ObjectDrawing> buildings,
        ObjectDrawing[] goods,
        ObjectImage[] settlers,
        ObjectImage[] markers,
        ObjectImage[] flameImages,
        IReadOnlyDictionary<BuildingType, IReadOnlyList<Flame>> flames,
        IReadOnlyList<Flame> earlySiteFlames,
        IReadOnlyDictionary<RoadMarker, ObjectImage> roadMarkers,
        ObjectImage pole,
        ObjectImage waterPost,
        int headroom
    )
    {
        _flameImages = flameImages;
        _flames = flames;
        _earlySiteFlames = earlySiteFlames;
        _settlers = settlers;
        _markers = markers;
        _roadMarkers = roadMarkers;
        _pole = pole;
        _waterPost = waterPost;
        _headroom = headroom;
        _terrain = terrain;
        _roads = roads;
        _flag = flag;
        _objects = objects;
        _buildings = buildings;
        _goods = goods;
        _sites = new SiteDrawings((type, layer) => SiteLayer(layer, type));
    }

    /// <summary>The number of player colours; any other player number, negative included, takes the colour of its number modulo this.</summary>
    public const int PlayerColours = 8;

    static readonly (byte R, byte G, byte B)[] Colours =
    [
        (220, 40, 40),
        (40, 90, 220),
        (240, 210, 40),
        (40, 180, 70),
        (240, 130, 30),
        (150, 60, 200),
        (40, 200, 210),
        (240, 240, 240),
    ];

    /// <summary>The colour of the mark pixels in a settler's art: pure magenta, which the art uses for nothing else.</summary>
    public static readonly (byte R, byte G, byte B) MarkColour = (255, 0, 255);

    /// <summary>The colour a player's mark shows in: the pixels of a settler's image in the mark colour take it.</summary>
    public static (byte R, byte G, byte B) PlayerColour(int player) => Colours[ColourIndex(player)];

    // The mathematical modulo: a negative number counts back from the last colour.
    static int ColourIndex(int player) => (player % PlayerColours + PlayerColours) % PlayerColours;

    /// <summary>The manifest key of the castle.</summary>
    public const string CastleKey = "building/castle";

    /// <summary>The manifest key of the woodcutter's hut.</summary>
    public const string WoodcutterKey = "building/woodcutter";

    /// <summary>The manifest key of the stonecutter's hut.</summary>
    public const string StonecutterKey = "building/stonecutter";

    /// <summary>The manifest key of the forester's hut.</summary>
    public const string ForesterKey = "building/forester";

    /// <summary>The manifest key of the sawmill.</summary>
    public const string SawmillKey = "building/sawmill";

    /// <summary>The manifest key of the fisher's hut.</summary>
    public const string FisherKey = "building/fisher";

    /// <summary>The manifest key of the farm.</summary>
    public const string FarmKey = "building/farm";

    /// <summary>The manifest key of the baker.</summary>
    public const string BakerKey = "building/baker";

    /// <summary>The manifest key of the mill.</summary>
    public const string MillKey = "building/mill";

    /// <summary>The manifest key of the pig farm.</summary>
    public const string PigFarmKey = "building/pig-farm";

    /// <summary>The manifest key of the butcher.</summary>
    public const string ButcherKey = "building/butcher";

    /// <summary>The manifest key of a building site of a small building that has not started (progress 1).</summary>
    public const string SiteSmallKey = "building/site-small";

    /// <summary>The manifest key of a building site of a large building that has not started (progress 0).</summary>
    public const string SiteLargeKey = "building/site-large";

    // The finished buildings the set draws, by type, and the sites by their progress.
    internal static readonly (BuildingType Type, string Key)[] FinishedKeys =
    [
        (BuildingType.Castle, CastleKey),
        (BuildingType.Woodcutter, WoodcutterKey),
        (BuildingType.Stonecutter, StonecutterKey),
        (BuildingType.Forester, ForesterKey),
        (BuildingType.Sawmill, SawmillKey),
        (BuildingType.Fisher, FisherKey),
        (BuildingType.Farm, FarmKey),
        (BuildingType.Baker, BakerKey),
        (BuildingType.Mill, MillKey),
        (BuildingType.PigFarm, PigFarmKey),
        (BuildingType.Butcher, ButcherKey),
    ];

    /// <summary>The manifest key of the scaffold a small building's site grows as its progress rises (the stage object 186 of the program).</summary>
    public const string StageSmallKey = "building/stage-small";

    /// <summary>The manifest key of the scaffold a large building's site grows (the stage object 181 of the program).</summary>
    public const string StageLargeKey = "building/stage-large";

    static readonly string[] BuildingKeys =
    [
        .. FinishedKeys.Select(finished => finished.Key),
        SiteSmallKey,
        SiteLargeKey,
        StageSmallKey,
        StageLargeKey,
    ];

    // The art of a layer of a building site (SiteStages): the ground and the base of a site, the scaffold of the size of the type's
    // building, and the finished building.
    ObjectDrawing? SiteLayer(SiteLayer layer, BuildingType type) =>
        layer switch
        {
            Import.SiteLayer.Ground => _buildings[SiteLargeKey],
            Import.SiteLayer.Base => _buildings[SiteSmallKey],
            Import.SiteLayer.Stage when SiteStages.ScaffoldIsLarge(type) is { } large => _buildings[
                large ? StageLargeKey : StageSmallKey
            ],
            Import.SiteLayer.Finished => FinishedKeys.FirstOrDefault(finished => finished.Type == type)
                is { Key: not null } known
                ? _buildings[known.Key]
                : null,
            _ => null,
        };

    /// <summary>The number of goods the set draws: a good's low 5 bits select one.</summary>
    public const int GoodCount = 32;

    /// <summary>The manifest key of a good, by its low 5 bits.</summary>
    public static string GoodKey(int code) => $"good/{code & (GoodCount - 1)}";

    /// <summary>The manifest key of a build marker: <c>marker/flag</c>, <c>marker/mine</c>, <c>marker/small</c>, or <c>marker/large</c>.</summary>
    public static string MarkerKey(BuildMarker marker) => $"marker/{marker.ToString().ToLowerInvariant()}";

    // The number of phases a flame is drawn in, 0 to 7.
    const int FlamePhases = 8;

    /// <summary>The manifest key of the image of a flame of a size at a phase, such as <c>flame/small/0</c>.</summary>
    public static string FlameKey(FlameSize size, int phase) => $"flame/{size.ToString().ToLowerInvariant()}/{phase}";

    /// <summary>The manifest key of the flames of a burning finished small building (<see cref="SiteStages.ScaffoldIsLarge"/> false).</summary>
    public const string SmallFlamesKey = "flames/small";

    /// <summary>The manifest key of the flames of a burning finished large building (<see cref="SiteStages.ScaffoldIsLarge"/> true).</summary>
    public const string LargeFlamesKey = "flames/large";

    /// <summary>The manifest key of the flames of a burning building site below 16000 progress.</summary>
    public const string EarlySiteFlamesKey = "flames/early-site";

    static readonly string[] FlamesKeys = [SmallFlamesKey, LargeFlamesKey, EarlySiteFlamesKey];

    /// <summary>The largest height difference either way the set draws a slope mark for.</summary>
    const int SlopeRange = 4;

    // The road markers the set draws, each with its manifest key: the three marks and a slope for each difference up to
    // the range either way.
    static readonly (RoadMarker Marker, string Key)[] RoadMarkerKeys =
    [
        (RoadMarker.Of(RoadMarkerKind.End), "road-marker/end"),
        (RoadMarker.Of(RoadMarkerKind.Blocked), "road-marker/blocked"),
        (RoadMarker.Of(RoadMarkerKind.WayBack), "road-marker/way-back"),
        .. Enumerable
            .Range(-SlopeRange, 2 * SlopeRange + 1)
            .Select(difference => (RoadMarker.Slope(difference), $"road-marker/slope/{difference}")),
    ];

    /// <summary>The manifest key of the border post on land.</summary>
    public const string BorderPoleKey = "border/pole";

    /// <summary>The manifest key of the border post in water.</summary>
    public const string BorderWaterKey = "border/water";

    /// <summary>The manifest key of the flag.</summary>
    public const string FlagKey = "object/flag";

    /// <summary>The manifest key of a map object of code 8 to 127.</summary>
    public static string ObjectKey(int code) => $"object/{code}";

    /// <summary>The manifest key of a terrain code's upper or lower triangle.</summary>
    public static string TerrainKey(int code, bool upper) => $"terrain/{code}/{(upper ? "upper" : "lower")}";

    /// <summary>The manifest key of a road direction's strip.</summary>
    public static string RoadKey(RoadDirection direction) =>
        direction switch
        {
            RoadDirection.East => "road/east",
            RoadDirection.SouthEast => "road/south-east",
            _ => "road/south",
        };

    /// <summary>
    /// Reads the content set in <paramref name="folder"/> and decodes each image through <paramref name="decoder"/>. Throws
    /// <see cref="InvalidDataException"/> with a message of the form <c>manifest.json: key: reason</c> for the first
    /// fault: malformed JSON (no key), a term with no image, a missing or undecodable PNG, a missing or non-integer
    /// anchor, a duplicate key, a path that is absolute or leaves the set, and missing or malformed flames. A broken set draws nothing, so the view
    /// never meets a missing image mid-frame.
    /// </summary>
    public static OpenSettlersSource Load(string folder, IImageDecoder decoder)
    {
        var (entries, flameLists) = ReadManifest(folder);
        var images = new Dictionary<(string Path, int X, int Y), ObjectImage>();
        var decoded = new Dictionary<string, DecodedImage>();
        ObjectImage Image(string key)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                throw Fault(key, "no image");
            }

            var (path, anchorX, anchorY) = entry;
            if (!images.TryGetValue((path, anchorX, anchorY), out var image))
            {
                if (!decoded.TryGetValue(path, out var pixels))
                {
                    var file = Path.Combine(folder, path);
                    if (!File.Exists(file))
                    {
                        throw Fault(key, $"the image {path} is missing");
                    }

                    pixels =
                        decoder.Decode(file) is { Width: > 0, Height: > 0 } read
                        && read.Rgba.Length == read.Width * read.Height * 4
                            ? read
                            : throw Fault(key, $"the image {path} cannot be decoded");
                    decoded[path] = pixels;
                }

                image = new ObjectImage
                {
                    Width = pixels.Width,
                    Height = pixels.Height,
                    OffsetX = anchorX,
                    OffsetY = anchorY,
                    Rgba = pixels.Rgba,
                };
                images[(path, anchorX, anchorY)] = image;
            }

            return image;
        }

        var terrain = new ObjectImage[TerrainCodes * 2];
        for (var code = 0; code < TerrainCodes; code++)
        {
            terrain[code * 2] = Image(TerrainKey(code, upper: true));
            terrain[code * 2 + 1] = Image(TerrainKey(code, upper: false));
        }

        var roads = new ObjectImage[3];
        for (var direction = 0; direction < roads.Length; direction++)
        {
            roads[direction] = Image(RoadKey((RoadDirection)direction));
        }

        var flag = new ObjectDrawing { Image = Image(FlagKey), Shadow = null };
        var objects = new ObjectDrawing[LastObjectCode - FirstObjectCode + 1];
        for (var code = FirstObjectCode; code <= LastObjectCode; code++)
        {
            objects[code - FirstObjectCode] = new ObjectDrawing { Image = Image(ObjectKey(code)), Shadow = null };
        }

        var buildings = BuildingKeys.ToDictionary(
            key => key,
            key => new ObjectDrawing { Image = Image(key), Shadow = null }
        );
        var goods = new ObjectDrawing[GoodCount];
        for (var code = 0; code < GoodCount; code++)
        {
            goods[code] = new ObjectDrawing { Image = Image(GoodKey(code)), Shadow = null };
        }

        var settlers = new ObjectImage[Jobs.Count * Kinds.Length];
        for (var job = 0; job < Jobs.Count; job++)
        {
            for (var kind = 0; kind < Kinds.Length; kind++)
            {
                settlers[job * Kinds.Length + kind] = Image(SettlerKey(Jobs[job].Key, Kinds[kind]));
            }
        }

        ObjectImage[] markers = [.. Enum.GetValues<BuildMarker>().Select(marker => Image(MarkerKey(marker)))];
        ObjectImage[] flameImages =
        [
            .. Enum.GetValues<FlameSize>()
                .SelectMany(size => Enumerable.Range(0, FlamePhases).Select(phase => Image(FlameKey(size, phase)))),
        ];
        var placements = FlamesKeys.ToDictionary(
            key => key,
            key => flameLists.TryGetValue(key, out var list) ? list : throw Fault(key, "no flames")
        );
        var flames = new Dictionary<BuildingType, IReadOnlyList<Flame>>();
        foreach (var (type, _) in FinishedKeys)
        {
            if (SiteStages.ScaffoldIsLarge(type) is { } large)
            {
                flames[type] = placements[large ? LargeFlamesKey : SmallFlamesKey];
            }
        }
        var roadMarkers = RoadMarkerKeys.ToDictionary(pair => pair.Marker, pair => Image(pair.Key));
        var pole = Image(BorderPoleKey);
        var waterPost = Image(BorderWaterKey);

        var headroom = objects
            .Concat([flag])
            .Concat(buildings.Values)
            .Concat(goods)
            .Max(drawing => drawing.Image!.Height);

        return new OpenSettlersSource(
            terrain,
            roads,
            flag,
            objects,
            buildings,
            goods,
            settlers,
            markers,
            flameImages,
            flames,
            placements[EarlySiteFlamesKey],
            roadMarkers,
            pole,
            waterPost,
            headroom
        );
    }

    // The jobs the game draws a settler for, by the name their manifest keys carry: the looks of the source (the registry is
    // generated from the look modules).
    internal static readonly IReadOnlyList<OpenSettlersLook> Jobs = OpenSettlersLookRegistry.Build();

    // The index of the job's look, or -1 for a job without one, which draws nothing.
    static int IndexOfLook(Job job)
    {
        for (var at = 0; at < Jobs.Count; at++)
        {
            if (Jobs[at].Job == job)
            {
                return at;
            }
        }

        return -1;
    }

    // The kinds of activity a settler is drawn in; one frame each, whatever the heading, step, or frame.
    static readonly string[] Kinds = ["walks", "carries", "cuts", "door", "hands-over", "waits"];

    /// <summary>The manifest key of the frame of a job (<c>carrier</c>, <c>woodcutter</c>, <c>stonecutter</c>, <c>fisher</c>) in an activity kind.</summary>
    public static string SettlerKey(string job, string kind) => $"settler/{job}/{kind}";

    // The kind of activity a settler term is drawn in: what it does, or that it carries a good.
    static int KindOf(SettlerDraw draw) =>
        draw.Activity switch
        {
            Cuts
            or CutsStone
            or Builds
            or Plants
            or TendsField
            or Digs
            or Saws
            or Grinds
            or Fishes
            or Bakes
            or Butchers
            or TendsPigs => 2,
            Door => 3,
            HandsOver => 4,
            WaitsOffRoad => 5,
            WalksFree { Trip.Purpose: BackToHut { Carrying: true } }
            or WaitsOutStep { Then: PutsDownGood }
            or PutsDownGood => 1,
            _ => draw.Carried.IsNone ? 0 : 1,
        };

    // The manifest's entries by key, each as its image path and anchor, checked for everything but the image file, and its
    // flame placements by key.
    static (
        Dictionary<string, (string Path, int X, int Y)> Entries,
        Dictionary<string, IReadOnlyList<Flame>> Flames
    ) ReadManifest(string folder)
    {
        var file = Path.Combine(folder, ManifestFile);
        if (!File.Exists(file))
        {
            throw new InvalidDataException($"{ManifestFile}: the content set has no manifest in {folder}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(file));
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{ManifestFile}: not valid JSON: {e.Message}", e);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"{ManifestFile}: not valid JSON: the manifest is not an object");
            }

            var entries = new Dictionary<string, (string, int, int)>();
            var flames = new Dictionary<string, IReadOnlyList<Flame>>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var key = property.Name;
                if (entries.ContainsKey(key) || flames.ContainsKey(key))
                {
                    throw Fault(key, "duplicate key");
                }

                if (key.StartsWith("flames/", StringComparison.Ordinal))
                {
                    flames[key] = FlameList(key, property.Value);
                }
                else
                {
                    entries[key] = Entry(key, property.Value);
                }
            }

            return (entries, flames);
        }
    }

    static (string Path, int X, int Y) Entry(string key, JsonElement value)
    {
        if (
            value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("image", out var image)
            || image.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(image.GetString())
        )
        {
            throw Fault(key, "no image");
        }

        var path = image.GetString()!;
        if (!Inside(path))
        {
            throw Fault(key, $"the path {path} is outside the content set");
        }

        if (
            !value.TryGetProperty("anchor", out var anchor)
            || anchor.ValueKind != JsonValueKind.Array
            || anchor.GetArrayLength() != 2
            || !Integer(anchor[0], out var x)
            || !Integer(anchor[1], out var y)
        )
        {
            throw Fault(key, "the anchor is missing or is not two integers, [x, y]");
        }

        return (path, x, y);
    }

    // The flames of a placement entry, each a size and an offset.
    static List<Flame> FlameList(string key, JsonElement value)
    {
        if (
            value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("flames", out var list)
            || list.ValueKind != JsonValueKind.Array
        )
        {
            throw Fault(key, "no flames");
        }

        var flames = new List<Flame>();
        foreach (var flame in list.EnumerateArray())
        {
            if (
                flame.ValueKind != JsonValueKind.Object
                || !flame.TryGetProperty("size", out var size)
                || size.ValueKind != JsonValueKind.String
                || !flame.TryGetProperty("at", out var at)
                || at.ValueKind != JsonValueKind.Array
                || at.GetArrayLength() != 2
                || !Integer(at[0], out var x)
                || !Integer(at[1], out var y)
            )
            {
                throw Fault(key, "a flame is not { \"size\": \"small\" or \"large\", \"at\": [x, y] }");
            }

            flames.Add(
                new Flame
                {
                    Size = size.GetString() switch
                    {
                        "small" => FlameSize.Small,
                        "large" => FlameSize.Large,
                        var other => throw Fault(key, $"the flame size {other} is neither small nor large"),
                    },
                    X = x,
                    Y = y,
                }
            );
        }

        return flames;
    }

    static bool Integer(JsonElement element, out int number)
    {
        number = 0;

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out number);
    }

    // A relative path whose segments, split at / and \, never go up a folder; a drive, root, or share is no relative path.
    static bool Inside(string path) =>
        !Path.IsPathRooted(path)
        && !path.StartsWith('/')
        && !path.StartsWith('\\')
        && !path.Contains(':')
        && Array.IndexOf(path.Split('/', '\\'), "..") < 0;

    static InvalidDataException Fault(string key, string reason) => new($"{ManifestFile}: {key}: {reason}");

    /// <inheritdoc />
    public int ObjectHeadroom => _headroom;

    /// <inheritdoc />
    public ObjectImage? Draw(TerrainTriangle triangle) =>
        triangle.Terrain.Code is >= 0 and < TerrainCodes
            ? _terrain[triangle.Terrain.Code * 2 + (triangle.Upper ? 0 : 1)]
            : null;

    /// <inheritdoc />
    public ObjectImage? Draw(RoadStrip strip) => _roads[(int)strip.Direction];

    /// <inheritdoc />
    public ObjectImage? Draw(BorderPost post) => post.Ground.IsWater ? _waterPost : _pole;

    /// <inheritdoc />
    public ObjectDrawing? Draw(MapObjectDraw draw) =>
        draw.Object == MapObject.Flag ? _flag
        : draw.Object.Code is >= FirstObjectCode and <= LastObjectCode ? _objects[draw.Object.Code - FirstObjectCode]
        : null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(BuildingDraw draw) =>
        draw.SiteProgress is { } progress ? _sites.Of(draw.Type, progress)
        : FinishedKeys.FirstOrDefault(finished => finished.Type == draw.Type) is { Key: not null } known
            ? _buildings[known.Key]
        : null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(GoodAtFlag draw) => _goods[draw.Good.Code & (GoodCount - 1)];

    /// <inheritdoc />
    public ObjectDrawing? Draw(SettlerDraw draw)
    {
        var job = IndexOfLook(draw.Job);
        if (draw.Part != SettlerPart.Figure || job < 0)
        {
            return null;
        }

        var index = job * Kinds.Length + KindOf(draw);
        var key = (index, ColourIndex(draw.Player));
        if (!_marked.TryGetValue(key, out var drawing))
        {
            drawing = new ObjectDrawing { Image = Marked(_settlers[index], PlayerColour(draw.Player)), Shadow = null };
            _marked[key] = drawing;
        }

        return drawing;
    }

    // The image with each pixel of the mark colour replaced by the player's colour.
    static ObjectImage Marked(ObjectImage image, (byte R, byte G, byte B) colour)
    {
        var rgba = (byte[])image.Rgba.Clone();
        for (var at = 0; at < rgba.Length; at += 4)
        {
            if (
                rgba[at] == MarkColour.R
                && rgba[at + 1] == MarkColour.G
                && rgba[at + 2] == MarkColour.B
                && rgba[at + 3] == 255
            )
            {
                (rgba[at], rgba[at + 1], rgba[at + 2]) = colour;
            }
        }

        return image with
        {
            Rgba = rgba,
        };
    }

    /// <inheritdoc />
    public RippleImage? Draw(WaterRipple ripple) => null;

    /// <inheritdoc />
    public ObjectImage? Draw(BuildMarker marker) => _markers[(int)marker];

    /// <inheritdoc />
    public IReadOnlyList<Flame> Flames(BuildingType type, bool earlySite) =>
        earlySite ? _earlySiteFlames : _flames.GetValueOrDefault(type, []);

    /// <inheritdoc />
    public ObjectImage? Draw(FlameDraw flame) =>
        _flameImages[(int)flame.Size * FlamePhases + (flame.Phase & (FlamePhases - 1))];

    /// <inheritdoc />
    public ObjectImage? Draw(RoadMarker marker) => _roadMarkers.GetValueOrDefault(marker);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>
/// The graphics source of the repository's own content set <c>content/open-settlers/</c> (ADR 0014, ADR 0015). The set
/// holds PNG images and one JSON manifest, <see cref="ManifestFile"/>: an object whose keys name draw terms and whose
/// values are <c>{ "image": "&lt;path&gt;", "anchor": [x, y] }</c>. The path is relative to the set's folder, and the
/// anchor is the offset of the image's top left corner from the term's position. The keys: <see cref="TerrainKey"/> for
/// each terrain code and triangle, and <see cref="RoadKey"/> for each road direction. The art is flat: one image per
/// terrain code and triangle, one strip per direction, whatever the slope. Terms the set has no art for yet (objects,
/// buildings, goods, settlers, ripples) draw nothing, and so the source leaves no object headroom and no flag
/// geometry. Every key above must be in the manifest.
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

    OpenSettlersSource(ObjectImage[] terrain, ObjectImage[] roads, ObjectDrawing flag, ObjectDrawing[] objects)
    {
        _terrain = terrain;
        _roads = roads;
        _flag = flag;
        _objects = objects;
    }

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
    /// anchor, a duplicate key, and a path that is absolute or leaves the set. A broken set draws nothing, so the view
    /// never meets a missing image mid-frame.
    /// </summary>
    public static OpenSettlersSource Load(string folder, IImageDecoder decoder)
    {
        var entries = ReadManifest(folder);
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

                image = new ObjectImage(pixels.Width, pixels.Height, anchorX, anchorY, pixels.Rgba);
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

        var flag = new ObjectDrawing(Image(FlagKey), null);
        var objects = new ObjectDrawing[LastObjectCode - FirstObjectCode + 1];
        for (var code = FirstObjectCode; code <= LastObjectCode; code++)
        {
            objects[code - FirstObjectCode] = new ObjectDrawing(Image(ObjectKey(code)), null);
        }

        return new OpenSettlersSource(terrain, roads, flag, objects);
    }

    // The manifest's entries by key, each as its image path and anchor, checked for everything but the image file.
    static Dictionary<string, (string Path, int X, int Y)> ReadManifest(string folder)
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
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var key = property.Name;
                if (entries.ContainsKey(key))
                {
                    throw Fault(key, "duplicate key");
                }

                entries[key] = Entry(key, property.Value);
            }

            return entries;
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
    public int ObjectHeadroom => 0;

    /// <inheritdoc />
    public ObjectImage? Draw(TerrainTriangle triangle) =>
        triangle.Terrain.Code is >= 0 and < TerrainCodes
            ? _terrain[triangle.Terrain.Code * 2 + (triangle.Upper ? 0 : 1)]
            : null;

    /// <inheritdoc />
    public ObjectImage? Draw(RoadStrip strip) => _roads[(int)strip.Direction];

    /// <inheritdoc />
    public ObjectDrawing? Draw(MapObjectDraw draw) =>
        draw.Object == MapObject.Flag ? _flag
        : draw.Object.Code is >= FirstObjectCode and <= LastObjectCode ? _objects[draw.Object.Code - FirstObjectCode]
        : null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(BuildingDraw draw) => null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(GoodAtFlag draw) => null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(SettlerDraw draw) => null;

    /// <inheritdoc />
    public RippleImage? Draw(WaterRipple ripple) => null;
}

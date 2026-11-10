using System;
using System.Collections.Generic;
using System.IO;
using OpenSettlers.Assets;

namespace OpenSettlers.MapView;

/// <summary>
/// The graphics source of the History Edition install (ADR 0014). It keeps the original's encoding inside itself: the
/// slope tables of <see cref="TerrainRules"/> and the texture picks of <see cref="RoadRules"/> choose a mask and a
/// texture entry of <c>loca/SPAD.PA</c>, and the source bakes the two into one RGBA image. The textured fill repeats the
/// texture down every <see cref="TerrainGraphics.TextureHeight"/> rows from the top of the mask
/// (<c>docs/rules/graphics-images.md</c>, "Textured fill"). A term the original has no mask for draws nothing.
/// </summary>
public sealed class HistoryEditionSource : IGraphicsSource
{
    readonly TerrainGraphics _terrain;
    readonly RoadGraphics? _roads;
    readonly Dictionary<(int Mask, int Texture), ObjectImage?> _terrainImages = [];
    readonly Dictionary<(int Mask, int Texture), ObjectImage?> _roadImages = [];

    HistoryEditionSource(TerrainGraphics terrain, RoadGraphics? roads, string? roadsProblem)
    {
        _terrain = terrain;
        _roads = roads;
        RoadsProblem = roadsProblem;
    }

    /// <summary>Why the road graphics did not load, or null. The source draws no road then and still draws the terrain.</summary>
    public string? RoadsProblem { get; }

    /// <summary>
    /// Reads the graphics from the install. Throws <see cref="FileNotFoundException"/> for a missing archive and
    /// <see cref="InvalidDataException"/>, naming the entry and the reason, where the terrain graphics are absent or
    /// unreadable. Unreadable road graphics set <see cref="RoadsProblem"/> instead.
    /// </summary>
    public static HistoryEditionSource Load(Install install)
    {
        var terrain = TerrainGraphics.Load(install);
        try
        {
            return new HistoryEditionSource(terrain, RoadGraphics.Load(install), null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return new HistoryEditionSource(terrain, null, e.Message);
        }
    }

    /// <inheritdoc />
    public ObjectImage? Draw(TerrainTriangle triangle)
    {
        var pick = triangle.Upper
            ? TerrainRules.Upper(triangle.Apex, triangle.Left, triangle.Right, triangle.Terrain)
            : TerrainRules.Lower(triangle.Apex, triangle.Left, triangle.Right, triangle.Terrain);

        return pick is { } draw
            ? Image(
                _terrainImages,
                draw.Mask,
                draw.Texture,
                _terrain.Masks[TerrainGraphics.MaskLayer(draw.Mask)],
                _terrain.Textures[TerrainGraphics.TextureLayer(draw.Texture)],
                -TerrainGraphics.LayerWidth / 2
            )
            : null;
    }

    /// <inheritdoc />
    public ObjectImage? Draw(RoadStrip strip) =>
        _roads is not null && RoadRules.Of(strip) is { } draw
            ? Image(
                _roadImages,
                draw.Mask,
                draw.Texture,
                _roads.Masks[RoadGraphics.MaskLayer(draw.Mask)],
                _roads.Textures[RoadGraphics.TextureLayer(draw.Texture)],
                0
            )
            : null;

    // The image of a mask and a texture entry, made once: the texture's color where the mask covers, nothing elsewhere.
    // A mask with no pixel draws nothing. A terrain mask hangs from its anchor centered, so the caller passes the shift.
    static ObjectImage? Image(
        Dictionary<(int Mask, int Texture), ObjectImage?> cache,
        int maskEntry,
        int textureEntry,
        MaskImage mask,
        byte[] texture,
        int shiftX
    )
    {
        if (cache.TryGetValue((maskEntry, textureEntry), out var cached))
        {
            return cached;
        }

        ObjectImage? image = null;
        if (mask.Width > 0 && mask.Height > 0)
        {
            var rgba = new byte[mask.Width * mask.Height * 4];
            for (var y = 0; y < mask.Height; y++)
            {
                for (var x = 0; x < mask.Width; x++)
                {
                    if (mask.Layer[y * TerrainGraphics.LayerWidth + x] != 0)
                    {
                        Array.Copy(
                            texture,
                            ((y % TerrainGraphics.TextureHeight) * TerrainGraphics.LayerWidth + x) * 4,
                            rgba,
                            (y * mask.Width + x) * 4,
                            4
                        );
                    }
                }
            }

            image = new ObjectImage(mask.Width, mask.Height, mask.OffsetX + shiftX, mask.OffsetY, rgba);
        }

        cache[(maskEntry, textureEntry)] = image;

        return image;
    }
}

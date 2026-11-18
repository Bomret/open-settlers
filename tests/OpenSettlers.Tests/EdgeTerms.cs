using System.Collections.Generic;
using System.Linq;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The terms the road pass of the views (TerrainQuads.Edges) hands its source, in the order it hands them: every road
// strip and every border post of the map.
static class EdgeTerms
{
    public static List<object> Of(Map map)
    {
        var source = new Recorder();
        TerrainQuads.Edges(map, source, new ImageLayers());
        return source.Terms;
    }

    public static List<RoadStrip> Strips(Map map) => [.. Of(map).OfType<RoadStrip>()];

    public static List<BorderPost> Posts(Map map) => [.. Of(map).OfType<BorderPost>()];

    // Records each strip and post it is asked for and draws nothing.
    sealed class Recorder : IGraphicsSource
    {
        public List<object> Terms { get; } = [];

        public ObjectImage? Draw(TerrainTriangle triangle) => null;

        public ObjectImage? Draw(BuildMarker marker) => null;

        public ObjectImage? Draw(RoadStrip strip)
        {
            Terms.Add(strip);
            return null;
        }

        public ObjectImage? Draw(BorderPost post)
        {
            Terms.Add(post);
            return null;
        }

        public ObjectDrawing? Draw(MapObjectDraw draw) => null;

        public ObjectDrawing? Draw(BuildingDraw draw) => null;

        public ObjectDrawing? Draw(GoodAtFlag draw) => null;

        public ObjectDrawing? Draw(SettlerDraw draw) => null;

        public RippleImage? Draw(WaterRipple ripple) => null;

        public int ObjectHeadroom => 0;
    }
}

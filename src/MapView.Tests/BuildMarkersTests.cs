using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The marker the original draws for each judgment of the build-site evaluator (docs/rules/build-sites.md, "The build-site
// overlay", 0x450d85).
[TestClass]
public sealed class BuildMarkersTests
{
    static BuildMarker? Of(int node, int site) =>
        BuildMarkers.Of(
            new BuildSiteResult
            {
                Node = node,
                Site = site,
                Height = 0,
            }
        );

    [TestMethod]
    public void EveryNodeStateAndSiteGivesTheMarkerOfTheRuleNote()
    {
        BuildMarker?[] bySite =
        [
            null,
            BuildMarker.Flag,
            BuildMarker.Mine,
            BuildMarker.Small,
            BuildMarker.Large,
            BuildMarker.Large,
        ];
        for (var node = 0; node <= 7; node++)
        {
            for (var site = 0; site <= 5; site++)
            {
                var expected =
                    node <= 3 ? null
                    : node == 4 ? (site == 0 ? null : BuildMarker.Flag)
                    : bySite[site];

                Assert.AreEqual(expected, Of(node, site), $"node state {node}, site {site}");
            }
        }
    }

    [TestMethod]
    public void TheEvaluatorsDefaultResultShowsNoMarker() => Assert.IsNull(BuildMarkers.Of(default));

    [TestMethod]
    public void TheOriginalDrawsAFlagMarkerWhereAFlagStandsSouthEast()
    {
        // Node state 5 means a flag stands on the south-east node; the flag click refuses it (AllowsFlag), but the
        // original's marker for site 1 there is a flag.
        var result = new BuildSiteResult
        {
            Node = 5,
            Site = 1,
            Height = 0,
        };

        Assert.IsFalse(result.AllowsFlag);
        Assert.AreEqual(BuildMarker.Flag, BuildMarkers.Of(result));
    }

    [TestMethod]
    public void TheCastleSiteShowsTheLargeBuildingMarker() => Assert.AreEqual(BuildMarker.Large, Of(7, 5));
}

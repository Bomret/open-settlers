using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The building pass's turn of a building site (0x42d328 for a large site, 0x42d55f for any, docs/rules/construction-materials.md): the
// rank of the flag's requests for planks and stones from what the site holds on site and on the way, and the large site's wait for
// the ground to be levelled. Tables of made-up values; the rank values the program gives are in ConstructionMaterialsTests.
[TestClass]
public sealed class SitePassTests
{
    private const int SiteFlag = 1;
    private static readonly Request Planks = new(ConstructionTables.PlankClass, 0);
    private static readonly Request Stones = new(ConstructionTables.StoneClass, 0);

    // The requests the site's pass sent to the castle, in order, with the building each came from.
    private static readonly List<(Building Site, WorkerRequest Request)> Asked = [];

    private static (BehaviourBench Bench, SitePass Pass, Building Site) Bench(
        BuildingType type,
        BuildingNeeds needs,
        ushort progress = 1
    )
    {
        Asked.Clear();
        var bench = new BehaviourBench();
        bench.Flags.Add(4, 4);
        bench.Flags.Add(8, 6);
        bench.Flags[SiteFlag].SetRequest(RequestWord.First, Planks);
        bench.Flags[SiteFlag].SetRequest(RequestWord.Second, Stones);
        var site = new Building(type, 7, 5, SiteFlag, constructing: true)
        {
            Progress = progress,
            Site = new BuildingSite
            {
                Planks = needs.Planks,
                Stones = needs.Stones,
                TargetHeight = 3,
            },
        };
        bench.Buildings.Add(site);
        var tables = ConstructionTables.Tables();

        return (
            bench,
            new SitePass(bench.Map, bench.Flags, tables, (building, request) => Asked.Add((building, request))),
            site
        );
    }

    // The node of the site and its six neighbours at one height, then the node itself at another.
    private static void Uneven(BehaviourBench bench, int ground = 3, int node = 2)
    {
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            var (dx, dy) = Direction.Steps[direction];
            bench.Map.At(7 + dx, 5 + dy).Height = ground;
        }

        bench.Map.At(7, 5).Height = node;
    }

    private static int Rank(BehaviourBench bench, RequestWord word) => bench.Flags[SiteFlag].RequestOf(word).Rank;

    [TestMethod]
    public void ASiteWithNothingAsksForPlanksAtTheRankTheStartGives()
    {
        var (bench, pass, site) = Bench(BuildingType.Woodcutter, new BuildingNeeds(2, 0));

        pass.Turn(site);

        // 0xff shifted right by the goods held (0), by 2 where no builder is inside, with bit 0 cleared.
        Assert.AreEqual(0x3e, Rank(bench, RequestWord.First));
        Assert.AreEqual(0, Rank(bench, RequestWord.Second), "a site that needs no stone asks for none");
    }

    [TestMethod]
    public void ASiteWithItsBuilderInsideAsksAtFullRank()
    {
        var (bench, pass, site) = Bench(BuildingType.Woodcutter, new BuildingNeeds(2, 0));
        site.WorkerInside = true;

        pass.Turn(site);

        Assert.AreEqual(0xfe, Rank(bench, RequestWord.First));
    }

    [TestMethod]
    public void TheRankFallsByAHalfForEachPlankOnSiteOrOnTheWay()
    {
        var (bench, pass, site) = Bench(BuildingType.Woodcutter, new BuildingNeeds(3, 0));
        site.WorkerInside = true;
        site.FillLow = 0x01; // one on the way

        pass.Turn(site);
        Assert.AreEqual(0x7e, Rank(bench, RequestWord.First));

        site.FillLow = 0x11; // one on site, one on the way
        pass.Turn(site);
        Assert.AreEqual(0x3e, Rank(bench, RequestWord.First));
    }

    [TestMethod]
    public void ASiteNeverAsksForMoreThanItNeeds()
    {
        var (bench, pass, site) = Bench(BuildingType.Woodcutter, new BuildingNeeds(2, 0));
        site.WorkerInside = true;
        site.FillLow = 0x11; // one on site, one on the way: the two it needs

        pass.Turn(site);

        Assert.AreEqual(0, Rank(bench, RequestWord.First));
    }

    [TestMethod]
    public void StonesStartFromTheFullByteWhateverThePlayerWordHolds()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2));
        site.WorkerInside = true;

        pass.Turn(site);

        Assert.AreEqual(0xfe, Rank(bench, RequestWord.First));
        Assert.AreEqual(0xfe, Rank(bench, RequestWord.Second));
    }

    [TestMethod]
    public void TheStonesAreCountedInTheHighFillByte()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2));
        site.WorkerInside = true;
        site.FillHigh = 0x01;

        pass.Turn(site);

        Assert.AreEqual(0xfe, Rank(bench, RequestWord.First), "no plank is held");
        Assert.AreEqual(0x7e, Rank(bench, RequestWord.Second));
    }

    [TestMethod]
    public void ALargeSiteAsksForNothingUntilTheGroundAroundItIsLevelled()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2), progress: 0);
        for (var direction = 0; direction < Direction.Count; direction++)
        {
            var (dx, dy) = Direction.Steps[direction];
            bench.Map.At(7 + dx, 5 + dy).Height = 3;
        }

        bench.Map.At(7, 5).Height = 2;
        pass.Turn(site);

        Assert.AreEqual((ushort)0, site.Progress);
        Assert.AreEqual(0, Rank(bench, RequestWord.First));

        bench.Map.At(7, 5).Height = 3;
        pass.Turn(site);

        Assert.AreEqual((ushort)1, site.Progress, "the levelled site starts");
        Assert.AreEqual(0x3e, Rank(bench, RequestWord.First));
    }

    [TestMethod]
    public void ALargeSiteOnUnevenGroundAsksForADiggerAndNotForItsBuilder()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2), progress: 0);
        Uneven(bench);

        pass.Turn(site);

        Assert.AreEqual(1, Asked.Count);
        Assert.AreSame(site, Asked[0].Site);
        Assert.AreEqual(new WorkerRequest(Job.Digger, ConstructionTables.DiggerTool), Asked[0].Request);
        Assert.AreEqual((ushort)0, site.Progress, "the site waits for the ground");
    }

    [TestMethod]
    public void ASiteWhoseLastRequestFailedWaitsForTheRetryScanBeforeItAsksAgain()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2), progress: 0);
        Uneven(bench);
        site.RequestFailed = true;

        pass.Turn(site);
        Assert.AreEqual(0, Asked.Count);

        site.RequestFailed = false;
        pass.Turn(site);
        Assert.AreEqual(1, Asked.Count);
    }

    [TestMethod]
    public void ADiggerInsideOrOnTheWayLeavesTheGroundToHim()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2), progress: 0);
        Uneven(bench);
        site.WorkerOnTheWay = true;
        pass.Turn(site);
        site.WorkerOnTheWay = false;
        site.WorkerInside = true;
        pass.Turn(site);

        Assert.AreEqual(0, Asked.Count);
        Assert.AreEqual((ushort)0, site.Progress);
    }

    [TestMethod]
    public void ALevelLargeSiteAsksForNoDiggerBeforeItStartsAndAsksForItsBuilder()
    {
        var (bench, pass, site) = Bench(BuildingType.Sawmill, new BuildingNeeds(3, 2), progress: 0);
        Uneven(bench, node: 3);

        pass.Turn(site);

        Assert.AreEqual(1, Asked.Count);
        Assert.AreEqual(Job.Builder, Asked[0].Request.Job);
    }

    [TestMethod]
    public void ASmallSiteOnUnevenGroundNeverAsksForADigger()
    {
        var (bench, pass, site) = Bench(BuildingType.Woodcutter, new BuildingNeeds(2, 0));
        Uneven(bench);

        pass.Turn(site);

        Assert.IsTrue(Asked.TrueForAll(asked => asked.Request.Job != Job.Digger));
    }
}

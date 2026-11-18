using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The digest of a codeless job's waiting settler (ADR 0020). It reads the adapters' digest (GameDigest) of a game whose castle list it
// empties through the castle roster (CastleRoster.ClearWaiting), which is internal to the application: no test pair reaches both, and
// no public operation empties the castle's list and changes nothing else the digest reads. Two layouts whose castles name different
// settlers (a woodcutter or none, or two counts of them) differ in the settler table as well, and GameDigest mixes each settler's job
// word (JobWords.Of, in MixSettlers) before the castle's list: their digests differ whether or not the list is mixed by name, so
// that comparison cannot fail on the list alone. The rest of the class is in src/MapView.Tests.
[TestClass]
public sealed class JobCatalogTests
{
    // A game on the made-up tables with a layout that stands the castle; no job of the catalog has an original code, as under the basic
    // set.
    static Game Started()
    {
        var layout = Layout.Read(LayoutText.Text(extra: """ "castle": {"x": 10, "y": 10} """), "test.jsonc").Layout!;
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var jobs = TestCatalog.Jobs(codes: false);
        var buildings = TestCatalog.Buildings(door: 8);
        var game = GameFactory.Create(
            new RuleTables(
                classes,
                MadeProgram.MadeStartStock(),
                catalog: new ContentCatalog(TestCatalog.StandIn(buildings).Goods, buildings, jobs)
            ),
            layout.BuildMap()
        );
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    [TestMethod]
    public void TheDigestOfAJobWithNoCodeMixesTheCastlesWaitingSettlerOfThatJobByName()
    {
        // The list of waiting settlers of a set whose jobs have no codes holds no fixed place for them: each waiting one adds its own part.
        var first = Started();
        var other = Started();
        for (var tick = 0; tick < 200 && first.Roster.Waiting(new Woodcutter()) == 0; tick++)
        {
            first.Tick();
            other.Tick();
        }

        Assert.AreNotEqual(0, first.Roster.Waiting(new Woodcutter()), "a woodcutter waits in the castle");
        Assert.AreEqual(first.Digest(), other.Digest());
        other.Roster.ClearWaiting(new Woodcutter());
        Assert.AreNotEqual(first.Digest(), other.Digest());
    }
}

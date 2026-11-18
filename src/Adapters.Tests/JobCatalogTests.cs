using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The digest's part for the castle's list of waiting settlers (GameDigest.MixWaiting, ADR 0020) where no job of the catalog has an
// original code, as under the basic set: a job with no code that has a settler waiting adds the hash of its stable name and the slot,
// and one with no settler waiting adds nothing. The part is read on its own, so the rest of the game the digest reads plays no role;
// the test does not empty the list, it compares castles that hold one settler of a job. The rest of the class is in src/MapView.Tests.
[TestClass]
public sealed class JobCatalogTests
{
    // A game on the made-up tables with a layout that stands the castle with the one settler of the job named after its first; no job
    // of the catalog has an original code.
    static Game Started(string job)
    {
        var layout = Layout
            .Read(
                LayoutText.Text(extra: $$$""" "castle": {"x": 10, "y": 10, "settlers": {"{{{job}}}": 1}} """),
                "test.jsonc"
            )
            .Layout!;
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

    // The game's waiting part mixed into the same start value.
    static ulong WaitingPart(Game game)
    {
        var hash = 0x1234_5678_9ABC_DEF0UL;
        GameDigest.MixWaiting(game, ref hash);

        return hash;
    }

    [TestMethod]
    public void TheDigestOfAJobWithNoCodeMixesTheCastlesWaitingSettlerOfThatJobByName()
    {
        // The list of waiting settlers of a set whose jobs have no codes holds no fixed place for them: each waiting one adds its own part.
        var woodcutter = Started("woodcutter");
        var forester = Started("forester");
        Assert.AreEqual(0, woodcutter.CastleWaitingSlot(new Woodcutter()), "no woodcutter waits before his turn");
        Assert.AreEqual(
            WaitingPart(woodcutter),
            WaitingPart(forester),
            "a job with no settler waiting adds nothing, whatever its name"
        );

        for (
            var tick = 0;
            tick < 200
                && (
                    woodcutter.CastleWaitingSlot(new Woodcutter()) == 0
                    || forester.CastleWaitingSlot(new Forester()) == 0
                );
            tick++
        )
        {
            woodcutter.Tick();
            forester.Tick();
        }

        Assert.AreNotEqual(0, woodcutter.CastleWaitingSlot(new Woodcutter()), "a woodcutter waits in the castle");
        Assert.AreEqual(
            woodcutter.CastleWaitingSlot(new Woodcutter()),
            forester.CastleWaitingSlot(new Forester()),
            "the forester waits in the same slot"
        );
        Assert.AreEqual(WaitingPart(woodcutter), WaitingPart(woodcutter), "the same list gives the same part");
        Assert.AreNotEqual(
            WaitingPart(woodcutter),
            WaitingPart(forester),
            "the same slot under another job's name gives another part"
        );
    }
}

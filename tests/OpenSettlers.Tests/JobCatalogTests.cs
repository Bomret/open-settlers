using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The jobs of the content catalog (ADR 0020): the catalog's refusals, order, and lookups, what the exporters write for a job with no
// original code, the readout, and the play script's lookup by stable name, with made-up entries.
[TestClass]
public sealed class JobCatalogTests
{
    static JobEntry Entry(string name, int? code = null) => new() { Name = name, OriginalCode = code };

    [TestMethod]
    public void TheCatalogOrdersItsJobsByStableNameWhateverOrderTheSourceGives()
    {
        var forward = new ContentCatalog([], null, [Entry("woodcutter"), Entry("baker"), Entry("carrier")]);
        var backward = new ContentCatalog([], null, [Entry("carrier"), Entry("baker"), Entry("woodcutter")]);

        CollectionAssert.AreEqual(
            new[] { "baker", "carrier", "woodcutter" },
            forward.Jobs.Select(entry => entry.Name).ToArray()
        );
        CollectionAssert.AreEqual(
            forward.Jobs.Select(entry => entry.Name).ToArray(),
            backward.Jobs.Select(entry => entry.Name).ToArray()
        );
    }

    [TestMethod]
    public void TheCatalogRefusesADuplicateNameADuplicateCodeAndABadNameNamingTheEntry()
    {
        var name = Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentCatalog([], null, [Entry("baker"), Entry("baker")])
        );
        var code = Assert.ThrowsExactly<ArgumentException>(() =>
            new ContentCatalog([], null, [Entry("baker", 5), Entry("miller", 5)])
        );
        var bad = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([], null, [Entry("Pig Farmer")]));
        var empty = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([], null, [Entry("")]));

        StringAssert.Contains(name.Message, "baker");
        StringAssert.Contains(code.Message, "miller");
        StringAssert.Contains(code.Message, "original code");
        StringAssert.Contains(bad.Message, "Pig Farmer");
        StringAssert.Contains(empty.Message, "job");
    }

    [TestMethod]
    public void AnUnportedJobIsNeverAnEntryAndAJobTheCatalogLacksIsAnErrorNamingIt()
    {
        var catalog = new ContentCatalog([], null, [Entry("baker")]);

        Assert.IsNull(catalog.EntryOf(new UnportedJob(9)));
        Assert.IsNull(catalog.EntryOf(new Miller()));
        Assert.AreEqual("baker", catalog.Require(new Baker()).Name);
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => catalog.Require(new Miller()));
        StringAssert.Contains(error.Message, "miller");
        StringAssert.Contains(error.Message, "baker");
        Assert.ThrowsExactly<InvalidOperationException>(() => catalog.Require(new UnportedJob(9)));
    }

    [TestMethod]
    public void AJobWithACodeWritesItsCodeAndAJobWithNoneTheWordOfItsNameWithTheLowByteClear()
    {
        var catalog = new ContentCatalog([], null, [Entry("baker", 16), Entry("miller"), Entry("woodcutter", 5)]);

        Assert.AreEqual(16, JobWords.Of(catalog, new Baker()));
        Assert.AreEqual(5, JobWords.Of(catalog, new Woodcutter()));
        var word = JobWords.Of(catalog, new Miller());
        Assert.AreEqual(0, word & 0xff, "it never equals a number of the original");
        Assert.AreEqual((int)(Fnv1a.Of("miller") & 0x7fffff00), word);
        Assert.IsTrue(JobWords.HasCode(catalog, new Baker()));
        Assert.IsFalse(JobWords.HasCode(catalog, new Miller()));
    }

    [TestMethod]
    public void AnUnportedJobWritesItsRawNumberWhateverTheCatalogHolds()
    {
        Assert.AreEqual(18, JobWords.Of(ContentCatalog.Empty, new UnportedJob(18)));
        Assert.IsTrue(JobWords.HasCode(ContentCatalog.Empty, new UnportedJob(18)));
    }

    [TestMethod]
    public void AJobTheCatalogLacksStopsTheExporterWithAnErrorNamingIt()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            JobWords.Of(ContentCatalog.Empty, new Digger())
        );

        StringAssert.Contains(error.Message, "digger");
    }

    // A game on the made-up tables with a layout that stands the castle. With codeless, no job of the catalog has an original code, as
    // under the basic set; a job named in omit is left out of the catalog.
    static Game Started(bool codeless, string? omit)
    {
        var layout = Layout.Read(LayoutTests.Text(extra: """ "castle": {"x": 10, "y": 10} """), "test.jsonc").Layout!;
        var classes = new ObjectClass[RuleTables.ClassCount];
        classes[MapObject.Flag.Code] = 3;
        var jobs = TestCatalog.Jobs(codes: !codeless).Where(entry => entry.Name != omit);
        var buildings = TestCatalog.Buildings(door: 8);
        var game = GameFactory.Create(
            new RuleTables(
                classes,
                TestProgram.MadeStartStock(),
                catalog: new ContentCatalog(TestCatalog.StandIn(buildings).Goods, buildings, jobs)
            ),
            layout.BuildMap()
        );
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    [TestMethod]
    public void AJobWithNoCodeExportsItsWordAndShowsItsNameWithNoRawValueBesideIt()
    {
        var game = Started(codeless: true, omit: null);

        var settler = game.Settlers[0];
        var catalog = game.Tables.Catalog;
        Assert.AreEqual(new CastleSettler(), settler.Job);
        Assert.AreEqual(
            JobWords.Of(catalog, settler.Job),
            game.History.Events.Of(game.History.SettlerId(0)).Last().State.Fields.Single(f => f.Field == "job").Value
        );
        Assert.AreEqual((int)(Fnv1a.Of("castle-settler") & 0x7fffff00), JobWords.Of(catalog, settler.Job));
        var line = Thing.Of(settler, catalog).Line();
        StringAssert.Contains(line, "castle-settler");
        Assert.IsFalse(line.Contains("job="), "a job with no code shows its name alone");
    }

    [TestMethod]
    public void AJobWithACodeShowsItsNumberBesideItsName()
    {
        var game = Started(codeless: false, omit: null);

        var line = Thing.Of(game.Settlers[0], game.Tables.Catalog).Line();

        StringAssert.Contains(line, "job=4 ");
        StringAssert.Contains(line, "castle-settler");
        Assert.AreEqual(
            4,
            game.History.Events.Of(game.History.SettlerId(0)).Last().State.Fields.Single(f => f.Field == "job").Value
        );
    }

    [TestMethod]
    public void TheDigestOfJobsWithNoCodeIsTheSameOnEveryRunAndDiffersFromTheOneOfTheirOriginalCodes()
    {
        var first = Started(codeless: true, omit: null).Digest();
        var again = Started(codeless: true, omit: null).Digest();
        var coded = Started(codeless: false, omit: null).Digest();

        Assert.AreEqual(first, again);
        Assert.AreNotEqual(first, coded);
    }

    [TestMethod]
    public void TheDigestOfAJobWithNoCodeMixesTheCastlesWaitingSettlerOfThatJobByName()
    {
        // The list of waiting settlers of a set whose jobs have no codes holds no fixed place for them: each waiting one adds its own part.
        var first = Started(codeless: true, omit: null);
        var other = Started(codeless: true, omit: null);
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

    [TestMethod]
    public void ARuleThatMeetsAJobTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        // The castle's first settler is the first the exporters meet.
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            Started(codeless: false, omit: "castle-settler")
        );

        StringAssert.Contains(error.Message, "castle-settler");
    }

    [TestMethod]
    public void AScriptNamesAJobByItsStableNameWhichTheCatalogOfEitherSetHolds()
    {
        foreach (var job in Job.Ported)
        {
            var (selector, problem) = ThingSelector.Parse(job.Name);
            Assert.IsNotNull(selector, $"{job.Name}: {problem}");
            Assert.AreEqual(job, FastForwardJobStep.Named(job.Name), job.Name);
        }

        Assert.IsNull(FastForwardJobStep.Named("chef"));
        Assert.IsNull(FastForwardJobStep.Named("unported-9"));
    }

    [TestMethod]
    public void TheJobsOfTheMadeUpCatalogsAreTheJobsOfTheEngine()
    {
        CollectionAssert.AreEqual(
            Job.Ported.Select(job => job.Name).Order(StringComparer.Ordinal).ToArray(),
            TestCatalog.Default.Jobs.Select(entry => entry.Name).ToArray()
        );
    }
}

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// A good with no original code (ADR 0020), `ai-token`, which only the made-up catalog's data gives: what the exporters write for it
// (the digest, the history's text, the snapshots) and the error a rule gives for a good its game's catalog lacks.
[TestClass]
public sealed class CodelessGoodTests
{
    const string CastleOnly = "\n\"castle\": {\"x\": 10, \"y\": 10}";

    // A game with the castle at 10,10 and its flag as flag 0, on the 26 goods and, where given, the code-less good.
    static Game Started(bool withAiToken)
    {
        var layout = Layout.Read(LayoutTests.Text(extra: CastleOnly), "t.jsonc").Layout!;
        var tables = LayoutSettlersTests.MadeTables(
            catalog: withAiToken ? TestCatalog.WithAiToken() : TestCatalog.Of()
        );
        var game = GameFactory.Create(tables, layout.BuildMap());
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    [TestMethod]
    public void ACodeLessGoodWithAZeroCountLeavesTheDigestAsItIs()
    {
        var without = Started(withAiToken: false);
        var with = Started(withAiToken: true);

        Assert.AreEqual(0, with.CastleStock.Count(TestCatalog.AiToken));
        Assert.AreEqual(without.Digest(), with.Digest());
    }

    [TestMethod]
    public void ACodeLessGoodWithANonZeroCountChangesTheDigestAndEachCountGivesItsOwn()
    {
        var game = Started(withAiToken: true);
        var none = game.Digest();

        game.CastleStock.Add(TestCatalog.AiToken);
        var one = game.Digest();
        game.CastleStock.Add(TestCatalog.AiToken);
        var two = game.Digest();
        game.CastleStock.Take(TestCatalog.AiToken);
        game.CastleStock.Take(TestCatalog.AiToken);

        Assert.AreNotEqual(none, one);
        Assert.AreNotEqual(one, two);
        Assert.AreEqual(none, game.Digest(), "the stock back at zero is the digest it was");
    }

    [TestMethod]
    public void ACodeLessGoodOnAFlagChangesTheDigest()
    {
        var game = Started(withAiToken: true);
        var before = game.Digest();

        game.Goods.LayGood(0, 2, TestCatalog.AiToken);

        Assert.AreNotEqual(before, game.Digest());
    }

    [TestMethod]
    public void ACodeLessGoodOnAFlagWritesAFlagSnapshotFieldNoOriginalGoodHasAndKeepsTheDirectionBits()
    {
        var catalog = TestCatalog.WithAiToken();
        var flags = new FlagTable { { 4, 5 } };
        flags[0].Places[2].Good = TestCatalog.AiToken;
        flags[0].Places[3].Good = TestCatalog.AiToken;
        flags[0].Places[3].Way = 2;
        var state = new SnapshotExporter([], [], flags, catalog).Flag(0);

        var plain = state.Get("good2")!.Value;
        var directed = state.Get("good3")!.Value;

        Assert.AreEqual(GoodWords.Of(catalog, TestCatalog.AiToken), plain);
        Assert.IsGreaterThan(0xff, plain, "no original good has that number");
        Assert.AreEqual(plain | 3 << 5, directed, "the direction plus one sits in bits 5 to 7 beside the word");
    }

    [TestMethod]
    public void EventTextNamesAnOriginalGoodByItsNumberAndACodeLessGoodByItsName()
    {
        var exporter = new SnapshotExporter([], [], [], TestCatalog.WithAiToken());

        Assert.AreEqual("7", exporter.GoodText(Good.Log));
        Assert.AreEqual("26", exporter.GoodText(new Good { Name = "shield" }));
        Assert.AreEqual("0", exporter.GoodText(Good.None));
        Assert.AreEqual("ai-token", exporter.GoodText(TestCatalog.AiToken));
    }

    [TestMethod]
    public void TwoCodeLessGoodsGetTwoWordsThatNoOriginalGoodHas()
    {
        var other = new GoodEntry
        {
            Name = "wine-skin",
            Counter = FillCounter.Low,
            HandoverCounter = FillCounter.Low,
            CountsAgainstResource1 = false,
            TransportRank = 101,
        };
        var catalog = new ContentCatalog([.. TestCatalog.WithAiToken().Goods, other]);

        var words = new[]
        {
            TestCatalog.AiToken,
            new Good { Name = "wine-skin" },
        }.Select(good => GoodWords.Of(catalog, good)).ToArray();

        Assert.AreNotEqual(words[0], words[1]);
        Assert.IsTrue(words.All(word => word > 0xff && (word & 0xff) == 0));
    }

    [TestMethod]
    public void ARuleThatMeetsAGoodTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        var game = Started(withAiToken: false);
        game.Goods.LayGood(0, 0, TestCatalog.AiToken);

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => game.Goods.Turn(0));

        StringAssert.Contains(error.Message, "ai-token");
    }

    // Both names share the exporter word: the hash's low 31 bits, low byte cleared (found by search).
    [TestMethod]
    public void TwoGoodsWithNoCodeAndOneExporterWordAreRefusedNamingBoth()
    {
        var catalog = new ContentCatalog([CodelessEntry("g5258", 1), CodelessEntry("g7361", 2)]);

        var error = Assert.ThrowsExactly<ArgumentException>(() => GoodWords.Check(catalog));

        StringAssert.Contains(error.Message, "g5258");
        StringAssert.Contains(error.Message, "g7361");
    }

    [TestMethod]
    public void CreatingAGameOverTwoCodeLessGoodsWithOneExporterWordIsRefusedNamingBothAndTheTablesParameter()
    {
        var layout = Layout.Read(LayoutTests.Text(extra: CastleOnly), "t.jsonc").Layout!;
        var tables = LayoutSettlersTests.MadeTables(
            catalog: new ContentCatalog([CodelessEntry("g5258", 1), CodelessEntry("g7361", 2)])
        );

        var error = Assert.ThrowsExactly<ArgumentException>(() => GameFactory.Create(tables, layout.BuildMap()));

        StringAssert.Contains(error.Message, "g5258");
        StringAssert.Contains(error.Message, "g7361");
        Assert.AreEqual("tables", error.ParamName);
    }

    static GoodEntry CodelessEntry(string name, int rank) =>
        new()
        {
            Name = name,
            Counter = FillCounter.Low,
            HandoverCounter = FillCounter.Low,
            CountsAgainstResource1 = false,
            TransportRank = rank,
        };
}

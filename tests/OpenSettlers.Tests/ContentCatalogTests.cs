using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The content catalog of goods (ADR 0020): its order, its refusals, and its lookups, with made-up entries.
[TestClass]
public sealed class ContentCatalogTests
{
    static GoodEntry Entry(
        string name,
        int rank,
        int? code = null,
        GoodClass? request = null,
        FillCounter counter = FillCounter.Low
    ) =>
        new()
        {
            Name = name,
            OriginalCode = code,
            Request = request,
            Counter = counter,
            HandoverCounter = counter,
            CountsAgainstResource1 = false,
            TransportRank = rank,
        };

    [TestMethod]
    public void TheCatalogOrdersItsGoodsByStableNameWhateverOrderTheSourceGives()
    {
        GoodEntry[] entries = [Entry("plank", 1), Entry("ai-token", 2), Entry("log", 3), Entry("Zed".ToLowerInvariant(), 4)];

        var forward = new ContentCatalog(entries);
        var backward = new ContentCatalog(entries.Reverse());

        CollectionAssert.AreEqual(new[] { "ai-token", "log", "plank", "zed" }, forward.Goods.Select(good => good.Name).ToArray());
        CollectionAssert.AreEqual(forward.Goods.Select(good => good.Name).ToArray(), backward.Goods.Select(good => good.Name).ToArray());
    }

    [TestMethod]
    public void TheOrderIsOrdinalSoAHyphenSortsBeforeALetter()
    {
        var catalog = new ContentCatalog([Entry("iron-ore", 1), Entry("iron", 2), Entry("iron2", 3)]);

        CollectionAssert.AreEqual(new[] { "iron", "iron-ore", "iron2" }, catalog.Goods.Select(good => good.Name).ToArray());
    }

    [TestMethod]
    public void TwoGoodsWithOneStableNameAreRefusedNamingTheGood()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([Entry("log", 1), Entry("log", 2)]));

        StringAssert.Contains(error.Message, "log");
        StringAssert.Contains(error.Message, "stable name");
    }

    [TestMethod]
    public void TwoGoodsWithOneOriginalCodeAreRefusedNamingTheGood()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(
            () => new ContentCatalog([Entry("log", 1, code: 7), Entry("plank", 2, code: 7)])
        );

        StringAssert.Contains(error.Message, "plank");
        StringAssert.Contains(error.Message, "original code");
    }

    [TestMethod]
    [DataRow("Log")]
    [DataRow("iron ore")]
    [DataRow("iron_ore")]
    [DataRow("")]
    [DataRow("ärmel")]
    public void AStableNameOutsideLowercaseLettersDigitsAndHyphensIsRefusedNamingTheGood(string name)
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([Entry(name, 1)]));

        StringAssert.Contains(error.Message, $"'{name}'");
        StringAssert.Contains(error.Message, "stable name");
    }

    [TestMethod]
    public void TwoGoodsWithOneTransportRankAreRefusedNamingTheGood()
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([Entry("log", 5), Entry("plank", 5)]));

        StringAssert.Contains(error.Message, "plank");
        StringAssert.Contains(error.Message, "transport rank");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-3)]
    public void ATransportRankBelowOneIsRefusedNamingTheGood(int rank)
    {
        var error = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([Entry("log", rank)]));

        StringAssert.Contains(error.Message, "log");
        StringAssert.Contains(error.Message, "transport rank");
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(8)]
    public void ARequestClassThatDoesNotFitItsRequestWordIsRefusedNamingTheGood(int requestClass)
    {
        var request = new GoodClass { Class = requestClass, Word = RequestWord.First };

        var error = Assert.ThrowsExactly<ArgumentException>(() => new ContentCatalog([Entry("log", 1, request: request)]));

        StringAssert.Contains(error.Message, "log");
        StringAssert.Contains(error.Message, "request class");
    }

    [TestMethod]
    public void AClassOfZeroToSevenFitsEitherRequestWord()
    {
        var catalog = new ContentCatalog(
            [
                Entry("log", 1, request: new GoodClass { Class = 0, Word = RequestWord.First }),
                Entry("plank", 2, request: new GoodClass { Class = 7, Word = RequestWord.Second }),
            ]
        );

        Assert.AreEqual(2, catalog.Goods.Length);
    }

    [TestMethod]
    public void AGoodIsFoundByItsNameOrElseByItsOriginalCodeAndNoGoodIsNeverFound()
    {
        var catalog = new ContentCatalog([Entry("log", 1, code: 7), Entry("ai-token", 2)]);

        Assert.AreEqual("log", catalog.EntryOf(new Good { Code = 7, Name = "log" })!.Name);
        Assert.AreEqual("log", catalog.EntryOf(new Good { Code = 7, Name = "made-up" })!.Name, "by the code where the name is unknown");
        Assert.AreEqual("log", catalog.EntryOf(new Good { Code = 7 })!.Name);
        Assert.AreEqual("ai-token", catalog.EntryOf(new Good { Code = 99, Name = "ai-token" })!.Name);
        Assert.IsNull(catalog.EntryOf(new Good { Code = 99 }));
        Assert.IsNull(catalog.EntryOf(Good.None));
    }
}

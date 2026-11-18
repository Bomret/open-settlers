using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The castle's stock at a start stock level (docs/rules/castle.md): each count steps from its value in the lower row of the table
// towards the higher row by the tenths of the level, rounded half up, less what the castle's site and first settlers take.
// The made-up tables (TestProgram.Made) hold 10 * row + good in the entry of each good, so the rows differ by 10 everywhere and each
// tenth of a level adds 1: the expected counts are hand arithmetic, level 0 counting the entries of the good as they are less the
// subtraction vector of docs/rules/castle.md (entry 7 by 7, 9 by 2, 15 by 1, 16 by 4, 17 by 1, 20 by 1, 21 by 2, 22 by 3, 24 by 3,
// 25 by 3).
[TestClass]
public sealed class StartStockTests
{
    // Entry e less its subtraction at level 0, whose row 0 holds e in the entry e.
    static readonly int[] LevelZero =
    [
        0,
        1,
        2,
        3,
        4,
        5,
        6,
        0,
        8,
        7,
        10,
        11,
        12,
        13,
        14,
        14,
        12,
        16,
        18,
        19,
        19,
        19,
        19,
        23,
        21,
        22,
    ];

    // The counts of the goods in the order of the made-up catalog's codes.
    static int[] AtLevel(int level)
    {
        var stock = StartStock.At(TestProgram.Made(), level);

        return [.. Enumerable.Range(1, TestCatalog.Names.Length).Select(code => stock[TestCatalog.GoodOf(code)])];
    }

    [TestMethod]
    public void AStartStockGoodTheCatalogLacksStopsWithAnErrorNamingIt()
    {
        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            TestProgram.MadeStartStock(),
            goods: new ContentCatalog([TestCatalog.Entry(7, 1)])
        );

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => StartStock.At(tables, 0));

        StringAssert.Contains(error.Message, $"'{tables.StartStock[0].First().Key.Name}'");
        StringAssert.Contains(error.Message, "is no entry of the content catalog");
    }

    [TestMethod]
    public void ADeductionForAGoodNoRowHoldsStopsWithAnErrorNamingIt()
    {
        var rows = TestProgram
            .MadeStartStock()
            .Select(row => (IReadOnlyDictionary<Good, byte>)row.Where(pair => pair.Key.Name != "plank").ToDictionary())
            .ToArray();
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], rows, goods: TestCatalog.Of());

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => StartStock.At(tables, 0));

        StringAssert.Contains(error.Message, "'plank'");
    }

    [TestMethod]
    public void LevelZeroTakesTheCastlesSubtractionsFromTheFirstRow()
    {
        CollectionAssert.AreEqual(LevelZero, AtLevel(0));
    }

    [TestMethod]
    [DataRow(5, 5)] // half way from row 0 to row 1: 10 * 5 / 10
    [DataRow(10, 10)] // level 10 is row 1: 10 more than row 0
    [DataRow(15, 15)] // row 1 plus half of the 10 to row 2
    [DataRow(25, 25)] // row 2 plus half of the 10 to row 3
    [DataRow(39, 39)] // row 3 plus 9 tenths of the 10 to row 4 = 30 + 9
    [DataRow(40, 40)] // level 40 is row 4
    [DataRow(255, 40)] // no row beyond the fifth: the counts stay at row 4
    public void TheCountsStepFromTheLowerRowTowardsTheHigherByTheTenthsOfTheLevel(int level, int rise)
    {
        CollectionAssert.AreEqual(LevelZero.Select(count => count + rise).ToArray(), AtLevel(level));
    }

    // Level 1 is one tenth of the way from row 0 to row 1. With the row 0 at 100 and the row 1 at 100 + d the count is
    // 100 + d / 10 rounded half up (hand arithmetic): d = 4 gives 0.4 -> 0, 5 gives 0.5 -> 1, 14 gives 1.4 -> 1, 15 gives 1.5 -> 2,
    // 24 gives 2.4 -> 2, and 25 gives 2.5 -> 3. The entries 0 to 5 have no subtraction.
    [TestMethod]
    public void TheInterpolatedCountIsRoundedHalfUp()
    {
        var rows = Enumerable
            .Range(0, RuleTables.StartStockRows)
            .Select(_ => TestCatalog.Names.ToDictionary(name => new Good { Name = name }, _ => (byte)100))
            .ToArray();
        int[] differences = [4, 5, 14, 15, 24, 25];
        for (var good = 0; good < differences.Length; good++)
        {
            rows[1][TestCatalog.GoodOf(good + 1)] = (byte)(100 + differences[good]);
        }

        var tables = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            [.. rows.Select(row => (IReadOnlyDictionary<Good, byte>)row)],
            goods: TestCatalog.Of()
        );
        var stock = StartStock.At(tables, 1);

        CollectionAssert.AreEqual(
            new[] { 100, 101, 101, 102, 102, 103 },
            Enumerable.Range(1, differences.Length).Select(code => stock[TestCatalog.GoodOf(code)]).ToArray()
        );
    }
}

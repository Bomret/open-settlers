using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The rule tables give the engine values that no consumer can change, and each lookup keeps its answer in range and out of
// range. The tables are built from made-up values that differ from one another, so a lookup that reads the wrong entry shows.
[TestClass]
public sealed class RuleTablesShapeTests
{
    static readonly Type[] Mutable =
    [
        typeof(List<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(Dictionary<,>),
        typeof(IDictionary<,>),
        typeof(HashSet<>),
        typeof(ISet<>),
        typeof(Span<>),
        typeof(Memory<>),
    ];

    static bool GivesMutableView(Type type) =>
        type.IsArray
        || type.IsByRef
        || (type.IsGenericType && Mutable.Contains(type.GetGenericTypeDefinition()))
        || (type.IsGenericType && type.GetGenericArguments().Any(GivesMutableView));

    static IEnumerable<string> Offenders(Type type)
    {
        const BindingFlags Members =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var field in type.GetFields(Members).Where(f => GivesMutableView(f.FieldType)))
        {
            yield return $"{type.Name}.{field.Name}";
        }

        foreach (var method in type.GetMethods(Members).Where(m => GivesMutableView(m.ReturnType)))
        {
            yield return $"{type.Name}.{method.Name}";
        }
    }

    [TestMethod]
    public void NoPublicMemberOfTheRuleTablesGivesAnArrayOrAnotherMutableView()
    {
        Assert.IsEmpty(Offenders(typeof(RuleTables)).ToList());
    }

    static RuleTables Distinct() =>
        new(
            [.. Enumerable.Range(0, RuleTables.ClassCount).Select(i => (ObjectClass)(i % 4))],
            TestProgram.MadeStartStock(),
            stepTimes: [.. Enumerable.Range(0, RuleTables.StepTimeCount).Select(i => (ushort)(1000 + i))],
            doorSpeeds: [.. Enumerable.Range(0, RuleTables.DoorSpeedCount).Select(i => (byte)(100 + i))],
            scanWindows: [.. Enumerable.Range(0, RuleTables.ScanWindowCount).Select(i => (ushort)(300 + i))],
            roadCapacities: [.. Enumerable.Range(0, RuleTables.RoadCapacityCount).Select(i => (byte)(10 + i))],
            walkRows:
            [
                .. Enumerable
                    .Range(0, RuleTables.WalkRowCount * RuleTables.WalkRowSize)
                    .Select(i => (byte)(4 * (i % 6 + 1))),
            ],
            buildingSizes: [.. Enumerable.Repeat((byte)2, RuleTables.BuildingTypeCount)],
            buildingNeeds: [.. Enumerable.Range(0, 2 * RuleTables.BuildingTypeCount).Select(i => (byte)(i + 1))]
        );

    [TestMethod]
    public void TheLookupsThatReadAnEntryOutsideTheirTableThrow()
    {
        var tables = Distinct();

        Assert.Throws<IndexOutOfRangeException>(() => tables.DoorSpeed(RuleTables.DoorSpeedCount));
        Assert.Throws<IndexOutOfRangeException>(() => tables.RoadCapacity(RuleTables.RoadCapacityCount));
        Assert.Throws<IndexOutOfRangeException>(() => tables.ScanWindow(RuleTables.ScanWindowCount));
        Assert.Throws<IndexOutOfRangeException>(() => tables.WalkDirection(RuleTables.WalkRowCount, 0));
        Assert.Throws<IndexOutOfRangeException>(() => tables.ClassOf(new MapObject { Code = RuleTables.ClassCount }));
    }

    [TestMethod]
    public void TheLookupsGiveTheEntryOfTheirIndexInRange()
    {
        var tables = Distinct();

        Assert.AreEqual((byte)107, tables.DoorSpeed(7));
        Assert.AreEqual((byte)15, tables.RoadCapacity(5));
        Assert.AreEqual((ushort)303, tables.ScanWindow(3));
        Assert.AreEqual(4, tables.WalkDirection(2, 0), "the entry 16 holds the code 4 * 5");
    }

    [TestMethod]
    public void TheStartStockHoldsTheGoodsOfEachRowAtTheLevels0To40()
    {
        var tables = TestProgram.Made();

        Assert.AreEqual(RuleTables.StartStockRows, tables.StartStock.Length);
        for (var row = 0; row < RuleTables.StartStockRows; row++)
        {
            Assert.AreEqual(TestCatalog.Names.Length, tables.StartStock[row].Count);
            Assert.AreEqual((byte)(row * 10 + 4), tables.StartStock[row][TestCatalog.GoodOf(5)], $"row {row}");
        }
    }

    [TestMethod]
    public void TheFigureTablesGiveTheFrameRowOrRowZeroPastTheFrames()
    {
        var rows = new sbyte[RuleTables.FrameRowCount];
        rows[10] = -1;
        var figures = new FigureTables(frameRows: rows);

        Assert.AreEqual(-1, figures.FrameRow(10));
        Assert.AreEqual(0, figures.FrameRow(RuleTables.FrameRowCount));
    }

    [TestMethod]
    public void TheFigureTablesGiveTheCarriedWordOrZeroPastTheGoods()
    {
        var carried = new short[RuleTables.CarriedFigureCount];
        carried[7] = 0x0b00;
        var figures = new FigureTables(carriedFigures: carried);

        Assert.AreEqual((short)0x0b00, figures.CarriedWord(7));
        Assert.AreEqual((short)0, figures.CarriedWord(RuleTables.CarriedFigureCount));
    }

    [TestMethod]
    public void TheFigureTablesRefuseWaitingCarrierTablesOfALengthThatIsNoPowerOfTwo()
    {
        Assert.Throws<ArgumentException>(() => new FigureTables(idlePhases: new short[12]));
        Assert.Throws<ArgumentException>(() => new FigureTables(idleFigures: new short[100]));
    }

    [TestMethod]
    public void TheFigureTablesAreCopiedAndHoldNoArrayOfTheCaller()
    {
        var bases = new short[2 * RuleTables.FigureBaseRowCount];
        var figures = new FigureTables(figureBases: bases);
        bases[0] = 99;

        Assert.AreEqual((short)0, figures.Bases[0]);
        Assert.IsEmpty(Offenders(typeof(FigureTables)).ToList());
    }

    [TestMethod]
    public void TheStartStockIsCopiedAndHoldsNoRowOfTheCaller()
    {
        var rows = TestProgram.MadeStartStock();
        var tables = new RuleTables(new ObjectClass[RuleTables.ClassCount], rows);
        ((Dictionary<Good, byte>)rows[2])[TestCatalog.GoodOf(5)] = 99;

        Assert.AreEqual((byte)24, tables.StartStock[2][TestCatalog.GoodOf(5)]);
    }

    [TestMethod]
    public void AStartStockRowPastTheLastIsNoRow()
    {
        var tables = TestProgram.Made();

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => tables.StartStock[RuleTables.StartStockRows]);
    }
}

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// A job is a type (ADR 0016): Job.Of finds the type of a number, and the table answers the original's number gave sit on the types.
[TestClass]
public sealed class JobTests
{
    private static readonly (int Code, Type Type)[] Ported =
    [
        (0, typeof(Carrier)),
        (2, typeof(Digger)),
        (3, typeof(Builder)),
        (4, typeof(CastleSettler)),
        (5, typeof(Woodcutter)),
        (6, typeof(Sawmiller)),
        (7, typeof(Stonecutter)),
        (8, typeof(Forester)),
        (11, typeof(TypeOneHut)),
        (21, typeof(Jobless)),
    ];

    [TestMethod]
    public void OfGivesAJobWhoseCodeIsTheNumberForEveryByte()
    {
        for (var code = 0; code <= 255; code++)
        {
            Assert.AreEqual(code, Job.Of(code).Code, $"job {code}");
        }
    }

    [TestMethod]
    public void OfGivesTheTypeOfEachPortedNumber()
    {
        foreach (var (code, type) in Ported)
        {
            Assert.AreEqual(type, Job.Of(code).GetType(), $"job {code}");
        }
    }

    [TestMethod]
    public void OfGivesAKnightOfTheLevelForTheNumbers22To26()
    {
        for (var code = 22; code <= 26; code++)
        {
            Assert.AreEqual(new Knight(code - 22), Job.Of(code), $"job {code}");
        }
    }

    [TestMethod]
    public void OfGivesAnUnportedJobForEveryOtherNumber()
    {
        var ported = Ported.Select(entry => entry.Code).Concat(Enumerable.Range(22, 5)).ToHashSet();
        for (var code = 0; code <= 255; code++)
        {
            if (!ported.Contains(code))
            {
                Assert.AreEqual(new UnportedJob(code), Job.Of(code), $"job {code}");
            }
        }
    }

    [TestMethod]
    public void AJobBuiltAnyOtherWayEqualsOfItsNumber()
    {
        Assert.AreEqual(Job.Of(0), Job.Carrier);
        Assert.AreEqual(Job.Of(5), Job.Woodcutter);
        Assert.AreEqual(Job.Of(11), Job.TypeOneHut);
        Assert.AreEqual(Job.Of(21), Job.Jobless);
        Assert.AreEqual(Job.Of(24), new Knight(2));
        Assert.AreEqual(Job.Of(9).GetHashCode(), new UnportedJob(9).GetHashCode());
        Assert.AreEqual(Job.Of(27), Job.Of(26).Promoted);
    }

    [TestMethod]
    public void AJobPrintsAsItsBareNumber()
    {
        Assert.AreEqual("5", Job.Woodcutter.ToString());
        Assert.AreEqual("24", new Knight(2).ToString());
        Assert.AreEqual("9", new UnportedJob(9).ToString());
    }

    [TestMethod]
    public void AnUnportedJobRefusesANumberAnotherJobOwns()
    {
        foreach (var code in new[] { 0, 5, 11, 21, 22, 26 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new UnportedJob(code), $"job {code}");
        }
    }

    [TestMethod]
    public void AKnightRefusesALevelThatIsNotOneOfItsNumbers()
    {
        foreach (var level in new[] { -22, -17, -1, 5, 100 })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new Knight(level), $"level {level}");
        }
    }

    [TestMethod]
    public void ASettlerIsNeverWithoutAJob()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Settler(null!, 0, 1, 2, 0, new LooksForTree()));
        var settler = new Settler(Job.Carrier, 0, 1, 2, 0, new LooksForTree());
        Assert.ThrowsExactly<ArgumentNullException>(() => settler.TakeUp(null!));
        Assert.AreEqual(Job.Carrier, settler.Job);
    }

    [TestMethod]
    public void AWorkerRequestIsNeverWithoutAJob()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new WorkerRequest(null!, Good.None));
    }

    [TestMethod]
    public void AKeptAccessorGivesTheSameJobOnEveryCall()
    {
        var first = new[] { Job.Carrier, Job.Woodcutter };
        var second = new[] { Job.Carrier, Job.Woodcutter };
        Assert.AreSame(first[0], second[0]);
        Assert.AreSame(first[1], second[1]);
    }

    [TestMethod]
    public void TheTableMembersKeepTheirValueForEveryNumberFrom0To31()
    {
        for (var code = 0; code <= 31; code++)
        {
            var job = Job.Of(code);
            var knight = code is >= 22 and <= 26;
            var above = code >= 22;
            Assert.AreEqual(knight, job.IsKnight, $"IsKnight {code}");
            Assert.AreEqual(above, job.IsKnightOrAbove, $"IsKnightOrAbove {code}");
            Assert.AreEqual(code - 22, job.KnightLevel, $"KnightLevel {code}");
            Assert.AreEqual(code + 1, job.Promoted.Code, $"Promoted {code}");
            var own = code is 7 or 8 or 11 or 14 or 20 || code == 5 || above;
            Assert.AreEqual(!own, job.ArrivesHomeGenerically, $"ArrivesHomeGenerically {code}");
        }
    }
}

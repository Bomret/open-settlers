using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// A job is a type (ADR 0016): Job.Of finds the type of a number, and the table answers the original's number gave sit on the types.
[TestClass]
public sealed class JobTests
{
    static readonly (int Code, Type Type)[] Ported =
    [
        (0, typeof(Carrier)),
        (2, typeof(Digger)),
        (3, typeof(Builder)),
        (4, typeof(CastleSettler)),
        (5, typeof(Woodcutter)),
        (6, typeof(Sawmiller)),
        (7, typeof(Stonecutter)),
        (8, typeof(Forester)),
        (11, typeof(Fisher)),
        (12, typeof(PigFarmer)),
        (13, typeof(Butcher)),
        (14, typeof(Farmer)),
        (15, typeof(Miller)),
        (16, typeof(Baker)),
        (21, typeof(Jobless)),
    ];

    [TestMethod]
    public void OfGivesAJobWhoseCodeIsTheNumberForEveryByte()
    {
        for (var code = 0; code <= 255; code++)
        {
            Assert.AreEqual((int?)code, Job.Of(code).OriginalCode, $"job {code}");
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
        Assert.AreEqual(Job.Of(0), new Carrier());
        Assert.AreEqual(Job.Of(5), new Woodcutter());
        Assert.AreEqual(Job.Of(11), new Fisher());
        Assert.AreEqual(Job.Of(21), new Jobless());
        Assert.AreEqual(Job.Of(24), new Knight(2));
        Assert.AreEqual(Job.Of(9).GetHashCode(), new UnportedJob(9).GetHashCode());
        Assert.AreEqual(new Knight(3), new Knight(2).Promoted);
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Knight(4).Promoted);
        Assert.IsFalse(new Knight(4).CanBePromoted);
    }

    [TestMethod]
    public void AJobPrintsAsItsStableNameAndAnUnportedJobAsItsRawNumber()
    {
        Assert.AreEqual("woodcutter", new Woodcutter().ToString());
        Assert.AreEqual("knight-level-2", new Knight(2).ToString());
        Assert.AreEqual("9", new UnportedJob(9).ToString());
    }

    [TestMethod]
    public void EveryPortedJobAndEveryKnightLevelHasAStableNameWithItsEvidence()
    {
        Assert.HasCount(Ported.Length + 5, Job.Ported);
        var names = Job.Ported.Select(job => job.Name).ToArray();
        CollectionAssert.AllItemsAreUnique(names);
        foreach (var job in Job.Ported)
        {
            Assert.IsTrue(
                job.Name.Length > 0 && job.Name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'),
                job.Name
            );
            Assert.IsFalse(string.IsNullOrWhiteSpace(job.NameEvidence), $"the evidence of {job.Name}");
        }

        for (var level = 0; level < 5; level++)
        {
            Assert.AreEqual($"knight-level-{level}", new Knight(level).Name);
        }

        Assert.AreEqual("woodcutter", new Woodcutter().Name);
        Assert.AreEqual("castle-settler", new CastleSettler().Name);
        Assert.AreEqual("pig-farmer", new PigFarmer().Name);
    }

    [TestMethod]
    public void AnUnportedJobHasNoNameAndNoEvidence()
    {
        Assert.AreEqual("", new UnportedJob(9).Name);
        Assert.AreEqual("", new UnportedJob(9).NameEvidence);
        Assert.DoesNotContain(new UnportedJob(9), Job.Ported);
    }

    [TestMethod]
    public void TheRegistryKeepsItsOrderByTypeNameThenLevel()
    {
        var keys = Job.Ported.Select(job => job.GetType().Name).ToArray();

        CollectionAssert.AreEqual(keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(), keys);
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 5).Select(level => new Knight(level)).ToArray(),
            Job.Ported.OfType<Knight>().ToArray()
        );
    }

    [TestMethod]
    public void NamedFindsEachJobByItsStableNameAndNothingElse()
    {
        foreach (var job in Job.Ported)
        {
            Assert.AreEqual(job, Job.Named(job.Name), job.Name);
        }

        Assert.IsNull(Job.Named("chef"));
        Assert.IsNull(Job.Named(""));
        Assert.IsNull(Job.Named("Woodcutter"));
        Assert.IsNull(Job.Named("unported-9"));
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
        var settler = new Settler(new Carrier(), 0, 1, 2, 0, new LooksForTree());
        Assert.ThrowsExactly<ArgumentNullException>(() => settler.TakeUp(null!));
        Assert.AreEqual(new Carrier(), settler.Job);
    }

    [TestMethod]
    public void AWorkerRequestIsNeverWithoutAJob()
    {
        var built = Assert.ThrowsExactly<ArgumentNullException>(() =>
            new WorkerRequest { Job = null!, Tool = Good.None }
        );
        Assert.AreEqual("Job", built.ParamName);

        var request = new WorkerRequest { Job = new Carrier(), Tool = Good.None };
        var copied = Assert.ThrowsExactly<ArgumentNullException>(() => request with { Job = null! });
        Assert.AreEqual("Job", copied.ParamName);
        Assert.AreEqual(new Carrier(), request.Job);
    }

    [TestMethod]
    public void TheJobTypeHoldsNoAccessorAndNoNumberOfAnyJob()
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // The registry of the generator is the one static member; no number constant and no rule on the type reads a number.
        CollectionAssert.AreEqual(
            new[] { "Ported" },
            typeof(Job).GetProperties(Declared).Select(p => p.Name).ToArray()
        );
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            typeof(Job).GetFields(Declared).Select(field => field.Name).Where(name => !name.StartsWith('<')).ToList()
        );
        var members = typeof(Job).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsFalse(
            members.Any(member => member.Name is "IsKnight" or "IsKnightOrAbove" or "KnightLevel" or "Code")
        );
    }

    [TestMethod]
    public void TheTypesOfTheJobsKeepTheGenericArrivalHomeOfTheOriginalsTableForEveryNumberFrom0To31()
    {
        for (var code = 0; code <= 31; code++)
        {
            var own = code is 5 or 7 or 8 or 11 or 14 or >= 22 and <= 26;
            Assert.AreEqual(!own, Job.Of(code).ArrivesHomeGenerically, $"ArrivesHomeGenerically {code}");
        }
    }

    [TestMethod]
    public void AnUnportedJobTheTableExceptsStatesItAndStaysEqualToTheJobOfItsNumber()
    {
        var twenty = new UnportedJob(20) { Generic = false };

        Assert.IsFalse(twenty.ArrivesHomeGenerically);
        Assert.AreEqual(Job.Of(20), twenty);
        Assert.AreEqual(Job.Of(20).GetHashCode(), twenty.GetHashCode());
    }
}

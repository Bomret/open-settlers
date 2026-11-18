using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Tests;

// The rule tables port of the basic content set: the checked-in rule files in content/basic/rules/ load into RuleTables with no
// install, every table of them reaches the tables, and a broken file is refused with its file and key.
[TestClass]
public sealed partial class OpenSettlersRuleTablesTests
{
    const string World = OpenSettlersRuleTables.WorldFile;
    const string Goods = OpenSettlersRuleTables.GoodsFile;
    const string Buildings = OpenSettlersRuleTables.BuildingsFile;
    const string Jobs = OpenSettlersRuleTables.JobsFile;

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }

    static string BasicFolder() => Path.Combine(RepositoryRoot(), "content", "basic");

    static RuleTables Checked() => new OpenSettlersRuleTables(BasicFolder()).Read();

    // A copy of the checked-in rule files in the temp folder; a test edits one file's text and reads.
    sealed class RuleFolder : IDisposable
    {
        public RuleFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "open-settlers-rules-" + Guid.NewGuid().ToString("N"));
            foreach (var file in OpenSettlersRuleTables.Files)
            {
                var target = Path.Combine(Root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(BasicFolder(), file), target);
            }
        }

        public string Root { get; }

        public void Replace(string file, string find, string replacement)
        {
            var path = Path.Combine(Root, file);
            var text = File.ReadAllText(path);
            Assert.Contains(find, text, $"the fixture edit {find} is not in {file}");
            File.WriteAllText(path, text.Replace(find, replacement));
        }

        public string Refusal()
        {
            var thrown = Assert.ThrowsExactly<InvalidDataException>(() => new OpenSettlersRuleTables(Root).Read());

            return thrown.Message;
        }

        public void Dispose() => Directory.Delete(Root, true);
    }

    static void Refuses(string file, string find, string replacement, string key, string reason)
    {
        using var folder = new RuleFolder();
        folder.Replace(file, find, replacement);

        var message = folder.Refusal();

        Assert.StartsWith($"{file}: {key}: ", message);
        Assert.Contains(reason, message);
    }

    [TestMethod]
    public void TheCheckedInRuleFilesLoadWithNoInstall()
    {
        // The port is built from the set's folder alone and names no install: it needs no SIEDLER_DIR.
        var tables = Checked();

        Assert.IsTrue(tables.ClassOf(MapObject.Flag).IsFlag);
    }

    [TestMethod]
    public void EveryTableOfTheFilesReachesTheTablesAndNoneStaysAtTheConstructorsDefault()
    {
        var made = Checked();
        var empty = new RuleTables(
            new ObjectClass[RuleTables.ClassCount],
            [.. Enumerable.Range(0, 5).Select(_ => (IReadOnlyDictionary<Good, byte>)new Dictionary<Good, byte>())]
        );

        Assert.IsFalse(made.Classes.SequenceEqual(empty.Classes), "object classes");
        Assert.IsFalse(made.StartStock.SelectMany(row => row.Values).All(count => count == 0), "start stock");
        Assert.IsFalse(made.StepTimes.SequenceEqual(empty.StepTimes), "step times");
        Assert.IsFalse(made.DoorSpeeds.SequenceEqual(empty.DoorSpeeds), "door speeds");
        Assert.IsFalse(made.ScanWindows.SequenceEqual(empty.ScanWindows), "scan windows");
        Assert.IsFalse(made.Figures.FrameRows.SequenceEqual(empty.Figures.FrameRows), "frame rows");
        Assert.AreEqual(0, empty.Catalog.Goods.Length, "no goods by default");
        Assert.AreEqual(26, made.Catalog.Goods.Length, "goods");
        Assert.IsTrue(made.Catalog.Goods.All(good => good.OriginalCode is null), "no original codes");
        Assert.AreEqual(FillCounter.High, made.FillCounterOf(Good.Log), "good kinds");
        Assert.IsFalse(made.RoadCapacities.SequenceEqual(empty.RoadCapacities), "road capacities");
        Assert.IsFalse(made.WalkRows.SequenceEqual(empty.WalkRows), "walk rows");
        Assert.AreNotEqual(0, made.TransportPriority(Good.Log), "transport priorities");
        Assert.IsFalse(made.BuildingSizes.SequenceEqual(empty.BuildingSizes), "building sizes");
        Assert.IsFalse(made.BuildingNeedsTable.SequenceEqual(empty.BuildingNeedsTable), "building needs");
        Assert.IsNotNull(made.ClassOfGood(Good.Plank), "good classes");
        Assert.IsGreaterThan(0, made.DistributionOrder(0).Length, "distribution orders");
        Assert.AreNotEqual(0, made.PlankRankBase, "plank rank base");
        Assert.AreNotEqual(0, made.MillRankBase, "mill rank base");
        Assert.IsNotNull(made.WorkerOf(BuildingType.Woodcutter), "worker requests");
        Assert.IsTrue(made.StoneAt(BuildingType.Mill, 2), "material order");
        Assert.AreNotEqual(0, made.BlowStep(BuildingType.Woodcutter, late: false), "blow steps");
        Assert.AreEqual(new Builder(), made.Get<WorkerRequest>(new Builder()).Job, "job data");
    }

    [TestMethod]
    public void TheValuesOfTheFilesAreEncodedAsTheTablesHoldThem()
    {
        var tables = Checked();

        Assert.IsTrue(tables.ClassOf(new MapObject { Code = 4 }).IsCastle);
        Assert.IsTrue(tables.ClassOf(new MapObject { Code = 72 }).IsStone);
        Assert.AreEqual(64, tables.StepTime(4), "a flat step, shape 4 + 9 * 0");
        Assert.AreEqual(124, tables.StepTime(4 + 9 * 5 + 4), "a steep climb in the sixth direction");
        Assert.AreEqual(20, tables.DoorSpeed(BuildingType.Castle.Code | 0x20), "the castle's door while it is built");
        Assert.AreEqual(16, tables.ScanWindow(3), "the map size value 3 is 64 by 64");
        Assert.AreEqual(-1, tables.Figures.FrameRow(81));
        Assert.AreEqual(3, tables.RoadCapacity(5));
        Assert.AreEqual(0, tables.WalkDirection(0, 0), "row 0 starts east");
        Assert.AreEqual(5, tables.WalkDirection(12, 0), "row 12 starts north");
        Assert.AreEqual(26, tables.TransportPriority(Good.Plank));
        Assert.AreEqual(FillCounter.High, tables.FillCounterOf(Good.Log));
        Assert.AreEqual(FillCounter.Low, tables.FillCounterOf(Good.Plank));
        Assert.AreEqual(RequestWord.Second, tables.ClassOfGood(Good.Stone)!.Value.Word);
        Assert.AreEqual(Good.Stone, tables.DistributionOrder(0)[0].Goods.Single(), "the draw 0 takes the second list");
        Assert.AreEqual(Good.Log, tables.DistributionOrder(1)[0].Goods.Single(), "the draw 1 takes the third list");
        Assert.AreEqual(
            Good.Plank,
            tables.DistributionOrder(2)[0].Goods.Single(),
            "any other draw takes the first list"
        );
        Assert.AreEqual(240, tables.PlankRankBase);
        Assert.AreEqual(BuildingSize.Large, tables.SizeOf(BuildingType.Sawmill));
        Assert.AreEqual(2, tables.NeedsOf(BuildingType.Sawmill).Stones);
        Assert.AreEqual("hatchet", tables.WorkerOf(BuildingType.Woodcutter)!.Tool.Name, "the hatchet");
        Assert.AreEqual(4096, tables.BlowStep(BuildingType.Woodcutter, late: false), "32768 over eight blows");
        Assert.AreEqual(12, tables.StartStock[0][Good.Plank], "the plank of the first start stock level");
        Assert.AreEqual(180, tables.Get<PigFarmRates>(new PigFarmer()).RankBase);
        Assert.AreEqual(58982, tables.Get<PigFarmRates>(new PigFarmer()).Growth[0], "90 percent of the 16-bit draw");
    }

    [TestMethod]
    public void TheCountsOfEveryGoodAgreeWithItsHandoverCounter()
    {
        var tables = Checked();

        foreach (var entry in tables.Catalog.Goods)
        {
            var good = new Good { Name = entry.Name };

            Assert.AreEqual(entry.HandoverCounter, tables.FillCounterOf(good), $"the counts of {entry.Name}");
        }
    }

    // The handover counter and resource-1 counting the basic set plays with, as the file states them; the values are the ones the
    // domain derived from the good's number before the file stated them.
    [TestMethod]
    public void TheFileStatesTheHandoverCounterAndResource1CountingOfEveryGood()
    {
        var tables = Checked();
        var low = new[] { "fish", "pig", "meat", "grain", "flour", "bread", "plank", "coal" };

        Assert.AreEqual(26, tables.Catalog.Goods.Length);
        foreach (var entry in tables.Catalog.Goods)
        {
            var good = new Good { Name = entry.Name };

            Assert.AreEqual(
                low.Contains(entry.Name) ? FillCounter.Low : FillCounter.High,
                tables.HandoverCounterOf(good),
                $"the handover of {entry.Name}"
            );
            Assert.AreEqual(
                entry.Name is "gold-ore" or "gold",
                tables.CountsAgainstResource1(good),
                $"resource 1 counting of {entry.Name}"
            );
        }
    }

    [TestMethod]
    public void TheCatalogOfTheFilesHoldsGoodsWithNoOriginalCodeInOrdinalOrderOfTheirNames()
    {
        var names = Checked().Catalog.Goods.Select(entry => entry.Name).ToArray();

        CollectionAssert.AreEqual(names.Order(StringComparer.Ordinal).ToArray(), names);
        Assert.IsTrue(Checked().Catalog.Goods.All(entry => entry.OriginalCode is null));
    }

    [GeneratedRegex(@"\.Get<(\w+)>\(new (\w+)\(\)\)")]
    private static partial Regex JobDataRead();

    [GeneratedRegex(@"\.Get<\w+>\(")]
    private static partial Regex AnyGenericGet();

    // The simulation, the adapters, and the views read a job's data through RuleTables.Get<T>(new Job()). The History Edition
    // source's own looks read the install's figure adds and are no simulation; the content set's source reads none.
    [TestMethod]
    public void EveryJobDataTheSimulationReadsIsSuppliedByTheFiles()
    {
        var source = Path.Combine(RepositoryRoot(), "src");
        var texts = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains($"{Path.DirectorySeparatorChar}Import.HistoryEdition{Path.DirectorySeparatorChar}")
                && !file.Contains($"{Path.DirectorySeparatorChar}Import.OpenSettlers{Path.DirectorySeparatorChar}")
            )
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .ToList();
        foreach (var (file, text) in texts)
        {
            Assert.AreEqual(
                AnyGenericGet().Count(text),
                JobDataRead().Count(text),
                $"{file} reads job data in a form the scan does not resolve: write Get<Data>(new Job())"
            );
        }

        var reads = texts
            .SelectMany(entry =>
                JobDataRead()
                    .Matches(entry.Text)
                    .Select(match => (Data: match.Groups[1].Value, Job: match.Groups[2].Value))
            )
            .Distinct()
            .ToList();
        var tables = Checked();
        var types = typeof(RuleTables).Assembly.GetTypes();
        var get = typeof(RuleTables).GetMethod(nameof(RuleTables.Get))!;

        Assert.IsNotEmpty(reads, "no read of a job's data found: the scan is broken");
        foreach (var (data, job) in reads)
        {
            var dataType = types.Single(type => type.Name == data);
            var jobInstance = Activator.CreateInstance(types.Single(type => type.Name == job));
            try
            {
                Assert.IsNotNull(get.MakeGenericMethod(dataType).Invoke(tables, [jobInstance]));
            }
            catch (TargetInvocationException e)
            {
                Assert.Fail(
                    $"the simulation reads {data} of {job}, which the basic files do not supply: {e.InnerException?.Message}"
                );
            }
        }
    }

    [TestMethod]
    public void AMalformedFileIsRefusedWithItsNameAndTheLine()
    {
        using var folder = new RuleFolder();
        folder.Replace(World, "\"objectClasses\": {", "\"objectClasses\": {{");

        var message = folder.Refusal();

        Assert.StartsWith($"{World}: line ", message);
        Assert.Contains("not valid JSONC", message);
    }

    [TestMethod]
    public void AMissingFileIsRefusedWithItsNameAndTheRootKey()
    {
        using var folder = new RuleFolder();
        File.Delete(Path.Combine(folder.Root, Jobs));

        Assert.StartsWith($"{Jobs}: $: missing", folder.Refusal());
    }

    [TestMethod]
    public void AFrameKeyThatIsNotInCanonicalSpellingIsRefused()
    {
        Refuses(World, "\"81\": -1,", "\"081\": -1,", "frameRows.frames.081", "not a frame number");
    }

    [TestMethod]
    public void AMissingTableIsRefusedWithItsKey()
    {
        Refuses(World, "\"stepTimes\": {", "\"stepTimesTypo\": {", "stepTimes", "missing");
    }

    [TestMethod]
    public void AMissingJobsDataIsRefusedWithItsKey()
    {
        Refuses(Jobs, "\"digger\": {", "\"diggerTypo\": {", "jobs.digger", "missing");
    }

    [TestMethod]
    public void AKeyTheFileDoesNotKnowIsRefusedWithItsKey()
    {
        Refuses(Jobs, "\"builder\": {", "\"extra\": 1, \"builder\": {", "jobs.extra", "unknown key");
    }

    [TestMethod]
    public void ARoadTableOfTheWrongCountIsRefused()
    {
        Refuses(
            World,
            "    4, // Every longer road: four carriers, the most the road tables allow for.\n",
            "",
            "roadCapacities",
            "8"
        );
    }

    [TestMethod]
    public void AWalkRowOfTheWrongCountIsRefused()
    {
        Refuses(
            World,
            "[\"east\", \"south-east\", \"north\", \"south\", \"north-west\", \"west\"], // Last stepped east: on",
            "[\"east\"], // Last stepped east: on",
            "walkRows[0]",
            "6"
        );
    }

    [TestMethod]
    public void ADuplicateKeyIsRefusedWithItsKey()
    {
        Refuses(World, "\"flag\": [1],", "\"flag\": [1], \"flag\": [2],", "objectClasses.flag", "duplicate key");
    }

    [TestMethod]
    public void AnUnknownDirectionIsRefused()
    {
        Refuses(
            World,
            "[\"east\", \"south-east\", \"north\", \"south\", \"north-west\", \"west\"], // Last stepped east: on",
            "[\"east\", \"up\", \"north\", \"south\", \"north-west\", \"west\"], // Last stepped east: on",
            "walkRows[0][1]",
            "up"
        );
    }

    [TestMethod]
    public void AnUnknownSizeIsRefused()
    {
        Refuses(
            Buildings,
            "\"size\": \"small\", // A hut beside the trees.",
            "\"size\": \"huge\",",
            "buildings.woodcutter.size",
            "huge"
        );
    }

    [TestMethod]
    public void AnUnknownMaterialIsRefused()
    {
        Refuses(
            Buildings,
            "\"materials\": [\"plank\", \"plank\"], // Two planks, in turn.\n      \"blows\": { \"firstHalf\": 8, \"secondHalf\": 8 }, // Eight blows a material, two materials, split evenly.\n      \"worker\": { \"job\": \"woodcutter\"",
            "\"materials\": [\"plank\", \"brick\"],\n      \"blows\": { \"firstHalf\": 8, \"secondHalf\": 8 },\n      \"worker\": { \"job\": \"woodcutter\"",
            "buildings.woodcutter.materials[1]",
            "brick"
        );
    }

    [TestMethod]
    public void ADoorSpeedOutOfItsRangeIsRefused()
    {
        Refuses(
            Buildings,
            "\"door\": { \"standing\": 20,",
            "\"door\": { \"standing\": 32,",
            "buildings.castle.door.standing",
            "0 to 31"
        );
    }

    [TestMethod]
    public void TransportPrioritiesThatAreNotEachRankOnceAreRefused()
    {
        Refuses(Goods, "\"priority\": 26,", "\"priority\": 25,", "goods.stone.priority", "rank 25");
    }

    [TestMethod]
    public void AHandoverCounterOtherThanLowOrHighIsRefused()
    {
        Refuses(
            Goods,
            "\"fish\": { \"counts\": \"low\", \"handover\": \"low\",",
            "\"fish\": { \"counts\": \"low\", \"handover\": \"none\",",
            "goods.fish.handover",
            "handover counter"
        );
    }

    [TestMethod]
    public void AResource1CountingOtherThanCountsOrNoneIsRefused()
    {
        Refuses(
            Goods,
            "\"fish\": { \"counts\": \"low\", \"handover\": \"low\", \"resource1\": \"none\",",
            "\"fish\": { \"counts\": \"low\", \"handover\": \"low\", \"resource1\": \"yes\",",
            "goods.fish.resource1",
            "resource 1 counting"
        );
    }

    [TestMethod]
    public void AGoodWithNoHandoverKeyIsRefused()
    {
        Refuses(
            Goods,
            "\"fish\": { \"counts\": \"low\", \"handover\": \"low\", ",
            "\"fish\": { \"counts\": \"low\", ",
            "goods.fish.handover",
            "missing"
        );
    }

    [TestMethod]
    public void PigGrowthOfNotExactlyEightStepsIsRefused()
    {
        Refuses(
            Jobs,
            "        20, // Seven pigs held: the pens are crowded, so growth slows.\n",
            "",
            "jobs.pig-farmer.growth",
            "8"
        );
    }

    [TestMethod]
    public void ADistributionOrderCountOtherThanThreeIsRefused()
    {
        Refuses(Goods, "// Last stones.\n    ],\n", "// Last stones.\n    ],\n    [],\n", "distribution", "3");
    }

    [TestMethod]
    public void AStartStockRowOfTheWrongCountIsRefused()
    {
        Refuses(Goods, "\"fish\": [0, 0, 1, 2, 4],", "\"fish\": [0, 0, 1, 2],", "startStock.fish", "5");
    }

    // Every value of a rule file states its reason: a line that holds a value ends in a comment or sits under a comment line.
    [TestMethod]
    public void EveryValueLineOfTheRuleFilesCarriesAComment()
    {
        var lacking = new List<string>();
        foreach (var file in OpenSettlersRuleTables.Files)
        {
            var lines = File.ReadAllLines(Path.Combine(BasicFolder(), file));
            for (var at = 0; at < lines.Length; at++)
            {
                var code = CodeOf(lines[at]).Trim().TrimEnd(',');
                var structural =
                    code.Length == 0 || code is "{" or "}" or "[" or "]" || code.EndsWith('{') || code.EndsWith('[');
                var commented =
                    lines[at].Contains("//") && !lines[at].TrimStart().StartsWith("//")
                    || at > 0 && lines[at - 1].TrimStart().StartsWith("//");
                if (!structural && !commented)
                {
                    lacking.Add($"{file}:{at + 1}");
                }
            }
        }

        Assert.IsEmpty(lacking, $"value lines without a reason: {string.Join(", ", lacking)}");
    }

    // The line without its trailing comment: a // inside a string is no comment.
    static string CodeOf(string line)
    {
        var quoted = false;
        for (var at = 0; at < line.Length - 1; at++)
        {
            quoted ^= line[at] == '"';
            if (!quoted && line[at] == '/' && line[at + 1] == '/')
            {
                return line[..at];
            }
        }

        return line;
    }
}

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The fields are the object codes 0x69 to 0x6f and 0x79 to 0x7e (docs/rules/farming.md): their kind, their growth in the object pass, and
// what a harvest does.
[TestClass]
public sealed class FieldObjectTests
{
    static MapObject Of(int code) => new() { Code = (byte)code };

    [TestMethod]
    public void TheGrowingHarvestedAndRipeCodesAreFieldsAndTheSignsAreNot()
    {
        var fields = Enumerable.Range(0, 128).Where(code => Of(code).Kind == MapObjectKind.Field).ToArray();

        CollectionAssert.AreEqual(
            new[] { 0x69, 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x6f, 0x79, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e },
            fields
        );
    }

    [TestMethod]
    public void TheObjectPassTakesAGrowingOrRipeFieldOneStageOn()
    {
        CollectionAssert.AreEqual(
            new[] { 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e },
            new[] { 0x69, 0x6a, 0x6b, 0x6c, 0x6d, 0x79, 0x7a, 0x7b, 0x7c, 0x7d }
                .Select(code => (int)Of(code).AfterObjectPass!.Value.Code)
                .ToArray()
        );
    }

    [TestMethod]
    public void TheObjectPassRipensTheGrownFieldDecaysTheLastRipeOneAndClearsTheHarvestedOne()
    {
        Assert.AreEqual(
            (0x79, 0x6f, 0),
            (
                Of(0x6e).AfterObjectPass!.Value.Code,
                Of(0x7e).AfterObjectPass!.Value.Code,
                Of(0x6f).AfterObjectPass!.Value.Code
            )
        );
    }

    [TestMethod]
    public void TheObjectPassLeavesACodeThatIsNoFieldToItsOtherHandlers()
    {
        Assert.IsNull(Of(0x70).AfterObjectPass);
        Assert.IsNull(Of(0x08).AfterObjectPass);
        Assert.IsNull(Of(0x7f).AfterObjectPass);
    }

    [TestMethod]
    public void OnlyTheGrownAndTheRipeFieldsCanBeHarvested()
    {
        CollectionAssert.AreEqual(
            new[] { 0x6e, 0x79, 0x7a, 0x7b, 0x7c, 0x7d, 0x7e },
            Enumerable.Range(0, 128).Where(code => Of(code).IsHarvestable).ToArray()
        );
    }

    [TestMethod]
    public void AHarvestTakesTheFieldOneStageOnAndTheLastOneToTheHarvestedField()
    {
        Assert.AreEqual(
            (0x79, 0x7b, 0x6f),
            (Of(0x6e).Harvested().Code, Of(0x7a).Harvested().Code, Of(0x7e).Harvested().Code)
        );
    }
}

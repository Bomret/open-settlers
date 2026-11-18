using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenSettlers.Tests;

// The History Edition source's half of this class: the core state codes it declares for the generator. The adapters' name
// table is checked against the same codes in src/Adapters.Tests, in the test of this name.
[TestClass]
public sealed class NameTableTests
{
    [TestMethod]
    public void TheCoreStateCodesTheGeneratorChecksAreTheStatesTheTableNamesItself()
    {
        var listed = typeof(CoreStateCodesAttribute)
            .Assembly.GetCustomAttributes(typeof(CoreStateCodesAttribute), false)
            .Cast<CoreStateCodesAttribute>()
            .SelectMany(attribute => attribute.Codes);

        CollectionAssert.AreEquivalent(
            new[] { 1, 2, 3, 4, 5, 6, 7, 0xf, 0x10, 0x19, 0x1c, 0x42, 0x4a },
            listed.ToList()
        );
    }
}

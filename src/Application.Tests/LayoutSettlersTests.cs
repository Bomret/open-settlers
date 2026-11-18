using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The wording of the walk behavior's refusal for a worker whose hut is not standing. The layout's settler tests are in the
// root tests' class of the same name.
[TestClass]
public sealed class LayoutSettlersTests
{
    sealed class StandingRules<TJob>(bool stands) : JobRules<TJob>(null!)
        where TJob : Job
    {
        public override bool CanStandInHut => stands;
    }

    [TestMethod]
    public void TheRefusalReadsRightForAnyNumberOfJobsThatStandInTheirHut()
    {
        var woodcutter = new StandingRules<Woodcutter>(true);
        var forester = new StandingRules<Forester>(true);
        var stonecutter = new StandingRules<Stonecutter>(true);
        var carrier = new StandingRules<Carrier>(false);

        CollectionAssert.AreEqual(
            new[]
            {
                "no finished building stands on the node",
                "no finished woodcutter's building stands on the node",
                "no finished woodcutter's or forester's building stands on the node",
                "no finished woodcutter's, stonecutter's, or forester's building stands on the node",
            },
            new[]
            {
                WalkBehavior.NoHutRefusal([carrier]),
                WalkBehavior.NoHutRefusal([carrier, woodcutter]),
                WalkBehavior.NoHutRefusal([forester, carrier, woodcutter]),
                WalkBehavior.NoHutRefusal([forester, stonecutter, woodcutter]),
            }
        );
    }
}

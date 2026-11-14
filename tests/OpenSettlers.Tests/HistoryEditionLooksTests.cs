using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The branches of the History Edition looks (the figure handlers at 0x43f8ae to 0x43fed8, docs/rules/settlers.md): each look
// adds one constant of the job's table by the figure byte (from 0x80 is the work) and the activity. The tables are made up with a
// different constant in each field, so the sum names the branch taken.
[TestClass]
public sealed class HistoryEditionLooksTests
{
    // The made-up adds: the walk 1, the way back with a load 2, the work 4, the work facing back 8.
    static readonly FigureAdds Adds = new()
    {
        Walk = 1,
        Carrying = 2,
        Cutting = 4,
    };

    static RuleTables Tables(short[]? carried = null) =>
        new(
            [.. Enumerable.Repeat(0, RuleTables.ClassCount).Select(value => (ObjectClass)value)],
            [.. Enumerable.Range(0, RuleTables.StartStockRows).Select(_ => new byte[RuleTables.Goods])],
            carriedFigures: carried,
            jobData:
            [
                .. TestProgram.MadeJobData(),
                (new Stonecutter(), Adds),
                (new Forester(), new ForesterFigureAdds { Walk = 1, Work = 4 }),
                (
                    new TypeOneHut(),
                    new HutFigureAdds
                    {
                        Walk = 1,
                        Carrying = 2,
                        High = 4,
                        HighFacing = 8,
                    }
                ),
            ]
        );

    static SettlerDraw Draw(Job job, Activity activity, Heading? heading = null, Good carried = default) =>
        new()
        {
            Row = 0,
            X = 0,
            Y = 0,
            Part = SettlerPart.Figure,
            Job = job,
            Activity = activity,
            Heading = heading,
            Frame = 0,
            Step = 0,
            Player = 0,
            Carried = carried,
        };

    const int Low = 0x7f;
    const int High = 0x80;

    static readonly Activity BackWithLoad = new WalksFree
    {
        Trip = new Trip
        {
            Remaining = new Vector(0, 0),
            Purpose = new BackToHut { Carrying = true },
        },
    };

    // The stonecutter's handler at 0x43fcd8: the stroke from 0x80, below it the way back with the stone and the step back off the
    // heap (state 0x17, byte 13 = 2, the phase 2 of cutting stone) the carrying constant, any other frame the walk's.
    [TestMethod]
    public void AStonecuttersLookAddsTheStrokeForAWorkFigureWhateverTheActivity()
    {
        var look = new StonecutterLook(Tables());

        Assert.AreEqual(4, look.Add(Draw(new Stonecutter(), BackWithLoad), High));
    }

    [TestMethod]
    public void AStonecuttersLookAddsTheCarryingConstantOnTheWayBackWithAStone()
    {
        var look = new StonecutterLook(Tables());

        Assert.AreEqual(2, look.Add(Draw(new Stonecutter(), BackWithLoad), Low));
    }

    [TestMethod]
    public void AStonecuttersLookAddsTheCarryingConstantOnTheStepBackOffTheHeap()
    {
        var look = new StonecutterLook(Tables());

        Assert.AreEqual(
            2,
            look.Add(
                Draw(
                    new Stonecutter(),
                    new CutsStone
                    {
                        Back = new Vector(0, 0),
                        Phase = 2,
                        Quarter = 0,
                    }
                ),
                Low
            )
        );
    }

    [TestMethod]
    public void AStonecuttersLookAddsTheWalkForAnyOtherFrame()
    {
        var look = new StonecutterLook(Tables());

        Assert.AreEqual(
            1,
            look.Add(
                Draw(
                    new Stonecutter(),
                    new CutsStone
                    {
                        Back = new Vector(0, 0),
                        Phase = 1,
                        Quarter = 0,
                    }
                ),
                Low
            )
        );
        Assert.AreEqual(
            1,
            look.Add(
                Draw(
                    new Stonecutter(),
                    new WalksFree
                    {
                        Trip = new Trip
                        {
                            Remaining = new Vector(0, 0),
                            Purpose = new BackToHut { Carrying = false },
                        },
                    }
                ),
                Low
            )
        );
    }

    // The forester's handler at 0x43fd88: the work's constant for a figure byte from 0x80, the walk's below it.
    [TestMethod]
    public void AForestersLookAddsTheWorkFromFigureByte0x80AndTheWalkBelowIt()
    {
        var look = new ForesterLook(Tables());
        var draw = Draw(
            new Forester(),
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = new Vector(0, 0),
                    Purpose = new BackToHut { Carrying = false },
                },
            }
        );

        Assert.AreEqual(4, look.Add(draw, High));
        Assert.AreEqual(1, look.Add(draw, Low));
    }

    // The type 1 hut's handler at 0x43fed8: a settler that walks with the heading 1 (byte 14) faces back, any other heading does not.
    [TestMethod]
    public void ATypeOneHutLookAddsTheFacingBackWorkConstantForAWalkWithHeading1()
    {
        var look = new TypeOneHutLook(Tables());

        Assert.AreEqual(
            8,
            look.Add(
                Draw(
                    new TypeOneHut(),
                    new Walks
                    {
                        Errand = new ToStorage(),
                        Heading = new Heading { Value = 1 },
                    },
                    new Heading { Value = 1 }
                ),
                High
            )
        );
    }

    [TestMethod]
    public void ATypeOneHutLookAddsThePlainWorkConstantForAWalkWithAnotherHeading()
    {
        var look = new TypeOneHutLook(Tables());

        Assert.AreEqual(
            4,
            look.Add(
                Draw(
                    new TypeOneHut(),
                    new Walks
                    {
                        Errand = new ToStorage(),
                        Heading = new Heading { Value = 0 },
                    },
                    new Heading { Value = 0 }
                ),
                High
            )
        );
        Assert.AreEqual(
            4,
            look.Add(
                Draw(
                    new TypeOneHut(),
                    new Walks
                    {
                        Errand = new ToStorage(),
                        Heading = new Heading { Value = 2 },
                    },
                    new Heading { Value = 2 }
                ),
                High
            )
        );
    }

    // The type 1 hut's handler at 0x43fed8 (docs/rules/settlers.md, job 11): the way back with a load sets byte 14 to 1, so at a
    // work figure it adds the facing-back constant; a free walk with no load does not.
    [TestMethod]
    public void ATypeOneHutLookAddsTheFacingBackWorkConstantOnTheWayBackWithALoad()
    {
        var look = new TypeOneHutLook(Tables());

        Assert.AreEqual(8, look.Add(Draw(new TypeOneHut(), BackWithLoad), High));
    }

    [TestMethod]
    public void ATypeOneHutLookAddsThePlainWorkConstantOnAFreeWalkWithNoLoad()
    {
        var look = new TypeOneHutLook(Tables());
        var empty = new WalksFree
        {
            Trip = new Trip
            {
                Remaining = new Vector(0, 0),
                Purpose = new BackToHut { Carrying = false },
            },
        };

        Assert.AreEqual(4, look.Add(Draw(new TypeOneHut(), empty), High));
    }

    // The carrier's handler at 0x43f8ae (docs/rules/goods.md, "Drawing a carrier with a good"): good 0 never reads its word, so a
    // carrier with no good adds nothing even where the table's first word is not 0.
    [TestMethod]
    public void ACarrierLookAddsNothingForNoGoodWhateverTheTablesFirstWord()
    {
        var carried = new short[RuleTables.CarriedFigureCount];
        carried[0] = 0x1100;
        var look = new CarrierLook(Tables(carried));

        Assert.AreEqual(
            0,
            look.Add(
                Draw(
                    new Carrier(),
                    new WalksRoad
                    {
                        Load = null,
                        Heading = new Heading { Value = 0 },
                    },
                    carried: Good.None
                ),
                0
            )
        );
    }

    // The carrier's handler at 0x43f8ae: a good beyond the carried table (27 entries, goods 0 to 26) adds nothing, and the last good
    // the table covers adds its word.
    [TestMethod]
    public void ACarrierLookAddsTheWordOfTheLastGoodTheTableCoversAndNothingBeyondIt()
    {
        var carried = new short[RuleTables.CarriedFigureCount];
        carried[RuleTables.CarriedFigureCount - 1] = 0x1a00;
        var look = new CarrierLook(Tables(carried));
        var road = new WalksRoad
        {
            Load = null,
            Heading = new Heading { Value = 0 },
        };

        Assert.AreEqual(
            0x1a00,
            look.Add(Draw(new Carrier(), road, carried: new Good { Code = RuleTables.CarriedFigureCount - 1 }), 0)
        );
        Assert.AreEqual(
            0,
            look.Add(Draw(new Carrier(), road, carried: new Good { Code = RuleTables.CarriedFigureCount }), 0)
        );
        Assert.AreEqual(0, look.Add(Draw(new Carrier(), road, carried: new Good { Code = 99 }), 0));
    }
}

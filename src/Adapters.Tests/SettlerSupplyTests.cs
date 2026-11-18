using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The settler supply (docs/rules/new-settlers.md) on made-up tables, with no install: the map of the seed 1234,5678,9ABC with
// its castle at 15,5 and its flag at 16,6, no tree anywhere, and the flags 18,6, 19,4, and 17,3, as SettlerJobTests lays them.
// At rate 40 a period is 1000 clock units, 125 passes; at rate 0 it is 3000, 375 passes.
[TestClass]
public sealed class SettlerSupplyTests
{
    const int RateForty = 40;
    const int PeriodForty = 125;

    static Game Town(bool supply = true, int rate = RateForty, bool castle = true)
    {
        var map = MapGenerator.Generate(
            MadeProgram.Made(),
            new MapSeed
            {
                W0 = 0x1234,
                W1 = 0x5678,
                W2 = 0x9ABC,
            },
            MapSize.Offered[0]
        );
        for (var index = 0; index < map.Count; index++)
        {
            if (map.At(index).Object.Kind == MapObjectKind.Tree)
            {
                map.At(index).Object = MapObject.None;
            }
        }

        var game = GameFactory.Create(MadeProgram.Made(), map, settlerSupply: supply, settlerRate: rate);
        if (castle)
        {
            Do(game, CommandKind.PlaceCastle, 15, 5);
        }

        return game;
    }

    static void Do(Game game, int kind, int x, int y, int[]? directions = null)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
            }
        );
        game.Tick();
    }

    // The passes, counted from the game's first, at whose end the settler table had grown.
    static List<long> Growth(Game game, int passes)
    {
        var grown = new List<long>();
        for (var pass = 0; pass < passes; pass++)
        {
            var before = game.Settlers.Count;
            game.Tick();
            if (game.Settlers.Count > before)
            {
                grown.Add(game.TickCount);
            }
        }

        return grown;
    }

    static void Empty(Game game, Good good)
    {
        while (game.CastleStock.Take(good)) { }
    }

    [TestMethod]
    public void AGameWithoutTheSupplyMakesNoSettler()
    {
        var game = Town(supply: false);
        var count = game.Settlers.Count;

        Assert.AreEqual(0, Growth(game, 10000).Count);
        Assert.AreEqual(count, game.Settlers.Count);
        Assert.IsNull(game.SupplyState);
    }

    [TestMethod]
    public void BeforeTheCastleNoSettlerIsMadeAndTheSupplyStaysAtItsStart()
    {
        var game = Town(castle: false);
        // The timer's state; the cap is worked out at phase 32 for a player in the game whether or not its castle stands.
        string Timer()
        {
            var supply = game.SupplyState!;

            return $"{supply.Countdown} {supply.LastClock} {supply.KnightAccumulator} {supply.KnightsOwed} {supply.Running}";
        }

        var start = Timer();

        Assert.AreEqual(0, Growth(game, 1000).Count);
        Assert.AreEqual(start, Timer());
        Assert.AreEqual(1000, game.SupplyState!.Countdown);
    }

    [TestMethod]
    public void TheFirstSettlerComesOnePeriodAfterAPlacementAtALaterTick()
    {
        var game = Town(castle: false);
        for (var pass = 0; pass < 2000; pass++)
        {
            game.Tick();
        }

        // The castle is placed in the tick 2000, whose pass moves the clock 8 units past the placement.
        Do(game, CommandKind.PlaceCastle, 15, 5);
        var grown = Growth(game, 3 * PeriodForty);

        // The countdown goes below zero in the pass after the one that moves the clock a full period past the placement.
        Assert.AreEqual(
            2001 + PeriodForty,
            grown[0],
            "one full period after the placement, no burst for the passes before it"
        );
        Assert.AreEqual(3, grown.Count);
        Assert.AreEqual(PeriodForty, grown[1] - grown[0]);
    }

    [TestMethod]
    public void AtRateZeroThePeriodIs375Passes()
    {
        var game = Town(rate: 0);
        var grown = Growth(game, 1200);

        Assert.AreEqual(3, grown.Count);
        Assert.AreEqual(1 + 375, grown[0]);
        Assert.AreEqual(375, grown[1] - grown[0]);
    }

    [TestMethod]
    public void TheTimerKeepsItsPeriodAcrossTheClockWrap()
    {
        var game = Town();
        // The 16-bit clock wraps every 8192 passes.
        var grown = Growth(game, 8192 + 4 * PeriodForty);

        Assert.IsTrue(grown[^1] > 8192);
        CollectionAssert.AreEqual(
            Enumerable.Repeat(PeriodForty, grown.Count - 1).ToList(),
            grown.Zip(grown.Skip(1), (a, b) => (int)(b - a)).ToList()
        );
    }

    [TestMethod]
    public void ANewSettlerWaitsInTheCastleWithoutAJob()
    {
        var game = Town();
        var count = game.Settlers.Count;
        Growth(game, PeriodForty + 1);

        var settler = game.Settlers[count];
        Assert.AreEqual(new Jobless(), settler.Job);
        Assert.AreEqual((15, 5), (settler.X, settler.Y), "on the castle's node");
        Assert.IsInstanceOfType<WaitsInCastle>(settler.Activity);
        Assert.AreNotEqual(default, game.History.SettlerId(count), "the history knows the settler");
    }

    [TestMethod]
    public void AnOwedKnightIsMadeWhereTheStockHoldsASwordAndAShield()
    {
        var game = Town();
        game.CastleStock.Add(Good.Sword);
        game.CastleStock.Add(Good.Shield);
        var swords = game.CastleStock.Count(Good.Sword);
        var shields = game.CastleStock.Count(Good.Shield);
        var count = game.Settlers.Count;

        // From 0x8000, the second try carries: it owes a knight.
        Growth(game, 2 * PeriodForty + 1);

        Assert.AreEqual(new Jobless(), game.Settlers[count].Job);
        Assert.AreEqual(new Knight(0), game.Settlers[count + 1].Job);
        Assert.AreEqual(swords - 1, game.CastleStock.Count(Good.Sword));
        Assert.AreEqual(shields - 1, game.CastleStock.Count(Good.Shield));
        Assert.AreEqual(0, game.SupplyState!.KnightsOwed);
    }

    [TestMethod]
    public void WithoutSwordsOrShieldsTheKnightStaysOwedAndAThirdIsNotCounted()
    {
        var game = Town();
        Empty(game, Good.Sword);
        var count = game.Settlers.Count;

        // The second, the fifth, and the eighth try carry.
        Growth(game, 8 * PeriodForty + 1);

        Assert.AreEqual(count + 8, game.Settlers.Count);
        Assert.IsTrue(game.Settlers.Skip(count).All(settler => settler.Job == new Jobless()));
        Assert.AreEqual(2, game.SupplyState!.KnightsOwed);
    }

    // The castle holds two settlers without a job (its castle command names its settlers), which ConstructionWorld's two roads take;
    // the road from 17,3 asks for a third before the supply's first settler comes, a period after the castle stands.
    [TestMethod]
    public void ARoadThatFoundNoSettlerGetsTheNewOne()
    {
        var game = ConstructionWorld.Create(
            settlerSupply: true,
            settlerRate: RateForty,
            castleSettlers: [new Jobless(), new Jobless(), .. ConstructionWorld.CastleJobsWithout(new Jobless())]
        );
        // The two carriers have left for their roads.
        ConstructionWorld.Run(game, 60);
        Assert.AreEqual(2, ConstructionWorld.OfJob(game, new Carrier()).Length);
        Assert.IsEmpty(ConstructionWorld.OfJob(game, new Jobless()), "the castle holds no settler without a job");

        var count = game.Settlers.Count;
        ConstructionWorld.Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        ConstructionWorld.Run(game, 40);

        Assert.AreEqual(2, ConstructionWorld.OfJob(game, new Carrier()).Length, "no carrier for the third road");

        ConstructionWorld.Run(game, PeriodForty + 120);

        var carrier = game.Settlers.Skip(count).Single(settler => settler.Job == new Carrier());
        Assert.IsNotInstanceOfType<WaitsInCastle>(carrier.Activity, "it has left the castle");
    }

    [TestMethod]
    public void SettlersLeaveTheCastleWhileANewOneWaitsInIt()
    {
        var game = ConstructionWorld.Create(settlerSupply: true, settlerRate: RateForty);
        var count = game.Settlers.Count;
        ConstructionWorld.Run(game, PeriodForty + 20);
        Assert.IsInstanceOfType<WaitsInCastle>(game.Settlers[count].Activity, "a new settler waits in the castle");

        ConstructionWorld.Do(game, CommandKind.BuildRoad, 17, 3, [0, 1]);
        ConstructionWorld.Run(game, 120);

        var carriers = ConstructionWorld.OfJob(game, new Carrier());
        Assert.AreEqual(3, carriers.Length, "the third road has its carrier");
        Assert.IsTrue(
            carriers.All(carrier => carrier.Activity is not WaitsInCastle),
            "every carrier has left the castle"
        );
    }

    // Map_SetGeometry (0x42195c): (width / 32) * (height / 32) * 500 for the 32 x 32, 64 x 32, and 64 x 64 maps.
    [TestMethod]
    [DataRow(1, 500)]
    [DataRow(2, 1000)]
    [DataRow(3, 2000)]
    public void EachMapSizeGivesItsSettlerTableSize(int value, int size)
    {
        MapSize.TryFromValue(value, out var mapSize, out _);

        Assert.AreEqual(size, SettlerTable.SizeOf(mapSize!));
    }

    // The original's slot 0 is a dummy that MapGen_InitParams takes from the table's free count, so a game holds one settler less
    // than the table's size; the engine's table has no dummy.
    static void FillTo(Game game, int count)
    {
        var table = (SettlerTable)game.Settlers;
        while (table.Count < count)
        {
            table.Add(new Settler(new Jobless(), 0, 0, 0, 0, new RawState { StateCode = 0 }));
        }
    }

    [TestMethod]
    public void ATableOneShortOfFullTakesOneMoreSettlerAndAFullTableNone()
    {
        var game = Town();
        var held = ((SettlerTable)game.Settlers).Capacity - 1;
        Assert.AreEqual(499, held);
        FillTo(game, held - 1);

        var grown = Growth(game, 3 * PeriodForty);

        Assert.AreEqual(1, grown.Count, "one settler fills the table");
        Assert.AreEqual(held, game.Settlers.Count);
        Assert.AreEqual(
            0x6A60, // 0x8000 and three tries of 20000, in 16 bits
            game.SupplyState!.KnightAccumulator,
            "the periods pass and feed the knight accumulator all the same"
        );
    }

    [TestMethod]
    public void TheCapIsWorkedOutAtPhase32WithoutTheGoodsHandOut()
    {
        var game = Town();
        Assert.AreEqual(0, game.SupplyState!.Cap, "no cap is worked out before the first phase 32");

        while (game.PassPhase != 32)
        {
            game.Tick();
        }

        Assert.AreEqual(500, game.SupplyState!.Cap, "one player's cap is the table's size");
        Assert.IsTrue(game.SupplyState.MayMake);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(41)]
    public void ARateOutside0To40IsRefused(int rate)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Town(rate: rate, castle: false));
    }

    [TestMethod]
    public void WithTheSupplyOffTheRateIsNotRead()
    {
        Assert.IsNull(Town(supply: false, rate: 41, castle: false).SupplyState);
    }

    [TestMethod]
    public void TwoGamesThatDifferOnlyInTheSupplysStateGiveDifferentDigests()
    {
        var a = Town(rate: 40);
        var b = Town(rate: 39);

        Assert.AreNotEqual(a.Digest(), b.Digest());
    }
}

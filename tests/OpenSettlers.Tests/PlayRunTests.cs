using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayRunTests
{
    const double Frame = 1.0 / 30;

    readonly string _out = Path.Combine(Path.GetTempPath(), "play-run-tests-" + Guid.NewGuid().ToString("N"), "stem");
    readonly List<PlayStep> _sent = [];
    PlayState _state = new()
    {
        CameraX = 5,
        CameraY = 6,
        Zoom = 1f,
        Seed = "1",
        Size = 1,
    };

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(Path.GetDirectoryName(_out)))
        {
            Directory.Delete(Path.GetDirectoryName(_out)!, true);
        }
    }

    PlayRun Run(string script, Func<PlayStep, string?>? input = null) =>
        new(
            PlayScript.Parse(script, "play/t.play"),
            () => _state,
            _out,
            input
                ?? (
                    step =>
                    {
                        _sent.Add(step);
                        return null;
                    }
                )
        );

    static void Frames(PlayRun run, int count)
    {
        for (var i = 0; i < count; i++)
        {
            run.Advance(Frame);
        }
    }

    [TestMethod]
    public void AnEmptyScriptFinishesOnTheFirstFrame()
    {
        var run = Run("# nothing");

        run.Advance(Frame);

        Assert.IsTrue(run.Finished);
        Assert.IsEmpty(run.Problems);
    }

    [TestMethod]
    public void AWaitHoldsTheNextStepBackForItsSimulatedTime()
    {
        var run = Run("wait 1\npress A");

        Frames(run, 29);
        Assert.IsEmpty(_sent);
        Frames(run, 2);

        Assert.HasCount(1, _sent);
    }

    [TestMethod]
    public void StepsOfOneFrameRunInFileOrder()
    {
        var run = Run("press A\npress B\nscroll up");

        run.Advance(Frame);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _sent.ConvertAll(s => s.Line));
    }

    [TestMethod]
    public void AHoldKeepsTheKeyDownForItsTimeThenReleasesIt()
    {
        var run = Run("hold Right 1\npress B");

        run.Advance(Frame);
        Assert.IsInstanceOfType<HoldStep>(_sent[0]);
        Frames(run, 28);
        Assert.HasCount(1, _sent);
        Frames(run, 2);

        Assert.IsInstanceOfType<ReleaseStep>(_sent[1]);
        Assert.IsInstanceOfType<PressStep>(_sent[2]);
    }

    [TestMethod]
    public void AdvancingAFinishedRunSendsNothingAndStaysFinished()
    {
        // The run ended on a failed expect with `press A` still pending: only the Finished guard keeps it from running.
        var run = Run("expect camera-x > 10\npress A");
        Frames(run, 2);
        Assert.IsTrue(run.Finished);
        Assert.HasCount(1, run.Problems);

        Frames(run, 100);

        Assert.IsEmpty(_sent);
        Assert.IsTrue(run.Finished);
        Assert.HasCount(1, run.Problems);
    }

    // A hold at the end of the script: the run releases the key when its time has passed, and only then finishes.
    [TestMethod]
    public void AHoldThatEndsTheScriptReleasesItsKeyBeforeTheRunFinishes()
    {
        var run = Run("hold Right 1");

        Frames(run, 29);
        Assert.IsFalse(run.Finished);
        Frames(run, 3);

        Assert.IsTrue(run.Finished);
        Assert.HasCount(2, _sent);
        Assert.IsInstanceOfType<ReleaseStep>(_sent[1]);
        Assert.IsEmpty(run.Problems);
    }

    [TestMethod]
    public void TheRunFinishesAfterTheLastWaitHasPassed()
    {
        var run = Run("press A\nwait 0.5");

        Frames(run, 10);
        Assert.IsFalse(run.Finished);
        Frames(run, 10);

        Assert.IsTrue(run.Finished);
        Assert.IsEmpty(run.Problems);
    }

    [TestMethod]
    public void ADumpWritesTheStateToItsFileInTheOutputFolder()
    {
        var run = Run("dump here");

        run.Advance(Frame);

        Assert.AreEqual(_state.Dump(), File.ReadAllText(Path.Combine(_out, "here.txt")));
    }

    [TestMethod]
    public void AListWritesTheThingsOfTheStateToItsFileInTheOutputFolder()
    {
        _state = _state with
        {
            Things =
            [
                new Thing
                {
                    Kind = "object",
                    X = 3,
                    Y = 1,
                    Values = [("code", "0x0a")],
                    Name = "tree",
                },
            ],
        };
        var run = Run("list here");

        run.Advance(Frame);

        Assert.AreEqual("object 3,1 code=0x0a tree\n", File.ReadAllText(Path.Combine(_out, "here.things.txt")));
    }

    [TestMethod]
    public void AListOfNoThingsWritesAnEmptyFile()
    {
        var run = Run("list here");

        run.Advance(Frame);

        Assert.IsEmpty(run.Problems);
        Assert.AreEqual("", File.ReadAllText(Path.Combine(_out, "here.things.txt")));
    }

    [TestMethod]
    public void AFailedExpectEndsTheRunNamingScriptLineExpectedAndActual()
    {
        var run = Run("wait 0.1\nexpect camera-x > 10\npress A");

        Frames(run, 10);

        Assert.IsTrue(run.Finished);
        Assert.HasCount(1, run.Problems);
        Assert.AreEqual("play/t.play line 2: expect camera-x > 10 failed: actual 5", run.Problems[0]);
        Assert.IsEmpty(_sent);
    }

    [TestMethod]
    public void AHeldExpectSeesTheStateAtItsFrame()
    {
        var run = Run("expect camera-x = 5\nwait 0.1\nexpect camera-x = 9");

        run.Advance(Frame);
        _state = _state with { CameraX = 9 };
        Frames(run, 5);

        Assert.IsTrue(run.Finished);
        Assert.IsEmpty(run.Problems);
    }

    [TestMethod]
    public void AnInputProblemEndsTheRunNamingItsLine()
    {
        var run = Run("wait 0.1\npress A", _ => "no such key");

        Frames(run, 10);

        Assert.AreEqual("play/t.play line 2: no such key", run.Problems[0]);
        Assert.IsTrue(run.Finished);
    }

    [TestMethod]
    public void AnInputStepThatThrowsEndsTheRunNamingItsLine()
    {
        var run = Run("press A", _ => throw new InvalidOperationException("boom"));

        run.Advance(Frame);

        StringAssert.StartsWith(run.Problems[0], "play/t.play line 1: ");
        StringAssert.Contains(run.Problems[0], "boom");
    }

    [TestMethod]
    public void AnUnwritableDumpEndsTheRunNamingItsLine()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_out)!);
        File.WriteAllText(_out, "a file where the folder should be");
        var run = Run("dump x");

        run.Advance(Frame);

        StringAssert.StartsWith(run.Problems[0], "play/t.play line 1: ");
    }

    [TestMethod]
    public void AClickStepTheInputRefusesEndsTheRunNamingItsLine()
    {
        var run = Run(
            "wait 0\nclick-tile 40 3\npress A",
            step => step is ClickTileStep ? "tile 40,3 is outside the map" : null
        );

        Frames(run, 2);

        Assert.IsTrue(run.Finished);
        Assert.HasCount(1, run.Problems);
        Assert.AreEqual("play/t.play line 2: tile 40,3 is outside the map", run.Problems[0]);
    }

    [TestMethod]
    public void TheClickStepsGoToTheInput()
    {
        var run = Run("click-tile 1 2\nbutton Road");

        Frames(run, 1);

        Assert.HasCount(2, _sent);
        Assert.IsInstanceOfType<ClickTileStep>(_sent[0]);
        Assert.IsInstanceOfType<ButtonStep>(_sent[1]);
    }
}

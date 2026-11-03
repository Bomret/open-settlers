using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayScriptTests
{
    static PlayScript Parse(string text) => PlayScript.Parse(text, "play/t.play");

    [TestMethod]
    public void CommentsAndBlankLinesAreSkippedAndStepsKeepTheirLine()
    {
        var script = Parse("# a comment\n\n  wait 1.5  \n\t# another\nwait 2");

        Assert.IsEmpty(script.Problems);
        Assert.HasCount(2, script.Steps);
        Assert.AreEqual(new WaitStep { Line = 3, Seconds = 1.5 }, script.Steps[0]);
        Assert.AreEqual(5, script.Steps[1].Line);
    }

    [TestMethod]
    public void TheStemIsTheFileNameWithoutItsExtension()
    {
        Assert.AreEqual("t", Parse("").Stem);
    }

    [TestMethod]
    public void AnUnknownStepIsAProblemNamingScriptAndLine()
    {
        var script = Parse("wait 1\n\nfly 3");

        Assert.HasCount(1, script.Problems);
        Assert.AreEqual("play/t.play line 3: unknown step 'fly'", script.Problems[0]);
        Assert.HasCount(1, script.Steps);
    }

    [TestMethod]
    [DataRow("wait", "takes 1 argument")]
    [DataRow("wait soon", "not a number of seconds")]
    [DataRow("wait -1", "not a number of seconds")]
    [DataRow("wait NaN", "not a number of seconds")]
    [DataRow("wait 1 2", "takes 1 argument")]
    public void ABadWaitIsAProblemNamingItsLine(string line, string reason)
    {
        var script = Parse("# first\n" + line);

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], reason);
    }

    [TestMethod]
    public void EveryBadLineIsReported()
    {
        var script = Parse("nope\nwait x\nwait 1");

        Assert.HasCount(2, script.Problems);
        Assert.HasCount(1, script.Steps);
    }

    [TestMethod]
    public void TheInputStepsReadTheirArguments()
    {
        var script = Parse(
            "press Shift+Equal\nhold Right 0.5\nscroll up\nscroll down 3\nscroll up 2 100 50\n"
                + "drag 1 2 30 40\npinch 1.5\nswipe -2 3.5\nshot a\ndump b"
        );

        Assert.IsEmpty(script.Problems);
        Assert.AreEqual(new PressStep { Line = 1, Key = "Shift+Equal" }, script.Steps[0]);
        Assert.AreEqual(
            new HoldStep
            {
                Line = 2,
                Key = "Right",
                Seconds = 0.5,
            },
            script.Steps[1]
        );
        Assert.AreEqual(
            new ScrollStep
            {
                Line = 3,
                Up = true,
                Count = 1,
            },
            script.Steps[2]
        );
        Assert.AreEqual(
            new ScrollStep
            {
                Line = 4,
                Up = false,
                Count = 3,
            },
            script.Steps[3]
        );
        Assert.AreEqual(
            new ScrollStep
            {
                Line = 5,
                Up = true,
                Count = 2,
                At = (100, 50),
            },
            script.Steps[4]
        );
        Assert.AreEqual(
            new DragStep
            {
                Line = 6,
                FromX = 1,
                FromY = 2,
                ToX = 30,
                ToY = 40,
            },
            script.Steps[5]
        );
        Assert.AreEqual(new PinchStep { Line = 7, Factor = 1.5f }, script.Steps[6]);
        Assert.AreEqual(
            new SwipeStep
            {
                Line = 8,
                Dx = -2,
                Dy = 3.5f,
            },
            script.Steps[7]
        );
        Assert.AreEqual(new ShotStep { Line = 9, Name = "a" }, script.Steps[8]);
        Assert.AreEqual(new DumpStep { Line = 10, Name = "b" }, script.Steps[9]);
    }

    [TestMethod]
    [DataRow("press", "takes 1 argument")]
    [DataRow("press Control+A", "modifier")]
    [DataRow("press +", "key")]
    [DataRow("hold Left", "takes 2 arguments")]
    [DataRow("hold Left x", "not a number of seconds")]
    [DataRow("scroll sideways", "up or down")]
    [DataRow("scroll up 0", "whole number")]
    [DataRow("scroll up 1 5", "takes 1 to 4")]
    [DataRow("scroll up 1 a b", "not a number")]
    [DataRow("drag 1 2 3", "takes 4 arguments")]
    [DataRow("drag 1 2 3 x", "not a number")]
    [DataRow("pinch 0", "positive")]
    [DataRow("pinch big", "positive")]
    [DataRow("swipe 1", "takes 2 arguments")]
    [DataRow("shot", "takes 1 argument")]
    [DataRow("shot a/b", "no file name")]
    [DataRow("dump ..", "no file name")]
    [DataRow("shot ../x", "no file name")]
    [DataRow("dump ../x", "no file name")]
    public void ABadInputStepIsAProblemNamingItsLine(string line, string reason)
    {
        var script = Parse("wait 1\n" + line);

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], reason);
    }

    [TestMethod]
    public void AnExpectStepReadsItsFieldOperatorAndValue()
    {
        var script = Parse("expect camera-x >= 10\nexpect zoom = 1.25\nexpect seed = 1234 5678 1234 5678");

        Assert.IsEmpty(script.Problems);
        Assert.AreEqual(
            new ExpectStep
            {
                Line = 1,
                Field = "camera-x",
                Op = ">=",
                Value = "10",
            },
            script.Steps[0]
        );
        Assert.AreEqual("1.25", ((ExpectStep)script.Steps[1]).Value);
        Assert.AreEqual("1234 5678 1234 5678", ((ExpectStep)script.Steps[2]).Value);
    }

    [TestMethod]
    [DataRow("expect zoom", "takes a field, an operator, and a value")]
    [DataRow("expect height = 3", "unknown field 'height'")]
    [DataRow("expect zoom == 3", "unknown operator '=='")]
    [DataRow("expect zoom = big", "not a number")]
    [DataRow("expect zoom = 1 2", "not a number")]
    [DataRow("expect seed < 3", "takes only '='")]
    public void ABadExpectIsAProblemNamingItsLine(string line, string reason)
    {
        var script = Parse("wait 1\n" + line);

        Assert.HasCount(1, script.Problems);
        StringAssert.StartsWith(script.Problems[0], "play/t.play line 2: ");
        StringAssert.Contains(script.Problems[0], reason);
    }

    [TestMethod]
    public void EveryCheckedInScriptReadsWithoutProblems()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
            folder = folder.Parent;
        var scripts = Directory.GetFiles(Path.Combine(folder!.FullName, "play"), "*.play");

        Assert.IsNotEmpty(scripts);
        foreach (var path in scripts)
        {
            var script = PlayScript.Read(path);

            Assert.IsEmpty(script.Problems, string.Join("; ", script.Problems));
            Assert.IsNotEmpty(script.Steps);
        }
    }

    [TestMethod]
    public void AMissingFileIsOneProblem()
    {
        var script = PlayScript.Read("/no/such/dir/x.play");

        Assert.HasCount(1, script.Problems);
        StringAssert.Contains(script.Problems[0], "/no/such/dir/x.play");
    }
}

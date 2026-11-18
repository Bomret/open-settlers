using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The tests of the open settlers source that need the map view and the game besides the source: the tests of the source alone
// are in its test project, in the class of the same name. The jobs the content set draws are those the History Edition's figure
// tables hold a handler for (SettlerCases.JobsWithAFigureHandler); the tables' test of the same list is in
// src/Import.HistoryEdition.Tests.
[TestClass]
public sealed class OpenSettlersSourceTests
{
    // Reads the size of a PNG from its header; the pixels are a stand-in, since only Godot decodes them.
    sealed class HeaderDecoder : IImageDecoder
    {
        public DecodedImage? Decode(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (
                bytes.Length < 24
                || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            )
            {
                return null;
            }

            var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));

            return new DecodedImage
            {
                Width = width,
                Height = height,
                Rgba = new byte[width * height * 4],
            };
        }
    }

    static string RepoPath(string relative)
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "tools", "play-run.sh")))
        {
            dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("tools/play-run.sh not found");
        }

        return Path.Combine(dir, relative);
    }

    // A walk steps port with no install behind it: the frames 7 and 8 have a step at the node, every other frame none.
    sealed class FakeWalkSteps : IWalkSteps
    {
        public StepOffset? At(int frame, int timeLeft) => frame is 7 or 8 ? new StepOffset { Dx = 0, Dy = 0 } : null;
    }

    [TestMethod]
    public void TheCheckedInContentSetDrawsEverySettlerTheBuildersEmit()
    {
        var source = OpenSettlersSource.Load(RepoPath("content/basic"), new HeaderDecoder());
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
        var figures = new SettlerFigures(new FakeWalkSteps(), new FigureTables());
        var heading = new Heading { Value = 0 };
        // The activities the builder draws (a frame that has a step gives each a figure term) and those it does not.
        Activity[] drawnActivities =
        [
            new Walks { Errand = new ToStorage(), Heading = heading },
            new WalksRoad { Load = null, Heading = heading },
            new WalksRoad
            {
                Load = new Load { Good = Good.Log, Destination = null },
                Heading = heading,
            },
            new WaitsOutStep
            {
                Then = new Walks { Errand = new ToStorage(), Heading = heading },
            },
            new HandsOver { Load = null, Heading = heading },
            new HandsOver
            {
                Load = new Load { Good = Good.Log, Destination = null },
                Heading = heading,
            },
            new Door { TimeLeft = 0 },
            new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = new Vector(0, 0),
                    Purpose = new BackToHut { Carrying = false },
                },
            },
            new Cuts
            {
                Back = new Vector(0, 0),
                SecondVariant = false,
                Count = 0,
                Memory = 0,
            },
            new SearchesHome(),
        ];
        Activity[] otherActivities =
        [
            new WaitsAtFlag
            {
                Flag = 0,
                Direction = 0,
                Approach = heading.Retried,
            },
            new ClaimsNode { Heading = heading, Approach = heading.Retried },
            new WaitsToGoHome { Approach = heading.Retried },
            new WaitsToRejoin { Approach = heading.Retried },
        ];

        // Every job the item's byte 0 can carry (bits 2 to 6): a job the History Edition's tables hold a figure handler for must
        // have a figure in the content set for every activity it is drawn in, and a job without one none, so the two sources draw
        // the same jobs.
        var jobsDrawn = 0;
        for (var code = 0; code < 32; code++)
        {
            var job = Job.Of(code);
            Settler Of(Activity activity) => new(job, 3, 4, 3, 0, activity) { Frame = 7 };

            if (!SettlerCases.JobsWithAFigureHandler.Contains(job))
            {
                foreach (var activity in drawnActivities)
                {
                    Assert.IsTrue(
                        figures.Draws(map, [Of(activity)], 0).All(term => term.Ask(source) is null),
                        $"job {code} {activity}"
                    );
                }

                continue;
            }

            jobsDrawn++;
            foreach (var activity in drawnActivities)
            {
                var terms = figures.Draws(map, [Of(activity)], 0);
                Assert.IsTrue(terms.Any(term => term.Part == SettlerPart.Figure), $"job {code} {activity}");
                foreach (var term in terms)
                {
                    Assert.AreEqual(
                        term.Part == SettlerPart.Figure,
                        term.Ask(source) is not null,
                        $"job {code} {activity}"
                    );
                }
            }

            foreach (var activity in otherActivities)
            {
                foreach (var term in figures.Draws(map, [Of(activity)], 0))
                {
                    Assert.AreEqual(
                        term.Part == SettlerPart.Figure,
                        term.Ask(source) is not null,
                        $"job {code} {activity}"
                    );
                }
            }
        }

        Assert.IsGreaterThanOrEqualTo(3, jobsDrawn);

        // A carrier waiting off the road is drawn by the builder's second path, whose Figure term the source must answer.
        var waiting = figures.Draws(
            map,
            [
                new Settler(
                    new Carrier(),
                    3,
                    4,
                    3,
                    0,
                    new WaitsAtFlag
                    {
                        Flag = 0,
                        Direction = 0,
                        Approach = heading.Retried,
                    }
                )
                {
                    Frame = 7,
                },
            ],
            0
        );
        Assert.IsTrue(waiting.Any(term => term.Part == SettlerPart.Figure));
        Assert.IsTrue(waiting.Where(term => term.Part == SettlerPart.Figure).All(term => term.Ask(source) is not null));
    }
}

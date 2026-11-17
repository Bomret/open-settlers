using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSettlers.Import.ContentSet;

/// <summary>
/// The walk steps port of the basic content set (ADR 0017, ADR 0018): the dx and dy of every step of every frame, read from the
/// hand-written JSONC file <see cref="WalkStepsFile"/> of the set's folder. The file holds named sequences of
/// <see cref="Entries"/> offsets and lists the frames each sequence serves; its values are the project's own invention
/// (INV-0001), each with its reason beside it. The settler's frame picks the sequence and the time it has left picks the entry,
/// 8 clock units to an entry.
/// </summary>
public sealed class OpenSettlersWalkSteps : IWalkSteps
{
    /// <summary>The walk steps file, by its path under the content set's folder.</summary>
    public const string WalkStepsFile = "rules/walk-steps.jsonc";

    /// <summary>The number of frames the file covers: a settler's frame is a byte.</summary>
    public const int Frames = 256;

    /// <summary>The number of offsets every sequence holds: the time left, shifted right 3, picks one.</summary>
    public const int Entries = 16;

    // The offset is a signed byte, as the install's walk steps hold it.
    const int MaxOffset = 127;

    readonly StepOffset[][] _frames;

    OpenSettlersWalkSteps(StepOffset[][] frames) => _frames = frames;

    /// <summary>
    /// Reads the walk steps file in <paramref name="folder"/>. Throws an <see cref="InvalidDataException"/> of the form
    /// <c>&lt;file&gt;: &lt;key&gt;: &lt;reason&gt;</c> for the first fault of the file: malformed JSONC, a missing key, a sequence of
    /// the wrong length, an unknown sequence name, a frame outside 0 to 255 or listed twice, a value outside what a pixel
    /// offset allows, or a duplicate key. An <see cref="IOException"/> or an <see cref="UnauthorizedAccessException"/> reports a
    /// file that cannot be read.
    /// </summary>
    public static OpenSettlersWalkSteps Load(string folder)
    {
        var root = StandInRulesReader.Open(folder, WalkStepsFile).Fields(["sequences", "frames", "default"]);
        var sequences = root["sequences"].Members().ToDictionary(member => member.Key, member => Sequence(member.Node));
        var frames = Enumerable.Repeat(root["default"].Term(sequences, "sequence"), Frames).ToArray();
        var listed = new bool[Frames];
        foreach (var (name, listing) in root["frames"].Members())
        {
            if (!sequences.TryGetValue(name, out var sequence))
            {
                throw listing.Fault($"unknown sequence; the known ones are {string.Join(", ", sequences.Keys)}");
            }

            foreach (var item in listing.Items())
            {
                var (first, last) = item.Range(0, Frames - 1);
                for (var frame = first; frame <= last; frame++)
                {
                    frames[frame] = listed[frame] ? throw item.Fault($"frame {frame} is listed twice") : sequence;
                    listed[frame] = true;
                }
            }
        }

        return new OpenSettlersWalkSteps(frames);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A time left past the last entry takes the last entry, so a settler at work or at rest (whose timer runs to 16 bits) has a
    /// step. Null only for a frame outside 0 to 255 and a negative time.
    /// </remarks>
    public StepOffset? At(int frame, int timeLeft) =>
        frame is < 0 or >= Frames || timeLeft < 0 ? null : _frames[frame][Math.Min(timeLeft >> 3, Entries - 1)];

    static StepOffset[] Sequence(RuleNode node) =>
        [
            .. node.Items(Entries)
                .Select(entry =>
                {
                    var pair = entry.Items(2);

                    return new StepOffset
                    {
                        Dx = pair[0].Int(-MaxOffset, MaxOffset),
                        Dy = pair[1].Int(-MaxOffset, MaxOffset),
                    };
                }),
        ];
}

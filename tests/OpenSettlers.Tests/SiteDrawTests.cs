using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The lines of site-draws.txt are the output of the emulator verb draw with site@X,Y in place of the camera (tools/GhidraDump/emulate.py,
// EmulateDraw.java, ADR 0008): the original's building draw of a tile (0x44e329, which reaches the draw of a building under construction,
// 0x44fa79) run on a building site after the passes of a construct game. A pass line gives the site's 18 bytes (b=: the type in bits 2 to 6
// of byte 4, the progress word at 12) and what the draw drew (d=), in order: sh<entry> a shadow, <entry>/<fraction>/x,y a sprite (the
// fraction -1 for a sprite drawn whole), and good<entry> the sprite of a good on site. The engine's layers for the progress word
// (SiteStages.Parts), each with its History Edition entry (ObjectSprites.SiteLayerEntry) after its shadow (the mask at the entry plus 250,
// HistoryEditionSource), must be what the original drew. The piles of the goods on site are not ported and are left out.
[TestClass]
public sealed class SiteDrawTests
{
    const string Fixture = "site-draws.txt";

    [TestMethod]
    [DataRow("forester-site")]
    [DataRow("sawmill-site")]
    public void TheLayersOfASiteAreTheOnesTheOriginalDraws(string name)
    {
        var lines = File.ReadAllLines(Fixture)
            .SkipWhile(line => !line.StartsWith($"# {name} ", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("# ", StringComparison.Ordinal))
            .Where(line => line.StartsWith("pass=", StringComparison.Ordinal))
            .ToList();
        Assert.IsNotEmpty(lines, name);

        foreach (var line in lines)
        {
            var fields = line.Split(' ')
                .Select(field => field.Split('=', 2))
                .ToDictionary(pair => pair[0], pair => pair[1]);
            var bytes = Convert.FromHexString(fields["b"]);
            var type = new BuildingType { Code = (bytes[4] >> 2) & 0x1f };
            var progress = (ushort)(bytes[12] | bytes[13] << 8);
            var drawn = fields["d"]
                .Split(';')
                .Where(call => !call.StartsWith("good", StringComparison.Ordinal))
                .Select(call =>
                    call.StartsWith("sh", StringComparison.Ordinal) ? call : string.Join('/', call.Split('/')[..2])
                );
            var engine = SiteStages
                .Parts(progress)
                .SelectMany(part =>
                {
                    var entry = ObjectSprites.SiteLayerEntry(part.Layer, type)!.Value;

                    return new[] { $"sh{entry + 250}", $"{entry}/{part.Fraction ?? -1}" };
                });

            CollectionAssert.AreEqual(
                drawn.ToList(),
                engine.ToList(),
                $"{name}, pass {fields["pass"]}, progress {progress:X4}"
            );
        }
    }
}

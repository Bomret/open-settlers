using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;

namespace OpenSettlers.Tests;

// SettlerSprites.Load reads the palette (entry 3), the walk sequences (entry 2), and the shadow mask (entry 4) of the graphics
// archive. The messages are the form of TerrainGraphics.Entry: "<what> (entry <n>) of loca/SPAD.PA is unreadable: <reason>".
[TestClass]
public sealed class SettlerSpritesLoadTests
{
    const string Archive = "loca/SPAD.PA";

    static Install Open(FakeInstall fake)
    {
        Assert.IsTrue(Install.TryOpen(fake.Root, out var install, out _));

        return install;
    }

    // A player color outside the palette is refused with its value, whether or not the overlay has a covered pixel.
    [TestMethod]
    [DataRow(256, true)]
    [DataRow(-1, true)]
    [DataRow(256, false)]
    public void APlayerColorOutsideThePaletteIsRefusedWithTheValue(int colour, bool covered)
    {
        var overlay = new ObjectImage
        {
            Width = 1,
            Height = 1,
            OffsetX = 0,
            OffsetY = 0,
            Rgba = covered ? [1, 2, 3, 255] : [0, 0, 0, 0],
        };

        var failure = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SettlerSprites.Fill(overlay, OpenSettlers.Formats.PaletteFile.Read(new byte[768]), colour)
        );

        StringAssert.Contains(failure.Message, colour.ToString());
    }

    // The rule tables are read only after the three entries, so a load that fails on an entry never reaches them.
    static RuleTables Tables() => new(new ObjectClass[RuleTables.ClassCount], MadeProgram.MadeStartStock());

    static InvalidDataException Refused(Dictionary<int, byte[]> payloads)
    {
        using var fake = new FakeInstall().Put(Archive, ArchiveBytes.Build(5, payloads));

        return Assert.ThrowsExactly<InvalidDataException>(() => SettlerSprites.Load(Open(fake), Tables()));
    }

    [TestMethod]
    public void AnAbsentPaletteNamesItsEntry()
    {
        var failure = Refused(new Dictionary<int, byte[]> { [2] = GraphicEntries.Sequences() });

        StringAssert.Contains(failure.Message, "the palette (entry 3) of loca/SPAD.PA is unreadable");
    }

    [TestMethod]
    public void AnAbsentSequencesEntryNamesItsEntry()
    {
        var failure = Refused(new Dictionary<int, byte[]> { [3] = new byte[768] });

        StringAssert.Contains(failure.Message, "the walk sequences (entry 2) of loca/SPAD.PA is unreadable");
    }

    [TestMethod]
    public void AnUnreadableShadowMaskNamesItsEntry()
    {
        var failure = Refused(
            new Dictionary<int, byte[]>
            {
                [2] = GraphicEntries.Sequences(),
                [3] = new byte[768],
                [4] = [1, 2, 3],
            }
        );

        StringAssert.Contains(failure.Message, "the shadow mask (entry 4) of loca/SPAD.PA is unreadable");
    }
}

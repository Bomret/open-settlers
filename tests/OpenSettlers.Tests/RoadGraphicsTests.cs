using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class RoadGraphicsTests
{
    const string Archive = "loca/SPAD.PA";

    // A mask of width x height whose rows each cover all but the first pixel.
    static byte[] Mask(int width, int height, short offsetX = 0, short offsetY = 0)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)width));
        bytes.AddRange(BitConverter.GetBytes((ushort)height));
        bytes.AddRange(BitConverter.GetBytes(offsetX));
        bytes.AddRange(BitConverter.GetBytes(offsetY));
        for (var row = 0; row < height; row++)
        {
            bytes.AddRange([1, (byte)(width - 1), 0, 0]);
        }

        return [.. bytes];
    }

    static byte[] Texture(byte index)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)32));
        bytes.AddRange(BitConverter.GetBytes((ushort)20));
        bytes.AddRange(new byte[4]);
        bytes.AddRange(new byte[32 * 20]);
        for (var i = 10; i < bytes.Count; i++)
        {
            bytes[i] = index;
        }

        return [.. bytes];
    }

    static Dictionary<int, byte[]> Payloads()
    {
        // Palette indexes 10 and 19 hold the red components 40 and 76; the rest is black.
        var palette = new byte[768];
        palette[3 * 10] = 40;
        palette[3 * 19] = 76;
        var payloads = new Dictionary<int, byte[]> { [3] = palette };
        for (var n = RoadGraphics.FirstMask; n <= RoadGraphics.LastMask; n++)
        {
            payloads[n] = Mask(16, 9 + n % 4, 0, (short)(n - 240));
        }

        for (var n = RoadGraphics.FirstTexture; n <= RoadGraphics.LastTexture; n++)
        {
            payloads[n] = Texture((byte)(n - 290));
        }

        return payloads;
    }

    static Install Open(FakeInstall fake)
    {
        Assert.IsTrue(
            Install.TryOpen(OpenSettlers.Launch.LaunchArgs.Parse(["--game", fake.Root], null), out var install, out _)
        );

        return install;
    }

    [TestMethod]
    public void LoadingReadsTheStripMasksAndTheRoadTexturesInEntryOrder()
    {
        using var fake = new FakeInstall().Put(Archive, ArchiveBytes.Build(311, Payloads()));

        var graphics = RoadGraphics.Load(Open(fake));

        Assert.HasCount(27, graphics.Masks);
        Assert.HasCount(10, graphics.Textures);
        Assert.AreEqual(9 + 230 % 4, graphics.Masks[RoadGraphics.MaskLayer(230)].Height);
        CollectionAssert.AreEqual(new byte[] { 40, 0, 0, 255 }, graphics.Textures[RoadGraphics.TextureLayer(300)][..4]);
        CollectionAssert.AreEqual(new byte[] { 76, 0, 0, 255 }, graphics.Textures[RoadGraphics.TextureLayer(309)][..4]);
    }

    [TestMethod]
    public void AMissingArchiveAndAnAbsentEntryNameThemselves()
    {
        using var empty = new FakeInstall();
        var missing = Assert.ThrowsExactly<FileNotFoundException>(() => RoadGraphics.Load(Open(empty)));
        StringAssert.Contains(missing.Message, Archive);

        var payloads = Payloads();
        payloads.Remove(240);
        using var fake = new FakeInstall().Put(Archive, ArchiveBytes.Build(311, payloads));
        var absent = Assert.ThrowsExactly<InvalidDataException>(() => RoadGraphics.Load(Open(fake)));
        StringAssert.Contains(absent.Message, "entry 240");
    }
}

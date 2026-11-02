using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TerrainGraphicsTests
{
    const string Archive = "loca/SPAD.PA";

    // A mask of width x height whose rows each cover all but the first pixel.
    static byte[] Mask(int width, int height, short offsetY = 0)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)width));
        bytes.AddRange(BitConverter.GetBytes((ushort)height));
        bytes.AddRange(BitConverter.GetBytes((short)0));
        bytes.AddRange(BitConverter.GetBytes(offsetY));
        for (var row = 0; row < height; row++)
        {
            bytes.AddRange([1, (byte)(width - 1), 0, 0]);
        }

        return [.. bytes];
    }

    static byte[] Texture(int width, int height, byte index)
    {
        var bytes = new List<byte> { 0, 0 };
        bytes.AddRange(BitConverter.GetBytes((ushort)width));
        bytes.AddRange(BitConverter.GetBytes((ushort)height));
        bytes.AddRange(new byte[4]);
        bytes.AddRange(new byte[width * height]);
        for (var i = 10; i < bytes.Count; i++)
        {
            bytes[i] = index;
        }

        return [.. bytes];
    }

    static Dictionary<int, byte[]> Payloads(Func<int, byte[]?>? replace = null)
    {
        var payloads = new Dictionary<int, byte[]> { [3] = new byte[768] };
        for (var n = 60; n <= 221; n++)
        {
            payloads[n] = replace?.Invoke(n) ?? Mask(32, 9 + n % 3);
        }

        for (var n = 260; n <= 292; n++)
        {
            payloads[n] = replace?.Invoke(n) ?? Texture(32, 20, (byte)(n - 250));
        }

        return payloads;
    }

    static FakeInstall Install(Dictionary<int, byte[]> payloads) =>
        new FakeInstall().Put(Archive, ArchiveBytes.Build(293, payloads));

    static OpenSettlers.Assets.Install Open(FakeInstall fake)
    {
        Assert.IsTrue(
            OpenSettlers.Assets.Install.TryOpen(
                OpenSettlers.Launch.LaunchArgs.Parse(["--game", fake.Root], null),
                out var install,
                out _
            )
        );

        return install;
    }

    [TestMethod]
    public void LoadingReadsMaskLayersAndTexturesInEntryOrder()
    {
        using var fake = Install(Payloads());

        var graphics = TerrainGraphics.Load(Open(fake));

        Assert.HasCount(162, graphics.Masks);
        Assert.HasCount(33, graphics.Textures);
        Assert.AreEqual(9 + 60 % 3, graphics.Masks[0].Height);
        Assert.AreEqual(0, graphics.Masks[0].Layer[0]);
        Assert.AreEqual(255, graphics.Masks[0].Layer[1]);
        Assert.AreEqual(10, graphics.Textures[0][0]);
        Assert.HasCount(32 * 41, graphics.Masks[0].Layer);
        Assert.HasCount(256 * 4, graphics.PaletteRgba);
    }

    [TestMethod]
    public void AMaskKeepsItsOffsetAndAnEntryNumberFindsItsLayer()
    {
        using var fake = Install(Payloads(n => n == 141 ? Mask(32, 9, -8) : null));

        var graphics = TerrainGraphics.Load(Open(fake));

        Assert.AreEqual(-8, graphics.Masks[TerrainGraphics.MaskLayer(141)].OffsetY);
        Assert.AreEqual(0, TerrainGraphics.MaskLayer(60));
        Assert.AreEqual(0, TerrainGraphics.TextureLayer(260));
    }

    [TestMethod]
    public void AMissingArchiveIsAnErrorThatNamesIt()
    {
        using var fake = new FakeInstall();

        var problem = Assert.ThrowsExactly<FileNotFoundException>(() => TerrainGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, Archive);
    }

    [TestMethod]
    public void AnAbsentMaskNamesItsEntry()
    {
        var payloads = Payloads();
        payloads.Remove(100);
        using var fake = Install(payloads);

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => TerrainGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "entry 100");
    }

    [TestMethod]
    public void AMaskTheRulesNeverPickIsAllowedToBeAbsent()
    {
        var payloads = Payloads();
        payloads.Remove(65);
        payloads.Remove(141 + 5);
        using var fake = Install(payloads);

        var graphics = TerrainGraphics.Load(Open(fake));

        Assert.AreEqual(0, graphics.Masks[TerrainGraphics.MaskLayer(65)].Width);
        Assert.IsFalse(TerrainRules.UsesMask(65));
        Assert.IsFalse(TerrainRules.UsesMask(146));
        Assert.IsTrue(TerrainRules.UsesMask(100));
        Assert.IsTrue(TerrainRules.UsesMask(181));
    }

    [TestMethod]
    public void AnUnreadableMaskNamesItsEntry()
    {
        using var fake = Install(Payloads(n => n == 77 ? [1, 2, 3] : null));

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => TerrainGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "entry 77");
    }

    [TestMethod]
    public void ATextureThatIsNotThirtyTwoByTwentyNamesItsEntry()
    {
        using var fake = Install(Payloads(n => n == 270 ? Texture(16, 20, 1) : null));

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => TerrainGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "entry 270");
    }

    [TestMethod]
    public void AMissingPaletteNamesItsEntry()
    {
        var payloads = Payloads();
        payloads.Remove(3);
        using var fake = Install(payloads);

        var problem = Assert.ThrowsExactly<InvalidDataException>(() => TerrainGraphics.Load(Open(fake)));

        StringAssert.Contains(problem.Message, "entry 3");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The importer turns the original's layers into the typed map and the exporter turns the typed map back (ADR 0013).
[TestClass]
public sealed class MapImporterTests
{
    static readonly MapSeed[] Seeds = [new(0x1234, 0x5678, 0x9ABC), new(0x6359, 0xA0D2, 0xB4FA), new(1, 2, 3)];

    public static IEnumerable<object[]> SizesAndSeeds =>
        MapSize.Offered.SelectMany(size => Seeds.Select(seed => new object[] { size, seed }));

    [TestMethod]
    [DynamicData(nameof(SizesAndSeeds))]
    public void ImportingAndExportingAGeneratedMapGivesBackIdenticalLayers(MapSize size, MapSeed seed)
    {
        MapLayers? generated = null;
        var snapshot = new Dictionary<MapLayer, byte[]>();
        MapGenerator.Generate(
            TestProgram.Made(),
            seed,
            size,
            (pass, layers, _) =>
            {
                if (pass == MapPass.CountResource1)
                {
                    generated = layers;
                    foreach (var layer in Enum.GetValues<MapLayer>())
                    {
                        snapshot[layer] = layers.Layer(layer).ToArray();
                    }
                }
            }
        );

        var exported = MapLayerExporter.Export(MapImporter.Import(generated!));

        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            CollectionAssert.AreEqual(snapshot[layer], exported.Layer(layer).ToArray(), $"{size.Value} {layer}");
        }

        Assert.AreEqual(generated!.Generator, exported.Generator);
        Assert.AreEqual(generated.Resource1Total, exported.Resource1Total);
    }

    [TestMethod]
    public void EveryValueOfEveryLayerSurvivesImportAndExport()
    {
        var size = MapSize.Offered[0];
        var layers = new MapLayers(size);
        foreach (
            var layer in new[] { MapLayer.Flags, MapLayer.Height, MapLayer.Terrain, MapLayer.Object, MapLayer.Lake }
        )
        {
            var bytes = layers.Bytes(layer);
            for (var tile = 0; tile < bytes.Length; tile++)
            {
                bytes[tile] = (byte)(tile * 7 + (int)layer * 31);
            }
        }

        var exported = MapLayerExporter.Export(MapImporter.Import(layers));

        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            CollectionAssert.AreEqual(layers.Layer(layer).ToArray(), exported.Layer(layer).ToArray(), $"{layer}");
        }
    }

    [TestMethod]
    public void AnObjectCodeTheEngineHasNoNameForSurvivesImportAndExportAsItsRawValue()
    {
        var layers = new MapLayers(MapSize.Offered[0]);
        layers.Bytes(MapLayer.Object)[5] = 0x7e;
        layers.Bytes(MapLayer.Object)[6] = 0xfd;

        var map = MapImporter.Import(layers);

        Assert.AreEqual((MapObjectKind.Other, (byte)0x7e), (map.At(5).Object.Kind, map.At(5).Object.Code));
        Assert.AreEqual((MapObjectKind.Other, (byte)0x7d), (map.At(6).Object.Kind, map.At(6).Object.Code));
        Assert.IsTrue(map.At(6).WaterTriangle);
        var exported = MapLayerExporter.Export(map).Layer(MapLayer.Object);
        Assert.AreEqual((0x7e, 0xfd), ((int)exported[5], (int)exported[6]));
    }

    [TestMethod]
    public void TheImporterNamesEveryFieldOfANode()
    {
        var layers = new MapLayers(MapSize.Offered[0]);
        layers.Bytes(MapLayer.Flags)[3] = 0xc5;
        layers.Bytes(MapLayer.Height)[3] = 0x87;
        layers.Bytes(MapLayer.Terrain)[3] = 0x5b;
        layers.Bytes(MapLayer.Object)[3] = 0x84;
        layers.Bytes(MapLayer.Resource)[3] = 0x34;

        var node = MapImporter.Import(layers).At(3);

        Assert.AreEqual(RoadSet.Of(0).With(2), node.Roads);
        Assert.IsTrue(node.LakeMark);
        Assert.IsTrue(node.FlagMark);
        Assert.AreEqual((7, new Owner(4)), (node.Height, node.Owner));
        Assert.AreEqual((new Terrain(5), new Terrain(11)), (node.Upper, node.Lower));
        Assert.AreEqual((MapObjectKind.Castle, true), (node.Object.Kind, node.WaterTriangle));
        Assert.AreEqual((1, 20), (node.DepositType, node.DepositAmount));
    }

    [TestMethod]
    public void ALinkTakesTheByteOfTheDepositWhenTheMapIsExported()
    {
        var map = new Map(MapSize.Offered[0]);
        map.At(4).DepositType = 2;
        map.At(4).DepositAmount = 9;
        map.At(4).LinkTo(7);
        map.At(5).DepositType = 2;
        map.At(5).DepositAmount = 9;

        var exported = MapLayerExporter.Export(map).Layer(MapLayer.Resource);

        Assert.AreEqual((7, 0x49), ((int)exported[4], (int)exported[5]));
    }
}

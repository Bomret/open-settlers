using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// INV-0011 and ADR 0017: the simulation reaches game data through the import ports. The domain and the application reference no
// source assembly, and the adapter assembly names one History Edition type, the state names of JobTableNames.
[TestClass]
public sealed class ImportPortBoundaryTests
{
    const string HistoryEdition = "OpenSettlers.Import.HistoryEdition";
    const string ContentSet = "OpenSettlers.Import.OpenSettlers";

    [TestMethod]
    public void TheDomainAndTheApplicationReferenceNoSourceAssembly()
    {
        var referenced = new[] { typeof(RuleTables).Assembly, typeof(Game).Assembly }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(name => name.Name)
            .ToList();

        Assert.IsFalse(
            referenced.Contains(HistoryEdition) || referenced.Contains(ContentSet),
            string.Join(", ", referenced)
        );
    }

    [TestMethod]
    public void TheAdapterAssemblyNamesNoHistoryEditionTypeButTheJobTableNames()
    {
        CollectionAssert.AreEqual(
            new[] { $"{HistoryEdition}.JobTableNames" },
            TypesNamed(typeof(NameTable).Assembly, HistoryEdition).ToList()
        );
    }

    // The positive control: the scan finds the History Edition types an assembly does name, such as the tests' use of Install.
    [TestMethod]
    public void TheScanFindsTheHistoryEditionTypesAnAssemblyNames()
    {
        CollectionAssert.Contains(
            TypesNamed(typeof(ImportPortBoundaryTests).Assembly, HistoryEdition).ToList(),
            $"{HistoryEdition}.Install"
        );
    }

    // The full names of the types of the assembly <paramref name="source"/> that <paramref name="assembly"/> refers to in its
    // metadata, a nested type by its outermost type, sorted. A constant the compiler copied in leaves no reference.
    static IEnumerable<string> TypesNamed(Assembly assembly, string source)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in reader.TypeReferences)
        {
            var type = reader.GetTypeReference(handle);
            while (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                type = reader.GetTypeReference((TypeReferenceHandle)type.ResolutionScope);
            }

            if (
                type.ResolutionScope.Kind == HandleKind.AssemblyReference
                && reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name)
                    == source
            )
            {
                names.Add($"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}");
            }
        }

        return names;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ArchiveFileTests
{
    // The smallest table that holds every alias slot the program fills.
    const int Rows = 3768;

    static Archive Read(int rows, IReadOnlyDictionary<int, byte[]>? payloads = null) =>
        ArchiveFile.Read(ArchiveBytes.Build(rows, payloads));

    [TestMethod]
    public void EntriesAreNumberedFromOneAsTheProgramNumbersTheTableRows()
    {
        var archive = Read(4, new Dictionary<int, byte[]> { [3] = new byte[768] });

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, archive.Entries.Select(e => e.Number).ToList());
        Assert.AreEqual(4, archive.RowCount);
        Assert.AreEqual(768u, archive[3].Size);
        Assert.AreEqual(4u * 8, archive[3].Offset);
    }

    [TestMethod]
    public void AnEntryWithOffsetZeroIsAbsent()
    {
        var archive = Read(4, new Dictionary<int, byte[]> { [2] = [1] });

        Assert.AreEqual(EntryState.Absent, archive[1].State);
        Assert.AreEqual(EntryState.Present, archive[2].State);
    }

    [TestMethod]
    public void AnEntryDataIsTheBytesAtItsLocation()
    {
        var archive = Read(4, new Dictionary<int, byte[]> { [1] = [7, 8], [3] = [9] });

        CollectionAssert.AreEqual(new byte[] { 7, 8 }, archive.Data(archive[1]).ToArray());
        CollectionAssert.AreEqual(new byte[] { 9 }, archive.Data(archive[3]).ToArray());
    }

    [TestMethod]
    public void AnEmptySlotInACopyRangeShowsAsTheEntryItRepeats()
    {
        var archive = Read(Rows, new Dictionary<int, byte[]> { [1352] = [5, 6] });

        var alias = archive[1363];
        Assert.AreEqual(EntryState.Alias, alias.State);
        Assert.AreEqual(1352, alias.AliasOf);
        Assert.AreEqual(archive[1352].Offset, alias.Offset);
        Assert.AreEqual(archive[1352].Size, alias.Size);
        CollectionAssert.AreEqual(new byte[] { 5, 6 }, archive.Data(alias).ToArray());
    }

    [TestMethod]
    [DataRow(1352, 1363, 1368)]
    [DataRow(1602, 1613, 1618)]
    [DataRow(3450, 3451, 3455)]
    [DataRow(3456, 3457, 3461)]
    [DataRow(3732, 3733, 3737)]
    public void EveryDestinationOfACopyRangeRepeatsItsSource(int source, int first, int last)
    {
        var archive = Read(Rows, new Dictionary<int, byte[]> { [source] = [1] });

        for (var slot = first; slot <= last; slot++)
        {
            Assert.AreEqual(source, archive[slot].AliasOf, $"slot {slot}");
        }
    }

    [TestMethod]
    public void TheThreeSlotsAfterEntry3762RepeatEntries3762To3764OneForOne()
    {
        var archive = Read(
            Rows,
            new Dictionary<int, byte[]>
            {
                [3762] = [1],
                [3763] = [2, 2],
                [3764] = [3, 3, 3],
            }
        );

        Assert.AreEqual(3762, archive[3765].AliasOf);
        Assert.AreEqual(3763, archive[3766].AliasOf);
        Assert.AreEqual(3764, archive[3767].AliasOf);
    }

    [TestMethod]
    public void AnEmptySlotOutsideEveryCopyRangeStaysAbsent()
    {
        var archive = Read(Rows, new Dictionary<int, byte[]> { [1352] = [1], [3762] = [1] });

        Assert.AreEqual(EntryState.Absent, archive[1369].State);
        Assert.AreEqual(EntryState.Absent, archive[3738].State);
    }

    [TestMethod]
    public void ACopyOfAnAbsentSourceLeavesTheSlotAbsent()
    {
        var archive = Read(Rows);

        Assert.AreEqual(EntryState.Absent, archive[1363].State);
        Assert.IsNull(archive[1363].AliasOf);
    }

    [TestMethod]
    public void ACopyOverwritesAPresentSlotAsTheProgramDoes()
    {
        var archive = Read(Rows, new Dictionary<int, byte[]> { [1352] = [1], [1363] = [2, 2] });

        Assert.AreEqual(EntryState.Alias, archive[1363].State);
        Assert.AreEqual(archive[1352].Offset, archive[1363].Offset);
    }

    [TestMethod]
    public void NoCopyChainsBecauseNoSourceIsADestination()
    {
        var archive = Read(
            Rows,
            new Dictionary<int, byte[]>
            {
                [1352] = [1],
                [1363] = [2],
                [3450] = [3],
                [3451] = [4],
            }
        );

        Assert.IsTrue(
            archive
                .Entries.Where(e => e.State == EntryState.Alias)
                .All(e => archive[e.AliasOf!.Value].State == EntryState.Present)
        );
    }

    [TestMethod]
    public void ATableShorterThanTheCopyRangesFillsOnlyTheSlotsItHolds()
    {
        var archive = Read(1400, new Dictionary<int, byte[]> { [1352] = [1] });

        Assert.AreEqual(1352, archive[1368].AliasOf);
        Assert.AreEqual(1399, archive.Entries.Last().Number);
    }

    [TestMethod]
    public void AFileShorterThanItsHeaderIsCorruptAndTheMessageNamesTheOffset()
    {
        var failure = Assert.ThrowsExactly<InvalidDataException>(() => ArchiveFile.Read([1, 2, 3]));

        StringAssert.Contains(failure.Message, "header");
        StringAssert.Contains(failure.Message, "offset 8");
    }

    [TestMethod]
    public void AFileShorterThanItsTableIsCorruptAndTheMessageNamesTheOffset()
    {
        var bytes = ArchiveBytes.Build(10);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => ArchiveFile.Read(bytes[..40]));

        StringAssert.Contains(failure.Message, "table");
        StringAssert.Contains(failure.Message, "offset 80");
    }

    [TestMethod]
    public void AHeaderSizeThatDiffersFromTheFileLengthIsCorrupt()
    {
        var bytes = ArchiveBytes.Build(4, new Dictionary<int, byte[]> { [1] = [1, 2, 3] });
        ArchiveBytes.Put(bytes, 0, (uint)bytes.Length + 5);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => ArchiveFile.Read(bytes));

        StringAssert.Contains(failure.Message, $"{bytes.Length + 5}");
        StringAssert.Contains(failure.Message, $"{bytes.Length}");
        StringAssert.Contains(failure.Message, "offset 0");
    }

    [TestMethod]
    public void RowZeroIsTheHeaderAndNoEntry()
    {
        var archive = Read(4);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => archive[0]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => archive[4]);
    }

    [TestMethod]
    public void AnEntryPastTheEndOfTheFileFailsOnItsOwnAndNamesTheEntryAndTheOffset()
    {
        var bytes = ArchiveBytes.Build(4, new Dictionary<int, byte[]> { [1] = [1], [2] = [2, 2] });
        ArchiveBytes.Put(bytes, 2 * 8, 500);
        var archive = ArchiveFile.Read(bytes);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => archive.Data(archive[2]));

        StringAssert.Contains(failure.Message, "entry 2");
        StringAssert.Contains(failure.Message, $"offset {archive[2].Offset}");
        CollectionAssert.AreEqual(new byte[] { 1 }, archive.Data(archive[1]).ToArray());
    }

    // Source: graphics-archives.md, "The header and the table": the row count includes the header's own row, so a
    // table of 0 rows is the 8-byte header alone and a table of 1 row is the header row, with no entry.
    [TestMethod]
    public void ATableOfZeroRowsHoldsNoEntry()
    {
        var bytes = new byte[8];
        ArchiveBytes.Put(bytes, 0, 8);

        var archive = ArchiveFile.Read(bytes);

        Assert.AreEqual(0, archive.RowCount);
        Assert.AreEqual(0, archive.Entries.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => archive[1]);
    }

    [TestMethod]
    public void ATableOfOneRowHoldsNoEntry()
    {
        var archive = Read(1);

        Assert.AreEqual(1, archive.RowCount);
        Assert.AreEqual(0, archive.Entries.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => archive[1]);
    }

    // Source: graphics-archives.md, "Corrupt archives" (an entry beyond the end of the file is reported on its
    // own with its number) and "Archive_FillAliases" (an alias repeats the size and offset of its source row 1352).
    [TestMethod]
    public void AnAliasWhoseSourceRunsPastTheEndOfTheFileFailsOnItsOwnAndNamesTheAlias()
    {
        var bytes = ArchiveBytes.Build(1364);
        ArchiveBytes.Put(bytes, 1352 * 8, 100);
        ArchiveBytes.Put(bytes, 1352 * 8 + 4, (uint)bytes.Length - 10);
        var archive = ArchiveFile.Read(bytes);

        var failure = Assert.ThrowsExactly<InvalidDataException>(() => archive.Data(archive[1363]));

        Assert.AreEqual(EntryState.Alias, archive[1363].State);
        StringAssert.Contains(failure.Message, "entry 1363");
    }

    [TestMethod]
    public void AnAbsentEntryHasNoDataToRead()
    {
        var archive = Read(4);

        Assert.ThrowsExactly<InvalidOperationException>(() => archive.Data(archive[1]));
    }
}

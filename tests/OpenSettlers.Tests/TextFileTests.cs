using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Formats;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class TextFileTests
{
    static TextFile Read(string text) => TextFile.Read(Encoding.Latin1.GetBytes(text));

    static string[] Pairs(TextFile file) => file.Entries.Select(e => $"{e.Key}|{e.Value}").ToArray();

    [TestMethod]
    public void LinesOfKeyAndQuotedValueWithCrlfGiveKeysAndValues()
    {
        var file = Read("ALPHA=\"one\"\r\nBETA=\"two\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "BETA|two" }, Pairs(file));
        Assert.IsEmpty(file.Rejected);
    }

    [TestMethod]
    public void SpacesInsideAValueAreKept()
    {
        var file = Read("LEAD=\"  two\"\r\nTRAIL=\"two  \"\r\nONLY=\"   \"\r\nMID=\"a  b\"\r\n");

        CollectionAssert.AreEqual(new[] { "LEAD|  two", "TRAIL|two  ", "ONLY|   ", "MID|a  b" }, Pairs(file));
    }

    [TestMethod]
    public void AnEmptyValueIsAValue()
    {
        var file = Read("EMPTY=\"\"\r\n");

        CollectionAssert.AreEqual(new[] { "EMPTY|" }, Pairs(file));
    }

    [TestMethod]
    public void TextAfterTheClosingQuoteIsIgnored()
    {
        var file = Read("ALPHA=\"one\",-1 \r\nBETA=\"two\" more=\"x\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "BETA|two" }, Pairs(file));
        Assert.IsEmpty(file.Rejected);
    }

    [TestMethod]
    public void TextBetweenTheEqualsSignAndTheOpeningQuoteIsIgnored()
    {
        var file = Read("ALPHA= x \"one\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one" }, Pairs(file));
    }

    [TestMethod]
    public void TheKeyIsEverythingBeforeTheFirstEqualsSignWithItsSpaces()
    {
        var file = Read("A B =\"one\"\r\n");

        CollectionAssert.AreEqual(new[] { "A B |one" }, Pairs(file));
    }

    [TestMethod]
    public void ALastLineWithoutCrlfIsRead()
    {
        var file = Read("ALPHA=\"one\"\r\nBETA=\"two\"");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "BETA|two" }, Pairs(file));
        Assert.IsEmpty(file.Rejected);
    }

    [TestMethod]
    public void ALastLineWithoutCrlfAndWithoutAClosingQuoteIsRejected()
    {
        var file = Read("ALPHA=\"one\"\r\nBETA=\"two");

        CollectionAssert.AreEqual(new[] { "ALPHA|one" }, Pairs(file));
        Assert.AreEqual(2, file.Rejected.Single().Line);
    }

    [TestMethod]
    public void ALineFeedAloneAndACarriageReturnAloneEndALine()
    {
        var file = Read("ALPHA=\"one\"\nBETA=\"two\"\rGAMMA=\"three\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "BETA|two", "GAMMA|three" }, Pairs(file));
        Assert.IsEmpty(file.Rejected);
    }

    [TestMethod]
    public void TheTerminatorAfterTheLastLineStartsNoLine()
    {
        Assert.IsEmpty(Read("ALPHA=\"one\"\r\n").Rejected);
        Assert.IsEmpty(Read("").Rejected);
    }

    [TestMethod]
    public void ARepeatedKeyKeepsItsFirstPlaceAndItsLastValue()
    {
        var file = Read("ALPHA=\"one\"\r\nBETA=\"two\"\r\nALPHA=\"three\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|three", "BETA|two" }, Pairs(file));
    }

    [TestMethod]
    public void KeysAreCaseSensitive()
    {
        var file = Read("ALPHA=\"one\"\r\nalpha=\"two\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "alpha|two" }, Pairs(file));
    }

    [TestMethod]
    public void AValueLookupFindsTheValueOfAKey()
    {
        var file = Read("ALPHA=\"one\"\r\n");

        Assert.AreEqual("one", file.Get("ALPHA"));
        Assert.IsNull(file.Get("BETA"));
    }

    [TestMethod]
    public void ALineOfFourCharactersIsRejectedAsTooShort()
    {
        var file = Read("A=\"b\r\nALPHA=\"one\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one" }, Pairs(file));
        var rejected = file.Rejected.Single();
        Assert.AreEqual(1, rejected.Line);
        StringAssert.Contains(rejected.Reason, "too short");
    }

    [TestMethod]
    public void ALineOfFiveCharactersIsLongEnough()
    {
        var file = Read("A=\"b\"\r\n");

        CollectionAssert.AreEqual(new[] { "A|b" }, Pairs(file));
    }

    [TestMethod]
    public void ABlankLineIsRejectedAndTheLineNumbersCountIt()
    {
        var file = Read("ALPHA=\"one\"\r\n\r\nBETA=\"two\" \r\nno equals sign\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one", "BETA|two" }, Pairs(file));
        CollectionAssert.AreEqual(new[] { 2, 4 }, file.Rejected.Select(r => r.Line).ToArray());
    }

    [TestMethod]
    public void ALineWithoutAnEqualsSignIsRejected()
    {
        var rejected = Read("ALPHA \"one\"\r\n").Rejected.Single();

        StringAssert.Contains(rejected.Reason, "'='");
    }

    [TestMethod]
    public void ALineWithoutAQuoteAfterTheEqualsSignIsRejected()
    {
        var rejected = Read("ALPHA=one\"\r\n").Rejected.Single();

        StringAssert.Contains(rejected.Reason, "opening quote");
    }

    [TestMethod]
    public void ALineWithoutAClosingQuoteIsRejected()
    {
        var rejected = Read("ALPHA=\"one\r\n").Rejected.Single();

        StringAssert.Contains(rejected.Reason, "closing quote");
    }

    [TestMethod]
    public void AQuoteBeforeTheEqualsSignDoesNotOpenTheValue()
    {
        var rejected = Read("AL\"PHA=one\r\n").Rejected.Single();

        StringAssert.Contains(rejected.Reason, "opening quote");
    }

    [TestMethod]
    public void ALineOf255CharactersStopsTheProgramAndIsRejectedWhileOneOf254IsRead()
    {
        var value254 = new string('x', 254 - "K=\"\"".Length);
        var value255 = new string('x', 255 - "K=\"\"".Length);

        var file = Read($"K=\"{value254}\"\r\nL=\"{value255}\"\r\n");

        Assert.AreEqual(1, file.Entries.Count);
        var rejected = file.Rejected.Single();
        Assert.AreEqual(2, rejected.Line);
        StringAssert.Contains(rejected.Reason, "too long");
    }

    [TestMethod]
    public void AControlZEndsTheFile()
    {
        var file = Read("ALPHA=\"one\"\r\n\u001aBETA=\"two\"\r\n");

        CollectionAssert.AreEqual(new[] { "ALPHA|one" }, Pairs(file));
        Assert.IsEmpty(file.Rejected);
    }

    [TestMethod]
    public void ANulByteEndsWhatTheProgramSeesOfALine()
    {
        var file = Read("ALPHA=\"one\0two\"\r\n");

        StringAssert.Contains(file.Rejected.Single().Reason, "closing quote");
        Assert.IsEmpty(file.Entries);
    }

    [TestMethod]
    public void EachByteIsOneCharacter()
    {
        var file = TextFile.Read([(byte)'K', (byte)'=', (byte)'"', 0xE4, 0xFF, (byte)'"']);

        Assert.AreEqual("äÿ", file.Get("K"));
    }
}

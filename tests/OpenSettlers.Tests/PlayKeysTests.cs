using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class PlayKeysTests
{
    const long KeyA = 65;
    static readonly KeyMasks Masks = new(Shift: 1L << 25, Ctrl: 1L << 26, Alt: 1L << 27, Meta: 1L << 28);

    [TestMethod]
    public void ABareKeyHasNoFlags()
    {
        var problem = PlayKeys.Split("A", KeyA, Masks, out var chord);

        Assert.IsNull(problem);
        Assert.AreEqual(new KeyChord(KeyA, false, false, false, false), chord);
    }

    [TestMethod]
    public void EachModifierAloneSplitsOffItsFlagAndTheBareKeyHasNoModifierBits()
    {
        (long Mask, KeyChord Expected)[] cases =
        [
            (Masks.Shift, new KeyChord(KeyA, true, false, false, false)),
            (Masks.Ctrl, new KeyChord(KeyA, false, true, false, false)),
            (Masks.Alt, new KeyChord(KeyA, false, false, true, false)),
            (Masks.Meta, new KeyChord(KeyA, false, false, false, true)),
        ];

        foreach (var (mask, expected) in cases)
        {
            Assert.IsNull(PlayKeys.Split("x", KeyA | mask, Masks, out var chord));
            Assert.AreEqual(expected, chord);
        }
    }

    [TestMethod]
    public void ModifiersInCombinationSplitOffEachOfTheirFlags()
    {
        var code = KeyA | Masks.Shift | Masks.Alt | Masks.Meta;

        Assert.IsNull(PlayKeys.Split("x", code, Masks, out var chord));
        Assert.AreEqual(new KeyChord(KeyA, true, false, true, true), chord);
    }

    [TestMethod]
    public void AllFourModifiersTogetherSplitOffAllFourFlags()
    {
        var code = KeyA | Masks.Shift | Masks.Ctrl | Masks.Alt | Masks.Meta;

        Assert.IsNull(PlayKeys.Split("x", code, Masks, out var chord));
        Assert.AreEqual(new KeyChord(KeyA, true, true, true, true), chord);
    }

    [TestMethod]
    public void ANameThatResolvesToNoKeyIsRefused()
    {
        var problem = PlayKeys.Split("Banana", 0, Masks, out _);

        Assert.AreEqual("'Banana' is no Godot key name", problem);
    }

    [TestMethod]
    public void ModifierBitsWithoutAKeyAreNoKey()
    {
        var problem = PlayKeys.Split("Shift", Masks.Shift, Masks, out _);

        Assert.AreEqual("'Shift' is no Godot key name", problem);
    }

    [TestMethod]
    public void ExactlyTheHeldKeysTheWindowReleasedArePressedAgain()
    {
        var held = new HeldKeys();
        held.Hold(10);
        held.Hold(20);
        held.Hold(30);
        HashSet<long> pressed = [20];

        var again = held.ToPressAgain(pressed.Contains);

        CollectionAssert.AreEquivalent(new long[] { 10, 30 }, again.ToArray());
    }

    [TestMethod]
    public void AKeyReleasedByAStepIsNotPressedAgain()
    {
        var held = new HeldKeys();
        held.Hold(10);
        held.Hold(20);
        held.Release(10);

        var again = held.ToPressAgain(_ => false);

        CollectionAssert.AreEqual(new long[] { 20 }, again.ToArray());
    }

    [TestMethod]
    public void NoKeyIsPressedAgainWhileTheWindowReportsAllHeldKeysPressed()
    {
        var held = new HeldKeys();
        held.Hold(10);

        Assert.AreEqual(0, held.ToPressAgain(_ => true).Count);
    }
}

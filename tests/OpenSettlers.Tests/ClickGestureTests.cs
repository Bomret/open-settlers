using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class ClickGestureTests
{
    [TestMethod]
    public void APressReleasedWithoutMovementIsAClickAtThePress()
    {
        var gesture = new ClickGesture();

        gesture.Press(10, 20);

        Assert.IsTrue(gesture.Release(out var x, out var y));
        Assert.AreEqual((10.0, 20.0), (x, y));
    }

    [TestMethod]
    public void AMovementWithinTheSlopStillCountsAsAClick()
    {
        var gesture = new ClickGesture();

        gesture.Press(10, 20);
        gesture.Move(10 + ClickGesture.Slop, 20 - ClickGesture.Slop);

        Assert.IsTrue(gesture.Release(out _, out _));
    }

    [TestMethod]
    public void AMovedPressIsADragAndStaysOneWhenThePointerComesBack()
    {
        var gesture = new ClickGesture();

        gesture.Press(10, 20);
        gesture.Move(10 + ClickGesture.Slop + 1, 20);
        gesture.Move(10, 20);

        Assert.IsFalse(gesture.Release(out _, out _));
    }

    [TestMethod]
    public void AReleaseWithoutAPressIsNoClick()
    {
        Assert.IsFalse(new ClickGesture().Release(out _, out _));
    }

    [TestMethod]
    public void TheNextPressStartsANewGesture()
    {
        var gesture = new ClickGesture();
        gesture.Press(0, 0);
        gesture.Move(50, 50);
        gesture.Release(out _, out _);

        gesture.Press(5, 5);

        Assert.IsTrue(gesture.Release(out _, out _));
    }
}

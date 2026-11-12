using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The map view's key mapping. The view hands it the bare key (Godot's Keycode, which holds no modifier), so Shift+Equal
// arrives as Equal and Ctrl+Minus as Minus: the Equal and Minus rows below are those cases.
[TestClass]
public sealed class MapKeysTests
{
    [TestMethod]
    public void EscapeLeavesRoadMode()
    {
        Assert.AreEqual(
            MapKeyAction.LeaveRoadMode,
            MapKeys.Action(MapKey.Escape, pressed: true, echo: false, seedFocused: false)
        );
    }

    [TestMethod]
    [DataRow(MapKey.Plus)]
    [DataRow(MapKey.Equal)]
    [DataRow(MapKey.KeypadAdd)]
    public void PlusEqualAndKeypadPlusZoomIn(MapKey key)
    {
        Assert.AreEqual(MapKeyAction.ZoomIn, MapKeys.Action(key, pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    [DataRow(MapKey.Minus)]
    [DataRow(MapKey.KeypadSubtract)]
    public void MinusAndKeypadMinusZoomOut(MapKey key)
    {
        Assert.AreEqual(MapKeyAction.ZoomOut, MapKeys.Action(key, pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    public void AnotherKeyDoesNothing()
    {
        Assert.AreEqual(
            MapKeyAction.None,
            MapKeys.Action(MapKey.Other, pressed: true, echo: false, seedFocused: false)
        );
    }

    [TestMethod]
    public void AnEchoDoesNothing()
    {
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action(MapKey.Equal, pressed: true, echo: true, seedFocused: false));
    }

    [TestMethod]
    public void AReleasedKeyDoesNothing()
    {
        Assert.AreEqual(
            MapKeyAction.None,
            MapKeys.Action(MapKey.Equal, pressed: false, echo: false, seedFocused: false)
        );
    }

    [TestMethod]
    public void AnyKeyWhileTheSeedFieldHasFocusDoesNothingEscapeIncluded()
    {
        Assert.AreEqual(
            MapKeyAction.None,
            MapKeys.Action(MapKey.Escape, pressed: true, echo: false, seedFocused: true)
        );
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action(MapKey.Equal, pressed: true, echo: false, seedFocused: true));
    }
}

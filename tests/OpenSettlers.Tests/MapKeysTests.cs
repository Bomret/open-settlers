using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;

namespace OpenSettlers.Tests;

// The map view's key mapping: the bare key decides, whatever modifiers are held.
[TestClass]
public sealed class MapKeysTests
{
    [TestMethod]
    public void EscapeLeavesRoadMode()
    {
        Assert.AreEqual(
            MapKeyAction.LeaveRoadMode,
            MapKeys.Action("Escape", pressed: true, echo: false, seedFocused: false)
        );
    }

    [TestMethod]
    [DataRow("Plus")]
    [DataRow("Equal")]
    [DataRow("KpAdd")]
    public void PlusEqualAndKeypadPlusZoomIn(string key)
    {
        Assert.AreEqual(MapKeyAction.ZoomIn, MapKeys.Action(key, pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    [DataRow("Minus")]
    [DataRow("KpSubtract")]
    public void MinusAndKeypadMinusZoomOut(string key)
    {
        Assert.AreEqual(MapKeyAction.ZoomOut, MapKeys.Action(key, pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    public void ShiftEqualAndCtrlMinusZoomBecauseOnlyTheBareKeyIsAsked()
    {
        // The map view passes the key's Keycode, which holds no modifier: Shift+Equal reaches the mapping as Equal and
        // Ctrl+Minus as Minus, so a held modifier cannot change the answer.
        Assert.AreEqual(MapKeyAction.ZoomIn, MapKeys.Action("Equal", pressed: true, echo: false, seedFocused: false));
        Assert.AreEqual(MapKeyAction.ZoomOut, MapKeys.Action("Minus", pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    public void AnotherKeyDoesNothing()
    {
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action("A", pressed: true, echo: false, seedFocused: false));
    }

    [TestMethod]
    public void AnEchoDoesNothing()
    {
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action("Equal", pressed: true, echo: true, seedFocused: false));
    }

    [TestMethod]
    public void AReleasedKeyDoesNothing()
    {
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action("Equal", pressed: false, echo: false, seedFocused: false));
    }

    [TestMethod]
    public void AnyKeyWhileTheSeedFieldHasFocusDoesNothingEscapeIncluded()
    {
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action("Escape", pressed: true, echo: false, seedFocused: true));
        Assert.AreEqual(MapKeyAction.None, MapKeys.Action("Equal", pressed: true, echo: false, seedFocused: true));
    }
}

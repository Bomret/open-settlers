using System;

namespace OpenSettlers.MapView;

/// <summary>
/// Tells a click from a drag. A press that is released without having moved is a click; a press that moved is a
/// drag (a pan) and places nothing. A hand that rests on the button moves a pixel or two, so a motion within
/// <see cref="Slop"/> screen pixels of the press still counts as no movement, and a press that has once gone past
/// it stays a drag.
/// </summary>
public sealed class ClickGesture
{
    /// <summary>The distance in screen pixels from the press that a click may stray.</summary>
    public const double Slop = 4;

    private (double X, double Y) _press;
    private bool _pressed;
    private bool _moved;

    /// <summary>Starts a gesture with a press at a position in screen pixels.</summary>
    public void Press(double x, double y)
    {
        _press = (x, y);
        _pressed = true;
        _moved = false;
    }

    /// <summary>Notes that the pointer is now at a position in screen pixels.</summary>
    public void Move(double x, double y)
    {
        if (_pressed && Math.Max(Math.Abs(x - _press.X), Math.Abs(y - _press.Y)) > Slop)
        {
            _moved = true;
        }
    }

    /// <summary>Ends the gesture; true when it was a click, with the position where it was pressed.</summary>
    public bool Release(out double x, out double y)
    {
        var click = _pressed && !_moved;
        (x, y) = _press;
        _pressed = false;

        return click;
    }
}

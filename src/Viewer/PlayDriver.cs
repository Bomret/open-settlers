using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using OpenSettlers.MapView;

namespace OpenSettlers.Viewer;

/// <summary>
/// Feeds each frame's time to a <see cref="PlayRun"/> of the map view or the game view and quits the tree when it ends: exit code 0
/// for a run that finished its last step, 1 after printing each problem otherwise. Under Movie Maker every frame's
/// delta is the fixed time step, so the run is on simulated time. Every input step reaches the view as a Godot input
/// event through the view's own handlers (<c>OnCanvasInput</c>, <c>_UnhandledKeyInput</c>, the polled
/// <see cref="Input"/> state), at canvas coordinates; nothing calls the camera or the zoom. The node has the lowest
/// process priority, so a frame's steps run before the view's own step reads them. The run pins the window to
/// <see cref="WindowSize"/>, so frames and <see cref="Zoom.Fit"/> do not depend on the display.
/// </summary>
public partial class PlayDriver : Node
{
    /// <summary>The window and content size of a play run.</summary>
    public static readonly Vector2I WindowSize = new(1280, 720);

    // The bits of a Godot key value that hold the key code; the bits above them hold the modifier flags.
    const long CodeMask = 0x007FFFFF;

    readonly MapViewControl _view;
    readonly PlayRun _run;
    readonly string _outputFolder;
    readonly HashSet<Key> _held = [];

    /// <summary>The driver of a run of <c>script</c> on the view; shots go to <c>outputFolder</c>.</summary>
    public PlayDriver(MapViewControl view, PlayScript script, string outputFolder)
    {
        _view = view;
        _outputFolder = outputFolder;
        _run = new PlayRun(script, view.Snapshot, outputFolder, RunInput);
        ProcessPriority = int.MinValue;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        var window = GetWindow();
        window.ContentScaleSize = WindowSize;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
        window.Size = WindowSize;
        // Keyboard steps act whatever had the focus at start, such as the seed field.
        GetViewport().GuiReleaseFocus();
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        KeepHeld();
        _run.Advance(delta);
        if (!_run.Finished)
            return;

        foreach (var problem in _run.Problems)
        {
            GD.PrintErr($"ERROR {problem}");
        }

        GetTree().Quit(_run.Problems.Count == 0 ? 0 : PlayRun.FailedExitCode);
        SetProcess(false);
    }

    string? RunInput(PlayStep step)
    {
        switch (step)
        {
            case PressStep press:
                return SendKey(press.Key, true) ?? SendKey(press.Key, false);
            case HoldStep hold:
                return SendKey(hold.Key, true);
            case ReleaseStep release:
                return SendKey(release.Key, false);
            case ScrollStep scroll:
                var wheel = scroll.Up ? MouseButton.WheelUp : MouseButton.WheelDown;
                var at = Canvas(scroll.At is { } p ? new Vector2(p.X, p.Y) : _view.Canvas.Size / 2);
                for (var i = 0; i < scroll.Count; i++)
                {
                    Send(Button(wheel, at, true));
                    Send(Button(wheel, at, false));
                }

                return null;
            case DragStep drag:
                var from = Canvas(new Vector2(drag.FromX, drag.FromY));
                var to = Canvas(new Vector2(drag.ToX, drag.ToY));
                Send(Button(MouseButton.Left, from, true));
                Send(Motion(from.Lerp(to, 0.5f), (to - from) / 2));
                Send(Motion(to, (to - from) / 2));
                Send(Button(MouseButton.Left, to, false));

                return null;
            case PinchStep pinch:
                Send(new InputEventMagnifyGesture { Factor = pinch.Factor, Position = Canvas(_view.Canvas.Size / 2) });

                return null;
            case SwipeStep swipe:
                Send(
                    new InputEventPanGesture
                    {
                        Delta = new Vector2(swipe.Dx, swipe.Dy),
                        Position = Canvas(_view.Canvas.Size / 2),
                    }
                );

                return null;
            case ClickTileStep tile:
                return ClickTile(tile);
            case ButtonStep button:
                return ClickButton(button);
            case ShotStep shot:
                return Shot(shot);
            default:
                return $"step '{step.GetType().Name}' is no input step";
        }
    }

    // A press and release on one point, which the view's click gesture reads as a click on the tile drawn there. The
    // strip above the canvas is the button bar.
    string? ClickTile(ClickTileStep tile)
    {
        if (_view.Map is not { } map)
            return $"tile {tile.Column},{tile.Row}: no map is shown";

        var view = _view.Snapshot();
        var size = _view.Canvas.Size;
        var problem = MapPicking.Aim(
            map,
            view.CameraX,
            view.CameraY,
            view.Zoom,
            size.X,
            size.Y,
            // The button bar is taken to be the only thing above the canvas, so the canvas's offset is its height.
            _view.Canvas.GlobalPosition.Y,
            tile.Column,
            tile.Row,
            out var x,
            out var y
        );
        if (problem is not null)
            return problem;

        var at = Canvas(new Vector2((float)x, (float)y));
        Send(Button(MouseButton.Left, at, true));
        Send(Button(MouseButton.Left, at, false));

        return null;
    }

    // A press and release at the middle of the button, as a mouse click; a toggle button flips.
    string? ClickButton(ButtonStep step)
    {
        var shown = _view.ShownButtons;
        if (step.Missing([.. shown.Select(b => b.Text)]) is { } problem)
            return problem;

        var at = shown.First(b => b.Text == step.Name).GetGlobalRect().GetCenter();
        Send(Button(MouseButton.Left, at, true));
        Send(Button(MouseButton.Left, at, false));

        return null;
    }

    // A canvas point as the viewport coordinates the view's handlers receive it in.
    Vector2 Canvas(Vector2 point) => _view.Canvas.GlobalPosition + point;

    void Send(InputEvent e) => GetViewport().PushInput(e, true);

    static InputEventMouseButton Button(MouseButton button, Vector2 at, bool pressed) =>
        new()
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = at,
            GlobalPosition = at,
            ButtonMask = pressed && button == MouseButton.Left ? MouseButtonMask.Left : 0,
        };

    static InputEventMouseMotion Motion(Vector2 at, Vector2 relative) =>
        new()
        {
            Position = at,
            GlobalPosition = at,
            Relative = relative,
            ButtonMask = MouseButtonMask.Left,
        };

    // A key is a Godot key name; a chord names its modifiers first (Shift+Equal). The event carries the bare key and
    // the modifier flags, as a player's key press does, and goes through the Input singleton so that polled keys
    // see it until the release.
    string? SendKey(string name, bool pressed)
    {
        var code = OS.FindKeycodeFromString(name);
        var key = (Key)((long)code & CodeMask);
        if (key == Key.None)
            return $"'{name}' is no Godot key name";

        if (pressed)
            _held.Add(key);
        else
            _held.Remove(key);
        Press(code, pressed, echo: false);

        return null;
    }

    static void Press(Key code, bool pressed, bool echo)
    {
        var key = (Key)((long)code & CodeMask);
        var modifiers = (long)code & ~CodeMask;
        Godot.Input.ParseInputEvent(
            new InputEventKey
            {
                Keycode = key,
                PhysicalKeycode = key,
                ShiftPressed = (modifiers & (long)KeyModifierMask.MaskShift) != 0,
                CtrlPressed = (modifiers & (long)KeyModifierMask.MaskCtrl) != 0,
                AltPressed = (modifiers & (long)KeyModifierMask.MaskAlt) != 0,
                MetaPressed = (modifiers & (long)KeyModifierMask.MaskMeta) != 0,
                Pressed = pressed,
                Echo = echo,
            }
        );
        // Flushed at once, so the steps of one frame reach the handlers in file order.
        Godot.Input.FlushBufferedEvents();
    }

    // The window releases every pressed key when it loses focus, and the display server decides when that happens, so
    // a held arrow key would stop the camera at a different frame in each run. Each held key the window released is
    // pressed again, as an echo, before the frame's steps.
    void KeepHeld()
    {
        foreach (var key in _held)
        {
            if (!Godot.Input.IsKeyPressed(key))
                Press(key, true, echo: true);
        }
    }

    // The viewport's last drawn frame; a step that changes the view takes effect in the frame after it, so a wait
    // comes before the shot. With a tile, the part of the frame around it (ShotCrop), enlarged by the scale with
    // nearest sampling.
    string? Shot(ShotStep shot)
    {
        var file = Path.Combine(_outputFolder, shot.Name + ".png");
        try
        {
            var image = GetViewport().GetTexture().GetImage();
            if (shot.Tile is { } tile)
            {
                if (_view.Map is not { } map)
                    return $"tile {tile.Column},{tile.Row}: no map is shown";

                var view = _view.Snapshot();
                var size = _view.Canvas.Size;
                var origin = _view.Canvas.GlobalPosition;
                var problem = ShotCrop.Box(
                    map,
                    view.CameraX,
                    view.CameraY,
                    view.Zoom,
                    size.X,
                    size.Y,
                    origin.X,
                    // The button bar is taken to be the only thing above the canvas, as for click-tile.
                    origin.Y,
                    tile.Column,
                    tile.Row,
                    tile.Radius,
                    _view.ObjectHeadroom,
                    out var box
                );
                if (problem is not null)
                    return problem;
                if (ShotCrop.ScaleProblem(box, tile.Scale) is { } tooLarge)
                    return tooLarge;

                image = image.GetRegion(new Rect2I(box.X, box.Y, box.Width, box.Height));
                if (tile.Scale > 1)
                    image.Resize(box.Width * tile.Scale, box.Height * tile.Scale, Image.Interpolation.Nearest);
            }

            Directory.CreateDirectory(_outputFolder);
            var saved = image.SavePng(file);

            return saved == Error.Ok ? null : $"cannot write '{file}': {saved}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"cannot write '{file}': {e.Message}";
        }
    }
}

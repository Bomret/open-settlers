using System;
using System.IO;
using System.Linq;
using Godot;
using OpenSettlers.Launch;
using OpenSettlers.MapView;

namespace OpenSettlers.Viewer;

/// <summary>
/// Feeds each frame's time to a <see cref="PlayRun"/> of the map view or the game view and quits the tree when it ends: exit code 0
/// for a run that finished its last step, 1 after printing each problem otherwise. Under Movie Maker every frame's
/// delta is the fixed time step, so the run is on simulated time. Every input step reaches the view as a Godot input
/// event through the view's own handlers (<c>OnCanvasInput</c>, <c>_UnhandledKeyInput</c>, the polled
/// <see cref="Input"/> state), at canvas coordinates; nothing calls the camera or the zoom. The node has the lowest
/// process priority, so a frame's steps run before the view's own step reads them. The window starts at
/// <see cref="WindowSize"/> (project.godot sets it under Movie Maker, and <see cref="Launch.LaunchArgs"/> refuses a play run whose
/// window starts at another size), and the driver pins the content size to it, so frames and <see cref="Zoom.Fit"/> do not
/// depend on the display. A minimized run
/// (<c>--minimized</c>, the hidden run of the wrapper's repro mode) minimizes the window before its first frame and keeps
/// the content size; when the window could draw on every frame, it prints <see cref="NeverHiddenLine"/> at the end.
/// <para>
/// Every frame of the run is drawn, whatever the window's visibility. Platform-layer finding (Godot 4.7.2, checked on
/// macOS only): when the display server reports that the window cannot draw (hidden, minimized, or covered), the main
/// loop skips the frame's draw (<c>main/main.cpp:5081</c>), while Movie Maker still writes a frame on every iteration
/// (<c>main/main.cpp:5151</c>) from the last drawn viewport texture, and a shot reads that same stale texture. So each
/// frame the driver schedules a zero-second, process-always <see cref="SceneTreeTimer"/>; the tree runs timers after the
/// frame's last deferred-call flush and before Movie Maker takes the frame, and its callback forces the draw
/// (<see cref="RenderingServer.ForceDraw"/> with a frame step of 0) when <see cref="DisplayServer.WindowCanDraw"/> is
/// false. A frame the window can draw is drawn by Godot alone, as without the driver. The frame step of 0 holds a
/// shader's <c>TIME</c> still on a forced draw; no shader of the viewer reads it.
/// </para>
/// </summary>
public partial class PlayDriver : Node
{
    /// <summary>The window and content size of a play run.</summary>
    public static readonly Vector2I WindowSize = new(LaunchArgs.PlayWindowWidth, LaunchArgs.PlayWindowHeight);

    /// <summary>
    /// The line a minimized run prints when its window could draw on every frame. tools/play-run.sh matches the
    /// whole line as a literal in repro mode; keep the two alike.
    /// </summary>
    public const string NeverHiddenLine = "WARN play run: the window was never hidden";

    static readonly KeyMasks Masks = new()
    {
        Shift = (long)KeyModifierMask.MaskShift,
        Ctrl = (long)KeyModifierMask.MaskCtrl,
        Alt = (long)KeyModifierMask.MaskAlt,
        Meta = (long)KeyModifierMask.MaskMeta,
        Code = (long)KeyModifierMask.CodeMask,
    };

    readonly MapViewControl _view;
    readonly PlayRun _run;
    readonly string _outputFolder;
    readonly HeldKeys _held = new();
    readonly bool _minimized;
    bool _hidden;
    ulong? _drawScheduled;

    /// <summary>
    /// The driver of a run of <c>script</c> on the view; shots go to <c>outputFolder</c>. <c>minimized</c> minimizes the
    /// window.
    /// </summary>
    public PlayDriver(MapViewControl view, PlayScript script, string outputFolder, bool minimized)
    {
        _view = view;
        _minimized = minimized;
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
        if (_minimized)
        {
            window.Mode = Window.ModeEnum.Minimized;
        }

        // Keyboard steps act whatever had the focus at start, such as the seed field.
        GetViewport().GuiReleaseFocus();
        // The frame the driver enters may draw before its first _Process.
        ScheduleDraw();
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        _hidden |= !DisplayServer.WindowCanDraw();
        // Scheduled before the run can finish, so the last frame before Quit is drawn too.
        ScheduleDraw();
        KeepHeld();
        _run.Advance(delta);
        if (!_run.Finished)
        {
            return;
        }

        if (_minimized && !_hidden)
        {
            GD.Print(NeverHiddenLine);
        }

        foreach (var problem in _run.Problems)
        {
            GD.PrintErr($"ERROR {problem}");
        }

        GetTree().Quit(_run.Problems.Count == 0 ? 0 : PlayRun.FailedExitCode);
        SetProcess(false);
    }

    // One timer per frame: it fires in the frame it was made in and is gone after it.
    void ScheduleDraw()
    {
        var frame = Engine.GetProcessFrames();
        if (_drawScheduled == frame)
        {
            return;
        }

        _drawScheduled = frame;
        GetTree().CreateTimer(0, processAlways: true).Timeout += DrawIfHidden;
    }

    static void DrawIfHidden()
    {
        if (!DisplayServer.WindowCanDraw())
        {
            RenderingServer.ForceDraw(false, 0);
        }
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
            case SeedStep seed:
                _view.EnterSeed(seed.Text);

                return null;
            case FastForwardStep fastForward:
                return _view.FastForward(fastForward);
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
        {
            return $"tile {tile.Column},{tile.Row}: no map is shown";
        }

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
        {
            return problem;
        }

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
        {
            return problem;
        }

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
        var code = (long)OS.FindKeycodeFromString(name);
        if (PlayKeys.Split(name, code, Masks, out var chord) is { } problem)
        {
            return problem;
        }

        if (pressed)
        {
            _held.Hold(chord.Key);
        }
        else
        {
            _held.Release(chord.Key);
        }

        Press(chord, pressed, echo: false);

        return null;
    }

    static void Press(KeyChord chord, bool pressed, bool echo)
    {
        var key = (Key)chord.Key;
        Godot.Input.ParseInputEvent(
            new InputEventKey
            {
                Keycode = key,
                PhysicalKeycode = key,
                ShiftPressed = chord.Shift,
                CtrlPressed = chord.Ctrl,
                AltPressed = chord.Alt,
                MetaPressed = chord.Meta,
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
        foreach (var key in _held.ToPressAgain(key => Godot.Input.IsKeyPressed((Key)key)))
        {
            Press(
                new KeyChord
                {
                    Key = key,
                    Shift = false,
                    Ctrl = false,
                    Alt = false,
                    Meta = false,
                },
                true,
                echo: true
            );
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
                {
                    return $"tile {tile.Column},{tile.Row}: no map is shown";
                }

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
                {
                    return problem;
                }

                if (ShotCrop.ScaleProblem(box, tile.Scale) is { } tooLarge)
                {
                    return tooLarge;
                }

                // Canvas units are image pixels: the play run pins the window to WindowSize, so the content scale is 1.
                image = image.GetRegion(new Rect2I(box.X, box.Y, box.Width, box.Height));
                if (tile.Scale > 1)
                {
                    image.Resize(box.Width * tile.Scale, box.Height * tile.Scale, Image.Interpolation.Nearest);
                }
            }

            Directory.CreateDirectory(_outputFolder);
            var saved = image.SavePng(file);

            return saved == Error.Ok ? null : $"cannot write '{file}': {saved}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"cannot write '{file}': {e.Message}";
        }
        finally
        {
            // Every shot step releases a fast-forward's hold, a failed one too, so the run's later steps see the game run on.
            _view.ShotTaken();
        }
    }
}

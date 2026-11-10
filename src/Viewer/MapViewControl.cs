using System;
using System.IO;
using System.Linq;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Domain;
using OpenSettlers.Launch;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Viewer;

/// <summary>
/// The map view (<c>--map</c>): a seed field and a size picker above the terrain of the generated map. The terrain,
/// the roads, the objects, and the water are drawn on the GPU (ADR 0007) through a graphics source (ADR 0014): the
/// shared builders give a draw term for each triangle, strip, object, building, good, and ripple, the source answers
/// each with an RGBA image, and the images are layers of an array texture that <c>Objects.gdshader</c> (or
/// <c>Water.gdshader</c> for the ripples) samples (alpha the coverage), one mesh per band of rows. The objects are
/// quads drawn over the terrain. The arrow keys and a mouse drag move the <see cref="Camera"/>, which
/// wraps at the map's edges; the copies of <see cref="Camera.Copies"/> tile the plane around it. The mouse wheel,
/// and the plus and minus keys, and a touchpad pinch change the <see cref="Zoom"/>; the wheel and the pinch keep
/// the map point under the pointer in place. A two-finger touchpad swipe moves the camera.
/// </summary>
public partial class MapViewControl : Control
{
    // Rows of tiles in one mesh; 16 rows of the widest map are about 8,200 vertices.
    const int BandRows = 16;
    const float KeySpeed = 700f;

    // Screen pixels per unit of a touchpad swipe's delta.
    const float PanGestureSpeed = 10f;

    // The layer limit the view assumes of a GPU array texture: the 2048 layers Metal allows and desktop Vulkan devices
    // report. Nothing queries the device (the layers Vulkan guarantees are 256).
    const int MaxArrayLayers = 2048;

    readonly Install _install;
    readonly bool _gameView;
    readonly GraphicsSourceName _graphics;
    Game? _game;
    readonly TickPacer _pacer = new();

    // The frame counter of the animated objects (the game state's +0x206): the game's clock in the game view; in the map
    // view, a clock of its own that runs at the tick rate. The object meshes are rebuilt when it changes.
    readonly TickPacer _framePacer = new();
    ushort _frameCounter;
    ushort _framesDrawn;
    readonly System.Collections.Generic.List<System.Collections.Generic.List<MeshInstance2D>> _objectNodes = [];
    Label _ticks = null!;
    Label _logs = null!;
    Button _flagButton = null!;
    Button _roadButton = null!;
    Button _woodcutterButton = null!;
    Button _removeButton = null!;

    // The placement of the walking settlers (game view only), the images the source drew for them so far, and the material
    // over them.
    SettlerFigures? _settlerFigures;
    readonly ImageLayers _settlerLayers = new();
    ShaderMaterial? _settlerMaterial;
    int _settlerImages;
    readonly System.Collections.Generic.List<MeshInstance2D> _settlerMeshes = [];
    int _flagsDrawn;
    ObjectLayerWatch _objectWatch = new();
    int _buildingsDrawn;
    int _revisionDrawn;
    int _roadsDrawn;
    RoadDrawing? _drawing;
    Node2D _preview = null!;
    string? _roadsProblem;
    readonly string _openSeed;
    readonly int _openSize;
    readonly Layout? _layout;
    MapViewState _state = null!;
    Camera _camera = null!;
    readonly Zoom _zoom = new();
    LineEdit _seed = null!;
    OptionButton _size = null!;
    Label _message = null!;
    Control _canvas = null!;
    Node2D _world = null!;

    // The graphics source, and for each kind of draw the images it drew so far and the material over them: the terrain
    // and the roads, the objects, and the water ripples. The ripples keep the surface color their images show over.
    IGraphicsSource? _source;
    readonly ImageLayers _layers = new();
    ShaderMaterial? _surfaceMaterial;
    int _surfaceLayers;
    readonly ImageLayers _objectLayers = new();
    ShaderMaterial? _objectMaterial;
    int _objectImages;
    readonly ImageLayers _rippleLayers = new();
    ShaderMaterial? _waterMaterial;
    int _rippleImages;
    byte[]? _rippleSurface;
    readonly System.Collections.Generic.List<MeshInstance2D> _waterNodes = [];
    string? _graphicsProblem;
    string? _objectsProblem;
    string? _tablesProblem;
    bool _dragging;
    readonly ClickGesture _gesture = new();
    (int X, int Y, float Scale, float Width, float Height) _pressView;
    RuleTables? _tables;
    bool _castleDrawn;
    Vector2 _keyCarry;
    Vector2 _dragCarry;

    /// <summary>
    /// The view of the map of this install's graphics, opened on <c>seedText</c> and the size <c>sizeValue</c>: the
    /// seed field shows the seed and the size picker selects the size. With <paramref name="gameView"/> a
    /// <see cref="Game"/> on the shown map ticks at <see cref="Game.TicksPerSecond"/>; a new map starts a new game.
    /// <paramref name="graphics"/> is the source the view draws from; the rules come from the install under both.
    /// </summary>
    public MapViewControl(
        Install install,
        string seedText,
        int sizeValue,
        bool gameView = false,
        GraphicsSourceName graphics = GraphicsSourceName.HistoryEdition
    )
    {
        _install = install;
        _openSeed = seedText;
        _openSize = sizeValue;
        _gameView = gameView;
        _graphics = graphics;
    }

    /// <summary>
    /// The game view on the map a <paramref name="layout"/> draws: the seed field shows the layout's seed and the
    /// size picker its size, and neither can change.
    /// </summary>
    public MapViewControl(
        Install install,
        Layout layout,
        GraphicsSourceName graphics = GraphicsSourceName.HistoryEdition
    )
        : this(install, layout.Seed.ToScreen(), layout.Size.Value, true, graphics)
    {
        _layout = layout;
    }

    /// <summary>The area the map is drawn in and the mouse acts on; a play run aims its input at it.</summary>
    public Control Canvas => _canvas;

    /// <summary>The game-view buttons on show, in bar order; none in the map view. A play run's <c>button</c> step clicks one.</summary>
    public System.Collections.Generic.IReadOnlyList<Button> ShownButtons =>
        [.. new[] { _flagButton, _roadButton, _woodcutterButton, _removeButton }.Where(b => b.IsVisibleInTree())];

    /// <summary>The map shown; null when the seed has none. A play run's <c>click-tile</c> step aims at its tiles.</summary>
    public Map? Map => _state.Map;

    /// <summary>
    /// The height of the tallest object image the graphics source draws, in map pixels (<see cref="IGraphicsSource.ObjectHeadroom"/>),
    /// 0 with no source or no object graphics. A play run's cropped <c>shot</c> leaves this much room above its tiles.
    /// </summary>
    public int ObjectHeadroom => _source?.ObjectHeadroom ?? 0;

    /// <summary>What a play run observes of the view now.</summary>
    public PlayState Snapshot() =>
        new(
            _camera.X,
            _camera.Y,
            _zoom.Scale,
            _state.SeedText,
            _state.SizeValue,
            _gameView ? _game?.TickCount ?? 0 : null,
            _gameView ? _game?.CastleStock.Logs ?? 0 : null,
            !_gameView ? null
                : _game is null ? new System.Collections.Generic.Dictionary<(int X, int Y), int>()
                : ObjectDraws
                    .FlagGoodsOf(_game)
                    .ToDictionary(flag => flag.Key, flag => flag.Value.Count(good => !good.IsNone)),
            ListedThings(),
            _gameView ? _game : null
        );

    /// <summary>
    /// A play run's <c>fast-forward</c> step: ticks the game without drawing until the step's condition holds, then draws
    /// once. The game then ticks no more until the next shot (<see cref="ShotTaken"/>). Null when it holds; else the problem.
    /// </summary>
    public string? FastForward(FastForwardStep step)
    {
        var problem = step.Apply(_game, _gameView, _pacer);
        if (_game is null || !_gameView)
            return problem;

        _ticks.Text = $"Tick {_game.TickCount}";
        _logs.Text = CastleReadout.Text(_game.CastleStock.Logs);
        UpdateSettlers();
        Redraw();

        return problem;
    }

    /// <summary>A play run has taken a shot: a game held by a fast-forward runs on.</summary>
    public void ShotTaken() => _pacer.ShotTaken();

    // The map view lists map objects only; the game view lists every kind of the game.
    System.Collections.Generic.IReadOnlyList<Thing> ListedThings()
    {
        var names = _tables is null ? null : new NameTable(_tables);

        return Thing.Listed(_gameView, _game, _state.Map, names);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer();
        AddChild(column);
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bar = new HBoxContainer();
        column.AddChild(bar);
        bar.AddChild(new Label { Text = "Seed" });
        _seed = new LineEdit
        {
            Text = _openSeed,
            MaxLength = 19,
            CustomMinimumSize = new Vector2(240, 0),
        };
        _seed.TextSubmitted += text => Apply(text);
        bar.AddChild(_seed);
        bar.AddChild(new Label { Text = "Size" });
        _size = new OptionButton();
        foreach (var size in MapSize.Offered)
        {
            _size.AddItem($"{size.Value}: {size.Width} x {size.Height}", size.Value);
        }

        // A size change keeps the applied seed; text typed but not submitted with Enter stays unapplied.
        _size.ItemSelected += _ => Apply(_state.SeedText);
        _size.Select(_size.GetItemIndex(_openSize));
        bar.AddChild(_size);
        _message = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bar.AddChild(_message);
        _ticks = new Label { Visible = _gameView };
        bar.AddChild(_ticks);
        _logs = new Label { Visible = _gameView };
        bar.AddChild(_logs);
        // Stand-in for the original's flag button (0x442244): while it is pressed a click asks for a flag on the node
        // under it instead of the castle. The original's build panel is a later slice's.
        _flagButton = new Button
        {
            Text = "Flag",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        bar.AddChild(_flagButton);
        // Stand-in for the original's road button (0x440d23, action 0x18): while it is pressed the clicks draw a road,
        // from a flag a node at a time to the flag it ends on; Escape, a right click, or the button again cancels it.
        _roadButton = new Button
        {
            Text = "Road",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        _roadButton.Toggled += pressed =>
        {
            if (pressed)
            {
                _flagButton.ButtonPressed = false;
                _woodcutterButton.ButtonPressed = false;
                _removeButton.ButtonPressed = false;
            }
            else
            {
                CancelRoad();
            }
        };
        _flagButton.Toggled += pressed =>
        {
            if (pressed)
            {
                _roadButton.ButtonPressed = false;
                _woodcutterButton.ButtonPressed = false;
                _removeButton.ButtonPressed = false;
            }
        };
        bar.AddChild(_roadButton);
        // Stand-in for the original's build panel: while it is pressed a click asks for a woodcutter's hut on the node
        // under it, tied to the flag south-east of it. The hut stands finished at once (M4 builds it).
        _woodcutterButton = new Button
        {
            Text = "Woodcutter",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        _woodcutterButton.Toggled += pressed =>
        {
            if (pressed)
            {
                _flagButton.ButtonPressed = false;
                _roadButton.ButtonPressed = false;
                _removeButton.ButtonPressed = false;
            }
        };
        bar.AddChild(_woodcutterButton);
        // Stand-in for the original's remove click (0x4615cf for a flag or a building, 0x462d88 for a road): while
        // it is pressed a click on a node takes down the flag, the building, or the road the original's check lets
        // it (docs/rules/removal.md).
        _removeButton = new Button
        {
            Text = "Remove",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        _removeButton.Toggled += pressed =>
        {
            if (pressed)
            {
                _flagButton.ButtonPressed = false;
                _roadButton.ButtonPressed = false;
                _woodcutterButton.ButtonPressed = false;
            }
        };
        bar.AddChild(_removeButton);

        _canvas = new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            FocusMode = FocusModeEnum.Click,
        };
        _canvas.GuiInput += OnCanvasInput;
        column.AddChild(_canvas);
        _world = new Node2D();
        _canvas.AddChild(_world);
        _preview = new Node2D { ZAsRelative = false, ZIndex = 2 };
        _world.AddChild(_preview);

        try
        {
            _tables = ProgramTablesImporter.Import(_install.ReadAll(Install.Program));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _tablesProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        try
        {
            if (_graphics == GraphicsSourceName.OpenSettlers)
            {
                // A broken content set names its file and key here, and the view draws no map, as for a missing install file.
                _source = OpenSettlersSource.Load(
                    ProjectSettings.GlobalizePath("res://content/open-settlers"),
                    new GodotImageDecoder()
                );
            }
            else
            {
                LoadHistoryEditionSource();
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _graphicsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        if (_gameView && _tables is not null)
        {
            try
            {
                _settlerFigures = new SettlerFigures(
                    (_source as HistoryEditionSource)?.Steps ?? StepOffsets.Load(_install),
                    _tables.FrameRows
                );
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                GD.PrintErr($"map view: the settlers' sprites: {e.Message}");
            }
        }

        _state = _layout is null
            ? MapViewState.Open(_tables, _seed.Text, _openSize, _tablesProblem)
            : MapViewState.OpenLayout(_layout);
        _camera = new Camera(_state.Map?.Size ?? MapSize.Offered[0]);
        StartGame();
        Redraw();
        ShowMessage();
    }

    // The History Edition source of the install; a problem with the roads, objects, or settlers is kept for the message.
    void LoadHistoryEditionSource()
    {
        // The settlers are drawn in the game view only: the source gets the exe's tables for them there.
        var source = HistoryEditionSource.Load(_install, _gameView ? _tables : null);
        _source = source;
        if (source.RoadsProblem is not null)
        {
            _roadsProblem = source.RoadsProblem;
            GD.PrintErr($"map view: {source.RoadsProblem}");
        }

        if (source.ObjectsProblem is not null)
        {
            _objectsProblem = source.ObjectsProblem;
            GD.PrintErr($"map view: {source.ObjectsProblem}");
        }

        if (source.SettlersProblem is not null)
        {
            GD.PrintErr($"map view: the settlers' sprites: {source.SettlersProblem}");
        }
    }

    void StartGame()
    {
        if (_gameView && _state.Map is not null && _tables is not null)
        {
            // A layout whose entry the game refuses fails the load: nothing of it is shown and the run exits 4.
            var exit = LayoutLoad.Start(
                _layout,
                GameFactory.Create(_tables, _state.Map, _state.StockLevel, everyPassTurns: true),
                out _game,
                out var layoutProblem
            );
            if (exit != 0)
            {
                GD.Print(layoutProblem);
                GetTree().Quit(exit);

                return;
            }

            _drawing = new RoadDrawing(_game!);
            _castleDrawn = false;
            _flagsDrawn = 0;
            _objectWatch = new();
            _roadsDrawn = 0;
            _buildingsDrawn = 0;
            _flagButton.ButtonPressed = false;
            _roadButton.ButtonPressed = false;
            _woodcutterButton.ButtonPressed = false;
            _removeButton.ButtonPressed = false;
            _revisionDrawn = 0;
            DrawPreview();
            _pacer.Reset();
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (_game is not null)
        {
            // The frame rate does not set the tick rate: whole ticks are due, the fraction carries.
            for (var due = _pacer.Due(delta); due > 0; due--)
            {
                _game.Tick();
            }

            _ticks.Text = $"Tick {_game.TickCount}";
            _logs.Text = CastleReadout.Text(_game.CastleStock.Logs);
            UpdateSettlers();
            // Evaluated each frame, so the watch sees every change.
            var objectsChanged = _objectWatch.Changed(_game);
            if (
                objectsChanged
                || (_game.CastleStock.Castle is not null && !_castleDrawn)
                || _game.Flags.Count != _flagsDrawn
                || _game.Network.Roads.Count != _roadsDrawn
                || _game.Buildings.Count != _buildingsDrawn
                || _game.Revision != _revisionDrawn
            )
            {
                // The castle stands, a flag went up, a road was built, a hut stands, something was taken
                // down, or an object or a good on a flag changed: draw it.
                _revisionDrawn = _game.Revision;
                _castleDrawn = _game.CastleStock.Castle is not null;
                _flagsDrawn = _game.Flags.Count;
                _roadsDrawn = _game.Network.Roads.Count;
                _buildingsDrawn = _game.Buildings.Count;
                Redraw();
            }

            _frameCounter = _game.Clock.Now;
        }
        else
        {
            for (var due = _framePacer.Due(delta); due > 0; due--)
            {
                _frameCounter = (ushort)(_frameCounter + 8);
            }
        }

        if (_frameCounter != _framesDrawn)
        {
            UpdateObjects();
        }

        // The keys move the view at the same screen speed at every zoom.
        var step = (float)(KeySpeed * delta) / _zoom.Scale;
        var x = (Input.IsKeyPressed(Key.Right) ? step : 0) - (Input.IsKeyPressed(Key.Left) ? step : 0);
        var y = (Input.IsKeyPressed(Key.Down) ? step : 0) - (Input.IsKeyPressed(Key.Up) ? step : 0);
        if (_seed.HasFocus())
        {
            x = y = 0;
        }

        // The camera moves in whole pixels; the fraction carries to the next frame so a high frame rate still moves.
        _keyCarry += new Vector2(x, y);
        var dx = (int)_keyCarry.X;
        var dy = (int)_keyCarry.Y;
        _keyCarry -= new Vector2(dx, dy);
        if (dx != 0 || dy != 0)
        {
            _camera.Move(dx, dy);
        }

        _zoom.Fit(_camera, _canvas.Size.X, _canvas.Size.Y);
        _world.Scale = new Vector2(_zoom.Scale, _zoom.Scale);
        _world.Position = _canvas.Size / 2f - new Vector2(_camera.X, _camera.Y) * _zoom.Scale;
    }

    /// <inheritdoc/>
    public override void _UnhandledKeyInput(InputEvent input)
    {
        // An echo is a repeat of a held key (a play run re-presses a hold after a focus loss), not a new zoom step.
        if (input is not InputEventKey { Pressed: true, Echo: false } key || _seed.HasFocus())
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            _roadButton.ButtonPressed = false;
            CancelRoad();
            return;
        }

        var factor = key.Keycode switch
        {
            Key.Plus or Key.Equal or Key.KpAdd => Zoom.Step,
            Key.Minus or Key.KpSubtract => 1 / Zoom.Step,
            _ => 1,
        };
        if (factor != 1)
        {
            ZoomAt(factor, _canvas.Size / 2f);
        }
    }

    // Zooms by the factor and moves the camera so the map point under this canvas position stays under it.
    void ZoomAt(float factor, Vector2 at)
    {
        var fromCenter = at - _canvas.Size / 2f;
        var before = fromCenter / _zoom.Scale;
        _zoom.ByFactor(factor, _camera, _canvas.Size.X, _canvas.Size.Y);
        MoveMap(before - fromCenter / _zoom.Scale);
    }

    void OnCanvasInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                _canvas.GrabFocus();
                if (button.Pressed)
                {
                    _gesture.Press(button.Position.X, button.Position.Y);
                    _pressView = (_camera.X, _camera.Y, _zoom.Scale, _canvas.Size.X, _canvas.Size.Y);
                }
                else if (_gesture.Release(out var x, out var y))
                {
                    PlaceAt(x, y);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }:
                CancelRoad();
                break;
            case InputEventMouseButton
            {
                ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown,
                Pressed: true
            } wheel:
                ZoomAt(wheel.ButtonIndex == MouseButton.WheelUp ? Zoom.Step : 1 / Zoom.Step, wheel.Position);
                break;
            case InputEventMagnifyGesture pinch:
                ZoomAt(pinch.Factor, pinch.Position);
                break;
            case InputEventPanGesture swipe:
                Pan(swipe.Delta * PanGestureSpeed);
                break;
            case InputEventMouseMotion motion when _dragging:
                _gesture.Move(motion.Position.X, motion.Position.Y);
                Pan(-motion.Relative);
                break;
        }
    }

    // A click asks the game for the castle, or with the flag button pressed a flag, on the node under it. The node is
    // picked against the view as it was drawn when the button went down (camera, zoom scale, and canvas size): a hand
    // that strays within the slop of a click still pans the view a little, and a zoom or a resize during the press
    // changes none of the three.
    void PlaceAt(double screenX, double screenY)
    {
        if (_game is null || _state.Map is null)
        {
            return;
        }

        var (x, y) = MapPicking.Pick(
            _state.Map,
            _pressView.X,
            _pressView.Y,
            _pressView.Scale,
            _pressView.Width,
            _pressView.Height,
            screenX,
            screenY,
            ObjectDraws.FlagGeometry
        );
        if (_roadButton.ButtonPressed && _drawing is not null)
        {
            // The road is drawn outside the game, a node at a time, and one command builds it where it reaches a flag.
            if (_drawing.Click(x, y) is { } build)
            {
                _game.Enqueue(build);
            }

            DrawPreview();
            return;
        }

        if (_removeButton.ButtonPressed)
        {
            // The check decides what the click takes down; a click it refuses asks for nothing.
            if (_game.Removal.CommandAt(x, y) is { } removal)
            {
                _game.Enqueue(new Command(_game.TickCount, 0, removal, x, y));
            }

            return;
        }

        var kind =
            _flagButton.ButtonPressed ? CommandKind.PlaceFlag
            : _woodcutterButton.ButtonPressed ? CommandKind.PlaceBuilding
            : CommandKind.PlaceCastle;
        _game.Enqueue(new Command(_game.TickCount, 0, kind, x, y));
    }

    // Drops the road being drawn: nothing was built, so nothing is left behind.
    void CancelRoad()
    {
        _drawing?.Cancel();
        DrawPreview();
    }

    // The road being drawn as a line over the nodes it passes, in every copy of the map: a stand-in, where the original
    // draws the road's strips into the map as it is drawn.
    void DrawPreview()
    {
        foreach (var child in _preview.GetChildren())
        {
            child.QueueFree();
        }

        if (_drawing is not { Active: true } || _state?.Map is not { } map)
        {
            return;
        }

        var points = _drawing
            .Nodes.Select(node =>
            {
                var (px, py) = Camera.NodePosition(node.X, node.Y);
                var height = map.At(node.X, node.Y).Height;

                return new Vector2(px, py - height * TerrainTriangles.HeightStep);
            })
            .ToArray();
        foreach (var (x, y) in _camera.Copies())
        {
            var line = new Line2D
            {
                Points = points,
                Width = 3,
                DefaultColor = new Color(1, 1, 1),
                Position = new Vector2(x, y),
            };
            _preview.AddChild(line);
        }
    }

    // Moves the view by a distance in screen pixels.
    void Pan(Vector2 screen) => MoveMap(screen / _zoom.Scale);

    // Moves the view by a distance in map pixels. The camera moves in whole pixels; the fraction carries to the next
    // move, so slow drags at a high zoom and the zoom's own shifts add up without drift.
    void MoveMap(Vector2 map)
    {
        _dragCarry += map;
        var (mx, my) = ((int)_dragCarry.X, (int)_dragCarry.Y);
        _dragCarry -= new Vector2(mx, my);
        _camera.Move(mx, my);
    }

    void Apply(string seedText)
    {
        var changed = _state.Apply(seedText, _size.GetItemId(_size.Selected));
        if (changed)
        {
            _camera = new Camera(_state.Map!.Size);
            StartGame();
            Redraw();
        }

        ShowMessage();
    }

    void ShowMessage()
    {
        _message.Text = _graphicsProblem is not null
            ? $"Cannot draw the map: {_graphicsProblem}"
            : _state.Problem
                ?? (
                    _objectsProblem is not null ? $"Cannot draw the objects: {_objectsProblem}"
                    : _roadsProblem is not null ? $"Cannot draw the roads: {_roadsProblem}"
                    : ""
                );
        _message.Modulate = new Color(1, 0.4f, 0.4f);
    }

    void Redraw()
    {
        foreach (var child in _world.GetChildren())
        {
            if (child != _preview)
            {
                child.QueueFree();
            }
        }

        if (_state.Map is null || _source is null)
        {
            return;
        }

        var meshes = BuildMeshes(_source, TerrainTriangles.Of(_state.Map), _state.Map.Size);
        var roadMesh = BuildRoadMesh(_source, _state.Map);
        var objectMeshes = BuildObjectMeshes(_source, _state.Map);
        var waterMesh = BuildWaterMesh(_source, _state.Map);
        if (SurfaceMaterial() is not { } surface || ObjectMaterial() is not { } objectMaterial)
        {
            return;
        }

        var waterMaterial = waterMesh is null ? null : WaterMaterial();
        if (waterMesh is not null && waterMaterial is null)
        {
            return;
        }

        _framesDrawn = _frameCounter;
        _objectNodes.Clear();
        _waterNodes.Clear();
        _settlerMeshes.Clear();
        foreach (var (x, y) in _camera.Copies())
        {
            var copy = new Node2D { Position = new Vector2(x, y) };
            foreach (var mesh in meshes)
            {
                copy.AddChild(new MeshInstance2D { Mesh = mesh, Material = surface });
            }

            // The roads are strips over the terrain of the whole map and under the objects.
            if (roadMesh is not null)
            {
                copy.AddChild(new MeshInstance2D { Mesh = roadMesh, Material = surface });
            }

            // The water ripples paint over the terrain and the roads of their copy where the surface color shows, so the
            // screen is copied after this copy's terrain and roads are drawn. A source that draws no ripple makes no copy.
            if (waterMaterial is not null)
            {
                copy.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
                var water = new MeshInstance2D { Mesh = waterMesh, Material = waterMaterial };
                copy.AddChild(water);
                _waterNodes.Add(water);
            }

            // Objects paint over the terrain of every copy: an absolute z index above the terrain's 0.
            var objectNodes = new System.Collections.Generic.List<MeshInstance2D>();
            foreach (var mesh in objectMeshes)
            {
                var node = new MeshInstance2D
                {
                    Mesh = mesh,
                    Material = objectMaterial,
                    ZAsRelative = false,
                    ZIndex = 1,
                };
                copy.AddChild(node);
                objectNodes.Add(node);
            }

            _objectNodes.Add(objectNodes);

            // The settlers walk over the objects of every wrap copy; their mesh and material are set after each tick.
            if (_settlerFigures is not null)
            {
                var settlers = new MeshInstance2D { ZAsRelative = false, ZIndex = 2 };
                copy.AddChild(settlers);
                _settlerMeshes.Add(settlers);
            }

            _world.AddChild(copy);
        }

        UpdateSettlers();
        DrawPreview();
    }

    // The objects at the current frame counter, in the nodes Redraw made: an animated object shows its next frame.
    void UpdateObjects()
    {
        _framesDrawn = _frameCounter;
        if (_state.Map is null || _source is null || _objectNodes.Count == 0)
        {
            return;
        }

        var waterMesh = BuildWaterMesh(_source, _state.Map);
        if ((waterMesh is not null) != (_waterNodes.Count > 0))
        {
            // The source's ripples came or went with the animation step: the copies of the screen change with them.
            Redraw();
            return;
        }

        var meshes = BuildObjectMeshes(_source, _state.Map);
        // The step may have drawn images the array does not hold yet: the material takes them in then.
        if (ObjectMaterial() is not { } objectMaterial || (waterMesh is not null && WaterMaterial() is null))
        {
            return;
        }

        foreach (var node in _waterNodes)
        {
            node.Mesh = waterMesh;
            node.Material = _waterMaterial;
        }

        foreach (var nodes in _objectNodes)
        {
            if (nodes.Count != meshes.Count)
            {
                Redraw();
                return;
            }

            for (var band = 0; band < meshes.Count; band++)
            {
                nodes[band].Mesh = meshes[band];
                nodes[band].Material = objectMaterial;
            }
        }
    }

    // The walking settlers as one mesh of quads, the same in every wrap copy: the frame and the animation step come from
    // the game's settlers and clock, not from the frame time.
    void UpdateSettlers()
    {
        if (
            _game is null
            || _settlerFigures is null
            || _source is null
            || _state.Map is null
            || _settlerMeshes.Count == 0
        )
        {
            return;
        }

        // A term the source has no image for draws nothing: the render loop neither throws nor warns.
        var quads = ObjectQuads.Flat(
            _settlerFigures.Draws(_state.Map, _game.Settlers, _game.Clock.Now),
            _source,
            _settlerLayers
        );
        var material = quads.Count > 0 ? SettlerMaterial() : null;
        if (quads.Count > 0 && material is null)
        {
            return;
        }

        var mesh =
            quads.Count > 0
                ? BuildQuadMesh(quads.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer))))
                : null;
        foreach (var node in _settlerMeshes)
        {
            node.Mesh = mesh;
            node.Material = material;
        }
    }

    // The material of the settlers' images.
    ShaderMaterial? SettlerMaterial() =>
        LayerMaterial(_settlerLayers, "Objects", "the settlers", ref _settlerMaterial, ref _settlerImages);

    // One mesh of every road strip of the map, or null where there is none or the source draws none.
    ArrayMesh? BuildRoadMesh(IGraphicsSource source, Map map)
    {
        var quads = TerrainQuads.Strips(RoadDraws.Of(map), source, _layers);

        return quads.Count == 0
            ? null
            : BuildQuadMesh(quads.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer))));
    }

    // The material of the terrain and the roads.
    ShaderMaterial? SurfaceMaterial() =>
        LayerMaterial(_layers, "Objects", "the terrain and the roads", ref _surfaceMaterial, ref _surfaceLayers);

    // The material of the objects.
    ShaderMaterial? ObjectMaterial() =>
        LayerMaterial(_objectLayers, "Objects", "the objects", ref _objectMaterial, ref _objectImages);

    // The material of the water ripples: the ripples' images, and the surface color they show over.
    ShaderMaterial? WaterMaterial()
    {
        var material = LayerMaterial(_rippleLayers, "Water", "the water", ref _waterMaterial, ref _rippleImages);
        if (material is not null && _rippleSurface is { } surface)
        {
            material.SetShaderParameter(
                "surface",
                new Color(surface[0] / 255f, surface[1] / 255f, surface[2] / 255f, surface[3] / 255f)
            );
        }

        return material;
    }

    // A material of <paramref name="shader"/> over every image in <paramref name="layers"/>, one array texture layer
    // each. An image a draw added goes into its layer; the texture is built again only where the shape of
    // <paramref name="layers"/> grew. <paramref name="built"/> counts the images uploaded. Null, with the problem shown,
    // where the images pass the GPU's layer limit.
    ShaderMaterial? LayerMaterial(
        ImageLayers layers,
        string shader,
        string what,
        ref ShaderMaterial? material,
        ref int built
    )
    {
        var images = layers.Images;
        if (images.Count > MaxArrayLayers)
        {
            _graphicsProblem = $"{what} need {images.Count} images, more than the {MaxArrayLayers} layers of the GPU";
            GD.PrintErr($"map view: {_graphicsProblem}");
            ShowMessage();

            return null;
        }

        var (count, width, height) = layers.Shape;
        var sprites = material?.GetShaderParameter("sprites").As<Texture2DArray>();
        if (
            material is null
            || sprites is null
            || sprites.GetLayers() != count
            || sprites.GetWidth() != width
            || sprites.GetHeight() != height
        )
        {
            // The layers past the images are blank: an empty map's one layer gives the shader a texture to bind.
            var textures = new Godot.Collections.Array<Image>();
            for (var layer = 0; layer < count; layer++)
            {
                textures.Add(LayerImage(layer < images.Count ? images[layer] : ObjectImage.Empty, width, height));
            }

            sprites = new Texture2DArray();
            sprites.CreateFromImages(textures);
            material ??= new ShaderMaterial { Shader = GD.Load<Shader>($"res://src/Viewer/{shader}.gdshader") };
            material.SetShaderParameter("sprites", sprites);
        }
        else
        {
            for (var layer = built; layer < images.Count; layer++)
            {
                sprites.UpdateLayer(LayerImage(images[layer], width, height), layer);
            }
        }

        built = images.Count;

        return material;
    }

    // One array texture layer of <paramref name="image"/>, at the top left of a width x height layer.
    static Image LayerImage(ObjectImage image, int width, int height) =>
        Image.CreateFromData(width, height, false, Image.Format.Rgba8, image.Padded(width, height).Rgba);

    // The vertex color that carries an image's array texture layer to Objects.gdshader and Water.gdshader.
    static Color LayerColor(int layer)
    {
        var (red, green) = SpriteLayer.Channels(layer);

        return new Color(red, green, 0, 1);
    }

    // The corners of a quad in the order its two triangles use them.
    static readonly (int X, int Y)[] Corners = [(0, 0), (1, 0), (1, 1), (0, 1)];

    // One mesh of quads: each quad is a rectangle at its position, its color carrying the array texture layers.
    static ArrayMesh BuildQuadMesh(
        System.Collections.Generic.IEnumerable<(int X, int Y, int Width, int Height, Color Color)> quads
    )
    {
        var vertices = new System.Collections.Generic.List<Vector2>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var colors = new System.Collections.Generic.List<Color>();
        var indices = new System.Collections.Generic.List<int>();
        foreach (var quad in quads)
        {
            var origin = new Vector2(quad.X, quad.Y);
            var first = vertices.Count;
            foreach (var (cx, cy) in Corners)
            {
                var corner = new Vector2(cx * quad.Width, cy * quad.Height);
                vertices.Add(origin + corner);
                uvs.Add(corner);
                colors.Add(quad.Color);
            }

            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return mesh;
    }

    // One mesh of the water ripples of the whole map at the frame counter, the layer in COLOR; null where the source
    // draws none.
    ArrayMesh? BuildWaterMesh(IGraphicsSource source, Map map)
    {
        var (quads, surface) = ObjectQuads.Ripples(ObjectDraws.WaterOf(map, _frameCounter), source, _rippleLayers);
        _rippleSurface = surface ?? _rippleSurface;

        return quads.Count > 0
            ? BuildQuadMesh(quads.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer))))
            : null;
    }

    // One mesh per band of rows, one quad per object image and shadow: the layer in COLOR.
    System.Collections.Generic.List<ArrayMesh> BuildObjectMeshes(IGraphicsSource source, Map map)
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        foreach (
            var band in ObjectQuads.Bands(
                ObjectDraws.Of(
                    map,
                    _game?.CastleStock.Castle,
                    _game?.Buildings,
                    _game is null ? null : ObjectDraws.FlagGoodsOf(_game),
                    _frameCounter
                ),
                source,
                _objectLayers,
                map.Size.Height,
                BandRows
            )
        )
        {
            meshes.Add(BuildQuadMesh(band.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer)))));
        }

        return meshes;
    }

    // One mesh per band of rows, so the painting order of the rows holds across bands (each band is added after the
    // one above it) and no mesh grows with the map. A triangle is four vertices: the corners of its image.
    System.Collections.Generic.List<ArrayMesh> BuildMeshes(
        IGraphicsSource source,
        System.Collections.Generic.List<TerrainTriangle> triangles,
        MapSize size
    )
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        foreach (var band in TerrainQuads.Bands(triangles, source, _layers, size.Height, BandRows))
        {
            meshes.Add(BuildQuadMesh(band.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer)))));
        }

        return meshes;
    }
}

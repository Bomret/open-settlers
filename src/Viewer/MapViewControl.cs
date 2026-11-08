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
/// The map view (<c>--map</c>): a seed field and a size picker above the terrain of the generated map. The terrain
/// is drawn on the GPU (ADR 0007): one triangle mesh per band of rows, the fill masks and textures as array
/// textures of palette indices, and <c>Terrain.gdshader</c> looking each index up in a palette texture. The
/// generated objects are quads of sprite layers (<c>Objects.gdshader</c>: the indices and the coverage in their own
/// array textures) drawn over the terrain. The arrow keys and a mouse drag move the <see cref="Camera"/>, which
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

    // The layer limit of a GPU array texture.
    const int MaxArrayLayers = 256;

    readonly Install _install;
    readonly bool _gameView;
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
    SettlerGraphics? _settlerGraphics;
    ShaderMaterial? _settlerMaterial;
    readonly System.Collections.Generic.List<MeshInstance2D> _settlerMeshes = [];

    // The settler sprites (Entry, Colour) already warned about as having no loaded layer.
    readonly System.Collections.Generic.HashSet<(int Entry, int Colour)> _warnedSettlerSprites = [];
    int _flagsDrawn;
    ObjectLayerWatch _objectWatch = new();
    int _buildingsDrawn;
    int _revisionDrawn;
    int _roadsDrawn;
    RoadDrawing? _drawing;
    Node2D _preview = null!;
    RoadGraphics? _roadGraphics;
    ShaderMaterial? _roadMaterial;
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
    ShaderMaterial? _material;
    ShaderMaterial? _objectMaterial;
    ShaderMaterial? _waterMaterial;
    readonly System.Collections.Generic.List<MeshInstance2D> _waterNodes = [];
    ObjectGraphics? _objects;
    System.Collections.Generic.IReadOnlyList<MaskImage> _masks = [];
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
    /// </summary>
    public MapViewControl(Install install, string seedText, int sizeValue, bool gameView = false)
    {
        _install = install;
        _openSeed = seedText;
        _openSize = sizeValue;
        _gameView = gameView;
    }

    /// <summary>
    /// The game view on the map a <paramref name="layout"/> draws: the seed field shows the layout's seed and the
    /// size picker its size, and neither can change.
    /// </summary>
    public MapViewControl(Install install, Layout layout)
        : this(install, layout.Seed.ToScreen(), layout.Size.Value, true)
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
    /// The height of the tallest object sprite in map pixels (<see cref="ObjectGraphics.LayerHeight"/>), 0 with no
    /// object graphics loaded. A play run's cropped <c>shot</c> leaves this much room above its tiles.
    /// </summary>
    public int ObjectHeadroom => _objects?.LayerHeight ?? 0;

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
                    .ToDictionary(flag => flag.Key, flag => flag.Value.Count(good => good != 0)),
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
            var graphics = TerrainGraphics.Load(_install);
            _material = BuildMaterial(graphics);
            _masks = graphics.Masks;
            try
            {
                _roadGraphics = RoadGraphics.Load(_install);
                _roadMaterial = BuildRoadMaterial(_roadGraphics, graphics.PaletteRgba);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _roadGraphics = null;
                _roadsProblem = e.Message;
                GD.PrintErr($"map view: {e.Message}");
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _graphicsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        try
        {
            _objects = ObjectGraphics.Load(_install);
            _objectMaterial = BuildObjectMaterial(_objects);
            _waterMaterial = (ShaderMaterial)_objectMaterial.Duplicate();
            _waterMaterial.Shader = GD.Load<Shader>("res://src/Viewer/Water.gdshader");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _objects = null;
            _objectsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        try
        {
            _tables = ProgramTablesImporter.Import(_install.ReadAll(Install.Program));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _tablesProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        if (_gameView)
        {
            try
            {
                if (_tables is not null)
                {
                    // The sprites of the jobs whose settlers walk so far: the carrier, the woodcutter, and the settler of the type 1 hut.
                    _settlerGraphics = SettlerGraphics.Load(
                        _install,
                        _tables,
                        Job.Carrier.Code,
                        Job.Woodcutter.Code,
                        RuleTables.TypeOneHutJob
                    );
                    _settlerMaterial = BuildObjectMaterial(
                        new ObjectGraphics
                        {
                            PaletteRgba = _settlerGraphics.PaletteRgba,
                            LayerWidth = _settlerGraphics.LayerWidth,
                            LayerHeight = _settlerGraphics.LayerHeight,
                            Images = _settlerGraphics.Images,
                        }
                    );
                }
            }
            catch (Exception e)
                when (e
                        is IOException
                            or InvalidDataException
                            or UnauthorizedAccessException
                            or InvalidOperationException
                )
            {
                _settlerGraphics = null;
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

            _drawing = new RoadDrawing(_game);
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
                || (_game.Castle is not null && !_castleDrawn)
                || _game.Flags.Count != _flagsDrawn
                || _game.Roads.Count != _roadsDrawn
                || _game.Buildings.Count != _buildingsDrawn
                || _game.Revision != _revisionDrawn
            )
            {
                // The castle stands, a flag went up, a road was built, a hut stands, something was taken
                // down, or an object or a good on a flag changed: draw it.
                _revisionDrawn = _game.Revision;
                _castleDrawn = _game.Castle is not null;
                _flagsDrawn = _game.Flags.Count;
                _roadsDrawn = _game.Roads.Count;
                _buildingsDrawn = _game.Buildings.Count;
                Redraw();
            }

            _frameCounter = _game.Clock;
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
            _objects?.Images[ObjectGraphics.Layer(ObjectSprites.FlagEntry)]
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

        if (_state.Map is null || _material is null)
        {
            return;
        }

        var meshes = BuildMeshes(TerrainTriangles.Of(_state.Map), _state.Map.Size);
        var roadMesh = BuildRoadMesh(_state.Map);
        var objectMeshes = BuildObjectMeshes(_state.Map);
        var waterMesh = BuildWaterMesh(_state.Map);
        _framesDrawn = _frameCounter;
        _objectNodes.Clear();
        _waterNodes.Clear();
        _settlerMeshes.Clear();
        foreach (var (x, y) in _camera.Copies())
        {
            var copy = new Node2D { Position = new Vector2(x, y) };
            foreach (var mesh in meshes)
            {
                copy.AddChild(new MeshInstance2D { Mesh = mesh, Material = _material });
            }

            // The roads are strips over the terrain of the whole map and under the objects.
            if (roadMesh is not null)
            {
                copy.AddChild(new MeshInstance2D { Mesh = roadMesh, Material = _roadMaterial });
            }

            // The water sprites paint over the terrain and the roads of their copy where it shows palette index 8, so the
            // screen is copied after this copy's terrain and roads are drawn.
            if (_waterMaterial is not null)
            {
                copy.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
                var water = new MeshInstance2D { Mesh = waterMesh, Material = _waterMaterial };
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
                    Material = _objectMaterial,
                    ZAsRelative = false,
                    ZIndex = 1,
                };
                copy.AddChild(node);
                objectNodes.Add(node);
            }

            _objectNodes.Add(objectNodes);

            // The settlers walk over the objects of every wrap copy; their mesh is rebuilt after each tick.
            if (_settlerMaterial is not null)
            {
                var settlers = new MeshInstance2D
                {
                    Material = _settlerMaterial,
                    ZAsRelative = false,
                    ZIndex = 2,
                };
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
        if (_state.Map is null || _objectNodes.Count == 0)
        {
            return;
        }

        var waterMesh = BuildWaterMesh(_state.Map);
        foreach (var node in _waterNodes)
        {
            node.Mesh = waterMesh;
        }

        var meshes = BuildObjectMeshes(_state.Map);
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
            }
        }
    }

    // The walking settlers as one mesh of quads, the same in every wrap copy: the frame and the animation step come from
    // the game's settlers and clock, not from the frame time.
    void UpdateSettlers()
    {
        if (_game is null || _settlerGraphics is null || _state.Map is null || _settlerMeshes.Count == 0)
        {
            return;
        }

        var quads = new System.Collections.Generic.List<(int X, int Y, int Width, int Height, Color Color)>();
        foreach (
            var draw in _settlerGraphics.Figures.Draws(_state.Map, _game.Settlers, _game.Clock, _settlerGraphics.Shifts)
        )
        {
            // A sprite with no loaded layer is skipped and reported once per sprite: the render loop must not throw or
            // warn every frame. The tests assert that every draw of the recorded scenarios has a layer (LayerOf).
            if (!_settlerGraphics.TryLayerOf(draw, out var layer))
            {
                if (_warnedSettlerSprites.Add((draw.Entry, draw.Colour)))
                    GD.PushWarning($"No layer holds the settler sprite {draw.Entry}/{draw.Colour}.");
                continue;
            }

            var image = _settlerGraphics.Images[layer];
            if (image.Width > 0 && image.Height > 0)
            {
                quads.Add(
                    (draw.X + image.OffsetX, draw.Y + image.OffsetY, image.Width, image.Height, LayerColor(layer))
                );
            }
        }

        var mesh = quads.Count > 0 ? BuildQuadMesh(quads) : null;
        foreach (var node in _settlerMeshes)
        {
            node.Mesh = mesh;
        }
    }

    // One mesh of every road strip of the map, or null where there is none or the road graphics did not load.
    ArrayMesh? BuildRoadMesh(Map map)
    {
        if (_roadGraphics is null)
        {
            return null;
        }

        var quads = _roadGraphics.Quads(RoadDraws.Of(map));

        return quads.Count == 0
            ? null
            : BuildQuadMesh(
                quads.Select(q =>
                    (q.X, q.Y, q.Width, q.Height, new Color(q.MaskLayer / 255f, q.TextureLayer / 255f, 0, 1))
                )
            );
    }

    // The terrain's shader with the road masks and textures in place of the terrain's, and the terrain's palette.
    ShaderMaterial BuildRoadMaterial(RoadGraphics graphics, byte[] paletteRgba)
    {
        var masks = new Godot.Collections.Array<Image>();
        foreach (var mask in graphics.Masks)
        {
            masks.Add(
                Image.CreateFromData(
                    TerrainGraphics.LayerWidth,
                    TerrainGraphics.MaskLayerHeight,
                    false,
                    Image.Format.R8,
                    mask.Layer
                )
            );
        }

        var textures = new Godot.Collections.Array<Image>();
        foreach (var texture in graphics.Textures)
        {
            textures.Add(
                Image.CreateFromData(
                    TerrainGraphics.LayerWidth,
                    TerrainGraphics.TextureHeight,
                    false,
                    Image.Format.R8,
                    texture
                )
            );
        }

        var maskArray = new Texture2DArray();
        maskArray.CreateFromImages(masks);
        var textureArray = new Texture2DArray();
        textureArray.CreateFromImages(textures);
        var palette = ImageTexture.CreateFromImage(
            Image.CreateFromData(256, 1, false, Image.Format.Rgba8, paletteRgba)
        );
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/Viewer/Terrain.gdshader") };
        material.SetShaderParameter("masks", maskArray);
        material.SetShaderParameter("textures", textureArray);
        material.SetShaderParameter("palette", palette);

        return material;
    }

    ShaderMaterial BuildMaterial(TerrainGraphics graphics)
    {
        var masks = new Godot.Collections.Array<Image>();
        foreach (var mask in graphics.Masks)
        {
            masks.Add(
                Image.CreateFromData(
                    TerrainGraphics.LayerWidth,
                    TerrainGraphics.MaskLayerHeight,
                    false,
                    Image.Format.R8,
                    mask.Layer
                )
            );
        }

        var textures = new Godot.Collections.Array<Image>();
        foreach (var texture in graphics.Textures)
        {
            textures.Add(
                Image.CreateFromData(
                    TerrainGraphics.LayerWidth,
                    TerrainGraphics.TextureHeight,
                    false,
                    Image.Format.R8,
                    texture
                )
            );
        }

        if (masks.Count > MaxArrayLayers || textures.Count > MaxArrayLayers)
        {
            throw new InvalidDataException(
                "the terrain graphics need more array texture layers than the GPU limit of 256"
            );
        }

        var maskArray = new Texture2DArray();
        maskArray.CreateFromImages(masks);
        var textureArray = new Texture2DArray();
        textureArray.CreateFromImages(textures);
        var palette = ImageTexture.CreateFromImage(
            Image.CreateFromData(256, 1, false, Image.Format.Rgba8, graphics.PaletteRgba)
        );
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/Viewer/Terrain.gdshader") };
        material.SetShaderParameter("masks", maskArray);
        material.SetShaderParameter("textures", textureArray);
        material.SetShaderParameter("palette", palette);

        return material;
    }

    // The vertex color that carries a sprite's array texture layer to Objects.gdshader and Water.gdshader.
    static Color LayerColor(int layer)
    {
        var (red, green) = SpriteLayer.Channels(layer);

        return new Color(red, green, 0, 1);
    }

    ShaderMaterial BuildObjectMaterial(ObjectGraphics graphics)
    {
        Godot.Collections.Array<Image> Layers(System.Func<ObjectImage, byte[]> pixels)
        {
            var images = new Godot.Collections.Array<Image>();
            foreach (var image in graphics.Images)
            {
                images.Add(
                    Image.CreateFromData(
                        Math.Max(graphics.LayerWidth, 1),
                        Math.Max(graphics.LayerHeight, 1),
                        false,
                        Image.Format.R8,
                        pixels(image)
                    )
                );
            }

            return images;
        }

        var indices = new Texture2DArray();
        indices.CreateFromImages(Layers(image => image.Indices));
        var coverage = new Texture2DArray();
        coverage.CreateFromImages(Layers(image => image.Coverage));
        var palette = ImageTexture.CreateFromImage(
            Image.CreateFromData(256, 1, false, Image.Format.Rgba8, graphics.PaletteRgba)
        );
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/Viewer/Objects.gdshader") };
        material.SetShaderParameter("indices", indices);
        material.SetShaderParameter("coverage", coverage);
        material.SetShaderParameter("palette", palette);

        return material;
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

    // One mesh of the water sprites of the whole map at the frame counter, the layer in COLOR; null for none.
    ArrayMesh? BuildWaterMesh(Map map)
    {
        if (_objects is null)
        {
            return null;
        }

        var quads = ObjectQuads
            .Bands(ObjectDraws.WaterOf(map, _frameCounter), _objects.Images, map.Size.Height, map.Size.Height)
            .SelectMany(band => band)
            .Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer)))
            .ToList();

        return quads.Count > 0 ? BuildQuadMesh(quads) : null;
    }

    // One mesh per band of rows, one quad per object sprite: the layer in COLOR.
    System.Collections.Generic.List<ArrayMesh> BuildObjectMeshes(Map map)
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        if (_objects is null)
        {
            return meshes;
        }

        foreach (
            var band in ObjectQuads.Bands(
                ObjectDraws.Of(
                    map,
                    _game?.Castle,
                    _game?.Buildings,
                    _game is null ? null : ObjectDraws.FlagGoodsOf(_game),
                    _frameCounter
                ),
                _objects.Images,
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
    // one above it) and no mesh grows with the map. A triangle is four vertices: the corners of its mask.
    System.Collections.Generic.List<ArrayMesh> BuildMeshes(
        System.Collections.Generic.List<TerrainTriangle> triangles,
        MapSize size
    )
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        foreach (var band in TerrainQuads.Bands(triangles, _masks, size.Height, BandRows))
        {
            meshes.Add(
                BuildQuadMesh(
                    band.Select(q =>
                        (q.X, q.Y, q.Width, q.Height, new Color(q.MaskLayer / 255f, q.TextureLayer / 255f, 0, 1))
                    )
                )
            );
        }

        return meshes;
    }
}

using System;
using System.IO;
using System.Linq;
using Godot;
using OpenSettlers.Domain;
using OpenSettlers.Launch;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Viewer;

/// <summary>
/// The map view (<c>--map</c>): a seed field and a size picker above the terrain of the generated map. The terrain,
/// the roads, the objects, and the water are drawn on the GPU (ADR 0007) through a graphics source (ADR 0014): the
/// shared builders give a draw term for each triangle, strip, object, building, good, ripple, and (in the game view while
/// Build help is on) build marker, the source answers
/// each with an RGBA image, and the images are layers of an array texture that <c>Objects.gdshader</c> (or
/// <c>Water.gdshader</c> for the ripples) samples (alpha the coverage), one mesh per band of rows. The objects are
/// quads drawn over the terrain. The arrow keys and a mouse drag move the <see cref="Camera"/>, which
/// wraps at the map's edges; the copies of <see cref="Camera.Copies"/> tile the plane around it. The mouse wheel,
/// and the plus and minus keys, and a touchpad pinch change the <see cref="Zoom"/>; the wheel and the pinch keep
/// the map point under the pointer in place. A two-finger touchpad swipe moves the camera.
/// </summary>
/// <remarks>
/// The view of the map, opened on <c>seedText</c> and the size <c>sizeValue</c>: the
/// seed field shows the seed and the size picker selects the size. With <paramref name="gameView"/> a
/// <see cref="Game"/> on the shown map ticks at <see cref="Game.TicksPerSecond"/>; a new map starts a new game.
/// <paramref name="loadGraphics"/> gives the source the view draws from, which the host picks; <paramref name="tablesSource"/>
/// gives the rules, read from the install on the History Edition and from the set's own rule files on the basic set.
/// </remarks>
/// <param name="tablesSource">The rule tables port; reading it throws an <see cref="IOException"/> or <see cref="InvalidDataException"/> where the tables cannot be read.</param>
/// <param name="loadGraphics">Loads the graphics source the view draws from, given the rule tables the settlers are drawn by (null outside the game view); it throws an <see cref="IOException"/> or <see cref="InvalidDataException"/> where the source cannot be read.</param>
/// <param name="loadSteps">Reads the walk steps where the graphics load carries none, as on the History Edition, and only where the graphics load succeeded; it throws an <see cref="IOException"/> or <see cref="InvalidDataException"/> where the walk steps cannot be read.</param>
/// <param name="seedText">The seed the view opens on, as the seed field shows it.</param>
/// <param name="sizeValue">The map size the view opens on.</param>
/// <param name="gameView">Whether a game ticks on the shown map.</param>
/// <param name="stockLevel">The start stock level of the game's castle; null takes the level the seed draws.</param>
/// <param name="settlerRate">The settler rate of the game's settler supply; null takes the rate the seed draws.</param>
public partial class MapViewControl(
    IRuleTablesSource tablesSource,
    Func<RuleTables?, GraphicsLoad> loadGraphics,
    Func<IWalkSteps> loadSteps,
    string seedText,
    int sizeValue,
    bool gameView = false,
    int? stockLevel = null,
    int? settlerRate = null
) : Control
{
    // Rows of tiles in one mesh; 16 rows of the widest map are about 8,200 vertices.
    const int BandRows = 16;
    const float KeySpeed = 700f;

    // Screen pixels per unit of a touchpad swipe's delta.
    const float PanGestureSpeed = 10f;

    // The layer limit the view assumes of a GPU array texture: the 2048 layers Metal allows and desktop Vulkan devices
    // report. Nothing queries the device (the layers Vulkan guarantees are 256).
    const int MaxArrayLayers = 2048;

    readonly bool _gameView = gameView;
    readonly int? _stockLevel = stockLevel;
    readonly int? _settlerRate = settlerRate;
    Game? _game;
    readonly TickPacer _pacer = new();

    // The frame counter of the animated objects (the game state's +0x206): the game's clock in the game view; in the map
    // view, a clock of its own that runs at the tick rate. The object meshes are rebuilt when it changes.
    readonly TickPacer _framePacer = new();
    ushort _frameCounter;
    ushort _framesDrawn;

    // For each wrap copy, the node that holds the meshes of the objects and the settlers in painting order.
    readonly System.Collections.Generic.List<Node2D> _spriteNodes = [];
    Label _ticks = null!;
    Label _logs = null!;
    Button _flagButton = null!;
    Button _roadButton = null!;
    (Button Button, BuildingType Type)[] _buildButtons = [];
    Button _removeButton = null!;
    Button _buildHelpButton = null!;
    Button _workAreasButton = null!;

    // The placement of the walking settlers (game view only), the images the source drew for them so far, and the material
    // over them.
    SettlerFigures? _settlerFigures;
    readonly ImageLayers _settlerLayers = new();
    ShaderMaterial? _settlerMaterial;
    int _settlerImages;
    int _flagsDrawn;
    ObjectLayerWatch _objectWatch = new();
    int _buildingsDrawn;
    int _revisionDrawn;
    int _roadsDrawn;
    RoadDrawing? _drawing;
    Node2D _preview = null!;
    string? _roadsProblem;
    readonly string _openSeed = seedText;
    readonly int _openSize = sizeValue;
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

    // The walk steps the installed graphics parsed, or null under the other source or where they did not load.
    IWalkSteps? _installSteps;
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
    readonly ImageLayers _markerLayers = new();
    ShaderMaterial? _markerMaterial;
    int _markerImages;
    readonly ImageLayers _roadMarkerLayers = new();
    ShaderMaterial? _roadMarkerMaterial;
    int _roadMarkerImages;
    readonly System.Collections.Generic.List<MeshInstance2D> _waterNodes = [];
    string? _graphicsProblem;
    string? _objectsProblem;
    string? _settlersProblem;
    string? _tablesProblem;
    bool _dragging;
    readonly ClickGesture _gesture = new();
    (int X, int Y, float Scale, float Width, float Height) _pressView;
    RuleTables? _tables;
    bool _castleDrawn;
    Vector2 _keyCarry;
    Vector2 _dragCarry;

    /// <summary>
    /// The game view on the map a <paramref name="layout"/> draws: the seed field shows the layout's seed and the
    /// size picker its size, and neither can change.
    /// </summary>
    public MapViewControl(
        IRuleTablesSource tablesSource,
        Func<RuleTables?, GraphicsLoad> loadGraphics,
        Func<IWalkSteps> loadSteps,
        Layout layout
    )
        : this(tablesSource, loadGraphics, loadSteps, layout.Seed.ToScreen(), layout.Size.Value, true)
    {
        _layout = layout;
    }

    /// <summary>The area the map is drawn in and the mouse acts on; a play run aims its input at it.</summary>
    public Control Canvas => _canvas;

    /// <summary>
    /// The game-view buttons a play run's <c>button</c> step may click, in column order, top to bottom; none in the map
    /// view. The Speed button is left out, so a play run always runs at normal speed.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Button> ShownButtons =>
        [
            .. new[] { _flagButton, _roadButton }
                .Concat(_buildButtons.Select(entry => entry.Button))
                .Append(_removeButton)
                .Append(_buildHelpButton)
                .Append(_workAreasButton)
                .Where(b => b.IsVisibleInTree()),
        ];

    /// <summary>A play run's <c>seed</c> step: types the text into the seed field and submits it, as a player's Enter does.</summary>
    public void EnterSeed(string text)
    {
        _seed.Text = text;
        _seed.EmitSignal(LineEdit.SignalName.TextSubmitted, text);
    }

    /// <summary>The map shown; null when the seed has none. A play run's <c>click-tile</c> step aims at its tiles.</summary>
    public Map? Map => _state.Map;

    /// <summary>
    /// The height of the tallest object image the graphics source draws, in map pixels (<see cref="IGraphicsSource.ObjectHeadroom"/>),
    /// 0 with no source or no object graphics. A play run's cropped <c>shot</c> leaves this much room above its tiles.
    /// </summary>
    public int ObjectHeadroom => _source?.ObjectHeadroom ?? 0;

    /// <summary>What a play run observes of the view now, the things listed.</summary>
    public PlayState Snapshot() => Snapshot(true);

    /// <summary>
    /// What a play run observes of the view now; <paramref name="things"/> false leaves <see cref="PlayState.Things"/> null
    /// and lists nothing.
    /// </summary>
    PlayState Snapshot(bool things) =>
        new()
        {
            CameraX = _camera.X,
            CameraY = _camera.Y,
            Zoom = _zoom.Scale,
            Seed = _state.SeedText,
            Size = _state.SizeValue,
            Ticks = _gameView ? _game?.TickCount ?? 0 : null,
            Logs = _gameView ? _game?.CastleStock.Logs ?? 0 : null,
            FlagGoods =
                !_gameView ? null
                : _game is null ? []
                : ObjectDraws
                    .FlagGoodsOf(_game)
                    .ToDictionary(flag => flag.Key, flag => flag.Value.Count(good => !good.IsNone)),
            Things = things ? ListedThings() : null,
            Game = _gameView ? _game : null,
            Planks = _gameView ? _game?.CastleStock.Planks ?? 0 : null,
            Stones = _gameView ? _game?.CastleStock.Stones ?? 0 : null,
            Food = _gameView ? _game?.CastleStock.FoodCounts ?? new int[PlayFields.Food.Length] : null,
        };

    // The castle bar's text: the castle's logs, planks, stones, and each food good.
    static string CastleBar(Game game) =>
        CastleReadout.Text(
            game.CastleStock.Logs,
            game.CastleStock.Planks,
            game.CastleStock.Stones,
            game.CastleStock.FoodCounts
        );

    /// <summary>
    /// A play run's <c>fast-forward</c> step: ticks the game without drawing until the step's condition holds, then draws
    /// once. The game then ticks no more until the next shot (<see cref="ShotTaken"/>). Null when it holds; else the problem.
    /// </summary>
    public string? FastForward(FastForwardStep step)
    {
        var problem = step.Apply(_game, _gameView, _pacer, Snapshot);
        if (_game is null || !_gameView)
        {
            return problem;
        }

        _ticks.Text = $"Tick {_game.TickCount}";
        _logs.Text = CastleBar(_game);
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
        // The game-view buttons stand in one column right of the canvas, so the bar keeps its width for the
        // seed, the message, and the readouts.
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var buttons = new VBoxContainer { Visible = _gameView };
        // Stand-in for the original's flag button (0x442244): while it is pressed a click asks for a flag on the node
        // under it instead of the castle. The original's build panel is a later slice's.
        _flagButton = new Button
        {
            Text = "Flag",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        buttons.AddChild(_flagButton);
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
                ReleaseBuildButtons();
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
                ReleaseBuildButtons();
                _removeButton.ButtonPressed = false;
            }
        };
        buttons.AddChild(_roadButton);
        // Stand-in for the original's build panel: one toggle for each building type the placement builds. While one is pressed a click
        // asks for a building site of that type on the node under it, tied to the flag south-east of it (docs/rules/build-sites.md).
        _buildButtons =
        [
            .. new (string Text, BuildingType Type)[]
            {
                ("Woodcutter", BuildingType.Woodcutter),
                ("Forester", BuildingType.Forester),
                ("Stonecutter", BuildingType.Stonecutter),
                ("Sawmill", BuildingType.Sawmill),
                ("Fisher", BuildingType.Fisher),
                ("Farm", BuildingType.Farm),
                ("Pig farm", BuildingType.PigFarm),
                ("Butcher", BuildingType.Butcher),
                ("Mill", BuildingType.Mill),
                ("Baker", BuildingType.Baker),
            }.Select(entry =>
            {
                var button = new Button
                {
                    Text = entry.Text,
                    ToggleMode = true,
                    Visible = _gameView,
                    FocusMode = FocusModeEnum.None,
                };
                button.Toggled += pressed =>
                {
                    if (pressed)
                    {
                        _flagButton.ButtonPressed = false;
                        _roadButton.ButtonPressed = false;
                        _removeButton.ButtonPressed = false;
                        foreach (var other in _buildButtons.Where(other => other.Button != button))
                        {
                            other.Button.ButtonPressed = false;
                        }
                    }

                    // The reach markers follow the pressed build button.
                    if (_workAreasButton.ButtonPressed)
                    {
                        Redraw();
                    }
                };
                buttons.AddChild(button);

                return (button, entry.Type);
            }),
        ];
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
                ReleaseBuildButtons();
            }
        };
        buttons.AddChild(_removeButton);
        // Stand-in for the original's build-site overlay toggle (0x440d23): while it is pressed every node shows the build
        // marker of its judgment (docs/rules/build-sites.md, "The build-site overlay"). It starts off, stands apart from
        // the buttons above, and turns off when a game starts (StartGame).
        _buildHelpButton = new Button
        {
            Text = "Build help",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        _buildHelpButton.Toggled += _ => Redraw();
        buttons.AddChild(_buildHelpButton);
        // The work-area overlay (CONTEXT.md, "Work-area overlay"): while it is pressed every node in the work area of one of the
        // player's gatherer buildings or sites shows a tint mark. The original has no such toggle. It starts off and turns off
        // when a game starts (StartGame), as Build help does.
        _workAreasButton = new Button
        {
            Text = "Work areas",
            ToggleMode = true,
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        _workAreasButton.Toggled += _ => Redraw();
        buttons.AddChild(_workAreasButton);
        // Stand-in for the History Edition's game-speed switch (Speed_SetFactor, 0x405fe0): each press doubles the tick
        // rate, and 8 times goes back to normal speed (docs/rules/timing.md). The speed stays when a new game starts.
        var speedButton = new Button
        {
            Text = SpeedText(),
            Visible = _gameView,
            FocusMode = FocusModeEnum.None,
        };
        speedButton.Pressed += () =>
        {
            _pacer.NextSpeed();
            speedButton.Text = SpeedText();
        };
        buttons.AddChild(speedButton);

        _canvas = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipContents = true,
            FocusMode = FocusModeEnum.Click,
        };
        _canvas.GuiInput += OnCanvasInput;
        column.AddChild(row);
        row.AddChild(_canvas);
        row.AddChild(buttons);
        _world = new Node2D();
        _canvas.AddChild(_world);
        _preview = new Node2D { ZAsRelative = false, ZIndex = 2 };
        _world.AddChild(_preview);

        _tables = MapViewState.ReadTables(tablesSource, out _tablesProblem);
        if (_tablesProblem is not null)
        {
            GD.PrintErr($"map view: {_tablesProblem}");
        }

        try
        {
            // A broken content set or a missing install file names its file here, and the view draws no map.
            LoadSource();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _graphicsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        // No map is drawn where the graphics load failed, so the settlers are not loaded and their fault is not read twice.
        if (_gameView && _tables is not null && _graphicsProblem is null)
        {
            try
            {
                _settlerFigures = new SettlerFigures(_installSteps ?? loadSteps(), _tables.Figures);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                _settlersProblem = e.Message;
                GD.PrintErr($"map view: the settlers' sprites: {e.Message}");
            }
        }

        _state = _layout is null
            ? MapViewState.Open(_tables, _seed.Text, _openSize, _tablesProblem)
            : MapViewState.OpenLayout(_layout, _tablesProblem);
        _camera = new Camera(_state.Map?.Size ?? MapSize.Offered[0]);
        StartGame();
        Redraw();
        ShowMessage();
    }

    string SpeedText() => $"Speed {_pacer.Speed}x";

    // The graphics source the host loads; a problem with the roads, objects, or settlers is kept for the message.
    void LoadSource()
    {
        // The settlers are drawn in the game view only: the source gets the exe's tables for them there.
        var source = loadGraphics(_gameView ? _tables : null);
        _source = source.Source;
        _installSteps = source.Steps;
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
            _settlersProblem = source.SettlersProblem;
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
                GameFactory.Create(
                    _tables,
                    _state.Map,
                    _stockLevel ?? _state.StockLevel,
                    everyPassTurns: true,
                    handsOutGoods: true,
                    settlerSupply: true,
                    settlerRate: _settlerRate ?? _state.SettlerRate
                ),
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
            ReleaseBuildButtons();
            _removeButton.ButtonPressed = false;
            // The original clears the overlay's bit when it sets up a view at the start of a game (View_Setup, 0x41f86b).
            _buildHelpButton.ButtonPressed = false;
            _workAreasButton.ButtonPressed = false;
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
            // The frame rate does not set the tick rate: whole ticks are due, the fraction carries, up to the pacer's bound.
            // A tick moves the clock, so UpdateObjects below builds the settlers' new positions into the sprites.
            for (var due = _pacer.Due(delta); due > 0; due--)
            {
                _game.Tick();
            }

            _ticks.Text = $"Tick {_game.TickCount}";
            _logs.Text = CastleBar(_game);
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
        if (input is not InputEventKey key)
        {
            return;
        }

        var bare = key.Keycode switch
        {
            Key.Escape => MapKey.Escape,
            Key.Plus => MapKey.Plus,
            Key.Equal => MapKey.Equal,
            Key.KpAdd => MapKey.KeypadAdd,
            Key.Minus => MapKey.Minus,
            Key.KpSubtract => MapKey.KeypadSubtract,
            _ => MapKey.Other,
        };
        switch (MapKeys.Action(bare, key.Pressed, key.Echo, _seed.HasFocus()))
        {
            case MapKeyAction.LeaveRoadMode:
                _roadButton.ButtonPressed = false;
                CancelRoad();
                break;
            case MapKeyAction.ZoomIn:
                ZoomAt(Zoom.Step, _canvas.Size / 2f);
                break;
            case MapKeyAction.ZoomOut:
                ZoomAt(1 / Zoom.Step, _canvas.Size / 2f);
                break;
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
        // ClickDecision decides from the pressed buttons: the road is drawn outside the game, a node at a time, and one
        // command builds it where it reaches a flag.
        var mode = new ClickMode
        {
            Road = _roadButton.ButtonPressed,
            Remove = _removeButton.ButtonPressed,
            Flag = _flagButton.ButtonPressed,
            Buildings = _buildButtons.Where(entry => entry.Button.ButtonPressed).Select(entry => entry.Type).ToList(),
        };
        if (ClickDecision.Decide(mode, _game, _state.Map, _drawing, _game.Removal.CommandAt, x, y) is { } command)
        {
            _game.Enqueue(command);
        }

        if (mode.Road && _drawing is not null)
        {
            DrawPreview();
        }
    }

    // Releases every toggle of the build panel.
    void ReleaseBuildButtons()
    {
        foreach (var (button, _) in _buildButtons)
        {
            button.ButtonPressed = false;
        }
    }

    // Drops the road being drawn: nothing was built, so nothing is left behind.
    void CancelRoad()
    {
        _drawing?.Cancel();
        DrawPreview();
    }

    // The road being drawn as a line over the nodes it passes, in every copy of the map: a stand-in, where the original
    // draws the road's strips into the map as it is drawn. Over the line, the road markers of its end and the end's six
    // neighbors (docs/rules/roads.md, "The road markers"). Redraw runs this too, so a change of the game around the end
    // reaches the markers.
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

        var markerMesh = RoadMarkerMesh();
        var markerMaterial = markerMesh is null
            ? null
            : LayerMaterial(
                _roadMarkerLayers,
                "Objects",
                "the road markers",
                ref _roadMarkerMaterial,
                ref _roadMarkerImages
            );
        if (markerMaterial is null)
        {
            return;
        }

        // The road markers paint over the build markers and the road line of every copy.
        foreach (var (x, y) in _camera.Copies())
        {
            _preview.AddChild(
                new MeshInstance2D
                {
                    Mesh = markerMesh,
                    Material = markerMaterial,
                    Position = new Vector2(x, y),
                    ZAsRelative = false,
                    ZIndex = 4,
                }
            );
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
        _message.Text = ViewMessage.Of(
            graphics: _graphicsProblem,
            state: _state.Problem,
            objects: _objectsProblem,
            roads: _roadsProblem,
            settlers: _settlersProblem
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
        var waterMesh = BuildWaterMesh(_source, _state.Map);
        if (SurfaceMaterial() is not { } surface)
        {
            return;
        }

        var waterMaterial = waterMesh is null ? null : WaterMaterial();
        if (waterMesh is not null && waterMaterial is null)
        {
            return;
        }

        // The build markers are judged once for the whole map and drawn in every wrap copy.
        var markerMesh = BuildMarkerMesh(_source);
        var markerMaterial = markerMesh is null
            ? null
            : LayerMaterial(_markerLayers, "Objects", "the build markers", ref _markerMaterial, ref _markerImages);
        if (markerMesh is not null && markerMaterial is null)
        {
            return;
        }

        // The work-area tint is judged once for the whole map and drawn in every wrap copy.
        var tintMesh = WorkAreaMesh();
        var reachMesh = ReachMarkerMesh();
        _framesDrawn = _frameCounter;
        _spriteNodes.Clear();
        _waterNodes.Clear();
        foreach (var (x, y) in _camera.Copies())
        {
            var copy = new Node2D { Position = new Vector2(x, y) };
            foreach (var mesh in meshes)
            {
                copy.AddChild(new MeshInstance2D { Mesh = mesh, Material = surface });
            }

            // The roads are strips over the terrain of the whole map and under the objects; the border posts are drawn with
            // them, so the objects cover the posts too.
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

            // Objects and settlers paint over the terrain of every copy: an absolute z index above the terrain's 0. Their
            // meshes are set by UpdateSprites.
            var sprites = new Node2D { ZAsRelative = false, ZIndex = 1 };
            copy.AddChild(sprites);
            _spriteNodes.Add(sprites);

            // The work-area tint paints over the objects and the settlers of every copy, so a forest's area stays visible, and
            // under the markers.
            if (tintMesh is not null)
            {
                copy.AddChild(
                    new MeshInstance2D
                    {
                        Mesh = tintMesh,
                        ZAsRelative = false,
                        ZIndex = 2,
                    }
                );
            }

            // The markers paint over the terrain, the objects, and the settlers of every copy.
            if (markerMesh is not null)
            {
                copy.AddChild(
                    new MeshInstance2D
                    {
                        Mesh = markerMesh,
                        Material = markerMaterial,
                        ZAsRelative = false,
                        ZIndex = 3,
                    }
                );
            }

            // The reach markers paint over the build markers of every copy, so both show where Build help is on too.
            if (reachMesh is not null)
            {
                copy.AddChild(
                    new MeshInstance2D
                    {
                        Mesh = reachMesh,
                        ZAsRelative = false,
                        ZIndex = 3,
                    }
                );
            }

            _world.AddChild(copy);
        }

        UpdateSprites();
        DrawPreview();
    }

    // The objects at the current frame counter, in the nodes Redraw made: an animated object shows its next frame.
    void UpdateObjects()
    {
        _framesDrawn = _frameCounter;
        if (_state.Map is null || _source is null || _spriteNodes.Count == 0)
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

        // The step may have drawn images the array does not hold yet: the material takes them in then. Where the ripples
        // pass the layer limit they draw nothing, and the objects and the settlers still draw.
        var waterMaterial = waterMesh is null ? null : WaterMaterial();
        foreach (var node in _waterNodes)
        {
            node.Mesh = waterMaterial is null ? null : waterMesh;
            node.Material = waterMaterial;
        }

        UpdateSprites();
    }

    // The objects and the walking settlers in the nodes Redraw made, one mesh per run of ObjectQuads.Runs, the same in
    // every wrap copy: a settler paints after the objects of its row and before those of the rows below. The animation
    // steps come from the frame counter and the game's clock, not from the frame time. A node past the runs draws nothing,
    // and so does a run whose material failed.
    void UpdateSprites()
    {
        if (_state.Map is null || _source is null || _spriteNodes.Count == 0)
        {
            return;
        }

        // A term the source has no image for draws nothing: the render loop neither throws nor warns.
        var settlers =
            _game is not null && _settlerFigures is not null
                ? _settlerFigures.Draws(_state.Map, _game.Settlers, _game.Clock.Now)
                : [];
        var runs = ObjectQuads.Runs(
            ObjectDraws.Of(
                _state.Map,
                _game?.CastleStock.Castle,
                _game?.Buildings,
                _game is null ? null : ObjectDraws.FlagGoodsOf(_game),
                _frameCounter,
                // The game's current clock, never the frame counter, which lags it by one update.
                _game?.Clock.Now
                    ?? 0,
                _source.Flames
            ),
            settlers,
            _source,
            _objectLayers,
            _settlerLayers,
            _state.Map.Size.Height,
            BandRows
        );

        // The runs may have drawn images the arrays do not hold yet: the materials take them in then. A kind whose images
        // pass the layer limit has no material and draws nothing; the other kind still draws. A kind's material is asked
        // for only where it has a run.
        var objectMaterial = runs.Any(run => run.Kind == RunKind.Objects) ? ObjectMaterial() : null;
        var settlerMaterial = runs.Any(run => run.Kind == RunKind.Settlers) ? SettlerMaterial() : null;
        var meshes = runs.Select(run =>
            {
                var material = run.Kind == RunKind.Objects ? objectMaterial : settlerMaterial;
                var mesh = material is null
                    ? null
                    : BuildQuadMesh(run.Quads.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer))));

                return (Mesh: mesh, Material: material);
            })
            .ToList();

        // Every node is set again, since a run's place in the list moves with every build: a node never keeps a mesh
        // without its material.
        foreach (var sprites in _spriteNodes)
        {
            while (sprites.GetChildCount() < meshes.Count)
            {
                sprites.AddChild(new MeshInstance2D());
            }

            for (var index = 0; index < sprites.GetChildCount(); index++)
            {
                var node = sprites.GetChild<MeshInstance2D>(index);
                node.Mesh = index < meshes.Count ? meshes[index].Mesh : null;
                node.Material = index < meshes.Count ? meshes[index].Material : null;
            }
        }
    }

    // One mesh of the build markers of the view's player (player 0) while Build help is pressed; null where it is off, in
    // the map view, or where the source draws none.
    ArrayMesh? BuildMarkerMesh(IGraphicsSource source)
    {
        if (_game is null || !_buildHelpButton.ButtonPressed)
        {
            return null;
        }

        return MarkerMesh(BuildMarkerDraws.Of(_game, 0), source, _markerLayers);
    }

    // One mesh of the work-area tint of the view's player (player 0) while Work areas is pressed: a translucent diamond on each
    // marked node, plain shapes in one color. Null where it is off, in the map view, or where no node is marked.
    ArrayMesh? WorkAreaMesh()
    {
        if (_game is null || !_workAreasButton.ButtonPressed)
        {
            return null;
        }

        var draws = WorkAreaDraws.Of(_game, 0);
        if (draws.Count == 0)
        {
            return null;
        }

        const float HalfWidth = Camera.NodeSpacing / 2f;
        const float HalfHeight = Camera.RowSpacing / 2f;
        var tint = new Color(1f, 0.85f, 0.2f, 0.35f);
        var vertices = new Vector2[draws.Count * 6];
        var colors = new Color[draws.Count * 6];
        for (var index = 0; index < draws.Count; index++)
        {
            var (x, y) = (draws[index].X, draws[index].Y);
            Vector2 top = new(x, y - HalfHeight);
            Vector2 right = new(x + HalfWidth, y);
            Vector2 bottom = new(x, y + HalfHeight);
            Vector2 left = new(x - HalfWidth, y);
            var at = index * 6;
            (vertices[at], vertices[at + 1], vertices[at + 2]) = (top, right, bottom);
            (vertices[at + 3], vertices[at + 4], vertices[at + 5]) = (top, bottom, left);
            for (var corner = 0; corner < 6; corner++)
            {
                colors[at + corner] = tint;
            }
        }

        return TriangleMesh(vertices, colors);
    }

    // One mesh of the reach markers while Work areas and the build button of a gatherer are pressed: a filled green disc where the
    // worker would find a target, a red ring where he would find none. Null where no marker shows.
    ArrayMesh? ReachMarkerMesh()
    {
        if (_game is null || !_workAreasButton.ButtonPressed)
        {
            return null;
        }

        if (_buildButtons.FirstOrDefault(entry => entry.Button.ButtonPressed) is not { Button: not null } pressed)
        {
            return null;
        }

        var draws = ReachMarkerDraws.Of(_game, 0, pressed.Type);
        if (draws.Count == 0)
        {
            return null;
        }

        const int Sides = 12;
        const float Outer = 5f;
        const float Inner = 3f;
        var inReach = new Color(0.2f, 0.9f, 0.2f);
        var outOfReach = new Color(0.95f, 0.2f, 0.2f);
        var vertices = new System.Collections.Generic.List<Vector2>();
        var colors = new System.Collections.Generic.List<Color>();
        foreach (var draw in draws)
        {
            var center = new Vector2(draw.X, draw.Y);
            for (var side = 0; side < Sides; side++)
            {
                var from = Vector2.FromAngle(Mathf.Tau * side / Sides);
                var to = Vector2.FromAngle(Mathf.Tau * (side + 1) / Sides);
                if (draw.InReach)
                {
                    vertices.AddRange([center, center + from * Outer, center + to * Outer]);
                    colors.AddRange([inReach, inReach, inReach]);
                }
                else
                {
                    vertices.AddRange([center + from * Inner, center + from * Outer, center + to * Outer]);
                    vertices.AddRange([center + from * Inner, center + to * Outer, center + to * Inner]);
                    colors.AddRange([outOfReach, outOfReach, outOfReach, outOfReach, outOfReach, outOfReach]);
                }
            }
        }

        return TriangleMesh([.. vertices], [.. colors]);
    }

    // One mesh of plain triangles: each three vertices make one, each vertex in its color.
    static ArrayMesh TriangleMesh(Vector2[] vertices, Color[] colors)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return mesh;
    }

    // One mesh of the road markers of the road being drawn; null where none is drawn or the source draws none.
    ArrayMesh? RoadMarkerMesh() =>
        _game is null || _drawing is null || _source is null
            ? null
            : MarkerMesh(RoadMarkerDraws.Of(_game, _drawing), _source, _roadMarkerLayers);

    // One flat mesh of marker draws, each image in its layer; null where the source draws none of them.
    ArrayMesh? MarkerMesh(
        System.Collections.Generic.IReadOnlyList<ObjectTerm> draws,
        IGraphicsSource source,
        ImageLayers layers
    )
    {
        var quads = ObjectQuads.Flat(draws, source, layers);

        return quads.Count == 0
            ? null
            : BuildQuadMesh(quads.Select(q => (q.X, q.Y, q.Width, q.Height, LayerColor(q.Layer))));
    }

    // The material of the settlers' images.
    ShaderMaterial? SettlerMaterial() =>
        LayerMaterial(_settlerLayers, "Objects", "the settlers", ref _settlerMaterial, ref _settlerImages);

    // One mesh of every road strip and border post of the map, or null where there is none or the source draws none.
    ArrayMesh? BuildRoadMesh(IGraphicsSource source, Map map)
    {
        var quads = TerrainQuads.Edges(map, source, _layers);

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

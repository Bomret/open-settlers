using System;
using System.IO;
using System.Linq;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Viewer;

/// <summary>
/// The map view (<c>--map</c>): a seed field and a size picker above the terrain of the generated map. The terrain
/// is drawn on the GPU (ADR 0007): one triangle mesh per band of rows, the fill masks and textures as array
/// textures of palette indices, and <c>Terrain.gdshader</c> looking each index up in a palette texture. The
/// generated objects are quads of sprite layers (<c>Objects.gdshader</c>: the indices and the coverage in their own
/// array textures) drawn over the terrain. The arrow keys and a mouse drag move the <see cref="Camera"/>, which
/// wraps at the map's edges; the copies of <see cref="Camera.Copies"/> tile the plane around it. The mouse wheel, and the plus and
/// minus keys, and a touchpad pinch change the <see cref="Zoom"/>; the wheel and the pinch keep the map point under
/// the pointer in place. A two-finger touchpad swipe moves the camera.
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
    readonly bool _play;
    Game? _game;
    double _tickCarry;
    Label _ticks = null!;
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
    ObjectGraphics? _objects;
    System.Collections.Generic.IReadOnlyList<MaskImage> _masks = [];
    string? _graphicsProblem;
    string? _objectsProblem;
    bool _dragging;
    Vector2 _keyCarry;
    Vector2 _dragCarry;

    /// <summary>
    /// The view of the map of this install's graphics. With <paramref name="play"/> a <see cref="Game"/> on the
    /// shown map ticks at <see cref="Game.TicksPerSecond"/>; a new map starts a new game.
    /// </summary>
    public MapViewControl(Install install, bool play = false)
    {
        _install = install;
        _play = play;
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
            Text = MapViewState.DefaultSeed,
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
        bar.AddChild(_size);
        _message = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bar.AddChild(_message);
        _ticks = new Label { Visible = _play };
        bar.AddChild(_ticks);

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

        try
        {
            var graphics = TerrainGraphics.Load(_install);
            _material = BuildMaterial(graphics);
            _masks = graphics.Masks;
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
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _objects = null;
            _objectsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        _state = MapViewState.Open(_seed.Text, _size.GetItemId(0));
        _camera = new Camera(_state.Map?.Size ?? MapSize.Offered[0]);
        StartGame();
        Redraw();
        ShowMessage();
    }

    void StartGame()
    {
        if (_play && _state.Map is not null)
        {
            _game = new Game(_state.Map);
            _tickCarry = 0;
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (_game is not null)
        {
            // The frame rate does not set the tick rate: whole ticks are due, the fraction carries.
            _tickCarry += delta * Game.TicksPerSecond;
            for (; _tickCarry >= 1; _tickCarry--)
            {
                _game.Tick();
            }

            _ticks.Text = $"Tick {_game.TickCount}";
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
        if (input is not InputEventKey { Pressed: true } key || _seed.HasFocus())
        {
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
                Pan(-motion.Relative);
                break;
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
            : _state.Problem ?? (_objectsProblem is not null ? $"Cannot draw the objects: {_objectsProblem}" : "");
        _message.Modulate = new Color(1, 0.4f, 0.4f);
    }

    void Redraw()
    {
        foreach (var child in _world.GetChildren())
        {
            child.QueueFree();
        }

        if (_state.Map is null || _material is null)
        {
            return;
        }

        var meshes = BuildMeshes(TerrainTriangles.Of(_state.Map), _state.Map.Size);
        var objectMeshes = BuildObjectMeshes(_state.Map);
        foreach (var (x, y) in _camera.Copies())
        {
            var copy = new Node2D { Position = new Vector2(x, y) };
            foreach (var mesh in meshes)
            {
                copy.AddChild(new MeshInstance2D { Mesh = mesh, Material = _material });
            }

            // Objects paint over the terrain of every copy: an absolute z index above the terrain's 0.
            foreach (var mesh in objectMeshes)
            {
                copy.AddChild(
                    new MeshInstance2D
                    {
                        Mesh = mesh,
                        Material = _objectMaterial,
                        ZAsRelative = false,
                        ZIndex = 1,
                    }
                );
            }

            _world.AddChild(copy);
        }
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

    // One mesh per band of rows, one quad per object sprite: the layer in COLOR.
    System.Collections.Generic.List<ArrayMesh> BuildObjectMeshes(Map map)
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        if (_objects is null)
        {
            return meshes;
        }

        foreach (var band in ObjectQuads.Bands(ObjectDraws.Of(map), _objects.Images, map.Size.Height, BandRows))
        {
            meshes.Add(
                BuildQuadMesh(band.Select(q => (q.X, q.Y, q.Width, q.Height, new Color(q.Layer / 255f, 0, 0, 1))))
            );
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

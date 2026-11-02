using System;
using System.IO;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Viewer;

/// <summary>
/// The map view (<c>--map</c>): a seed field and a size picker above the terrain of the generated map. The terrain
/// is drawn on the GPU (ADR 0007): one triangle mesh per band of rows, the fill masks and textures as array
/// textures of palette indices, and <c>Terrain.gdshader</c> looking each index up in a palette texture. The
/// arrow keys and a mouse drag move the <see cref="Camera"/>, which wraps at the map's edges; nine copies of the
/// meshes tile the plane around it.
/// </summary>
public partial class MapViewControl : Control
{
    // Rows of tiles in one mesh; 16 rows of the widest map are about 8,200 vertices.
    const int BandRows = 16;
    const float KeySpeed = 700f;

    readonly Install _install;
    MapViewState _state = null!;
    Camera _camera = null!;
    LineEdit _seed = null!;
    OptionButton _size = null!;
    Label _message = null!;
    Control _canvas = null!;
    Node2D _world = null!;
    ShaderMaterial? _material;
    string? _graphicsProblem;
    bool _dragging;

    /// <summary>The view of the map of this install's graphics.</summary>
    public MapViewControl(Install install)
    {
        _install = install;
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
        _seed.TextChanged += _ => Apply();
        bar.AddChild(_seed);
        bar.AddChild(new Label { Text = "Size" });
        _size = new OptionButton();
        foreach (var size in MapSize.Offered)
        {
            _size.AddItem($"{size.Value}: {size.Width} x {size.Height}", size.Value);
        }

        _size.ItemSelected += _ => Apply();
        bar.AddChild(_size);
        _message = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bar.AddChild(_message);

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
            _material = BuildMaterial(TerrainGraphics.Load(_install));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _graphicsProblem = e.Message;
            GD.PrintErr($"map view: {e.Message}");
        }

        _state = MapViewState.Open(_seed.Text, _size.GetItemId(0));
        _camera = new Camera(_state.Map?.Size ?? MapSize.Offered[0]);
        Redraw();
        ShowMessage();
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        var step = (int)Math.Round(KeySpeed * delta);
        var dx = (Input.IsKeyPressed(Key.Right) ? step : 0) - (Input.IsKeyPressed(Key.Left) ? step : 0);
        var dy = (Input.IsKeyPressed(Key.Down) ? step : 0) - (Input.IsKeyPressed(Key.Up) ? step : 0);
        if (_seed.HasFocus())
        {
            dx = dy = 0;
        }

        if (dx != 0 || dy != 0)
        {
            _camera.Move(dx, dy);
        }

        _world.Position = new Vector2(_canvas.Size.X / 2f - _camera.X, _canvas.Size.Y / 2f - _camera.Y);
    }

    void OnCanvasInput(InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                _canvas.GrabFocus();
                break;
            case InputEventMouseMotion motion when _dragging:
                _camera.Move(-(int)Math.Round(motion.Relative.X), -(int)Math.Round(motion.Relative.Y));
                break;
        }
    }

    void Apply()
    {
        var changed = _state.Apply(_seed.Text, _size.GetItemId(_size.Selected));
        if (changed)
        {
            _camera = new Camera(_state.Map!.Size);
            Redraw();
        }

        ShowMessage();
    }

    void ShowMessage()
    {
        _message.Text = _graphicsProblem is not null
            ? $"Cannot draw the terrain: {_graphicsProblem}"
            : _state.Problem ?? "";
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
        foreach (var (x, y) in _camera.Copies())
        {
            var copy = new Node2D { Position = new Vector2(x, y) };
            foreach (var mesh in meshes)
            {
                copy.AddChild(new MeshInstance2D { Mesh = mesh, Material = _material });
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

        if (!TerrainTiling.FitsLayerLimit(masks.Count, 256) || !TerrainTiling.FitsLayerLimit(textures.Count, 256))
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
        _masks = graphics.Masks;

        return material;
    }

    System.Collections.Generic.IReadOnlyList<MaskImage> _masks = [];

    // One mesh per band of rows, so the painting order of the rows holds across bands (each band is added after the
    // one above it) and no mesh grows with the map. A triangle is four vertices: the corners of its mask.
    System.Collections.Generic.List<ArrayMesh> BuildMeshes(
        System.Collections.Generic.List<TerrainTriangle> triangles,
        MapSize size
    )
    {
        var meshes = new System.Collections.Generic.List<ArrayMesh>();
        var next = 0;
        foreach (var band in TerrainTiling.Chunks(1, size.Height, BandRows))
        {
            var vertices = new System.Collections.Generic.List<Vector2>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var colors = new System.Collections.Generic.List<Color>();
            var indices = new System.Collections.Generic.List<int>();
            while (next < triangles.Count && triangles[next].Row < band.Row + band.Height)
            {
                var triangle = triangles[next++];
                var maskLayer = TerrainGraphics.MaskLayer(triangle.Mask);
                var mask = _masks[maskLayer];
                // The mask hangs from its anchor: centered on it and moved by the header's offset.
                var origin = new Vector2(
                    triangle.X - TerrainGraphics.LayerWidth / 2 + mask.OffsetX,
                    triangle.Y + mask.OffsetY
                );
                var color = new Color(maskLayer / 255f, TerrainGraphics.TextureLayer(triangle.Texture) / 255f, 0, 1);
                var first = vertices.Count;
                foreach (var (cx, cy) in new[] { (0, 0), (1, 0), (1, 1), (0, 1) })
                {
                    var corner = new Vector2(cx * mask.Width, cy * mask.Height);
                    vertices.Add(origin + corner);
                    uvs.Add(corner);
                    colors.Add(color);
                }

                indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
            }

            if (vertices.Count == 0)
            {
                continue;
            }

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            meshes.Add(mesh);
        }

        return meshes;
    }
}

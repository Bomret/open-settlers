using System;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Launch;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Viewer;

/// <summary>
/// The project's main scene. With <c>--check</c> it runs the asset check, prints the report, and quits with its
/// exit code; otherwise it draws the asset browser: the categories, the assets of the selected kind, and the
/// preview of the selected asset.
///   godot --headless --path . -- --check --game &lt;install&gt;
///   godot --path . -- --game &lt;install&gt;
///   godot --path . -- --map --game &lt;install&gt;   (the map view, <see cref="MapViewControl"/>)
///   godot --path . -- --map --seed 1234567812345678 --size 2 --game &lt;install&gt;
/// <c>SIEDLER_DIR</c> stands in for <c>--game</c>. A bad argument prints its problem and quits with exit code 4
/// before the install is opened.
/// </summary>
public partial class AssetBrowserView : Control
{
    AssetBrowserState _state = null!;
    ItemList _kinds = null!;
    ItemList _assets = null!;
    VBoxContainer _pane = null!;
    System.Collections.Generic.IReadOnlyDictionary<System.Type, PreviewView> _views = null!;

    /// <inheritdoc/>
    public override void _Ready()
    {
        var args = LaunchArgs.Parse(
            OS.GetCmdlineUserArgs(),
            System.Environment.GetEnvironmentVariable(LaunchArgs.GameVariable)
        );
        if (args.Problems.Count > 0)
        {
            // A bad argument wins over a missing install, and no window opens for it.
            GD.PrintErr($"ERROR {string.Join("; ", args.Problems)}");
            GetTree().Quit(LaunchArgs.BadArgumentExitCode);

            return;
        }

        if (args.Mode == LaunchMode.SeedInfo)
        {
            // Seed info needs no install. A Godot callback exception does not set the exit code, so this owns it.
            var code = SeedInfo.Run(args.SeedText!, args.Seed!.Value, args.Size ?? MapSize.Offered[0], out var text);
            if (code == 0)
                GD.Print(text.TrimEnd('\n'));
            else
                GD.PrintErr($"ERROR {text}");
            GetTree().Quit(code);

            return;
        }

        var kinds = AssetKind.Discover();
        if (args.Mode == LaunchMode.Check)
        {
            RunCheck(args, kinds);

            return;
        }

        if (args.Mode == LaunchMode.Map)
        {
            RunMap(args);

            return;
        }

        _views = PreviewView.Discover();
        _state = AssetBrowserState.Open(args, kinds, selectFirst: false);
        if (_state.Categories.Count > 0 && Guard(() => _state.SelectKind(_state.Categories[0])) is { } crash)
        {
            AddChild(Wrapped(crash));

            return;
        }

        Build();
    }

    // AssetBrowserState propagates a programming error, and a Godot callback exception only reaches the log, so the
    // view catches it like RunCheck does and returns the text to show in its place.
    string? Guard(Action action)
    {
        try
        {
            action();

            return null;
        }
        catch (Exception e)
        {
            GD.PrintErr($"asset browser crashed: {e}");
            var subject = _state.SelectedAsset?.File ?? _state.SelectedKind;

            return $"{subject}: crashed: {e.GetType().Name}: {e.Message}";
        }
    }

    static Label Wrapped(string text) =>
        new()
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.Word,
            AnchorRight = 1,
            AnchorBottom = 1,
        };

    // Without an install the map view exits as the asset check does: the same message and exit code 2. A Godot
    // callback exception does not set the exit code, so this owns it.
    void RunMap(LaunchArgs args)
    {
        if (!Install.TryOpen(args, out var install, out var problem))
        {
            GD.Print($"ERROR {problem}");
            GetTree().Quit(2);

            return;
        }

        AddChild(
            new MapViewControl(
                install,
                args.SeedText ?? MapViewState.DefaultSeed,
                args.Size?.Value ?? MapSize.Offered[0].Value
            )
        );
    }

    // A Godot callback exception does not set the exit code, so the check owns it.
    void RunCheck(LaunchArgs args, System.Collections.Generic.IReadOnlyList<AssetKind> kinds)
    {
        int code;
        try
        {
            var report = AssetCheck.Run(args, kinds);
            foreach (var line in report.Lines)
            {
                GD.Print(line);
            }

            code = report.ExitCode;
        }
        catch (Exception e)
        {
            GD.PrintErr($"asset check crashed: {e}");
            code = 1;
        }

        GetTree().Quit(code);
    }

    void Build()
    {
        if (_state.Message != null)
        {
            AddChild(Wrapped(_state.Message));

            return;
        }

        var row = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
        _kinds = new ItemList { CustomMinimumSize = new Vector2(160, 0) };
        _assets = new ItemList { CustomMinimumSize = new Vector2(220, 0) };
        _pane = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_kinds);
        row.AddChild(_assets);
        row.AddChild(_pane);
        AddChild(row);
        foreach (var name in _state.Categories)
        {
            _kinds.AddItem(name);
        }

        _kinds.ItemSelected += index =>
        {
            var crash = Guard(() => _state.SelectKind(_state.Categories[(int)index]));
            ShowAssets(crash);
        };
        _assets.ItemSelected += index =>
        {
            var crash = Guard(() => _state.SelectAsset((int)index));
            ShowPreview(crash);
        };
        if (_state.Categories.Count > 0)
        {
            _kinds.Select(0);
        }

        ShowAssets();
    }

    void ShowAssets(string? crash = null)
    {
        _assets.Clear();
        foreach (var asset in _state.Assets)
        {
            _assets.AddItem(asset.Name);
        }

        if (_state.Assets.Count > 0)
        {
            _assets.Select(0);
        }

        ShowPreview(crash);
    }

    // A crash, from the selection or from drawing the preview, replaces the pane; the lists stay usable.
    void ShowPreview(string? crash = null)
    {
        ClearPane();

        crash ??= Guard(DrawPreview);
        if (crash != null)
        {
            ClearPane();

            _pane.AddChild(new Label { Text = crash, AutowrapMode = TextServer.AutowrapMode.Word });
        }
    }

    void ClearPane()
    {
        foreach (var child in _pane.GetChildren())
        {
            child.QueueFree();
        }
    }

    void DrawPreview()
    {
        if (_state.Notice != null)
        {
            _pane.AddChild(new Label { Text = _state.Notice });
        }
        else if (_state.Failure != null)
        {
            var name = _state.SelectedAsset!.File;
            _pane.AddChild(
                new Label { Text = $"{name}: {_state.Failure}", AutowrapMode = TextServer.AutowrapMode.Word }
            );
        }
        else if (_state.Preview is { } preview)
        {
            _pane.AddChild(
                _views.TryGetValue(preview.GetType(), out var view)
                    ? view.Create(preview)
                    : new Label { Text = preview.Summary }
            );
        }
    }
}

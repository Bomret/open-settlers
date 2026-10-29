using System;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Launch;

namespace OpenSettlers.Viewer;

/// <summary>
/// The project's main scene. With <c>--check</c> it runs the asset check, prints the report, and quits with its
/// exit code; otherwise it draws the asset browser: the categories, the assets of the selected kind, and the
/// preview of the selected asset.
///   godot --headless --path . -- --check --game &lt;install&gt;
///   godot --path . -- --game &lt;install&gt;
/// <c>SIEDLER_DIR</c> stands in for <c>--game</c>.
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
        var kinds = AssetKind.Discover();
        if (args.Mode == LaunchMode.Check)
        {
            RunCheck(args, kinds);

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

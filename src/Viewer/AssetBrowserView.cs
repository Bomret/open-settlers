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

        _state = AssetBrowserState.Open(args, kinds);
        Build();
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
            AddChild(
                new Label
                {
                    Text = _state.Message,
                    AutowrapMode = TextServer.AutowrapMode.Word,
                    AnchorRight = 1,
                    AnchorBottom = 1,
                }
            );

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
            _state.SelectKind(_state.Categories[(int)index]);
            ShowAssets();
        };
        _assets.ItemSelected += index =>
        {
            _state.SelectAsset((int)index);
            ShowPreview();
        };
        if (_state.Categories.Count > 0)
        {
            _kinds.Select(0);
        }

        ShowAssets();
    }

    void ShowAssets()
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

        ShowPreview();
    }

    void ShowPreview()
    {
        foreach (var child in _pane.GetChildren())
        {
            child.QueueFree();
        }

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
            var views = PreviewView.Discover();
            _pane.AddChild(
                views.TryGetValue(_state.SelectedKind!, out var view)
                    ? view.Create(preview)
                    : new Label { Text = preview.Summary }
            );
        }
    }
}

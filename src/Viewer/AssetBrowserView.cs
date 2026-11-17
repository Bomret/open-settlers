using System;
using System.Linq;
using Godot;
using OpenSettlers.Assets;
using OpenSettlers.Domain;
using OpenSettlers.Import.ContentSet;
using OpenSettlers.Import.HistoryEdition;
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
///   godot --path . --write-movie play-out/&lt;stem&gt;/frame.png --fixed-fps 30 -- --map --play &lt;script&gt;
///   (a play run, <see cref="PlayDriver"/>; <c>--game-view</c> in place of <c>--map</c> runs it on the game view)
///   godot --path . -- --map --content-set basic   (the basic content set: no install, no <c>--game</c>, no <c>SIEDLER_DIR</c>)
/// <c>SIEDLER_DIR</c> stands in for <c>--game</c> on the History Edition. A bad argument prints its problem and quits with
/// exit code 4 before the install is opened.
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
        // The window's pixel size at startup, read before PlayDriver resizes or minimizes it. Under canvas_items and
        // expand the content scale size and the visible rect stay 1152 x 648 for any 16:9 window.
        var window = DisplayServer.WindowGetSize();
        var args = LaunchArgs.Parse(
            OS.GetCmdlineUserArgs(),
            System.Environment.GetEnvironmentVariable(LaunchArgs.GameVariable),
            movieMaker: !string.IsNullOrEmpty(Engine.GetWriteMoviePath()),
            startWindow: (window.X, window.Y)
        );
        if (MapLaunch.BadArgument(args) is { } bad)
        {
            // A bad argument wins over a missing install, and no window opens for it.
            GD.PrintErr(bad.Message);
            GetTree().Quit(bad.Code);

            return;
        }

        if (args.Mode == LaunchMode.SeedInfo)
        {
            // Seed info reads the large objects through the rule tables port. A Godot callback exception does not set
            // the exit code, so this owns it.
            var code = SeedInfo.Run(
                LaunchInstall.RuleTables(args, ProjectSettings.GlobalizePath("res://")),
                args.SeedText!,
                args.Seed!.Value,
                args.Size ?? MapSize.Offered[0],
                out var text
            );
            if (code == 0)
            {
                GD.Print(text.TrimEnd('\n'));
            }
            else
            {
                GD.PrintErr($"ERROR {text}");
            }

            GetTree().Quit(code);

            return;
        }

        if (args.Mode == LaunchMode.Check)
        {
            RunCheck(args, LaunchInstall.Assets(args));

            return;
        }

        if (args.Mode is LaunchMode.Map or LaunchMode.GameView)
        {
            RunMap(args);

            return;
        }

        var views = PreviewView.Discover();
        _state = AssetBrowserState.Open(
            LaunchInstall.Assets(args),
            selectFirst: false,
            previewClash: PreviewClash.Of(views.Select(v => (v.GetType().Name, v.PreviewType)))
        );
        if (_state.Message == null)
        {
            _views = views.ToDictionary(v => v.PreviewType);
        }

        if (_state.Categories.Count > 0 && Guard(() => _state.SelectKind(_state.Categories[0])) is { } crash)
        {
            AddChild(Wrapped(crash));

            return;
        }

        Build();
    }

    // AssetBrowserState propagates a programming error, and a Godot callback exception only reaches the log, so the
    // view catches it like RunCheck does and returns the text to show in its place.
    string? Guard(Action action) => CrashText.Guard(action, () => CrashText.Subject(_state), line => GD.PrintErr(line));

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
        var decision = MapLaunch.Decide(
            args,
            path =>
            {
                var code = LayoutLoad.Run(path, out var loaded, out var message);

                return (code, loaded, message);
            },
            a => LaunchInstall.TryOpen(a, out var opened, out var problem) ? (opened, null) : (null, problem),
            PlayScript.Read,
            () => ProjectSettings.GlobalizePath("res://")
        );
        if (decision is MapLaunch.Exit exit)
        {
            if (exit.Stream == LaunchStream.Stderr)
            {
                GD.PrintErr(exit.Message);
            }
            else
            {
                GD.Print(exit.Message);
            }

            GetTree().Quit(exit.Code);

            return;
        }

        var open = (MapLaunch.Open)decision;
        var layout = open.Layout;
        var install = open.Install;
        var script = open.Script;
        var folder = open.PlayOutFolder;
        // MapLaunch opened the install on the History Edition and none on the basic set, so the install decides the content set
        // once and the History Edition's tables come from the install already open.
        var basicFolder = LaunchInstall.BasicFolder(ProjectSettings.GlobalizePath("res://"));
        IRuleTablesSource tablesSource = install is null
            ? new OpenSettlersRuleTables(basicFolder)
            : new HistoryEditionRuleTables(install);
        // The view calls this where it catches a read failure: a broken content set names its file and key, and the view draws
        // no map, as for a missing install file. The basic set reads its walk steps here too, so a broken walk steps file
        // draws no map either; it reads no install.
        Func<RuleTables?, GraphicsLoad> loadGraphics = install is null
            ? _ => new GraphicsLoad
            {
                Source = OpenSettlersSource.Load(basicFolder, new GodotImageDecoder()),
                Steps = OpenSettlersWalkSteps.Load(basicFolder),
            }
            : tables => HistoryEditionSource.LoadGraphics(install, tables);
        Func<IWalkSteps> loadSteps = () =>
            install is null ? OpenSettlersWalkSteps.Load(basicFolder) : StepOffsets.Load(install);
        var view = layout is not null
            ? new MapViewControl(tablesSource, loadGraphics, loadSteps, layout)
            : new MapViewControl(
                tablesSource,
                loadGraphics,
                loadSteps,
                args.SeedText ?? MapViewState.DefaultSeed,
                args.Size?.Value ?? MapSize.Offered[0].Value,
                args.Mode == LaunchMode.GameView,
                args.Stock
            );
        AddChild(view);
        if (script is not null)
        {
            AddChild(new PlayDriver(view, script, folder!, args.Minimized));
        }
    }

    // A Godot callback exception does not set the exit code, so the check owns it.
    void RunCheck(LaunchArgs args, IAssetsSource assets)
    {
        int code;
        try
        {
            var report = AssetCheck.Run(args, assets);
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

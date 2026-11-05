using System.Globalization;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The editor (roadmap 0.6, ADR 0010): the installed plugins in <see cref="RecordBrowser"/>.
/// </summary>
/// <remarks>
/// Command-line options, after <c>--</c>: <c>--data &lt;plugins folder&gt;</c> (default: the
/// data folder's), <c>--type &lt;type&gt;</c> and <c>--kind party|character|enemy</c>, which list that type, or that kind of
/// entity, as the side panel's dropdown and buttons do, <c>--go map|party|characters|enemies|items|shop|weapons|armor|shield</c>, which
/// presses that quick button on the toolbar, <c>--record &lt;id&gt;</c>, <c>--tab preview|fields|used-by|atlas</c>, and
/// <c>--screenshot &lt;png&gt;</c>, which saves the window once the record is shown and quits. For a record's 3D preview,
/// <c>--drive &lt;request&gt;</c> runs an actor's state machine, <c>--wireframe 1</c> and
/// <c>--scene-camera 1</c> set those switches, <c>--collision surface|role</c> colours a map's
/// collision, and <c>--wait &lt;seconds&gt;</c> waits before the
/// screenshot. <c>--checkpoint &lt;state&gt;</c> and <c>--save &lt;save&gt;</c> load a checkpoint's or a
/// save's game, which flag pages, scripts and the party's pages show, and
/// <c>--play &lt;moves&gt;</c> plays a script's page that many moves, with its conversation box shown at
/// <c>--zoom &lt;1-3&gt;</c>, <c>--ui-filter pixels|videoout|smooth</c>, <c>--aspect 4:3|16:9</c> and
/// <c>--ui-lines 240|360|480</c>. The inventory record's Party tab takes the same, and
/// <c>--member &lt;slot&gt;</c>, <c>--list inventory|spells|skills|stats</c>, <c>--row &lt;n&gt;</c> and
/// <c>--details 1</c>, and <c>--press &lt;a,b,up,down&gt;</c> presses those buttons in turn after. <c>--roll 1</c> rolls a
/// loot table's page, or opens the container a map object's page picks, in the loot window, whose buttons
/// <c>--press</c> also takes, with <c>left</c> and <c>right</c>. <c>--look 1</c>, after the wait, looks through a camera object's page's camera. <c>--ui-screenshot &lt;png&gt;</c> saves the UI screen too, in its own pixels,
/// before it's scaled up. With no current import it shows the import dialog (<see cref="ImportDialog"/>) first: <c>--from-game 1</c>,
/// which the game passes, ends it with Play, and <c>--rom &lt;path&gt;</c> and <c>--import 1</c> fill it in and press Import.
/// </remarks>
public partial class Editor : Control
{
    private readonly RecordBrowser _records = new() { Name = "Plugins" };
    private string? _uiShot;

    /// <summary>A portable install's data folder is found before any field reads a path, such as the browser's plugins folder.</summary>
    static Editor() => AppLauncher.UseAppFolder();

    public override void _Ready()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        // The editor's theme, dark or light as it was last left, before anything's built (--light 1 or 0 picks one for a run).
        if (CommandLine.Parse(OS.GetCmdlineUserArgs()).GetValueOrDefault("light") is { } light)
            EditorTheme.Use(GetWindow(), light == "1", keep: false);
        else
            EditorTheme.Use(GetWindow(), EditorTheme.SavedLight(), keep: false);
        // Lit meshes are lit as the RSP lights them, by the game's sky, as in the runtime (ADR 0010), not by Godot's lights.
        // The editor keeps linear light, so its own overlays draw as they are: no on-screen framebuffer.
        N64Material.RspLighting = true;
        _records.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_records);
        _ = BackgroundTask.Report(RunAsync(CommandLine.Parse(OS.GetCmdlineUserArgs())));
    }

    /// <summary>The run, which for a screenshot ends at once on an option it can't read, such as a mistyped record ID, not at the watchdog's minute.</summary>
    private async Task RunAsync(Dictionary<string, string> args)
    {
        try
        {
            await StartAsync(args);
        }
        catch (Exception e) when (args.ContainsKey("screenshot"))
        {
            GD.PrintErr($"The run stopped: {e.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task StartAsync(Dictionary<string, string> args)
    {
        var png = args.GetValueOrDefault("screenshot");
        _uiShot = args.GetValueOrDefault("ui-screenshot");
        // An import takes a few minutes.
        if (png is not null)
            CommandLine.Watchdog(GetTree(), args.GetValueOrDefault("import") == "1" ? 900 : 60);

        var plugins = args.GetValueOrDefault("data") ?? DataPaths.Plugins;
        if (RomPlugin.Diagnose(plugins) is not null && !await ImportFirstAsync(plugins, args, png))
            return;
        if (!await PickModFirstAsync(plugins, args, png))
            return;
        var loading = _records.LoadAsync(plugins);
        // --loading 1 saves the loading card, while the plugins load.
        if (png is not null && args.GetValueOrDefault("loading") == "1")
        {
            await Screenshot(png);
            return;
        }
        await loading;
        // A bad checkpoint or save shouldn't stop the record opening, or a script waits for the watchdog.
        if (args.TryGetValue("checkpoint", out var checkpoint))
        {
            try
            {
                _records.LoadCheckpoint(checkpoint);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Couldn't load the checkpoint {checkpoint}: {e.Message}");
            }
        }
        if (args.TryGetValue("save", out var save))
        {
            try
            {
                _records.LoadSave(save);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Couldn't load the save {save}: {e.Message}");
            }
        }
        if (args.TryGetValue("type", out var type))
            _records.SelectType(type);
        if (args.TryGetValue("kind", out var kind))
            _records.SelectKind(Enum.Parse<EntityKind>(kind, ignoreCase: true));
        if (args.TryGetValue("go", out var go))
            _records.PressQuick(go);
        if (args.TryGetValue("record", out var record))
            await _records.OpenAsync(RecordId.Parse(record));
        // After the record, as the loading card and the start page show the Preview tab, and the record says which tabs it has.
        if (args.TryGetValue("tab", out var tab))
            _records.SelectTab(tab);
        if (args.TryGetValue("play", out var moves))
            _records.PlayScript(int.Parse(moves, CultureInfo.InvariantCulture));
        if (args.GetValueOrDefault("roll") == "1")
            _records.Roll();
        _records.SetUiScreen(args.TryGetValue("zoom", out var zoom) ? int.Parse(zoom, CultureInfo.InvariantCulture) : null,
            args.TryGetValue("ui-filter", out var filter) ? Enum.Parse<AidynRetold.Core.Ui.UiFilter>(filter, ignoreCase: true) : null,
            args.TryGetValue("aspect", out var aspect) ? aspect == "16:9" : null,
            args.TryGetValue("ui-lines", out var lines) ? int.Parse(lines, CultureInfo.InvariantCulture) : null);
        _records.ShowPartyTab(args.TryGetValue("member", out var member) ? int.Parse(member, CultureInfo.InvariantCulture) : null,
            args.TryGetValue("list", out var list) ? Enum.Parse<AidynRetold.Core.Ui.PartyList>(list, ignoreCase: true) : null,
            args.TryGetValue("row", out var row) ? int.Parse(row, CultureInfo.InvariantCulture) : null,
            args.TryGetValue("details", out var details) ? details == "1" : null);
        if (args.TryGetValue("press", out var presses))
            _records.PressPartyTab(presses.Split(','));
        // An armor's, a weapon's or an item's page: --item-change name=…,price=… holds values as if typed, --item-save 1 saves them,
        // --item-picture 1 opens the picture's dialog and --item-model 1 the model's. The --armor- spellings, from before weapons and
        // items had pages of their own, do the same.
        string? Option(string name) => args.GetValueOrDefault($"item-{name}") ?? args.GetValueOrDefault($"armor-{name}");
        if (Option("change") is { } changes)
            _records.EditPage?.ChangeFor(changes.Split(',').Select(c => c.Split('=', 2)).Where(c => c.Length == 2).Select(c => (c[0], c[1])));
        if (Option("save") == "1")
            _records.EditPage?.SaveChanges();
        if (Option("picture") == "1")
            _records.EditPage?.PickPicture();
        if (Option("model") == "1")
            _records.EditPage?.PickModel();
        if (args.TryGetValue("page-tab", out var pageTab))
            _records.EditPage?.ShowTab(pageTab);
        if (args.TryGetValue("page-scroll", out var pageScroll))
            _records.EditPage?.ScrollTo(int.Parse(pageScroll, CultureInfo.InvariantCulture));
        if (args.TryGetValue("drive", out var request))
            _records.Model.Drive(int.Parse(request, CultureInfo.InvariantCulture));
        // --mod-dialog toolbar opens the toolbar's mod dialog, which closes by its cross.
        if (args.GetValueOrDefault("mod-dialog") == "toolbar")
            _ = ModSetup.AskAsync(this, plugins, blocking: false);
        if (args.TryGetValue("toast", out var toast))
            Toast.Show(this, toast, () => { }, seconds: 30);
        if (args.GetValueOrDefault("states") == "graph")
            _records.Model.ShowStatesAsGraph(true);
        if (args.TryGetValue("open", out var sections))
            _records.Inspector?.OpenOnly(sections.Split(','));
        if (args.TryGetValue("part", out var part))
            _records.Inspector?.Parts?.Select(part);
        _records.Model.SetWireframe(args.GetValueOrDefault("wireframe") == "1");
        _records.Model.SetSceneCamera(args.GetValueOrDefault("scene-camera") == "1");
        if (args.TryGetValue("collision", out var collision))
            _records.Map.SetCollision(collision);
        if (args.TryGetValue("wait", out var wait))
            await ToSignal(GetTree().CreateTimer(double.Parse(wait, CultureInfo.InvariantCulture)), SceneTreeTimer.SignalName.Timeout);
        if (args.GetValueOrDefault("look") == "1")
            _records.Map.LookThrough();
        if (png is not null)
            await Screenshot(png);
    }

    /// <summary>
    /// With no current import, the import dialog over the empty window, before anything loads (editor.md#the-import-dialog): true once
    /// it's imported and the editor is to go on, false if it stays up, as it does when the game started it (Play ends it) or a
    /// screenshot was taken. Cancelling it quits. <c>--rom</c> and <c>--import 1</c> fill it in and press Import, for scripts.
    /// </summary>
    private async Task<bool> ImportFirstAsync(string plugins, Dictionary<string, string> args, string? png)
    {
        var fromGame = AppLauncher.StartedFromGame(OS.GetCmdlineUserArgs());
        // Started for the game, the window is the game's until the game starts.
        if (fromGame)
            GetWindow().Title = "Aidyn Chronicles Retold";
        var dialog = new ImportDialog(plugins, fromGame);
        _records.Visible = false;
        AddChild(dialog);
        var imported = new TaskCompletionSource();
        dialog.Imported += () => imported.TrySetResult();
        if (args.TryGetValue("rom", out var rom))
            dialog.RomPath = rom;
        if (args.GetValueOrDefault("import") == "1")
            await dialog.ImportAsync();
        if (png is not null && !imported.Task.IsCompleted)
        {
            await Screenshot(png);
            return false;
        }
        await imported.Task;
        dialog.QueueFree();
        _records.Visible = true;
        return true;
    }

    /// <summary>
    /// The mod to save to, before anything loads (<see cref="ModSetup"/>): <c>--mod</c>'s for a run, else the one remembered for these
    /// plugins, else asked, blocking the editor as the import does. A screenshot run doesn't ask, unless <c>--mod-dialog 1</c>, which
    /// saves the dialog and quits: false then, and the editor goes no further.
    /// </summary>
    private async Task<bool> PickModFirstAsync(string plugins, Dictionary<string, string> args, string? png)
    {
        if (args.TryGetValue("mod", out var mod))
        {
            CurrentMod.Use(plugins, mod, keep: false);
            return true;
        }
        if (CurrentMod.Remembered(plugins) is { } known)
        {
            CurrentMod.Use(plugins, known, keep: false);
            return true;
        }
        var ask = args.GetValueOrDefault("mod-dialog") == "1";
        if (png is not null && !ask)
            return true;
        _records.Visible = false;
        var picked = ModSetup.AskAsync(this, plugins, blocking: true);
        if (png is not null)
        {
            await Screenshot(png);
            return false;
        }
        await picked;
        _records.Visible = true;
        return true;
    }

    private async Task Screenshot(string path)
    {
        // A few frames for the layout to settle and the preview to draw.
        await CommandLine.Frames(this, 5);
        var error = CommandLine.Save(GetViewport().GetTexture().GetImage(), path);
        if (_uiShot is { } ui && Descendants(this).OfType<UiScreen>().FirstOrDefault(s => s.IsVisibleInTree()) is { } screen)
            CommandLine.Save(screen.Capture(), ui);
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private static IEnumerable<Node> Descendants(Node node) => node.GetChildren().SelectMany(c => Descendants(c).Prepend(c));
}

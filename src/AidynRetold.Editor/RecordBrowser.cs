using System.Globalization;
using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Flags;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using AidynRetold.Rom;
using AidynRetold.Rom.Import;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The editor's view of the installed plugins (ADR 0010): every record and asset by type, with a
/// preview, the merged fields and what names it. Record IDs are links, with back and forward
/// (the buttons, Alt+Left and Alt+Right, or the mouse's side buttons).
/// </summary>
/// <remarks>
/// It reads plugins only. The ROM is the import dialog's (<see cref="ImportDialog"/>), which the editor shows before the browser
/// loads anything when there's no current import, and which is also the game's first run (ADR 0001, ADR 0012, ADR 0043).
/// </remarks>
public partial class RecordBrowser : Control
{
    private const string Preview = "Preview", Fields = "Fields", UsedBy = "Used by", Atlas = "Atlas";

    private readonly Button _back = new() { Text = "◀", TooltipText = "Back (Alt+Left)", Disabled = true };
    private readonly Button _forward = new() { Text = "▶", TooltipText = "Forward (Alt+Right)", Disabled = true };
    private readonly OptionButton _types = new() { TooltipText = "Type" };
    private readonly LineEdit _search = new()
    {
        PlaceholderText = "Record ID, Borg index, summary or script text",
        CustomMinimumSize = new Vector2(280, 0),
        TooltipText = "Filters the list. Enter opens a record ID, or the only match.",
    };
    private readonly Label _status = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Tree _list = new();
    private readonly RecordHeader _header = new();
    private readonly TabContainer _tabs = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Control _preview = new MarginContainer { Name = Preview };
    private readonly RecordFieldsView _fields = new() { Name = Fields };
    private readonly Tree _usedBy = new() { Name = UsedBy };
    private readonly AtlasView _atlas = new() { Name = Atlas };
    private readonly ModelView _model = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly MapSetView _map = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly Dictionary<RecordId, TreeItem> _rows = [];
    private readonly List<RecordId> _history = [];
    private readonly List<string> _typeNames = [];

    private PluginCatalog? _catalog;
    private Models? _models;
    private QuestIndex? _quests;
    private EditorGame? _game;
    private CharacterRules? _rules;
    private ItemCatalog? _items;
    private string _pluginsDirectory = DataPaths.Plugins;
    private int _position = -1;
    private RecordId? _current;
    private bool _selecting;
    private int _generation;
    private Task _showing = Task.CompletedTask;

    public override void _Ready() => BuildUi();

    /// <summary>
    /// The game's sky lights the previews' lit display lists, as the runtime's sky lights trek's (<see cref="N64Material.RspLighting"/>),
    /// worked out again each frame for its time of day, which a script played here can move on. The scenery stays untinted and
    /// unfogged.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_game?.Sky is not { } sky)
            return;
        sky.Apply();
        N64Material.SetSkyLight(sky);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete)
            return;
        // The preview not showing is out of the tree, and the models' templates never were in
        // it, so nothing else frees them. Godot reports them leaked at exit, one by one.
        foreach (var view in new Control[] { _model, _map })
        {
            if (view.GetParent() is null)
                view.Free();
        }
        _models?.Dispose();
    }

    /// <summary>The 3D preview, for scripted screenshots.</summary>
    public ModelView Model => _model;

    /// <summary>The map set preview, for scripted screenshots.</summary>
    public MapSetView Map => _map;

    /// <summary>Shows the Preview, Fields or Used by tab.</summary>
    public void SelectTab(string tab) => _tabs.CurrentTab = tab.ToLowerInvariant() switch { "fields" => 1, "used-by" => 2, "atlas" => 3, _ => 0 };

    /// <summary>Loads the plugins in a folder, as the runtime does: <c>aidyn.rom</c> first, then the base plugins (ADR 0039), then the rest in <see cref="PluginSet.LoadOrder"/>.</summary>
    public async Task LoadAsync(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        _catalog = null;
        _map.Clear();
        _models?.Dispose();
        _models = null;
        _quests = null;
        _current = null;
        _history.Clear();
        _position = -1;
        UpdateHistoryButtons();
        Populate();
        ShowNothing("");
        // The editor imports before it loads (ADR 0043), so this is only a ROM plugin gone or replaced while it ran: the game would
        // refuse it, so the editor shows the same words.
        if (RomPlugin.Diagnose(pluginsDirectory) is { } problem)
        {
            _status.Text = problem;
            return;
        }

        _status.Text = $"Loading {pluginsDirectory}…";
        var generation = ++_generation;
        var (catalog, quests, game, rules, items, report) = await Task.Run(() =>
        {
            var plugins = PluginSet.LoadInstalled(pluginsDirectory, DataPaths.BasePlugins);
            // The game refuses a set with a record it can't read; the editor shows the report and loads anyway, to fix it by.
            var report = plugins.Validate();
            return (PluginCatalog.Build(plugins), Partly(() => QuestIndex.Build(plugins), "No quest index"), Partly(() => new EditorGame(plugins), "No game"),
                Partly(() => EditorCharacters.Rules(plugins), "No character rules"), Partly(() => EditorCharacters.Items(plugins), "No items"), report);
        });
        if (generation != _generation)
            return;
        // Text is drawn in the typeface, as the runtime's Text Font setting does by default, and the game lays it out by its widths.
        TextFace.Current = TextFace.For(catalog.Plugins);
        game?.TextWidths = TextFace.Current.Widths;
        _catalog = catalog;
        _models = new Models(catalog.Plugins);
        _model.Plugins = catalog.Plugins;
        _quests = quests;
        _game = game;
        _rules = rules;
        _items = items;

        _typeNames.Clear();
        _typeNames.AddRange(catalog.Types);
        _types.Clear();
        _types.AddItem("All types", 0);
        for (var i = 0; i < _typeNames.Count; i++)
            _types.AddItem($"{_typeNames[i]} ({catalog.Entries.Count(e => e.Id.Type == _typeNames[i]):N0})", i + 1);
        Populate();
        ShowNothing(report.IsClean ? Welcome(catalog) : $"{report.Describe(limit: 12)}\nThe game refuses to load these; the editor shows them as they are.\n{Welcome(catalog)}");
    }

    /// <summary>
    /// What a part of the editor builds from the plugins, or none where a record it reads can't be read, which the validation report
    /// shown as they load names: so a mod's bad record leaves the rest of the editor to fix it in.
    /// </summary>
    private static T? Partly<T>(Func<T?> build, string what) where T : class
    {
        try
        {
            return build();
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or KeyNotFoundException or ArgumentException
            or System.Text.Json.JsonException)
        {
            GD.PrintErr($"{what}: {e.Message}");
            return null;
        }
    }

    /// <summary>Loads a checkpoint's game, for scripts (<c>--checkpoint</c>).</summary>
    public void LoadCheckpoint(string path) => _game?.LoadCheckpoint(path);

    /// <summary>Loads a save's game, for scripts (<c>--save</c>).</summary>
    public void LoadSave(string path) => _game?.LoadSave(path);

    /// <summary>Plays the script on the preview, if it's showing one, for a number of moves (<c>--play</c>).</summary>
    public void PlayScript(int moves)
    {
        if (_preview.GetChildren().OfType<ScriptPanel>().FirstOrDefault() is { } panel)
            panel.Play(moves);
    }

    /// <summary>
    /// Sets how the preview's UI screen is shown, if it has one: a script's conversation box or the
    /// Party tab (<c>--zoom</c>, <c>--ui-filter</c>, <c>--aspect</c>, <c>--ui-lines</c>).
    /// </summary>
    public void SetUiScreen(int? zoom, AidynRetold.Core.Ui.UiFilter? filter, bool? wide, int? lines)
    {
        if (_preview.GetChildren().OfType<ScriptPanel>().FirstOrDefault() is { } script)
            script.SetScreen(zoom, filter, wide, lines);
        if (_preview.GetChildren().OfType<PartyTabPanel>().FirstOrDefault() is { } party)
            party.SetScreen(zoom, filter, wide, lines);
        LootWindow?.SetScreen(zoom, filter, wide, lines);
        TrainingWindow?.SetScreen(zoom, filter, wide, lines);
    }

    /// <summary>The training window on the preview: a trainer's.</summary>
    private TrainingWindowPanel? TrainingWindow => _preview.GetChildren().OfType<CharacterPanel>().FirstOrDefault()?.Window;

    /// <summary>The barter window open on the preview: a loot table's roll, a shop's, or a container's on the map.</summary>
    private BarterWindowPanel? LootWindow => _preview.GetChildren().OfType<LootPanel>().FirstOrDefault()?.Window
        ?? _preview.GetChildren().OfType<ShopPanel>().FirstOrDefault()?.Window ?? _map.LootWindow;

    /// <summary>
    /// Rolls the loot table, or opens the container picked on the map, in the loot window, if the preview
    /// shows one (<c>--roll</c>).
    /// </summary>
    public void Roll()
    {
        if (_preview.GetChildren().OfType<LootPanel>().FirstOrDefault() is { } loot)
            loot.Roll();
        else
            _map.Roll();
    }

    /// <summary>Shows a member, list, row and the details on the Party tab, if it's showing (<c>--member</c>, <c>--list</c>, <c>--row</c>, <c>--details</c>).</summary>
    public void ShowPartyTab(int? member, AidynRetold.Core.Ui.PartyList? list, int? row, bool? details)
    {
        if (_preview.GetChildren().OfType<PartyTabPanel>().FirstOrDefault() is { } party)
            party.Show(member, list, row, details);
    }

    /// <summary>Presses buttons on the Party tab, if it's showing (<c>--press</c>).</summary>
    public void PressPartyTab(IEnumerable<string> buttons)
    {
        if (_preview.GetChildren().OfType<PartyTabPanel>().FirstOrDefault() is { } party)
            party.Press(buttons);
        LootWindow?.Press(buttons);
        TrainingWindow?.Press(buttons);
    }

    /// <summary>Opens a record, adding it to the history. The task completes when its preview is shown.</summary>
    public async Task OpenAsync(RecordId id)
    {
        Navigate(id);
        await _showing;
    }

    private void BuildUi()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        var bar = new HBoxContainer();
        bar.AddChild(_back);
        bar.AddChild(_forward);
        bar.AddChild(new VSeparator());
        bar.AddChild(_types);
        bar.AddChild(_search);
        bar.AddChild(_status);
        root.AddChild(bar);

        var split = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SplitOffsets = [520] };
        root.AddChild(split);

        _list.Columns = 3;
        _list.HideRoot = true;
        _list.ColumnTitlesVisible = true;
        _list.SelectMode = Tree.SelectModeEnum.Row;
        string[] titles = ["Record", "Borg", "Summary"];
        for (var c = 0; c < titles.Length; c++)
        {
            _list.SetColumnTitle(c, titles[c]);
            _list.SetColumnExpand(c, c == 2);
            _list.SetColumnClipContent(c, true);
        }
        _list.SetColumnCustomMinimumWidth(0, 190);
        _list.SetColumnCustomMinimumWidth(1, 60);
        _list.ItemSelected += () =>
        {
            if (!_selecting && _list.GetSelected()?.GetMetadata(0).AsString() is { } text && RecordId.TryParse(text, out var id))
                Navigate(id);
        };
        split.AddChild(_list);

        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 4);
        right.AddChild(_header);
        right.AddChild(_tabs);
        split.AddChild(right);
        _tabs.AddChild(_preview);
        _tabs.AddChild(_fields);
        _tabs.AddChild(_usedBy);
        _tabs.AddChild(_atlas);
        // The tabs are the header's, at the end of its row; the container below only shows the page.
        _tabs.TabsVisible = false;
        foreach (var name in new[] { Preview, Fields, UsedBy, Atlas })
            _header.Tabs.AddTab(name);
        _header.Tabs.TabChanged += tab => _tabs.CurrentTab = (int)tab;
        _tabs.TabChanged += tab => _header.Tabs.CurrentTab = (int)tab;

        _usedBy.Columns = 3;
        _usedBy.HideRoot = true;
        _usedBy.ColumnTitlesVisible = true;
        _usedBy.SelectMode = Tree.SelectModeEnum.Row;
        string[] usedByTitles = ["Record", "Fields", "Summary"];
        for (var c = 0; c < usedByTitles.Length; c++)
        {
            _usedBy.SetColumnTitle(c, usedByTitles[c]);
            _usedBy.SetColumnClipContent(c, true);
        }
        _usedBy.SetColumnExpand(0, false);
        _usedBy.SetColumnCustomMinimumWidth(0, 190);
        _usedBy.ItemActivated += () =>
        {
            if (_usedBy.GetSelected()?.GetMetadata(0).AsString() is { } text && RecordId.TryParse(text, out var id))
                FollowLink(id);
        };
        _fields.Opened += FollowLink;
        _map.Opened += FollowLink;
        _map.Saved += () => _ = Report(LoadAsync(_pluginsDirectory));

        _back.Pressed += () => Step(-1);
        _forward.Pressed += () => Step(1);
        _types.ItemSelected += _ => Populate();
        _search.TextChanged += _ => Populate();
        _search.TextSubmitted += Submit;
    }

    private Task Report(Task task) => BackgroundTask.Report(task, e => _status.Text = e.Message);

    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree())
            return;
        var step = e switch
        {
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Xbutton1 } => -1,
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Xbutton2 } => 1,
            InputEventKey { Pressed: true, AltPressed: true, Keycode: Key.Left } => -1,
            InputEventKey { Pressed: true, AltPressed: true, Keycode: Key.Right } => 1,
            _ => 0,
        };
        if (step == 0)
            return;
        Step(step);
        GetViewport().SetInputAsHandled();
    }

    // ---- The list -------------------------------------------------------------------------------

    /// <summary>Fills the list with the entries that match the type and search filters.</summary>
    private void Populate()
    {
        _list.Clear();
        _rows.Clear();
        if (_catalog is null)
            return;
        var type = _types.GetSelectedId() is > 0 and var t ? _typeNames[t - 1] : null;
        var search = _search.Text.Trim();
        // Scripts whose text holds the search, with the first line that does.
        var said = new Dictionary<RecordId, string>();
        if (search.Length >= 3 && _quests is { } quests)
        {
            foreach (var (script, node) in quests.FindText(search))
            {
                if (!said.ContainsKey(script) && _catalog.Plugins.TryGet<DialogueRecord>(script) is { } record)
                    said[script] = $"node {node}: {record.Nodes[node].Text.Replace('\n', ' ')}";
            }
        }
        var root = _list.CreateItem();
        foreach (var entry in _catalog.Entries)
        {
            var line = said.GetValueOrDefault(entry.Id);
            if ((type is not null && entry.Id.Type != type) || (line is null && !Matches(entry, search)))
                continue;
            var row = _list.CreateItem(root);
            var id = entry.Id.ToString();
            row.SetText(0, id);
            row.SetTooltipText(0, id);
            row.SetText(1, entry.Borg?.ToString(CultureInfo.InvariantCulture) ?? "");
            row.SetTextAlignment(1, HorizontalAlignment.Right);
            row.SetText(2, line ?? entry.Summary);
            row.SetTooltipText(2, line ?? entry.Summary);
            row.SetMetadata(0, id);
            _rows[entry.Id] = row;
        }
        if (_current is { } current && _rows.TryGetValue(current, out var selected))
            SelectRow(selected);
        _status.Text = $"{_rows.Count:N0} of {_catalog.Entries.Count:N0} records and assets";
    }

    private static bool Matches(CatalogEntry entry, string search) =>
        search.Length == 0
        || entry.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
        || entry.Summary.Contains(search, StringComparison.OrdinalIgnoreCase)
        || (entry.Borg is { } borg && borg.ToString(CultureInfo.InvariantCulture).StartsWith(search, StringComparison.Ordinal));

    /// <summary>Enter in the search box: open the record ID typed, or the only match.</summary>
    private void Submit(string text)
    {
        if (RecordId.TryParse(text.Trim(), out var id))
            Navigate(id);
        else if (_rows.Count == 1)
            Navigate(_rows.Keys.First());
    }

    private void SelectRow(TreeItem row)
    {
        _selecting = true;
        row.Select(0);
        _list.ScrollToItem(row, true);
        _selecting = false;
    }

    // ---- Navigation -----------------------------------------------------------------------------

    /// <summary>Opens a record from a link, on its preview.</summary>
    private void FollowLink(RecordId id)
    {
        _tabs.CurrentTab = 0;
        Navigate(id);
    }

    /// <summary>Opens a record from the atlas, staying on its tab.</summary>
    private void Explore(RecordId id) => Navigate(id);

    private void Navigate(RecordId id)
    {
        if (_current == id)
            return;
        _history.RemoveRange(_position + 1, _history.Count - _position - 1);
        _history.Add(id);
        _position = _history.Count - 1;
        Go(id);
    }

    private void Step(int by)
    {
        var to = _position + by;
        if (to < 0 || to >= _history.Count)
            return;
        _position = to;
        Go(_history[to]);
    }

    private void Go(RecordId id)
    {
        UpdateHistoryButtons();
        if (_rows.TryGetValue(id, out var row))
            SelectRow(row);
        else
            _list.DeselectAll();
        _showing = Report(ShowAsync(id));
    }

    private void UpdateHistoryButtons()
    {
        _back.Disabled = _position <= 0;
        _forward.Disabled = _position >= _history.Count - 1;
    }

    // ---- Showing a record -----------------------------------------------------------------------

    private void ShowNothing(string text)
    {
        _header.Show("", "", []);
        _fields.Show(null, _ => [], _ => false);
        _usedBy.Clear();
        SetTabTitle(2, UsedBy);
        SetTabTitle(3, Atlas);
        SetPreview(Message(text));
    }

    /// <summary>A tab's title, kept on the header's tab and the page's.</summary>
    private void SetTabTitle(int tab, string title)
    {
        _tabs.SetTabTitle(tab, title);
        _header.Tabs.SetTabTitle(tab, title);
    }

    private static string Welcome(PluginCatalog catalog)
    {
        var plugins = string.Join(", ", catalog.Plugins.Plugins.Select(p =>
            p.Manifest.Import is { } import ? $"{p.Manifest.Id} ({p.Manifest.Name}, imported from {import.RomVersion} by importer {import.Importer})" : p.Manifest.Id));
        var conflicts = catalog.Plugins.Conflicts.Count();
        var conflicted = conflicts == 0 ? "" : $" {conflicts:N0} {(conflicts == 1 ? "field is" : "fields are")} patched by more than one plugin (acre conflicts lists them).";
        return $"Loaded {plugins}.{conflicted} Pick a record, or type an ID such as aidyn:mapset/0 and press Enter.";
    }

    private async Task ShowAsync(RecordId id)
    {
        if (_catalog is not { } catalog)
            return;
        _current = id;
        var plugins = catalog.Plugins;
        var entry = catalog.Find(id);
        var voxel = id.Type == RecordTypes.Voxel;
        // A map object's fields are its map record's.
        var json = voxel ? MapObjects.Json(plugins, id) : plugins.GetJson(id);
        var references = catalog.ReferencesTo(id);

        if (voxel)
            ShowObjectHeader(catalog, id, json);
        else
            ShowHeader(catalog, id, entry, json, references.Count);
        _fields.Show(json, field => voxel ? plugins.Setters(MapRecord.MapOf(id), $"objects[{MapRecord.IndexOf(id)}].{field}") : plugins.Setters(id, field),
            other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel);
        ShowUsedBy(catalog, references);
        if (_quests is { } quests)
        {
            _atlas.Show(catalog, quests, id, Explore);
            SetTabTitle(3, _atlas.Count == 0 ? Atlas : $"{Atlas} ({_atlas.Count:N0})");
        }
        SetPreview(new Label { Text = "Loading…" });

        var preview = await BuildPreviewAsync(catalog, id, entry, json);
        if (_current != id || _catalog != catalog)
            return;
        switch (preview)
        {
            case Control control:
                SetPreview(control);
                break;
            case MapSetView.Focus focus when _models is { } models:
                SetPreview(_map);
                await _map.ShowAsync(catalog, models, _game, focus);
                break;
            case (byte[] glb, string animation, string note):
                SetPreview(_model);
                _model.Show(glb, animation.Length > 0 ? animation : null, note.Length > 0 ? note : null,
                    id.Type == RecordTypes.Actor ? plugins.TryGet<ActorRecord>(id) : null);
                break;
        }
    }

    /// <summary>
    /// The header: the record's summary, its ID and Borg, and the plugins it comes from, the definer's first, then each that patches
    /// it, with the fields it sets, and each that gives a file of its own in place of an earlier plugin's.
    /// </summary>
    private void ShowHeader(PluginCatalog catalog, RecordId id, CatalogEntry? entry, JsonObject? json, int usedBy)
    {
        var plugins = catalog.Plugins;
        var sources = new List<RecordHeader.Source>();
        if (json is not null && plugins.DefinedBy(id) is { } definer)
        {
            sources.Add(new(definer, "defines the record"));
            foreach (var patcher in plugins.Patchers(id))
            {
                var fields = json.Select(f => f.Key).Where(f => plugins.Setters(id, f).Contains(patcher)).ToList();
                sources.Add(new(patcher, $"patches {(fields.Count == 0 ? "its fields" : string.Join(", ", fields))}"));
            }
        }
        foreach (var asset in entry?.Assets ?? [])
        {
            var size = $"{new FileInfo(asset.Path).Length:N0} bytes";
            var original = plugins.FindOriginalAsset(id, asset.Extension) is { } path ? PluginOf(plugins, path) : asset.Plugin;
            if (sources.All(s => s.Plugin != original))
                sources.Add(new(original, original == asset.Plugin ? $"gives its .{asset.Extension}, {size}" : $"gives its original .{asset.Extension}"));
            if (original != asset.Plugin)
                sources.Add(new(asset.Plugin, $"replaces its .{asset.Extension}, {size}"));
        }
        var title = entry is null
            ? usedBy > 0 ? "No plugin has this record yet, but records name it" : "No plugin has this record"
            : entry.Summary.Length > 0 ? entry.Summary : id.ToString();
        _header.Show(title, id + (entry?.Borg is { } borg ? $" · Borg {borg}" : ""), sources);
    }

    /// <summary>A map object's header: what it is, its index in its map, and the plugins that place or change it.</summary>
    private void ShowObjectHeader(PluginCatalog catalog, RecordId id, JsonObject? json)
    {
        var map = MapRecord.MapOf(id);
        var index = MapRecord.IndexOf(id);
        if (json is null)
        {
            _header.Show("No plugin has this map object", id.ToString(), []);
            return;
        }
        var definer = catalog.Plugins.DefinedBy(map);
        var sources = new List<RecordHeader.Source>();
        if (definer is not null)
            sources.Add(new(definer, $"defines {map}"));
        foreach (var setter in catalog.Plugins.Setters(map, $"objects[{index}]").Where(p => p != definer))
            sources.Add(new(setter, "changes or places this object"));
        _header.Show(catalog.Describe(id), $"{id} · object {index} of {map}", sources);
    }

    /// <summary>The ID of the plugin whose folder holds <paramref name="path"/>.</summary>
    private static string PluginOf(PluginSet plugins, string path) =>
        plugins.Plugins.FirstOrDefault(p => path.StartsWith(Path.TrimEndingDirectorySeparator(p.Directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(Path.TrimEndingDirectorySeparator(p.Directory) + '/', StringComparison.OrdinalIgnoreCase))?.Manifest.Id ?? "?";

    private void ShowUsedBy(PluginCatalog catalog, IReadOnlyList<RecordReference> references)
    {
        _usedBy.Clear();
        var root = _usedBy.CreateItem();
        var byRecord = references.GroupBy(r => r.From).ToList();
        foreach (var group in byRecord)
        {
            var paths = group.Select(r => r.Path).ToList();
            var row = _usedBy.CreateItem(root);
            row.SetText(0, group.Key.ToString());
            row.SetMetadata(0, group.Key.ToString());
            row.SetText(1, paths.Count <= 3 ? string.Join(", ", paths) : $"{string.Join(", ", paths.Take(3))} and {paths.Count - 3:N0} more");
            row.SetTooltipText(1, string.Join("\n", paths.Take(40)) + (paths.Count > 40 ? "\n…" : ""));
            row.SetText(2, catalog.Find(group.Key)?.Summary ?? "");
        }
        SetTabTitle(2, byRecord.Count == 0 ? UsedBy : $"{UsedBy} ({byRecord.Count:N0})");
    }

    /// <summary>
    /// A Control, or a glTF to show in the model view with an animation and a note ("" for none).
    /// Files are read off the main thread.
    /// </summary>
    private async Task<object> BuildPreviewAsync(PluginCatalog catalog, RecordId id, CatalogEntry? entry, JsonObject? json)
    {
        var plugins = catalog.Plugins;
        if (id.Type is RecordTypes.Flag or RecordTypes.Journal)
            return _game is null ? Message("These plugins are too old for a game: import the ROM again.") : new FlagPanel(catalog, _game, id, FollowLink);
        if (id.Type == RecordTypes.Dialogue && plugins.TryGet<DialogueRecord>(id) is { } script)
            return new ScriptPanel(catalog, _game, _rules, _items, script, FollowLink);
        if (id.Type == RecordTypes.Entity && plugins.TryGet<EntityRecord>(id) is { } entity && _rules is { } rules)
            return new CharacterPanel(catalog, _game, entity, rules, _items, FollowLink);
        if (_items is { } items)
        {
            if (id.Type is RecordTypes.Weapon or RecordTypes.Armor or RecordTypes.Item && items.TryGet(id) is { } item)
                return new ItemPanel(catalog, _game, _rules, items, item, FollowLink);
            if (id.Type == RecordTypes.Shop && plugins.TryGet<ShopRecord>(id) is { } shop)
                return new ShopPanel(catalog, _game, _rules, items, shop, FollowLink);
            if (id.Type == RecordTypes.Loot && plugins.TryGet<LootRecord>(id) is { } loot)
                return new LootPanel(catalog, _game, items, loot, FollowLink);
            if (id.Type == RecordTypes.Recipe && plugins.TryGet<RecipeRecord>(id) is { } recipe)
                return new RecipePanel(catalog, _game, _rules, items, recipe, FollowLink);
            if (id.Type == RecordTypes.Inventory && _rules is { } partyRules && plugins.Contains(RecordId.Decimal(RecordTypes.Font, 0)))
                return new PartyTabPanel(catalog, _game, partyRules, items);
        }
        if (id.Type == RecordTypes.Actor && json is not null)
        {
            // An actor is its scene, playing its initial state's animation.
            if ((string?)json["scene"] is not { } sceneText || !RecordId.TryParse(sceneText, out var scene) || plugins.FindAsset(scene, "glb") is not { } path)
                return Message("The actor's scene has no asset.");
            var initial = (int?)json["initialState"] ?? 0;
            var animation = (string?)json["states"]?[initial]?["animation"] is { } a && RecordId.TryParse(a, out var animationId)
                ? SceneNames.Animation(Convert.ToInt32(animationId.Key, 16))
                : "";
            var note = $"Scene {scene.Key}, in its initial state ({initial})." + (json["head"] is not null ? " Its head isn't drawn here yet." : "");
            return (await File.ReadAllBytesAsync(path), animation, note);
        }
        if (MapFocus(plugins, id) is { } focus)
            return focus;
        if (entry?.Asset("glb") is { } glb)
        {
            var note = id.Type == RecordTypes.Map ? "Collision, in the chunk's own coordinates." : "";
            return (await File.ReadAllBytesAsync(glb.Path), "", note);
        }
        if (entry?.Asset("png") is { } png)
        {
            var image = await LoadPngAsync(png.Path);
            return image is null ? Message("Godot couldn't read the PNG.")
                : ImageGallery.Create([new ImageGallery.Item($"{image.GetWidth()}×{image.GetHeight()}", image)]);
        }
        if (json is not null && await ImagesNamedAsync(catalog, json) is { Count: > 0 } images)
            return ImageGallery.Create(images);
        return Message(json is null ? "" : "No preview for this type. See the fields.");
    }

    private static Label Message(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };

    private static Task<Image?> LoadPngAsync(string path) => Task.Run(() => Png.Load(path));

    /// <summary>The images and textures a record names, such as a terrain's sky gradient, once each, in the order first named.</summary>
    private async Task<List<ImageGallery.Item>> ImagesNamedAsync(PluginCatalog catalog, JsonObject json)
    {
        var named = new List<(RecordId Id, string Path, int Count)>();
        foreach (var (path, target) in Links(json, ""))
        {
            if (target.Type is not (RecordTypes.Image or RecordTypes.Texture) || catalog.Find(target)?.Asset("png") is null)
                continue;
            var i = named.FindIndex(n => n.Id == target);
            if (i < 0)
                named.Add((target, path, 1));
            else
                named[i] = named[i] with { Count = named[i].Count + 1 };
        }
        var items = new List<ImageGallery.Item>();
        foreach (var (target, path, count) in named)
        {
            if (await LoadPngAsync(catalog.Find(target)!.Asset("png")!.Path) is not { } image)
                continue;
            var label = count > 1 ? $"{path} and {count - 1} more" : path;
            items.Add(new ImageGallery.Item(label, image, () => FollowLink(target), $"{target}. Click to open."));
        }
        return items;
    }

    /// <summary>Every field naming a record, with its path.</summary>
    private static IEnumerable<(string Path, RecordId Id)> Links(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o)
                {
                    if (key == "id" && path.Length == 0)
                        continue;
                    foreach (var link in Links(value, path.Length == 0 ? key : $"{path}.{key}"))
                        yield return link;
                }
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    foreach (var link in Links(a[i], $"{path}[{i}]"))
                        yield return link;
                }
                break;
            case JsonValue v when v.TryGetValue(out string? text) && RecordId.TryParse(text, out var id):
                yield return (path, id);
                break;
        }
    }

    /// <summary>
    /// A map set shows whole. A map shows the 3×3 chunks around it, as the game loads them, and a
    /// map object its map's, picked, unless more of its map set is loaded already.
    /// </summary>
    private static MapSetView.Focus? MapFocus(PluginSet plugins, RecordId id)
    {
        if (id.Type == RecordTypes.MapSet)
            return plugins.TryGet<MapSetRecord>(id) is { } set ? new MapSetView.Focus(set, set.Chunks) : null;
        if (id.Type is not (RecordTypes.Map or RecordTypes.Voxel))
            return null;
        var map = id.Type == RecordTypes.Map ? id : MapRecord.MapOf(id);
        if (MapPlacements.For(plugins).First(map) is not { } place)
            return null;
        var around = place.Set.Chunks.Where(c => Math.Abs(c.Row - place.Chunk.Row) <= 1 && Math.Abs(c.Column - place.Chunk.Column) <= 1).ToList();
        return new MapSetView.Focus(place.Set, around, place.Chunk, id.Type == RecordTypes.Voxel ? id : null);
    }

    private void SetPreview(Control content)
    {
        foreach (var child in _preview.GetChildren())
        {
            _preview.RemoveChild(child);
            if (child != _model && child != _map)
                child.QueueFree();
        }
        if (content != _model)
            _model.Clear();
        _preview.AddChild(content);
    }
}

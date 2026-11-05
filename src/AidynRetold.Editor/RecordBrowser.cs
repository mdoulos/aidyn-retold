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
/// The editor's view of the installed plugins (ADR 0010): every record and asset by type, in a side panel with the type and the
/// search over it, and a record's preview, merged fields and what names it beside. Record IDs are links, with back and forward
/// (the toolbar's buttons, Alt+Left and Alt+Right, or the mouse's side buttons).
/// </summary>
/// <remarks>
/// It reads plugins only. The ROM is the import dialog's (<see cref="ImportDialog"/>), which the editor shows before the browser
/// loads anything when there's no current import, and which is also the game's first run (ADR 0001, ADR 0012, ADR 0043).
/// </remarks>
public partial class RecordBrowser : Control
{
    private const string Preview = "Preview", Fields = "Fields", UsedBy = "Used by", Atlas = "Atlas";

    // Back and forward, each in a box at the toolbar's left, their arrows the theme's.
    private readonly Button _back = new()
    {
        ThemeTypeVariation = "BackButton", TooltipText = "Back (Alt+Left)", Disabled = true, FocusMode = FocusModeEnum.None,
        IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(30, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };
    private readonly Button _forward = new()
    {
        ThemeTypeVariation = "ForwardButton", TooltipText = "Forward (Alt+Right)", Disabled = true, FocusMode = FocusModeEnum.None,
        IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(30, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter,
    };
    private readonly OptionButton _types = new() { TooltipText = "Type", CustomMinimumSize = new Vector2(150, 0) };
    private readonly LineEdit _search = new()
    {
        PlaceholderText = "Search records (Ctrl+F)",
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        ClearButtonEnabled = true,
        TooltipText = "Filters the list (Ctrl+F). Enter opens a record ID, or the only match; Escape clears it.",
    };
    /// <summary>What's gone wrong, in the toolbar.</summary>
    private readonly Label _status = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center, ClipText = true, ThemeTypeVariation = "ProblemLabel",
    };
    /// <summary>How many records the list shows, under it.</summary>
    private readonly Label _count = EditorStyle.DimLabel(size: 13);
    private readonly Tree _list = new();
    private readonly RecordHeader _header = new();
    /// <summary>The page's column: the header, then the tabs' pages. An actor's or a model's page takes the header over its view.</summary>
    private readonly VBoxContainer _right = new();
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
    /// <summary>Party, Characters, Enemies and Other, under the search while entities are listed; the one pressed lists its kind alone.</summary>
    private Control? _kindButtons;
    private readonly Dictionary<EntityKind, Button> _kindButtonOf = [];
    private EntityKind? _kind;
    /// <summary>The quick buttons at the toolbar's left, each with the key of its picture and its word for when there's none.</summary>
    private readonly List<(Button Button, string Art, string Text)> _quick = [];
    /// <summary>The first row the list shows, which a quick button opens when the page it would open isn't in the plugins.</summary>
    private RecordId? _first;
    /// <summary>Each entity's kind, looked up once from its record.</summary>
    private readonly Dictionary<RecordId, EntityKind> _entityKinds = [];

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
    // An edit page's held changes, carried over when its own record's page is built again: after the plugins reload for a texture or
    // a model it replaced, or when the mode changes.
    private (RecordId Id, EditPage.Held Held)? _carry;

    // A model's page can show the original a plugin replaces, from the ROM plugin alone, loaded when first asked; and the index of
    // which models use which textures, built when first asked. Both go with the plugins.
    private bool _original;
    private PluginSet? _originalPlugins;
    private Task<AidynRetold.Plugins.Assets.TextureUsage>? _usage;

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

    /// <summary>Shows the Preview, Fields, Used by or Atlas tab, unless the record has none of that kind (an actor has no atlas).</summary>
    public void SelectTab(string tab)
    {
        var index = tab.ToLowerInvariant() switch { "fields" => 1, "used-by" => 2, "atlas" => 3, _ => 0 };
        if (!_tabs.IsTabHidden(index))
            _tabs.CurrentTab = index;
    }

    /// <summary>Loads the plugins in a folder, as the runtime does: <c>aidyn.rom</c> first, then the ACRE plugin (ADRs 0039, 0045), then the mods in <see cref="PluginSet.LoadOrder"/>.</summary>
    public async Task LoadAsync(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        // The type listed and the game shown stay through a reload, as after a save.
        var listed = _types.GetSelectedId() is > 0 and var t && t <= _typeNames.Count ? _typeNames[t - 1] : null;
        var oldGame = _game;
        _catalog = null;
        _entityKinds.Clear();
        (_original, _originalPlugins, _usage) = (false, null, null);
        _map.Clear();
        _models?.Dispose();
        _models = null;
        _quests = null;
        _current = null;
        _history.Clear();
        _position = -1;
        UpdateHistoryButtons();
        _status.Text = "";
        Populate();
        ShowNothing("");
        // The editor imports before it loads (ADR 0043), so this is only a ROM plugin gone or replaced while it ran: the game would
        // refuse it, so the editor shows the same words.
        if (RomPlugin.Diagnose(pluginsDirectory) is { } problem)
        {
            _status.Text = problem;
            return;
        }

        ShowLoading();
        var generation = ++_generation;
        var loading = Task.Run(() =>
        {
            var plugins = PluginSet.LoadInstalled(pluginsDirectory, DataPaths.AcrePlugin);
            // The game refuses a set with a record it can't read; the editor shows the report and loads anyway, to fix it by.
            var report = plugins.Validate();
            return (PluginCatalog.Build(plugins), Partly(() => QuestIndex.Build(plugins), "No quest index"), Partly(() => new EditorGame(plugins), "No game"),
                Partly(() => EditorCharacters.Rules(plugins), "No character rules"), Partly(() => EditorCharacters.Items(plugins), "No items"), report);
        });
        try
        {
            await loading;
        }
        catch (Exception e)
        {
            // A plugin the set can't be made with: a bad manifest, a patch of a record no plugin has, a dependency loop.
            if (generation == _generation)
                ShowFailed(e.Message);
            return;
        }
        if (generation != _generation)
            return;
        var (catalog, quests, game, rules, items, report) = loading.Result;
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
        if (oldGame is not null && game is not null && !game.Carry(oldGame))
            _status.Text = $"{char.ToUpperInvariant(oldGame.Name[0])}{oldGame.Name[1..]} couldn't be loaded with these plugins, so pages show a new game.";

        _typeNames.Clear();
        _typeNames.AddRange(catalog.Types);
        _types.Clear();
        _types.AddItem("All types", 0);
        var counts = catalog.Entries.GroupBy(e => e.Id.Type).ToDictionary(g => g.Key, g => g.Count());
        for (var i = 0; i < _typeNames.Count; i++)
            _types.AddItem($"{_typeNames[i]} ({counts[_typeNames[i]]:N0})", i + 1);
        if (listed is not null && _typeNames.IndexOf(listed) is >= 0 and var at)
            _types.Select(_types.GetItemIndex(at + 1));
        Populate();
        ShowQuickButtons(catalog);
        ShowStart(catalog, report.IsClean ? null : report.Describe(limit: 12));
    }

    /// <summary>Lists one type's records, as picking it in the type dropdown does, or every record's for null.</summary>
    public void SelectType(string? type)
    {
        var id = type is null ? 0 : _typeNames.IndexOf(type) + 1;
        _types.Select(_types.GetItemIndex(Math.Max(id, 0)));
        Populate();
    }

    /// <summary>Lists one kind of entity, as pressing its button under the search does while entities are listed.</summary>
    public void SelectKind(EntityKind kind)
    {
        SelectType(RecordTypes.Entity);
        if (_kindButtonOf.TryGetValue(kind, out var button))
            button.ButtonPressed = true;
        _kind = kind;
        Populate();
    }

    /// <summary>Presses a quick button by its word (<c>--go map</c>), for scripts.</summary>
    public void PressQuick(string word)
    {
        if (_quick.FirstOrDefault(q => q.Text.Equals(word, StringComparison.OrdinalIgnoreCase)) is { Button: { } button })
            button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// A quick button's listing: the type, or the kind of entity, and the page opened: the one asked for if the plugins have it, else
    /// the first listed.
    /// </summary>
    private void ShowList(string type, EntityKind? kind, RecordId? open)
    {
        if (kind is { } k)
            SelectKind(k);
        else
            SelectType(type);
        var id = open is { } o && _catalog?.Entries.Any(e => e.Id == o) == true ? o : _first;
        if (id is { } go)
            Navigate(go);
    }

    /// <summary>
    /// Shows the quick buttons with the ACRE plugin's pictures (<c>acre:image/editor_map</c> and the rest, ADR 0041), or their words
    /// where no plugin has one.
    /// </summary>
    private void ShowQuickButtons(PluginCatalog catalog)
    {
        foreach (var (button, art, text) in _quick)
        {
            Texture2D? icon = null;
            if (catalog.Plugins.FindAsset(AidynRetold.Views.UiImages.OwnArtId(art), "png") is { } path)
            {
                var image = new Image();
                if (image.LoadPngFromBuffer(File.ReadAllBytes(path)) == Error.Ok)
                    icon = ImageTexture.CreateFromImage(AidynRetold.Views.Png.Mipmapped(image));
            }
            button.Icon = icon;
            button.Text = icon is null ? text : "";
            button.Visible = true;
        }
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
    private TrainingWindowPanel? TrainingWindow => _preview.GetChildren().OfType<EntityPage>().FirstOrDefault()?.Window;

    /// <summary>The barter window open on the preview: a shop's, or a container's on the map.</summary>
    private BarterWindowPanel? LootWindow => _preview.GetChildren().OfType<RecordPage>().FirstOrDefault()?.Window as BarterWindowPanel ?? _map.LootWindow;

    /// <summary>Opens the container picked on the map in the loot window, if the map shows one (<c>--roll</c>).</summary>
    public void Roll() => _map.Roll();

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
        // The toolbar, and the page right under it: its panels meet the toolbar's line, each padded at its own top.
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        var bar = EditorStyle.Row(6);
        // The quick buttons: each lists a type, or a kind of entity, and opens a page, with the ACRE plugin's picture once the plugins
        // are loaded (ShowQuickButtons), or its word without it.
        foreach (var (art, text, tip, go) in new (string, string, string, Action)[]
        {
            ("editor_map", "Map", "The map chunks, and the Overworld's map", () => ShowList(RecordTypes.Map, null, RecordId.Decimal(RecordTypes.MapSet, 0))),
            ("editor_party", "Party", "The party members, and Alaron", () => ShowList(RecordTypes.Entity, EntityKind.Party, AidynRetold.Core.GameState.Alaron)),
            ("editor_characters", "Characters", "Story characters, NPCs, trainers and shopkeepers", () => ShowList(RecordTypes.Entity, EntityKind.Character, RecordId.Hex(RecordTypes.Entity, 0x0204))),
            ("editor_enemies", "Enemies", "Monsters and enemies", () => ShowList(RecordTypes.Entity, EntityKind.Enemy, RecordId.Hex(RecordTypes.Entity, 0x0215))),
            ("editor_items", "Items", "The items, and the inventory's rules", () => ShowList(RecordTypes.Item, null, RecordTypes.Info(RecordTypes.Inventory).SingletonId)),
            ("editor_shop", "Shop", "The shops", () => ShowList(RecordTypes.Shop, null, null)),
            ("editor_weapons", "Weapons", "The weapons", () => ShowList(RecordTypes.Weapon, null, null)),
            ("editor_armor", "Armor", "Body armor", () => ShowList(RecordTypes.Armor, null, null)),
            ("editor_shield", "Shield", "The shields", () => ShowList(RecordTypes.Shield, null, null)),
        })
        {
            // As tall as the toolbar less its 3 of padding above and below, the picture filling it but for the style's 1 and 2.
            var button = new Button
            {
                Text = text, TooltipText = tip, ThemeTypeVariation = "IconButton", FocusMode = FocusModeEnum.None, ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(39, 37), Visible = false,
                TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            };
            button.Pressed += go;
            _quick.Add((button, art, text));
            bar.AddChild(button);
        }
        bar.AddChild(new VSeparator());
        bar.AddChild(_back);
        bar.AddChild(_forward);
        bar.AddChild(_status);
        bar.AddChild(new VSeparator());
        // The mod saves go to, which clicking changes (ModSetup).
        var mod = new Button
        {
            ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = "The mod your changes are saved to. Click to change it.",
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        void ShowMod() => mod.Text = CurrentMod.Id is { } id ? $"Saving to: {EditorStyle.PluginName(id)}" : "Not saving to a mod";
        ShowMod();
        CurrentMod.Changed += ShowMod;
        TreeExiting += () => CurrentMod.Changed -= ShowMod;
        mod.Pressed += () => _ = ModSetup.AskAsync(this, _pluginsDirectory, blocking: false);
        bar.AddChild(mod);
        bar.AddChild(new VSeparator());
        var light = new CheckButton
        {
            Text = "Light mode", ButtonPressed = EditorTheme.IsLight, FocusMode = FocusModeEnum.None, TooltipText = "Light or dark, kept for next time",
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        light.Toggled += on =>
        {
            EditorTheme.Use(GetWindow(), on);
            // What a page draws by code, such as a tree's colours and the timeline, is drawn again in the mode's, the list's too.
            if (_current is { } id)
            {
                _carry = HeldFor(id);
                _showing = Report(ShowAsync(id));
            }
            Populate();
        };
        bar.AddChild(light);
        var toolbar = new PanelContainer { ThemeTypeVariation = "Toolbar" };
        toolbar.AddChild(bar);
        root.AddChild(toolbar);

        // The page under the toolbar, inset from the window's edges.
        var page = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (var edge in new[] { "left", "right", "bottom" })
            page.AddThemeConstantOverride($"margin_{edge}", 6);
        root.AddChild(page);
        var split = new HSplitContainer { SplitOffsets = [520] };
        page.AddChild(split);

        _list.Columns = 3;
        _list.HideRoot = true;
        _list.ColumnTitlesVisible = true;
        _list.SelectMode = Tree.SelectModeEnum.Row;
        string[] titles = ["RECORD", "BORG", "SUMMARY"];
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
        // The side panel: the type and the search over the list, and the list's count under it, padded at its top.
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 6);
        var filters = EditorStyle.Row(6);
        filters.AddChild(_types);
        filters.AddChild(_search);
        side.AddChild(filters);
        // Under them, while entities are listed, the three kinds of entity, the party, the characters and the enemies, each an equal
        // share of the row: the one pressed lists its kind alone, and pressed again lists them all.
        var kinds = new List<Button>();
        var group = new ButtonGroup { AllowUnpress = true };
        foreach (var (kind, text, tip) in new[]
        {
            (EntityKind.Party, "Party", "The party members: the recruitable ones, and Alaron"),
            (EntityKind.Character, "Characters", "Story characters, NPCs, trainers and shopkeepers"),
            (EntityKind.Enemy, "Enemies", "Monsters and enemies"),
        })
        {
            var button = new Button { Text = text, TooltipText = tip, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
            button.Pressed += () =>
            {
                _kind = button.ButtonPressed ? kind : null;
                Populate();
            };
            kinds.Add(button);
            _kindButtonOf[kind] = button;
        }
        _kindButtons = new ButtonRows(3, [.. kinds]) { Visible = false };
        side.AddChild(_kindButtons);
        _list.SizeFlagsVertical = SizeFlags.ExpandFill;
        side.AddChild(_list);
        side.AddChild(_count);
        var sidePanel = new MarginContainer();
        sidePanel.AddThemeConstantOverride("margin_top", 6);
        sidePanel.AddChild(side);
        split.AddChild(sidePanel);

        // The record's page, one panel from the toolbar down, the header and the tabs' pages in it.
        _right.AddThemeConstantOverride("separation", 4);
        _right.AddChild(_header);
        _right.AddChild(_tabs);
        var recordPanel = new PanelContainer { ThemeTypeVariation = "PagePanel" };
        recordPanel.AddChild(_right);
        split.AddChild(recordPanel);
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
        string[] usedByTitles = ["RECORD", "FIELDS", "SUMMARY"];
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
        // A map's save, and its undoing, load the plugins again and show the page as it was, its view kept.
        _map.Saved += () =>
        {
            _map.KeepView();
            _ = Report(_current is { } id ? ReloadAt(id) : LoadAsync(_pluginsDirectory));
        };

        _back.Pressed += () => Step(-1);
        _forward.Pressed += () => Step(1);
        _types.ItemSelected += _ => Populate();
        _search.TextChanged += _ => Populate();
        _search.TextSubmitted += Submit;
    }

    private Task Report(Task task) => BackgroundTask.Report(task, e => _status.Text = e.Message);

    public override void _Input(InputEvent e)
    {
        // A dialog over the page has the keys and the mouse: back and forward under it would change the page it's acting on.
        if (!IsVisibleInTree() || EditorDialog.IsOpen(this))
            return;
        // Ctrl+F to the search box, Escape there to clear it.
        if (e is InputEventKey { Pressed: true, CtrlPressed: true, Keycode: Key.F })
        {
            _search.GrabFocus();
            _search.SelectAll();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape } && _search.HasFocus() && _search.Text.Length > 0)
        {
            _search.Clear();
            Populate();
            GetViewport().SetInputAsHandled();
            return;
        }
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

    /// <summary>Fills the list with the entries that match the type and search filters, and the kind's while entities are listed.</summary>
    private void Populate()
    {
        _list.Clear();
        _rows.Clear();
        _first = null;
        _count.Text = "";
        if (_catalog is null)
            return;
        var type = _types.GetSelectedId() is > 0 and var t ? _typeNames[t - 1] : null;
        var search = _search.Text.Trim();
        // The kinds filter entities alone: their buttons show while entities are listed, and the one pressed keeps its kind for next time.
        if (_kindButtons is { } buttons)
            buttons.Visible = type == RecordTypes.Entity;
        var kind = type == RecordTypes.Entity ? _kind : null;
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
            if ((type is not null && entry.Id.Type != type) || (kind is { } k && KindOf(entry.Id) != k) || (line is null && !Matches(entry, search)))
                continue;
            var row = _list.CreateItem(root);
            var id = entry.Id.ToString();
            // The ROM plugin's records without its namespace, which they all share; a mod's with theirs, to tell them by.
            row.SetText(0, entry.Id.Namespace == RecordId.RomNamespace ? $"{entry.Id.Type}/{entry.Id.Key}" : id);
            row.SetTooltipText(0, id);
            row.SetText(1, entry.Borg?.ToString(CultureInfo.InvariantCulture) ?? "");
            row.SetTextAlignment(1, HorizontalAlignment.Center);
            row.SetCustomColor(1, EditorStyle.Dim);
            row.SetText(2, line ?? entry.Summary);
            row.SetTooltipText(2, line ?? entry.Summary);
            row.SetMetadata(0, id);
            _rows[entry.Id] = row;
            _first ??= entry.Id;
        }
        if (_current is { } current && _rows.TryGetValue(current, out var selected))
            SelectRow(selected);
        _count.Text = $"{_rows.Count:N0} of {_catalog.Entries.Count:N0} records and assets";
    }

    /// <summary>An entity's kind, by its record, or Other for one the plugins can't read; kept once looked up.</summary>
    private EntityKind KindOf(RecordId id)
    {
        if (!_entityKinds.TryGetValue(id, out var kind))
            _entityKinds[id] = kind = Partly(() => _catalog!.Plugins.TryGet<EntityRecord>(id), "No entity kind")?.Kind ?? EntityKind.Other;
        return kind;
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
        Leave(() =>
        {
            _history.RemoveRange(_position + 1, _history.Count - _position - 1);
            _history.Add(id);
            _position = _history.Count - 1;
            Go(id);
        });
    }

    private void Step(int by)
    {
        var to = _position + by;
        if (to < 0 || to >= _history.Count)
            return;
        Leave(() =>
        {
            _position = to;
            Go(_history[to]);
        });
    }

    /// <summary>
    /// Leaves the page: at once, unless it's an edit page holding changes, which asks first, so they aren't lost without a word.
    /// Keeping them stays on the page, its row picked again in the list.
    /// </summary>
    private void Leave(Action go)
    {
        // The page moved on to shows its own problems, not the last one's.
        void Go()
        {
            _status.Text = "";
            go();
        }
        if (EditPage is not { HasChanges: true } page)
        {
            Go();
            return;
        }
        void Stay()
        {
            if (_current is { } current && _rows.TryGetValue(current, out var row))
                SelectRow(row);
            else
                _list.DeselectAll();
        }
        var n = page.ChangeCount;
        new EditorDialog("Leave without saving?",
            EditorDialog.Paragraphs($"{n} {(n == 1 ? "change" : "changes")} to {page.Name} {(n == 1 ? "isn't" : "aren't")} saved. Leaving the page loses them."),
            [
                new DialogButton("Keep Editing", DialogButtonKind.Cancel, Stay),
                new DialogButton("Discard Changes", DialogButtonKind.Danger, Go),
            ]).Open(this);
    }

    /// <summary>The changes the page holds, if it's an edit page of <paramref name="id"/> holding any, to carry over to its next page.</summary>
    private (RecordId, EditPage.Held)? HeldFor(RecordId id) => EditPage is { HasChanges: true } page && page.Record == id ? (id, page.Hold()) : null;

    private void Go(RecordId id)
    {
        // Another page shows the plugins' own, whatever the last one showed.
        _original = false;
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

    /// <summary>The loading card (<see cref="LoadingPage"/>), while the plugins load.</summary>
    private void ShowLoading()
    {
        _header.Visible = false;
        _tabs.CurrentTab = 0;
        SetPreview(new LoadingPage());
    }

    /// <summary>The start page (<see cref="StartPage"/>), before a record is picked, with the validation report's problems if any.</summary>
    private void ShowStart(PluginCatalog catalog, string? problems)
    {
        ShowNothing("");
        // The page's tabs are a record's; the start page has none.
        _header.Visible = false;
        _tabs.CurrentTab = 0;
        SetPreview(new StartPage(catalog, problems, FollowLink, SelectType));
    }

    private async Task ShowAsync(RecordId id)
    {
        if (_catalog is not { } catalog)
            return;
        _current = id;
        var carry = _carry is { } c && c.Id == id ? c.Held : null;
        _carry = null;
        PlaceHeader(inModel: false);
        _header.Visible = true;
        // An actor's, a model's and an armor's page is one page: what the tabs would hold is in its cards, so it has no tabs.
        var onePage = OnePage(id);
        _header.Tabs.Visible = !onePage;
        if (onePage)
            _tabs.CurrentTab = 0;
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
            other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel, other => catalog.Find(other)?.Summary);
        ShowUsedBy(catalog, references);
        // The atlas only on the types the quest index reads: an actor's, a texture's or a weapon's would always be empty.
        var atlas = _quests is not null && QuestIndex.Covers(id);
        if (!atlas && _tabs.CurrentTab == 3)
            _tabs.CurrentTab = 0;
        _tabs.SetTabHidden(3, !atlas);
        _header.Tabs.SetTabHidden(3, !atlas);
        if (atlas)
        {
            _atlas.Show(catalog, _quests!, id, Explore);
            SetTabTitle(3, _atlas.Count == 0 ? Atlas : $"{Atlas} ({_atlas.Count:N0})");
        }
        // The map stays up while another of its objects or maps opens, keeping the focus its keys need.
        if (!(MapFocus(plugins, id) is not null && _map.GetParent() == _preview))
            SetPreview(new Label { Text = "Loading…" });

        var preview = await BuildPreviewAsync(catalog, id, entry, json);
        if (_current != id || _catalog != catalog)
        {
            // Built for a page that's been left: it never shows, so nothing else frees it.
            if (preview is Control dropped && dropped != _model && dropped != _map)
                dropped.QueueFree();
            return;
        }
        switch (preview)
        {
            case Control control:
                SetPreview(control);
                if (control is EditPage page && carry is not null)
                    page.Restore(carry);
                break;
            case MapSetView.Focus focus when _models is { } models:
                SetPreview(_map);
                await _map.ShowAsync(catalog, models, _game, focus);
                break;
            case (byte[] glb, string animation, string note):
                PlaceHeader(inModel: OnePage(id));
                SetPreview(_model);
                var actor = id.Type != RecordTypes.Actor ? null
                    : _original && _originalPlugins?.TryGet<ActorRecord>(id) is { } originalActor ? originalActor : plugins.TryGet<ActorRecord>(id);
                var model = actor?.Scene ?? (id.Type == RecordTypes.Scene ? id : (RecordId?)null);
                _model.Show(glb, animation.Length > 0 ? animation : null, note.Length > 0 ? note : null, actor, model, original: _original);
                ShowInspector(catalog, id, actor, plugins.GetJson(id), references);
                break;
        }
    }

    /// <summary>
    /// Puts the header over the model view's view, so the inspector beside it runs to the page's top, for an actor's or a model's
    /// page; else back at the top of the page's column.
    /// </summary>
    private void PlaceHeader(bool inModel)
    {
        Node target = inModel ? _model.HeaderSlot : _right;
        _model.HeaderSlot.Visible = inModel;
        if (_header.GetParent() == target)
            return;
        _header.GetParent()?.RemoveChild(_header);
        target.AddChild(_header);
        if (!inModel)
            _right.MoveChild(_header, 0);
    }

    /// <summary>
    /// Whether a record's page is one page, with what the tabs would hold in its cards: an actor's, a model's, an armor's, a weapon's
    /// or an item's.
    /// </summary>
    private static bool OnePage(RecordId id) => id.Type is RecordTypes.Actor or RecordTypes.Scene or RecordTypes.Entity or RecordTypes.Armor or RecordTypes.Shield or RecordTypes.Weapon or RecordTypes.Item
        || RecordPaged(id.Type);

    /// <summary>The kinds whose page is the generic edit page of their fields (<see cref="RecordPage"/>): those with values worth a row each.</summary>
    private static bool RecordPaged(string type) => type is RecordTypes.Spell or RecordTypes.Skill or RecordTypes.WeaponSkill or RecordTypes.Stat or RecordTypes.Progression
        or RecordTypes.Combat or RecordTypes.Magic or RecordTypes.Camping or RecordTypes.Shop or RecordTypes.Loot or RecordTypes.Recipe or RecordTypes.Journal
        or RecordTypes.Terrain or RecordTypes.DialogEntity or RecordTypes.Trigger;

    /// <summary>
    /// Loads the plugins again, then opens the record afresh, the history kept, and an edit page's held changes with it: after a save,
    /// or its undoing, or a texture or a model replaced from the page.
    /// </summary>
    private async Task ReloadAt(RecordId id)
    {
        var carry = HeldFor(id);
        var (history, position) = (_history.ToList(), _position);
        await LoadAsync(_pluginsDirectory);
        if (_catalog is null)
            return;
        _carry = carry;
        _history.AddRange(history);
        _position = position;
        if (position >= 0 && position < history.Count && history[position] == id)
        {
            Go(id);
            await _showing;
        }
        else
        {
            await OpenAsync(id);
        }
    }

    /// <summary>The plugins couldn't be loaded: what's wrong, and <b>Load Again</b>, for after it's put right.</summary>
    private void ShowFailed(string message)
    {
        _status.Text = "The plugins couldn't be loaded.";
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        column.AddChild(new Label { Text = "The plugins couldn't be loaded", ThemeTypeVariation = "CardTitleLabel" });
        column.AddChild(Message($"{message}\n\nPut right or take out the plugin it names, then load them again."));
        var again = new Button { Text = "Load Again", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        again.Pressed += () => _ = Report(LoadAsync(_pluginsDirectory));
        column.AddChild(again);
        SetPreview(column);
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
        // A one-of-a-kind record by its type's name, such as Combat rules; another by its summary, which starts with its name.
        var title = entry is null
            ? usedBy > 0 ? "No plugin has this record yet, but records name it" : "No plugin has this record"
            : RecordTypes.TryInfo(id.Type)?.Singleton == true ? TypeGroups.Name(id.Type)
            : TypeGroups.One(id.Type) is { } one ? $"{one} {id.Key}"
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
        if (id.Type is RecordTypes.Flag)
            return _game is null ? Message("These plugins are too old for a game: import the ROM again.") : new FlagPanel(catalog, _game, id, FollowLink);
        if (id.Type == RecordTypes.Dialogue && plugins.TryGet<DialogueRecord>(id) is { } script)
            return new ScriptPanel(catalog, _game, _rules, _items, script, FollowLink);
        // An entity's, an armor's, a weapon's and an item's page is one of cards that edit it (EditPage).
        Func<Task<AidynRetold.Plugins.Assets.TextureUsage>> usage = () => _usage ??= Task.Run(() => AidynRetold.Plugins.Assets.TextureUsage.Build(catalog.Plugins));
        if (id.Type == RecordTypes.Entity && plugins.TryGet<EntityRecord>(id) is { } entity && _rules is { } rules && json is not null)
            return new EntityPage(catalog, _game, rules, _items, entity, json, catalog.ReferencesTo(id), _quests, FollowLink, () => ReloadAt(id), _models, usage);
        if (_items is { } items)
        {
            if (id.Type == RecordTypes.Armor && items.TryGet(id) is { } armorItem && plugins.TryGet<ArmorRecord>(id) is { } armor && json is not null)
                return new ArmorPage(catalog, _game, _rules, items, armorItem, armor, json, catalog.ReferencesTo(id), FollowLink, () => ReloadAt(id), _models, usage);
            if (id.Type == RecordTypes.Shield && items.TryGet(id) is { } shieldItem && plugins.TryGet<ShieldRecord>(id) is { } shield && json is not null)
                return new ShieldPage(catalog, _game, _rules, items, shieldItem, shield, json, catalog.ReferencesTo(id), FollowLink, () => ReloadAt(id), _models, usage);
            if (id.Type == RecordTypes.Weapon && items.TryGet(id) is { } weaponItem && plugins.TryGet<WeaponRecord>(id) is { } weapon && json is not null)
                return new WeaponPage(catalog, _game, _rules, items, weaponItem, weapon, json, catalog.ReferencesTo(id), FollowLink, () => ReloadAt(id), _models, usage);
            if (id.Type == RecordTypes.Item && items.TryGet(id) is { } itemItem && plugins.TryGet<ItemRecord>(id) is { } itemRecord && json is not null)
                return new ItemPage(catalog, _game, _rules, items, itemItem, itemRecord, json, catalog.ReferencesTo(id), FollowLink, () => ReloadAt(id), _models, usage);
            if (id.Type is RecordTypes.Weapon or RecordTypes.Armor or RecordTypes.Shield or RecordTypes.Item && items.TryGet(id) is { } item)
                return new ItemPanel(catalog, _game, _rules, items, item, FollowLink);
            if (id.Type == RecordTypes.Inventory && _rules is { } partyRules && plugins.Contains(RecordId.Decimal(RecordTypes.Font, 0)))
                return new PartyTabPanel(catalog, _game, partyRules, items);
        }
        if (RecordPaged(id.Type) && json is not null)
        {
            // A shop's page shows the shop's window on the editor's game, as a script opens it.
            var window = id.Type == RecordTypes.Shop && _game is { } game && plugins.TryGet<ShopRecord>(id) is { } shop
                && plugins.TryGet<EntityRecord>(shop.Shopkeeper) is { } shopkeeper
                ? BarterWindowPanel.ForShop(plugins, game, Shop.Open(shop, shopkeeper, game.Game.Party, game.Game.Party.Inventory.Catalog, game.Game.Flags))
                : null;
            return new RecordPage(catalog, id, json, catalog.ReferencesTo(id), _quests, await ImagesNamedAsync(catalog, json), window, FollowLink, () => ReloadAt(id), _models, usage);
        }
        if (id.Type == RecordTypes.Actor && json is not null)
        {
            // An actor is its scene, playing its initial state's animation: the original's, as the ROM plugin has them, when asked.
            if (_original && await OriginalPluginsAsync(plugins) is { } originals && originals.GetJson(id) is { } originalJson)
                json = originalJson;
            if ((string?)json["scene"] is not { } sceneText || !RecordId.TryParse(sceneText, out var scene)
                || (_original ? plugins.FindOriginalAsset(scene, "glb") : plugins.FindAsset(scene, "glb")) is not { } path)
                return Message("The actor's model has no file.");
            var initial = (int?)json["initialState"] ?? 0;
            var animation = (string?)json["states"]?[initial]?["animation"] is { } a && RecordId.TryParse(a, out var animationId)
                ? SceneNames.Animation(Convert.ToInt32(animationId.Key, 16))
                : "";
            var note = $"Model {scene.Key}, in its initial state ({initial}).";
            return (await File.ReadAllBytesAsync(path), animation, note);
        }
        if (MapFocus(plugins, id) is { } focus)
            return focus;
        if (entry?.Asset("glb") is { } glb)
        {
            var note = id.Type == RecordTypes.Map ? "Collision, in the chunk's own coordinates." : "";
            var path = _original && id.Type == RecordTypes.Scene && plugins.FindOriginalAsset(id, "glb") is { } originalPath ? originalPath : glb.Path;
            return (await File.ReadAllBytesAsync(path), "", note);
        }
        if (entry?.Asset("png") is { } png)
        {
            var image = await LoadPngAsync(png.Path);
            return image is null ? Message("Godot couldn't read the PNG.")
                : ImageGallery.Create([new ImageGallery.Item($"{image.GetWidth()}×{image.GetHeight()}", image)]);
        }
        if (json is not null && await ImagesNamedAsync(catalog, json) is { Count: > 0 } images)
            return ImageGallery.Create(images);
        if (json is null)
            return Message(entry is null ? "No plugin has it, so there's nothing to show. The Used by tab lists what names it." : "");
        // A record with no page of its own shows its fields here, as the Fields tab does.
        var fields = new RecordFieldsView { SizeFlagsVertical = SizeFlags.ExpandFill };
        fields.Show(json, field => plugins.Setters(id, field), other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel,
            other => catalog.Find(other)?.Summary);
        fields.Opened += FollowLink;
        return fields;
    }

    /// <summary>The armor's, weapon's or item's page, if one is showing, for scripts.</summary>
    public EditPage? EditPage => _preview.GetChildren().OfType<EditPage>().FirstOrDefault();

    /// <summary>The model page's inspector, if a model's page is showing.</summary>
    public ModelInspector? Inspector => _model.Inspector.GetChildren().OfType<ModelInspector>().FirstOrDefault();

    /// <summary>
    /// The model view's inspector, for an actor's or a scene's model, with what names the record as its first card and its fields
    /// after the model's; none for a map's collision or anything else.
    /// </summary>
    private void ShowInspector(PluginCatalog catalog, RecordId id, ActorRecord? actor, JsonObject? json, IReadOnlyList<RecordReference> references)
    {
        foreach (var child in _model.Inspector.GetChildren())
        {
            _model.Inspector.RemoveChild(child);
            child.QueueFree();
        }
        _model.InspectorVisible = id.Type is RecordTypes.Actor or RecordTypes.Scene;
        if (!_model.InspectorVisible)
            return;
        var inspector = new ModelInspector(new ModelContext
        {
            Catalog = catalog, View = _model, Scene = actor?.Scene ?? id, ActorId = actor is null ? null : id, Open = FollowLink, Models = _models,
            Original = _original,
            ShowOriginal = original =>
            {
                _original = original;
                _showing = Report(ShowAsync(id));
            },
            Reload = () => ReloadAt(id),
            Usage = () => _usage ??= Task.Run(() => AidynRetold.Plugins.Assets.TextureUsage.Build(catalog.Plugins)),
        });
        _model.Inspector.AddChild(inspector);
        var usedBy = references.Select(r => r.From).Distinct().Count();
        inspector.AddSection("Used by", UsedByList.Create(catalog, references, FollowLink), usedBy == 0 ? "" : $"{usedBy:N0} {(usedBy == 1 ? "record" : "records")}",
            flush: true, first: true);
        if (json is not null)
        {
            var fields = new RecordFieldsView { ThemeTypeVariation = "FlushTree", ColumnTitlesVisible = false };
            fields.Show(json, field => catalog.Plugins.Setters(id, field), other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel,
                other => catalog.Find(other)?.Summary);
            fields.Opened += FollowLink;
            // As high as the rows it shows, up to nine, growing as a branch opens.
            inspector.AddSection("Fields", new FittedTable(fields, 9), $"{json.Count - 1} fields", flush: true);
        }
    }

    /// <summary>The ROM plugin alone, for the original of a record a later plugin patches, loaded once when first asked.</summary>
    private async Task<PluginSet?> OriginalPluginsAsync(PluginSet plugins)
    {
        if (_originalPlugins is null && plugins.Plugins.Count > 0)
        {
            var directory = plugins.Plugins[0].Directory;
            _originalPlugins = await Task.Run(() => PluginSet.Load([directory]));
        }
        return _originalPlugins;
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
        // What's shown already stays, keeping its focus.
        if (_preview.GetChildCount() == 1 && _preview.GetChild(0) == content)
            return;
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

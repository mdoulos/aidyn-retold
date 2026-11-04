using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// What the editor shows before a record is picked (editor.md#plugins): what's loaded, the places to start, every type of
/// record by its ID's spelling, under a heading, with its count, the plugins in load order with what each is, and the keys that find
/// things.
/// </summary>
public partial class StartPage : ScrollContainer
{
    /// <summary>The places most edits start, if the plugins have them: a record, its name on the card, and what it holds.</summary>
    private static readonly (string Id, string Name, string About)[] Places =
    [
        ("aidyn:mapset/0", "Overworld", "Chunks, objects and collision"),
        ("aidyn:entity/0x023F", "Alaron", "Stats, skills, gear and body"),
        ("aidyn:inventory/0", "Party", "The Party tab, and carrying"),
        ("aidyn:worldmap/0", "World map", "Its pieces and towns"),
        ("aidyn:combat/0", "Combat rules", "Allies, fleeing and bosses"),
        ("aidyn:magic/0", "Magic rules", "Casting, resisting, spell levels"),
        ("aidyn:newgame/0", "New game", "Where it starts, who leads"),
        ("aidyn:title/0", "Title screen", "The fly-throughs behind the menu"),
    ];

    private readonly PluginCatalog _catalog;
    private readonly Action<RecordId> _open;
    private readonly Action<string> _showType;
    private readonly VBoxContainer _body = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly List<(string Id, Label Role)> _roles = [];

    /// <param name="problems">The validation report's description, if a record can't be read, which the page leads with.</param>
    /// <param name="showType">Lists a type's records, as picking it in the type dropdown does.</param>
    public StartPage(PluginCatalog catalog, string? problems, Action<RecordId> open, Action<string> showType)
    {
        _catalog = catalog;
        _open = open;
        _showType = showType;
        HorizontalScrollMode = ScrollMode.Disabled;
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        _body.AddThemeConstantOverride("separation", 10);
        margin.AddChild(_body);
        AddChild(margin);
        Build(problems);
        CurrentMod.Changed += ShowRoles;
        TreeExiting += () => CurrentMod.Changed -= ShowRoles;
    }

    private void Build(string? problems)
    {
        var plugins = _catalog.Plugins.Plugins;
        _body.AddChild(new Label { Text = "Aidyn Chronicles Retold Editor", ThemeTypeVariation = "TitleLabel" });
        _body.AddChild(EditorStyle.DimLabel($"{_catalog.Entries.Count:N0} records and assets from {plugins.Count} {(plugins.Count == 1 ? "plugin" : "plugins")}."));
        if (problems is not null)
        {
            var box = new PanelContainer { ThemeTypeVariation = "ChangedChip" };
            box.AddChild(new Label
            {
                Text = $"{problems}\nThe game refuses to load these; the editor shows them as they are, to fix them by.",
                ThemeTypeVariation = "ProblemLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 0),
            });
            _body.AddChild(box);
        }

        Heading("Start with");
        var places = new HFlowContainer();
        places.AddThemeConstantOverride("h_separation", 10);
        places.AddThemeConstantOverride("v_separation", 10);
        foreach (var (text, name, about) in Places)
        {
            if (RecordId.TryParse(text, out var id) && _catalog.Find(id) is not null)
                places.AddChild(Card(id, name, about));
        }
        _body.AddChild(places);

        Heading("Every type");
        var counts = _catalog.Entries.GroupBy(e => e.Id.Type).ToDictionary(g => g.Key, g => g.Count());
        var types = new GridContainer { Columns = 2 };
        types.AddThemeConstantOverride("h_separation", 18);
        types.AddThemeConstantOverride("v_separation", 2);
        foreach (var group in TypeGroups.For(counts.Keys))
        {
            var name = EditorStyle.DimLabel(group.Name);
            name.CustomMinimumSize = new Vector2(130, 0);
            name.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            name.VerticalAlignment = VerticalAlignment.Top;
            types.AddChild(name);
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 14);
            foreach (var type in group.Types)
                flow.AddChild(TypeLink(type, counts[type.Type]));
            types.AddChild(flow);
        }
        _body.AddChild(types);

        Heading("Plugins, in load order");
        var list = new GridContainer { Columns = 3 };
        list.AddThemeConstantOverride("h_separation", 14);
        list.AddThemeConstantOverride("v_separation", 6);
        foreach (var plugin in plugins)
        {
            var id = plugin.Manifest.Id;
            var chip = EditorStyle.Chip(EditorStyle.PluginName(id), id);
            chip.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            list.AddChild(chip);
            var text = EditorStyle.Row(8);
            text.AddChild(new Label { Text = plugin.Manifest.Name });
            text.AddChild(EditorStyle.DimLabel(plugin.Manifest.Import is { } import
                ? $"imported from {import.RomVersion} by importer {import.Importer}" : $"version {plugin.Manifest.Version}"));
            list.AddChild(text);
            var role = EditorStyle.DimLabel();
            _roles.Add((id, role));
            list.AddChild(role);
        }
        _body.AddChild(list);
        ShowRoles();
        var conflicts = _catalog.Plugins.Conflicts.Count();
        if (conflicts > 0)
            _body.AddChild(EditorStyle.DimLabel($"{conflicts:N0} {(conflicts == 1 ? "field is" : "fields are")} patched by more than one plugin; acre conflicts lists them."));

        Heading("Finding things");
        var keys = new GridContainer { Columns = 2 };
        keys.AddThemeConstantOverride("h_separation", 18);
        keys.AddThemeConstantOverride("v_separation", 4);
        foreach (var (key, does) in new[]
        {
            ("Ctrl+F", "Search the list: an ID, a name, a Borg number or a line a script says"),
            ("Enter", "Open the ID typed, such as aidyn:mapset/0, or the only match"),
            ("Escape", "Clear the search"),
            ("Alt+Left, Alt+Right", "Back and forward, as the mouse's side buttons do"),
            ("Click a record's ID", "Copy it"),
        })
        {
            keys.AddChild(new Label { Text = key, ThemeTypeVariation = "ChipLabel" });
            keys.AddChild(EditorStyle.DimLabel(does));
        }
        _body.AddChild(keys);
    }

    private void Heading(string text)
    {
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _body.AddChild(new Label { Text = text, ThemeTypeVariation = "HeadingLabel" });
    }

    /// <summary>A place to start: a button with the record's name, and what it holds under it, dim.</summary>
    private Button Card(RecordId id, string name, string about)
    {
        var card = new Button { CustomMinimumSize = new Vector2(230, 58), TooltipText = $"Open {id}." };
        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        text.AddThemeConstantOverride("separation", 0);
        text.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, 12);
        text.AddChild(new Label { Text = name, ThemeTypeVariation = "DialogTitleLabel", MouseFilter = MouseFilterEnum.Ignore });
        var dim = EditorStyle.DimLabel(about, 12);
        dim.MouseFilter = MouseFilterEnum.Ignore;
        dim.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        text.AddChild(dim);
        card.AddChild(text);
        card.Pressed += () => _open(id);
        return card;
    }

    /// <summary>A type as its IDs spell it, as the engine knows it, which lists its records, and its count, dim.</summary>
    private Control TypeLink(TypeName type, int count)
    {
        var row = EditorStyle.Row(4);
        var link = new Button
        {
            Text = type.Type, ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = $"{type.Name}: list them.",
        };
        link.Pressed += () => _showType(type.Type);
        row.AddChild(link);
        row.AddChild(EditorStyle.DimLabel($"{count:N0}", 12));
        return row;
    }

    /// <summary>What each plugin is: the original game, the remade game (a base plugin), or a mod, and the one changes are saved to.</summary>
    private void ShowRoles()
    {
        var basePlugins = DataPaths.BasePlugins is { } folder ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar : null;
        foreach (var (id, label) in _roles)
        {
            if (!IsInstanceValid(label))
                continue;
            var directory = _catalog.Plugins.Plugins.First(p => p.Manifest.Id == id).Directory;
            var role = id == RecordId.RomNamespace ? "The original game"
                : basePlugins is not null && Path.GetFullPath(directory).StartsWith(basePlugins, StringComparison.OrdinalIgnoreCase) ? "The remade game" : "Mod";
            var saving = CurrentMod.Id == id;
            label.Text = saving ? $"{role}, saving here" : role;
            label.ThemeTypeVariation = saving ? "ChangedLabel" : "DimLabel";
        }
    }
}

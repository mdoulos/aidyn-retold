using System.Globalization;
using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// An armor's, a weapon's or an item's page (editor.md#an-items-page), all of it on one page as cards, with no tabs: the thing itself,
/// its picture, name and values to change, held until <b>Save</b> writes them to the mod as one change (<see cref="PluginFiles.Write"/>);
/// its details as the game shows them; what names it; and every field as the plugins merge it. Beside them, where it has one, its
/// model, in a panel of its own the page's height, with its textures under it. <see cref="ArmorPage"/>, <see cref="WeaponPage"/> and
/// <see cref="ItemPage"/> give their own rows, and say whether a model goes with it and where the game draws it.
/// </summary>
/// <remarks>A page builds itself once its own fields are set (<see cref="Build"/>), as its rows read them.</remarks>
public abstract partial class ItemEditPage : HSplitContainer
{
    private const float LabelWidth = 260, PictureSize = 64;
    // The picture dialog's tiles, nine across, 6 apart, as wide as its words, its edges as far from the first and the last.
    protected const int PictureTile = 56, PictureColumns = 9, PictureGap = 6, PictureGrid = PictureColumns * PictureTile + (PictureColumns - 1) * PictureGap;
    // How far the grid's scroll reaches into the dialog's right margin, for its bar, which is about 4 wide.
    private const int DialogBarRoom = 8;

    protected readonly PluginCatalog Catalog;
    protected readonly Item Item;
    private readonly Func<Task> _reload;
    private readonly Dictionary<string, JsonNode?> _changes = [];
    private readonly VBoxContainer _rows = new();
    private readonly Label _count = EditorStyle.DimLabel();
    private readonly Button _discard = new() { Text = "Discard", Disabled = true, TooltipText = "Put every value back as it is" };
    private readonly Button _save = new() { Text = "Save", ThemeTypeVariation = "PrimaryButton", Disabled = true, TooltipText = "Save the changes to your mod" };
    private TextureRect? _picture;
    private Label? _pictureId;
    // A picture from a file, which saving writes as the mod's own image and points the record at.
    private byte[]? _png;
    // What the panel beside the cards needs, to build it again as the model, or what decides whether there's one, changes.
    private readonly AidynRetold.Views.Models? _models;
    private readonly Action<RecordId> _open;
    private readonly Func<Task<TextureUsage>> _usage;
    private Control? _side;
    // The Model row, where the model can be changed, and what it names.
    private Control? _modelRow;
    private Label? _modelName;

    protected ItemEditPage(PluginCatalog catalog, Item item, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
    {
        (Catalog, Item, _open, _reload, _models, _usage) = (catalog, item, open, reload, models, usage);
    }

    // ---- What a page says of its record ------------------------------------------------------------------------------------------

    /// <summary>The record's ID, its name and picture as the record has them, and its model, if it has one.</summary>
    protected abstract RecordId Id { get; }

    protected abstract string RecordName { get; }

    protected abstract RecordId Icon { get; }

    protected abstract RecordId? RecordModel { get; }

    /// <summary>The card's title: what it is, as Shield, Weapon or Ring.</summary>
    protected abstract string CardTitle { get; }

    /// <summary>The records file a mod's patch of it goes in when the mod doesn't define it: <c>armor.json</c> and the like.</summary>
    protected abstract string RecordsFile { get; }

    /// <summary>What it's called in words, for a picture's file dialog and the mod's own image's name: armor, weapon, item.</summary>
    protected abstract string Noun { get; }

    /// <summary>The rows after its picture and name, and before what it gives: its own values.</summary>
    protected abstract void AddValueRows();

    /// <summary>Whether the panel beside the cards shows its model now, as its values read: a shield's, a weapon's, an item's that has one.</summary>
    protected virtual bool ShowsModel => false;

    /// <summary>Whether its Model row shows now, where the model can be changed.</summary>
    protected virtual bool ShowsModelRow => ShowsModel;

    /// <summary>Where the game draws its model, for the Model row's words: on the arm, in the hand.</summary>
    protected virtual string ModelPlace => "";

    /// <summary>What has the models a Model row picks from, in words: the game's shields, the game's weapons.</summary>
    protected virtual string ModelOwners => "";

    /// <summary>Every model its kind of record has, with the name of each record that has it, for the Model row to pick from.</summary>
    protected virtual IEnumerable<(RecordId Model, string Name, RecordId Of)> ModelUsers() => [];

    /// <summary>What the panel says of it with no model, and why that matters in the game.</summary>
    protected virtual string NoModelText => "No model.";

    // ---- Building the page -------------------------------------------------------------------------------------------------------

    /// <summary>Builds the page: its card, the panel beside it, its details in the game, what names it and its fields.</summary>
    protected void Build(EditorGame? game, CharacterRules? rules, ItemCatalog items, JsonObject json, IReadOnlyList<RecordReference> references)
    {
        var cards = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(column);
        cards.AddChild(margin);
        AddChild(cards);

        column.AddChild(EditCard());
        UpdateSide();
        column.AddChild(InGame(game, rules, items));
        var usedBy = references.Select(r => r.From).Distinct().Count();
        column.AddChild(new Section("Used by", UsedByList.Create(Catalog, references, _open), foldable: false, flush: true)
        {
            Note = usedBy == 0 ? "" : $"{usedBy:N0} {(usedBy == 1 ? "record" : "records")}",
        });
        var fields = new RecordFieldsView { ThemeTypeVariation = "FlushTree", ColumnTitlesVisible = false };
        fields.Show(json, field => Catalog.Plugins.Setters(Id, field), other => Catalog.Find(other) is not null || other.Type == RecordTypes.Voxel,
            other => Catalog.Find(other)?.Summary);
        fields.Opened += _open;
        column.AddChild(new Section("All fields", new FittedTable(fields, 12), flush: true) { Note = "as the plugins merge them", Open = false });
    }

    /// <summary>A value as it reads now: the change held for it, or the record's.</summary>
    protected bool TryChanged(string field, out JsonNode? value) => _changes.TryGetValue(field, out value);

    /// <summary>The model as it reads now, changed or not; none where it has none.</summary>
    protected RecordId? CurrentModel => _changes.TryGetValue("model", out var model)
        ? (string?)model is { } text && RecordId.TryParse(text, out var id) ? id : null
        : RecordModel;

    /// <summary>
    /// The panel beside the cards, built again for the values as they read now: its model and the model's textures, or a card saying
    /// it has none, with <b>Choose Model</b> where it can have one; nothing where its kind has no model the game draws.
    /// </summary>
    private void UpdateSide()
    {
        var model = CurrentModel;
        _modelRow?.Visible = ShowsModelRow;
        _modelName?.Text = model is { } m ? ModelName(m) : NoModelLine;
        if (_side is not null)
        {
            RemoveChild(_side);
            _side.QueueFree();
            _side = null;
        }
        if (!ShowsModel || _models is null)
            return;
        _side = model is { } shown ? ModelPanel(shown, _models) : NoModelPanel();
        AddChild(_side);
    }

    private string NoModelLine => $"None: the game draws nothing {ModelPlace}.";

    /// <summary>A model by the other records whose model it is, and its key.</summary>
    private string ModelName(RecordId model)
    {
        var others = ModelUsers().Where(u => u.Model == model && u.Of != Id).Select(u => u.Name).ToList();
        return others.Count == 0 ? $"Model {model.Key}"
            : $"Model {model.Key}, {string.Join(", ", others.Take(3))}{(others.Count > 3 ? $" and {others.Count - 3} more" : "")}'s too";
    }

    /// <summary>No model: the panel's card says so, with <b>Choose Model</b> where the model can be changed.</summary>
    private Control NoModelPanel()
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 12);
        body.AddChild(new Label
        {
            Text = NoModelText, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(280, 0), ThemeTypeVariation = "DimLabel",
        });
        if (ShowsModelRow)
        {
            var choose = new Button { Text = "Choose Model", TooltipText = $"One of the models {ModelOwners} have" };
            choose.Pressed += PickModel;
            body.AddChild(new ButtonRows(choose));
        }
        return SidePanel(new Section("Model", body, foldable: false) { Note = "none" });
    }

    /// <summary>The panel's cards, in a column that scrolls when they're taller than the page, 6 in from its right and foot.</summary>
    private static Control SidePanel(params Control[] cards)
    {
        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        foreach (var card in cards)
            column.AddChild(card);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(column);
        var panel = new ScrollContainer { CustomMinimumSize = new Vector2(400, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(margin);
        return panel;
    }

    /// <summary>
    /// The model (a scene, <c>aidyn:scene/0x…</c>, not an actor: it has no states), in a panel beside the cards: a card the panel's
    /// height, the view framed close to its edges, turned by dragging and zoomed by scrolling, with <b>Export Model</b>,
    /// <b>Replace Model</b> and <b>Open Model</b> in a row under it; and under that the model's textures, which export and replace as
    /// a model's page's do (<see cref="TexturesSection"/>, reading the model's file with no view).
    /// </summary>
    private Control ModelPanel(RecordId model, AidynRetold.Views.Models models)
    {
        var path = Catalog.Plugins.FindAsset(model, "glb");
        var glb = path is null ? null : ReadGlb(path);
        var ctx = new ModelContext
        {
            Catalog = Catalog, Scene = model, Open = _open, Reload = _reload, Usage = _usage, Models = models, ShowOriginal = _ => { }, Glb = glb,
        };

        var thumb = new ModelThumb { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 280), TooltipText = "Drag to turn it, scroll to zoom" };
        // Framed once it's in the tree, where its bounds can be measured.
        thumb.Ready += () => thumb.Show(Catalog.Plugins, models, model);
        var framed = new PanelContainer { ThemeTypeVariation = "Frame", SizeFlagsVertical = SizeFlags.ExpandFill };
        framed.AddChild(thumb);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        body.AddChild(framed);
        var go = new Button { Text = "Open Model", TooltipText = $"Open {model}: its parts, and its original where a mod replaces it" };
        go.Pressed += () => _open(model);
        body.AddChild(new ButtonRows(3, [.. ModelSection.FileButtons(this, ctx), go]));
        // The view stands 8 in from the card's edges, closer than a card's body pads.
        var inset = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            inset.AddThemeConstantOverride($"margin_{side}", 8);
        inset.AddChild(body);
        var provider = path is null ? null : Catalog.Plugins.PluginWith(path);
        var modelCard = new Section("Model", inset, foldable: false, flush: true, fill: true)
        {
            Note = provider is null ? model.Key : $"{model.Key} · {EditorStyle.PluginName(provider)}",
        };

        // The model's card takes what height its textures' card leaves.
        var panel = glb is null ? SidePanel(modelCard) : SidePanel(modelCard, TexturesCard(ctx));
        // The parsed file goes with the panel, which a change of model builds again.
        panel.TreeExiting += () => glb?.Dispose();
        return panel;
    }

    private static Section TexturesCard(ModelContext ctx)
    {
        var textures = new TexturesSection(ctx);
        return new Section("Textures", textures) { Note = textures.Summary };
    }

    /// <summary>A model's glTF, parsed, or none where it can't be read.</summary>
    private static AidynRetold.Views.GlbScene? ReadGlb(string path)
    {
        try
        {
            return AidynRetold.Views.GlbScene.Parse(File.ReadAllBytes(path));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            GD.PrintErr($"Couldn't read {path}: {e.Message}");
            return null;
        }
    }

    // ---- In the game -------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Its details exactly as the game shows them when C-down is held on it (<see cref="MenuDetailsBuilder.Item"/>, the menus' own): its
    /// icon and name, then the game's own words and numbers, or a potion's, a scroll's or another item's description, and armor's totals
    /// for the editor's first party member, now and with it on. Armor and weapons have no description in the game.
    /// </summary>
    private Section InGame(EditorGame? game, CharacterRules? rules, ItemCatalog items)
    {
        var plugins = Catalog.Plugins;
        var wearer = rules is null ? null : EditorCharacters.CopyParty(plugins, rules, game, items).Members.FirstOrDefault();
        var details = MenuDetailsBuilder.Item(new ItemInstance(Item), wearer, MenuText.From(plugins), plugins.TryGet<SpellRecord>, items);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        box.AddThemeConstantOverride("separation", 6);
        if (details is null)
        {
            box.AddChild(EditorStyle.DimLabel("The game shows no details for it: a key item, or an item with no description."));
        }
        else
        {
            if (details.Title is { } title)
            {
                var head = EditorStyle.Row(10);
                if (details.Icon is { } icon && Texture(icon) is { } texture)
                {
                    head.AddChild(new TextureRect
                    {
                        Texture = texture, CustomMinimumSize = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = texture.GetWidth() <= 32 ? TextureFilterEnum.Nearest : TextureFilterEnum.LinearWithMipmaps,
                    });
                }
                head.AddChild(new Label { Text = title, ThemeTypeVariation = "FieldLabel" });
                box.AddChild(head);
            }
            foreach (var line in details.Lines)
                box.AddChild(DetailRow(line));
            if (details.Footer.Count > 0)
            {
                box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
                foreach (var line in details.Footer)
                    box.AddChild(DetailRow(line));
            }
        }
        // Armor's totals are the member's the details are shown for: the editor's first, in its game.
        var who = wearer is null ? null : game is { IsNewGame: false } ? wearer.Name : $"{wearer.Name}, new game";
        var totals = Item.Armor is not null && details?.Footer.Count > 0;
        return new Section("In the game", box, foldable: false) { Note = who is null || !totals ? "" : $"totals for {who}" };
    }

    /// <summary>A line of the details: its pieces from the left, indented where the game indents it, and its pieces at the right.</summary>
    private Control DetailRow(DetailLine line)
    {
        var row = EditorStyle.Row(4);
        if (line.Indent)
            row.AddChild(new Control { CustomMinimumSize = new Vector2(16, 0) });
        foreach (var piece in line.Left)
            row.AddChild(DetailPieceControl(piece, line.Wrap));
        if (line.Right.Count > 0)
        {
            row.AddChild(EditorStyle.Spacer());
            foreach (var piece in line.Right)
                row.AddChild(DetailPieceControl(piece, false));
        }
        return row;
    }

    /// <summary>A piece of a line: its text in the theme's ink, a gain green and a loss red, or its image at the game's size.</summary>
    private Control DetailPieceControl(DetailPiece piece, bool wrap)
    {
        switch (piece)
        {
            case DetailText text:
                var label = new Label { Text = text.Text };
                if (text.Color == MenuDetailsBuilder.Gain)
                    label.ThemeTypeVariation = "GoodLabel";
                else if (text.Color == MenuDetailsBuilder.Loss)
                    label.ThemeTypeVariation = "ProblemLabel";
                if (wrap)
                {
                    label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                }
                return label;
            case DetailImage image:
                var size = image.Size ?? 16;
                return new TextureRect
                {
                    Texture = Texture(image.Image), CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = image.Image.ToString(),
                };
            default:
                return new Control();
        }
    }

    // ---- The card ----------------------------------------------------------------------------------------------------------------

    /// <summary>The card: its title with the changes and Save at its right, then a row for each value.</summary>
    private Control EditCard()
    {
        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        var head = new PanelContainer { ThemeTypeVariation = "CardHead" };
        var row = EditorStyle.Row(8);
        row.AddChild(new Label { Text = CardTitle, ThemeTypeVariation = "CardTitleLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(_count);
        row.AddChild(_discard);
        row.AddChild(_save);
        head.AddChild(row);
        column.AddChild(head);
        _rows.AddThemeConstantOverride("separation", 0);
        column.AddChild(_rows);
        card.AddChild(column);
        _discard.Pressed += Discard;
        _save.Pressed += Save;
        BuildRows();
        return card;
    }

    private void BuildRows()
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        _modelRow = null;
        _modelName = null;
        AddRow("Picture", PictureField(), "Drawn wherever the game shows it: the inventory, a shop, the silhouette's slot. A larger picture is drawn into the original's size.");
        var name = new LineEdit { Text = RecordName, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        name.TextChanged += text => Change("name", text, RecordName);
        AddRow("Name", name, "What the menus, the shops and the conversations call it.");
        AddValueRows();
        var bonuses = Gives().ToList();
        AddRow("What it gives", new Label
        {
            Text = bonuses.Count == 0 ? "Nothing beyond the values above." : string.Join("; ", bonuses.Select(b => char.ToUpperInvariant(b[0]) + b[1..])) + ".",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }, "Its aspect, resistances and stat bonus, which All fields below lists.");
        UpdateCount();
    }

    /// <summary>What it gives beyond the values the rows change: its aspect, a stat or skill, a spell, a resistance.</summary>
    private IEnumerable<string> Gives()
    {
        string Name(RecordId id) => (string?)Catalog.Plugins.GetJson(id)?["name"] ?? id.Key;
        var b = Item.Bonuses;
        if (b.Aspect != Aspect.None)
            yield return $"only for the {b.Aspect} aspect";
        if (b.Stat is { } stat)
            yield return $"{stat.Amount:+0;-0} {Name(stat.Stat)}";
        if (b.Skill is { } skill)
            yield return $"{skill.Amount:+0;-0} {Name(skill.Skill)}";
        if (b.Spell is { } spell)
            yield return $"casts {Name(spell.Spell)} at level {spell.Level}";
        if (b.Enchantment is { } enchantment)
            yield return $"{Name(enchantment.Spell)} at level {enchantment.Level} " + (Item.Kind == ItemKind.Weapon ? "as it strikes" : "while worn");
        if (b.Resistance is { } resistance)
            yield return $"takes {resistance.Damage.ToString(CultureInfo.InvariantCulture)} × {resistance.Element} damage";
    }

    /// <summary>A row of the card: its label in a column of its own, its control, and a line under that saying what it is.</summary>
    protected Control AddRow(string label, Control field, string help)
    {
        var box = new PanelContainer { ThemeTypeVariation = _rows.GetChildCount() == 0 ? "CardRowFirst" : "CardRow" };
        var row = EditorStyle.Row(24);
        row.AddChild(new Label
        {
            Text = label, ThemeTypeVariation = "FieldLabel", CustomMinimumSize = new Vector2(LabelWidth, 0), SizeFlagsVertical = SizeFlags.ShrinkBegin,
        });
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 6);
        stack.AddChild(field);
        stack.AddChild(new Label
        {
            Text = help, ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(240, 0),
        });
        row.AddChild(stack);
        box.AddChild(row);
        _rows.AddChild(box);
        return box;
    }

    /// <summary>A whole number in a spin box, from <paramref name="min"/> to <paramref name="max"/>, held as <paramref name="field"/>.</summary>
    protected SpinBox Number(string field, int value, int min, int max, string suffix = "")
    {
        var spin = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = 1, Rounded = true, Value = value, Suffix = suffix.Trim(),
            CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SelectAllOnFocus = true,
        };
        spin.ValueChanged += v => Change(field, (int)v, value);
        return spin;
    }

    /// <summary>One of an enum's values, by name as the record writes it, held as <paramref name="field"/>.</summary>
    protected OptionButton Choice<TEnum>(string field, TEnum value, IEnumerable<TEnum> values, Func<TEnum, string>? label = null) where TEnum : struct, Enum
    {
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        var list = values.ToList();
        foreach (var v in list)
            choice.AddItem(label?.Invoke(v) ?? Words(v.ToString()));
        choice.Select(Math.Max(list.IndexOf(value), 0));
        choice.ItemSelected += i => Change(field, list[(int)i].ToString(), value.ToString());
        return choice;
    }

    /// <summary>On or off, held as <paramref name="field"/>.</summary>
    protected CheckBox Switch(string field, bool value, string text)
    {
        var box = new CheckBox { Text = text, ButtonPressed = value, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        box.Toggled += on => Change(field, on, value);
        return box;
    }

    /// <summary>Text over several lines, held as <paramref name="field"/>, none where it's left empty.</summary>
    protected TextEdit Paragraph(string field, string? value)
    {
        var edit = new TextEdit
        {
            Text = value ?? "", WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(300, 96),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ScrollFitContentHeight = true,
        };
        edit.TextChanged += () => Change(field, edit.Text.Length == 0 ? null : edit.Text, value);
        return edit;
    }

    /// <summary>An enum value's name in words: CloakOfDarkness as Cloak of darkness.</summary>
    protected static string Words(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    // ---- The model ---------------------------------------------------------------------------------------------------------------

    /// <summary>The Model row: the model the game draws, by the other records that share it, with <b>Change Model</b>.</summary>
    protected Control AddModelRow(string help)
    {
        var row = EditorStyle.Row(16);
        _modelName = new Label
        {
            Text = CurrentModel is { } model ? ModelName(model) : NoModelLine, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(160, 0),
        };
        row.AddChild(_modelName);
        var change = new Button { Text = "Change Model", SizeFlagsHorizontal = SizeFlags.ShrinkEnd, TooltipText = $"One of the models {ModelOwners} have, or none" };
        change.Pressed += PickModel;
        row.AddChild(change);
        _modelRow = AddRow("Model", row, help);
        _modelRow.Visible = ShowsModelRow;
        return _modelRow;
    }

    /// <summary>
    /// The models to pick from: every model its kind of record has, each by the records that have it, and none; the one picked shows
    /// beside the list, and <b>Use Model</b> holds it as the model until <b>Save</b>, as a double click does (<c>--item-model 1</c>).
    /// </summary>
    public void PickModel()
    {
        if (!ShowsModelRow)
            return;
        var plugins = Catalog.Plugins;
        var models = ModelUsers().GroupBy(u => u.Model).OrderBy(g => g.Min(u => u.Name), StringComparer.Ordinal).ToList();
        var list = new Tree { ThemeTypeVariation = "FlushTree", Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row };
        list.SetColumnExpand(1, false);
        list.SetColumnCustomMinimumWidth(1, 90);
        list.SetColumnClipContent(0, true);
        var root = list.CreateItem();
        var none = list.CreateItem(root);
        none.SetText(0, "None");
        none.SetCustomColor(0, EditorStyle.Dim);
        none.SetMetadata(0, "");
        var current = CurrentModel;
        TreeItem? selected = current is null ? none : null;
        foreach (var group in models)
        {
            var names = string.Join(", ", group.Select(u => u.Name).Distinct());
            var row = list.CreateItem(root);
            row.SetText(0, names);
            row.SetTooltipText(0, $"{names}: {group.Key}");
            row.SetText(1, group.Key.Key);
            row.SetCustomColor(1, EditorStyle.Dim);
            row.SetTextAlignment(1, HorizontalAlignment.Right);
            row.SetMetadata(0, group.Key.ToString());
            if (group.Key == current)
                selected = row;
        }
        var box = new PanelContainer { ThemeTypeVariation = "ListBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(new FittedTable(list, 9, inBox: true));
        var thumb = new ModelThumb { CustomMinimumSize = new Vector2(240, 240), SizeFlagsHorizontal = SizeFlags.ShrinkEnd, TooltipText = "Drag to turn it, scroll to zoom" };
        var framed = new PanelContainer { ThemeTypeVariation = "Frame", SizeFlagsVertical = SizeFlags.ShrinkBegin };
        framed.AddChild(thumb);
        var pick = EditorStyle.Row(12);
        pick.AddChild(box);
        pick.AddChild(framed);
        RecordId? Picked() => list.GetSelected()?.GetMetadata(0).AsString() is { Length: > 0 } text && RecordId.TryParse(text, out var id) ? id : null;
        void Preview()
        {
            if (_models is null || !thumb.IsInsideTree())
                return;
            if (Picked() is { } model)
            {
                thumb.Show(plugins, _models, model);
            }
            else
            {
                // An empty view for none, its frame kept.
                thumb.Clear();
                thumb.Visible = true;
            }
        }
        list.ItemSelected += Preview;
        thumb.Ready += () =>
        {
            if (selected is not null)
            {
                selected.Select(0);
                list.ScrollToItem(selected);
            }
            Preview();
        };
        EditorDialog? dialog = null;
        void Use()
        {
            Change("model", Picked()?.ToString(), RecordModel?.ToString());
            dialog?.Close();
        }
        list.ItemActivated += Use;
        dialog = new EditorDialog("Change the model",
            [.. EditorDialog.Paragraphs($"The model the game draws {ModelPlace}: one of the models {ModelOwners} have, or none. Saving puts it in your mod.", PictureGrid), pick],
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("Use Model", DialogButtonKind.Primary, Use, Closes: false),
            ], width: PictureGrid + 2 * EditorDialog.Inset);
        dialog.Open(this);
    }

    // ---- The picture -------------------------------------------------------------------------------------------------------------

    /// <summary>The picture, in a well, with Change Picture beside it and its image's ID under that.</summary>
    private Control PictureField()
    {
        var row = EditorStyle.Row(16);
        var well = new PanelContainer { ThemeTypeVariation = "Well", SizeFlagsVertical = SizeFlags.ShrinkBegin };
        _picture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(PictureSize, PictureSize), TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        };
        well.AddChild(_picture);
        row.AddChild(well);
        var side = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        side.AddThemeConstantOverride("separation", 6);
        var change = new Button { Text = "Change Picture", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        change.Pressed += PickPicture;
        side.AddChild(change);
        _pictureId = EditorStyle.DimLabel();
        side.AddChild(_pictureId);
        row.AddChild(side);
        ShowPicture(Texture(Icon), Icon.ToString());
        return row;
    }

    private void ShowPicture(Texture2D? texture, string what)
    {
        if (_picture is not null)
        {
            _picture.Texture = texture;
            // A picture at the original's size is drawn pixel for pixel; a larger one, smoothed.
            _picture.TextureFilter = texture is not null && texture.GetWidth() <= 32 ? TextureFilterEnum.Nearest : TextureFilterEnum.LinearWithMipmaps;
        }
        _pictureId?.Text = what;
    }

    /// <summary>An image record's PNG as a texture, mipmapped, or null if no plugin has it.</summary>
    protected Texture2D? Texture(RecordId image) =>
        Catalog.Find(image)?.Asset("png") is { } png && Png.Load(png.Path) is { } data ? ImageTexture.CreateFromImage(Png.Mipmapped(data)) : null;

    /// <summary>
    /// The pictures to pick from: every picture the game's weapons, armor and items have, and a PNG of one's own, which saving writes
    /// into the mod as its own image.
    /// </summary>
    public void PickPicture()
    {
        var icons = Catalog.Plugins.Ids.Where(i => i.Type is RecordTypes.Armor or RecordTypes.Weapon or RecordTypes.Item)
            .Select(i => (string?)Catalog.Plugins.GetJson(i)?["icon"]).OfType<string>().Distinct()
            .Select(t => RecordId.TryParse(t, out var r) ? r : (RecordId?)null).OfType<RecordId>().OrderBy(r => r.ToString(), StringComparer.Ordinal).ToList();
        var grid = new GridContainer { Columns = PictureColumns };
        grid.AddThemeConstantOverride("h_separation", PictureGap);
        grid.AddThemeConstantOverride("v_separation", PictureGap);
        EditorDialog? dialog = null;
        foreach (var icon in icons)
        {
            var texture = Texture(icon);
            if (texture is null)
                continue;
            var button = new Button
            {
                Icon = texture, ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(PictureTile, PictureTile),
                TooltipText = icon.ToString(), TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            };
            button.Pressed += () =>
            {
                _png = null;
                ShowPicture(texture, icon.ToString());
                Change("icon", icon.ToString(), Icon.ToString());
                dialog?.Close();
            };
            grid.AddChild(button);
        }
        // The grid as wide as the dialog's words, so its edges are as far from the first column as from the last; the scroll bar
        // stands in the margin past it, the scroll reaching into the margin by a little more than the bar.
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 4 * (PictureTile + PictureGap) + PictureTile / 2), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.AddChild(grid);
        var gutter = new MarginContainer();
        gutter.AddThemeConstantOverride("margin_right", -DialogBarRoom);
        gutter.AddChild(scroll);
        dialog = new EditorDialog("Change the picture",
            [.. EditorDialog.Paragraphs("Pick one of the pictures the game's weapons, armor and items have, or a PNG of your own, which saving puts in your mod.", PictureGrid), gutter],
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("From a File", DialogButtonKind.Secondary, () =>
                {
                    dialog!.Close();
                    PickFile();
                }, Closes: false),
            ], width: PictureGrid + 2 * EditorDialog.Inset);
        dialog.Open(this);
    }

    private void PickFile()
    {
        var files = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Filesystem, UseNativeDialog = true,
            Title = $"A picture for the {Noun}", Filters = ["*.png ; PNG pictures"],
        };
        files.FileSelected += path =>
        {
            var bytes = File.ReadAllBytes(path);
            if (Png.Load(path) is not { } image)
            {
                EditorDialog.Tell(this, "That isn't a picture the editor can read", $"{path} isn't a PNG Godot can read.");
                return;
            }
            _png = bytes;
            ShowPicture(ImageTexture.CreateFromImage(Png.Mipmapped(image)), $"{Path.GetFileName(path)}, {image.GetWidth()}×{image.GetHeight()}, saved into your mod");
            Change("icon", "file:" + path, Icon.ToString());
        };
        files.VisibilityChanged += () =>
        {
            if (!files.Visible)
                files.QueueFree();
        };
        GetTree().Root.AddChild(files);
        files.PopupCentered(new Vector2I(800, 500));
    }

    // ---- Changes -----------------------------------------------------------------------------------------------------------------

    /// <summary>Holds a field's new value, or lets it go when it's back to the record's.</summary>
    protected void Change(string field, JsonNode? value, JsonNode? original)
    {
        if (JsonNode.DeepEquals(value, original))
            _changes.Remove(field);
        else
            _changes[field] = value;
        UpdateCount();
        if (field == "model" || ChangesModelPanel(field))
            UpdateSide();
    }

    /// <summary>Whether a change to a field decides whether the panel shows a model: armor's slot.</summary>
    protected virtual bool ChangesModelPanel(string field) => false;

    private void UpdateCount()
    {
        var n = _changes.Count;
        _count.Text = n == 0 ? "" : n == 1 ? "1 change" : $"{n} changes";
        _discard.Disabled = _save.Disabled = n == 0;
    }

    /// <summary>Holds new values as if typed into the rows, for scripts (<c>--item-change</c>): a number as a number, else text.</summary>
    public void ChangeFor(IEnumerable<(string Field, string Value)> pairs)
    {
        var json = Catalog.Plugins.GetJson(Id);
        foreach (var (field, value) in pairs)
            Change(field, int.TryParse(value, out var n) ? n : value, json?[field]?.DeepClone());
    }

    /// <summary>Saves what's held, as Save does (<c>--item-save 1</c>).</summary>
    public void SaveChanges()
    {
        if (_changes.Count > 0)
            Save();
    }

    private void Discard()
    {
        _changes.Clear();
        _png = null;
        BuildRows();
        UpdateSide();
    }

    /// <summary>
    /// Writes the changes to the mod as one: into the mod's own record of it, if it defines it, else into its patch of it, in its
    /// records file (<see cref="RecordsFile"/>), merged with what the patch already sets; a picture from a file as the mod's own image
    /// beside it.
    /// </summary>
    private void Save()
    {
        var changes = new Dictionary<string, JsonNode?>(_changes);
        var png = _png;
        var id = Id;
        var name = changes.TryGetValue("name", out var renamed) && (string?)renamed is { Length: > 0 } n ? n : RecordName;
        PluginFiles.Write(this, Catalog.Plugins, name, folder =>
        {
            if (png is not null)
            {
                var image = folder.Own(RecordTypes.Image, $"{Noun}_{id.Key.ToLowerInvariant()}");
                folder.WriteAsset(image, "png", png);
                changes["icon"] = image.ToString();
            }
            var file = folder.FileDefining(id) ?? RecordsFile;
            var record = folder.Record(file, id) ?? new JsonObject { ["patch"] = id.ToString() };
            foreach (var (field, value) in changes)
                record[field] = value?.DeepClone();
            folder.WriteRecord(file, record);
        }, _reload);
    }
}

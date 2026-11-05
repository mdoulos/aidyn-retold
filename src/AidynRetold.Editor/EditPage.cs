using System.Text.Json.Nodes;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A record's page that edits it (editor.md#an-items-page): all of it on one page as cards, with no tabs: the thing itself, its picture,
/// name and values to change, held until <b>Save</b> writes them to the mod as one change (<see cref="PluginFiles.Write"/>); the cards
/// its kind adds; what names it; and every field as the plugins merge it. Beside them, where it has one, its model, in a panel of its
/// own the page's height, with its textures under it. <see cref="ItemEditPage"/> is the weapons', armor's, shields' and items' pages,
/// and <see cref="EntityPage"/> an entity's.
/// </summary>
/// <remarks>A page builds itself once its own fields are set (<see cref="Build"/>), as its rows read them.</remarks>
public abstract partial class EditPage : HSplitContainer
{
    /// <summary>How wide the rows' label column is: narrower on a page whose rows need the room, as an entity's columns do.</summary>
    protected virtual float LabelWidth => 260;
    // The picture dialog's tiles, nine across, 6 apart, as wide as its words, its edges as far from the first and the last.
    protected const int PictureTile = 56, PictureColumns = 9, PictureGap = 6, PictureGrid = PictureColumns * PictureTile + (PictureColumns - 1) * PictureGap;
    // How far the grid's scroll reaches into the dialog's right margin, for its bar, which is about 4 wide.
    private const int DialogBarRoom = 8;

    protected readonly PluginCatalog Catalog;
    private readonly Func<Task> _reload;
    private readonly Dictionary<string, JsonNode?> _changes = [];
    private readonly VBoxContainer _rows = new();
    private readonly Label _count = EditorStyle.DimLabel();
    private readonly Button _discard = new() { Text = "Discard", Disabled = true, TooltipText = "Put every value back as it is" };
    private readonly Button _save = new() { Text = "Save", ThemeTypeVariation = "PrimaryButton", Disabled = true, TooltipText = "Save the changes to your mod" };
    private TextureRect? _picture;
    private Label? _pictureId;
    // A picture from a file, which saving writes as the mod's own image and points the record at, and what the page says of it.
    private byte[]? _png;
    private string? _pngText;
    private TabBar? _tabs;
    // What the panel beside the cards needs, to build it again as the model, or what decides whether there's one, changes.
    private readonly AidynRetold.Views.Models? _models;
    private readonly Action<RecordId> _open;
    private readonly Func<Task<TextureUsage>> _usage;
    private Control? _side;
    private ScrollContainer? _cards;
    // The Model row, where the model can be changed, and what it names.
    private Control? _modelRow;
    private Label? _modelName;

    protected EditPage(PluginCatalog catalog, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models, Func<Task<TextureUsage>> usage)
    {
        (Catalog, _open, _reload, _models, _usage) = (catalog, open, reload, models, usage);
    }

    /// <summary>The changes a page holds, to carry over to the next page of the same record: its values, and a picture from a file.</summary>
    public sealed record Held(IReadOnlyDictionary<string, JsonNode?> Changes, byte[]? Png, string? PngText);

    /// <summary>The record the page edits.</summary>
    public RecordId Record => Id;

    /// <summary>Its name as the page has it now, changed or not.</summary>
    public string CurrentName => Text(Shown("name", RecordName)) is { Length: > 0 } name ? name : RecordName;

    /// <summary>Whether the page holds changes not saved yet, and how many.</summary>
    public bool HasChanges => _changes.Count > 0;

    public int ChangeCount => _changes.Count;

    /// <summary>The changes held, by field, as the rows read them.</summary>
    protected IReadOnlyDictionary<string, JsonNode?> Changes => _changes;

    /// <summary>What opens a record from the page: its links.</summary>
    protected Action<RecordId> Open => _open;

    /// <summary>The changes held, to carry over (<see cref="Restore"/>).</summary>
    public Held Hold() => new(_changes.ToDictionary(c => c.Key, c => c.Value?.DeepClone()), _png, _pngText);

    /// <summary>Holds another page's changes as this one's, as if typed again, but those the record now has anyway.</summary>
    public void Restore(Held held)
    {
        var json = Catalog.Plugins.GetJson(Id);
        _changes.Clear();
        foreach (var (field, value) in held.Changes)
        {
            if (!JsonNode.DeepEquals(value, json?[field]))
                _changes[field] = value?.DeepClone();
        }
        (_png, _pngText) = _changes.ContainsKey(PictureFieldName) ? (held.Png, held.PngText) : (null, null);
        _tabs?.SetTabTitle(0, CardTitle);
        BuildRows();
        UpdateSide();
    }

    // ---- What a page says of its record ------------------------------------------------------------------------------------------

    /// <summary>The record's ID, its name and picture as the record has them, and its model, if it has one.</summary>
    protected abstract RecordId Id { get; }

    protected abstract string RecordName { get; }

    /// <summary>Its picture as the record has it: an item's icon, an entity's portrait; none where it has none.</summary>
    protected abstract RecordId? Icon { get; }

    protected abstract RecordId? RecordModel { get; }

    /// <summary>The card's title: what it is, as Shield, Weapon or Ring.</summary>
    protected abstract string CardTitle { get; }

    /// <summary>The records file a mod's patch of it goes in when the mod doesn't define it: <c>armor.json</c> and the like.</summary>
    protected abstract string RecordsFile { get; }

    /// <summary>What it's called in words, for a picture's file dialog and the mod's own image's name: armor, weapon, item.</summary>
    protected abstract string Noun { get; }

    /// <summary>The rows after its picture and name: its own values.</summary>
    protected abstract void AddValueRows();

    /// <summary>The rows after its values, if its kind has any.</summary>
    protected virtual void AddLastRows()
    {
    }

    /// <summary>The field its picture is: an item's <c>icon</c>, an entity's <c>portrait</c>.</summary>
    protected virtual string PictureFieldName => "icon";

    /// <summary>What the Picture row says under its picture.</summary>
    protected virtual string PictureHelp => "Drawn wherever the game shows it.";

    /// <summary>The pictures its kind can pick from: every picture the game's records of its kind have.</summary>
    protected virtual IEnumerable<RecordId> PictureSources() => [];

    /// <summary>What the picture dialog says, over the pictures.</summary>
    protected virtual string PictureDialogText => "Pick one of the game's pictures, or a PNG of your own, which saving puts in your mod.";

    /// <summary>Whether the panel beside the cards shows its model now, as its values read: a shield's, a weapon's, an item's that has one.</summary>
    protected virtual bool ShowsModel => false;

    /// <summary>Whether the card starts with a Picture row: not for a kind with no picture, such as a loot table.</summary>
    protected virtual bool ShowsPicture => true;

    /// <summary>Whether the card has a Name row: not for a kind the game doesn't name, such as a shop, which its shopkeeper names.</summary>
    protected virtual bool ShowsName => true;

    /// <summary>Whether the name and the picture share the card's first row, the name first: an entity's do.</summary>
    protected virtual bool NameBesidePicture => false;

    /// <summary>The card's other tabs, after its own: each a page shown in the card's body in place of the rows.</summary>
    protected virtual IEnumerable<(string Title, Control Page)> ExtraTabs => [];

    /// <summary>
    /// A held value as the patch writes it: as it is, unless the kind has to say more, as a keyed list says which elements it removes.
    /// <paramref name="beneath"/> is the record as the plugins merge it without the mod, where that's known.
    /// </summary>
    protected virtual JsonNode? Patched(string field, JsonNode? value, JsonObject? beneath, JsonNode? existing, bool patch) => value;

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

    /// <summary>How large its picture is drawn in its well.</summary>
    protected virtual float PictureSize => 64;

    /// <summary>The model's outfits, where its actor has more than one: the model card's arrows dress it in each in turn.</summary>
    protected virtual IReadOnlyList<ActorOutfit>? Outfits => null;

    /// <summary>Which outfit the model is dressed in first.</summary>
    protected virtual int FirstOutfit(IReadOnlyList<ActorOutfit> outfits) => 0;

    // ---- Building the page -------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the page: its card, the panel beside it, the cards its kind adds (<paramref name="between"/>), what names it and its
    /// fields.
    /// </summary>
    protected void Build(JsonObject json, IReadOnlyList<RecordReference> references, IEnumerable<Control> between)
    {
        var cards = _cards = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
        foreach (var card in between)
            column.AddChild(card);
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

    /// <summary>A value as a row shows it: the change held for it, else the record's own.</summary>
    protected JsonNode? Shown(string field, JsonNode? value) => _changes.TryGetValue(field, out var held) ? held : value;

    /// <summary>A value's text, or null where it's none or not text.</summary>
    protected static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue(out string? text) ? text : null;

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
        var outfits = Outfits;
        if (outfits is not null && _outfit < 0)
            _outfit = Math.Clamp(FirstOutfit(outfits), 0, outfits.Count - 1);
        // Framed once it's in the tree, where its bounds can be measured, and dressed in its outfit.
        thumb.Ready += () =>
        {
            thumb.Show(Catalog.Plugins, models, model);
            if (outfits is not null)
                thumb.Dress(outfits, _outfit);
        };
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
        var textures = glb is null ? null : new TexturesSection(ctx);
        if (outfits is not null)
        {
            // The outfits' arrows in the card's head, before its note: each dresses the model and limits the textures to the outfit's.
            var which = EditorStyle.DimLabel();
            void Wear(int index)
            {
                _outfit = ((index % outfits.Count) + outfits.Count) % outfits.Count;
                which.Text = $"outfit {_outfit + 1} of {outfits.Count}";
                thumb.Dress(outfits, _outfit);
                textures?.Limit(glb is null ? null : ImagesOf(glb, outfits[_outfit].Meshes));
            }
            var back = new Button { Text = "◀", ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = "The outfit before" };
            var next = new Button { Text = "▶", ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = "The next outfit" };
            back.Pressed += () => Wear(_outfit - 1);
            next.Pressed += () => Wear(_outfit + 1);
            var at = modelCard.HeadRow.GetChildCount() - 1;
            foreach (var control in new Control[] { back, which, next })
            {
                modelCard.HeadRow.AddChild(control);
                modelCard.HeadRow.MoveChild(control, at++);
            }
            thumb.Ready += () => Wear(_outfit);
        }

        // The model's card takes what height its textures' card leaves.
        var panel = textures is null ? SidePanel(modelCard) : SidePanel(modelCard, new Section("Textures", textures) { Note = textures.Summary });
        // The parsed file goes with the panel, which a change of model builds again.
        panel.TreeExiting += () => glb?.Dispose();
        return panel;
    }

    // Which outfit the model is dressed in, kept as the panel is built again; none chosen yet below 0.
    private int _outfit = -1;

    /// <summary>
    /// The model's images that <paramref name="meshes"/>, nodes of its scene by name, draw: through each node's mesh, its primitives'
    /// materials and their colour and emissive textures. Null where the file names none of them, so nothing is limited.
    /// </summary>
    private static IReadOnlySet<int>? ImagesOf(AidynRetold.Views.GlbScene glb, IReadOnlyList<string> meshes)
    {
        var root = glb.Reader.Root;
        if (!root.TryGetProperty("nodes", out var nodes) || !root.TryGetProperty("meshes", out var meshList))
            return null;
        var wanted = meshes.ToHashSet();
        var images = new HashSet<int>();
        root.TryGetProperty("materials", out var materials);
        root.TryGetProperty("textures", out var textures);
        foreach (var node in nodes.EnumerateArray())
        {
            if (!node.TryGetProperty("name", out var name) || !wanted.Contains(name.GetString() ?? "") || !node.TryGetProperty("mesh", out var meshIndex))
                continue;
            var mesh = meshList[meshIndex.GetInt32()];
            if (!mesh.TryGetProperty("primitives", out var primitives))
                continue;
            foreach (var primitive in primitives.EnumerateArray())
            {
                if (!primitive.TryGetProperty("material", out var materialIndex) || materials.ValueKind != System.Text.Json.JsonValueKind.Array)
                    continue;
                var material = materials[materialIndex.GetInt32()];
                foreach (var slot in new[] { material.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorTexture", out var colour) ? colour : default,
                    material.TryGetProperty("emissiveTexture", out var emissive) ? emissive : default })
                {
                    if (slot.ValueKind == System.Text.Json.JsonValueKind.Object && slot.TryGetProperty("index", out var textureIndex)
                        && textures.ValueKind == System.Text.Json.JsonValueKind.Array && textures[textureIndex.GetInt32()].TryGetProperty("source", out var source))
                        images.Add(source.GetInt32());
                }
            }
        }
        return images.Count == 0 ? null : images;
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

    // ---- The card ----------------------------------------------------------------------------------------------------------------

    /// <summary>The card: its title with the changes and Save at its right, then a row for each value.</summary>
    private Control EditCard()
    {
        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        var head = new PanelContainer { ThemeTypeVariation = "CardHead" };
        var row = EditorStyle.Row(8);
        var tabs = _tabs = new TabBar { ClipTabs = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        tabs.AddTab(CardTitle);
        var pages = new List<Control>();
        foreach (var (title, page) in ExtraTabs)
        {
            tabs.AddTab(title);
            page.Visible = false;
            pages.Add(page);
        }
        row.AddChild(tabs);
        row.AddChild(_count);
        row.AddChild(_discard);
        row.AddChild(_save);
        head.AddChild(row);
        column.AddChild(head);
        _rows.AddThemeConstantOverride("separation", 0);
        column.AddChild(_rows);
        foreach (var page in pages)
        {
            var inset = new MarginContainer { Visible = false };
            foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
                inset.AddThemeConstantOverride(side, 16);
            page.Visible = true;
            inset.AddChild(page);
            column.AddChild(inset);
        }
        tabs.TabChanged += tab =>
        {
            _rows.Visible = tab == 0;
            for (var i = 0; i < pages.Count; i++)
                pages[i].GetParent<Control>().Visible = tab == i + 1;
        };
        card.AddChild(column);
        _discard.Pressed += Discard;
        _save.Pressed += Save;
        BuildRows();
        return card;
    }

    /// <summary>Builds the card's rows again, as the values read now.</summary>
    protected void BuildRows()
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        _modelRow = null;
        _modelName = null;
        const string nameHelp = "What the menus, the shops and the conversations call it.";
        if (ShowsPicture && ShowsName && NameBesidePicture)
        {
            var both = EditorStyle.Row(16);
            var name = NameField();
            (name.CustomMinimumSize, name.SizeFlagsHorizontal) = (new Vector2(160, 0), SizeFlags.ExpandFill);
            both.AddChild(name);
            both.AddChild(PictureField(controlsFirst: true));
            AddRow("Name", both, nameHelp);
        }
        else
        {
            if (ShowsPicture)
                AddRow("Picture", PictureField(controlsFirst: false), PictureHelp);
            if (ShowsName)
                AddRow("Name", NameField(), nameHelp);
        }
        AddValueRows();
        AddLastRows();
        UpdateCount();
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
        if (help.Length > 0)
        {
            stack.AddChild(new Label
            {
                Text = help, ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(240, 0),
            });
        }
        row.AddChild(stack);
        box.AddChild(row);
        _rows.AddChild(box);
        return box;
    }

    /// <summary>A whole number in a spin box, from <paramref name="min"/> to <paramref name="max"/>, held as <paramref name="field"/>.</summary>
    protected SpinBox Number(string field, int value, int min, int max, string suffix = "")
    {
        // A number typed counts as it's typed, so Save is there without Enter first.
        var spin = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = 1, Rounded = true, Suffix = suffix.Trim(), UpdateOnTextChanged = true,
            CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SelectAllOnFocus = true,
            Value = Shown(field, value) is JsonValue held && held.TryGetValue(out int n) ? n : value,
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
        var shown = Text(Shown(field, value.ToString())) is { } name && Enum.TryParse<TEnum>(name, out var held) ? held : value;
        choice.Select(Math.Max(list.IndexOf(shown), 0));
        choice.ItemSelected += i => Change(field, list[(int)i].ToString(), value.ToString());
        return choice;
    }

    /// <summary>On or off, held as <paramref name="field"/>.</summary>
    protected CheckBox Switch(string field, bool value, string text)
    {
        var shown = Shown(field, value) is JsonValue held && held.TryGetValue(out bool on) ? on : value;
        var box = new CheckBox { Text = text, ButtonPressed = shown, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        box.Toggled += on => Change(field, on, value);
        return box;
    }

    /// <summary>Text over several lines, held as <paramref name="field"/>, none where it's left empty.</summary>
    protected TextEdit Paragraph(string field, string? value)
    {
        var edit = new TextEdit
        {
            Text = _changes.ContainsKey(field) ? Text(_changes[field]) ?? "" : value ?? "", WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(300, 96),
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

    /// <summary>The Name box, held as <c>name</c>.</summary>
    private LineEdit NameField()
    {
        var name = new LineEdit { Text = CurrentName, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        name.TextChanged += text => Change("name", text, RecordName);
        return name;
    }

    /// <summary>The picture, in a well, with Change Picture beside it and its image's ID under that: after the picture, or before it.</summary>
    private Control PictureField(bool controlsFirst)
    {
        var row = EditorStyle.Row(16);
        var well = new PanelContainer { ThemeTypeVariation = "Well", SizeFlagsVertical = SizeFlags.ShrinkBegin, TooltipText = controlsFirst ? PictureHelp : "" };
        _picture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(PictureSize, PictureSize), TextureFilter = TextureFilterEnum.LinearWithMipmaps,
        };
        well.AddChild(_picture);
        var side = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        side.AddThemeConstantOverride("separation", 6);
        var change = new Button { Text = "Change Picture", SizeFlagsHorizontal = controlsFirst ? SizeFlags.ShrinkEnd : SizeFlags.ShrinkBegin };
        change.Pressed += PickPicture;
        side.AddChild(change);
        _pictureId = EditorStyle.DimLabel();
        _pictureId.HorizontalAlignment = controlsFirst ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        side.AddChild(_pictureId);
        if (controlsFirst)
        {
            row.AddChild(side);
            row.AddChild(well);
        }
        else
        {
            row.AddChild(well);
            row.AddChild(side);
        }
        // The picture held, if there's one: a file's, or another of the game's.
        var held = Text(Shown(PictureFieldName, null));
        if (held is not null && held.StartsWith("file:", StringComparison.Ordinal) && _png is not null && FromPng(_png) is { } file)
            ShowPicture(file, _pngText ?? held[5..]);
        else if (held is not null && RecordId.TryParse(held, out var picked))
            ShowPicture(Texture(picked), picked.ToString());
        else if (Icon is { } icon)
            ShowPicture(Texture(icon), icon.ToString());
        else
            ShowPicture(null, "None");
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

    /// <summary>A PNG's bytes as a texture, mipmapped, or null if it can't be read.</summary>
    private static Texture2D? FromPng(byte[] png)
    {
        var image = new Image();
        return image.LoadPngFromBuffer(png) == Error.Ok ? ImageTexture.CreateFromImage(Png.Mipmapped(image)) : null;
    }

    /// <summary>An image record's PNG as a texture, mipmapped, or null if no plugin has it.</summary>
    protected Texture2D? Texture(RecordId image) =>
        Catalog.Find(image)?.Asset("png") is { } png && Png.Load(png.Path) is { } data ? ImageTexture.CreateFromImage(Png.Mipmapped(data)) : null;

    /// <summary>
    /// The pictures to pick from: every picture the game's records of its kind have (<see cref="PictureSources"/>), and a PNG of one's
    /// own, which saving writes into the mod as its own image.
    /// </summary>
    public void PickPicture()
    {
        var icons = PictureSources().Distinct().OrderBy(r => r.ToString(), StringComparer.Ordinal).ToList();
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
                Change(PictureFieldName, icon.ToString(), Icon?.ToString());
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
            [.. EditorDialog.Paragraphs(PictureDialogText, PictureGrid), gutter],
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
            if (Png.Load(path) is not { } image)
            {
                EditorDialog.Tell(this, "That isn't a picture the editor can read", $"{path} isn't a PNG Godot can read.");
                return;
            }
            // The game lays a picture out at its own size, and a mod's own has no original to be drawn into, so it's fitted to the
            // original's as it's saved.
            var (width, height) = Icon is { } icon ? Catalog.Plugins.ImageSize(icon) : (0, 0);
            var fitted = Fitted(image, width, height);
            var size = fitted == image ? $"{image.GetWidth()}×{image.GetHeight()}" : $"{image.GetWidth()}×{image.GetHeight()} fitted to {width}×{height}";
            _png = fitted.SavePngToBuffer();
            _pngText = $"{Path.GetFileName(path)}, {size}, saved into your mod";
            ShowPicture(FromPng(_png), _pngText);
            Change(PictureFieldName, "file:" + path, Icon?.ToString());
        };
        files.VisibilityChanged += () =>
        {
            if (!files.Visible)
                files.QueueFree();
        };
        GetTree().Root.AddChild(files);
        files.PopupCentered(new Vector2I(800, 500));
    }

    /// <summary>
    /// A picture at <paramref name="width"/> × <paramref name="height"/>: scaled to fit, its shape kept, and centred, clear round it;
    /// itself where it's that size already, or there's no size to fit.
    /// </summary>
    private static Image Fitted(Image image, int width, int height)
    {
        if (width <= 0 || height <= 0 || (image.GetWidth() == width && image.GetHeight() == height))
            return image;
        var scale = Math.Min((float)width / image.GetWidth(), (float)height / image.GetHeight());
        var (w, h) = (Math.Max(1, (int)MathF.Round(image.GetWidth() * scale)), Math.Max(1, (int)MathF.Round(image.GetHeight() * scale)));
        var copy = (Image)image.Duplicate();
        copy.Convert(Image.Format.Rgba8);
        copy.Resize(w, h, Image.Interpolation.Lanczos);
        var canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        canvas.BlitRect(copy, new Rect2I(0, 0, w, h), new Vector2I((width - w) / 2, (height - h) / 2));
        return canvas;
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
        _tabs?.SetTabTitle(0, CardTitle);
        if (field == "model" || ChangesModelPanel(field))
            UpdateSide();
    }

    /// <summary>Whether a change to a field decides whether the panel shows a model. None does now that shields are their own type.</summary>
    protected virtual bool ChangesModelPanel(string field) => false;

    private void UpdateCount()
    {
        var n = _changes.Count;
        _count.Text = n == 0 ? "" : n == 1 ? "1 change" : $"{n} changes";
        _discard.Disabled = _save.Disabled = n == 0;
    }

    /// <summary>
    /// Holds new values as if typed into the rows, for scripts (<c>--item-change</c>): a number as a number, true and false as a
    /// switch's, else text. The rows show them.
    /// </summary>
    public void ChangeFor(IEnumerable<(string Field, string Value)> pairs)
    {
        var json = Catalog.Plugins.GetJson(Id);
        foreach (var (field, value) in pairs)
            Change(field, int.TryParse(value, out var n) ? n : bool.TryParse(value, out var on) ? on : value, json?[field]?.DeepClone());
        BuildRows();
    }

    /// <summary>Shows the card's tab with a title, such as As Trainer, for a screenshot (<c>--page-tab</c>); a title it hasn't leaves the card as it is.</summary>
    public void ShowTab(string title)
    {
        if (_tabs is null)
            return;
        for (var i = 0; i < _tabs.TabCount; i++)
        {
            if (string.Equals(_tabs.GetTabTitle(i), title, StringComparison.OrdinalIgnoreCase))
                _tabs.CurrentTab = i;
        }
    }

    /// <summary>Scrolls the cards down by <paramref name="pixels"/>, for a screenshot (<c>--page-scroll</c>), once they're laid out.</summary>
    public void ScrollTo(int pixels) => GetTree().CreateTimer(0.1).Timeout += () => _cards?.ScrollVertical = pixels;

    /// <summary>Saves what's held, as Save does (<c>--item-save 1</c>).</summary>
    public void SaveChanges()
    {
        if (_changes.Count > 0)
            Save();
    }

    private void Discard()
    {
        _changes.Clear();
        (_png, _pngText) = (null, null);
        _tabs?.SetTabTitle(0, CardTitle);
        BuildRows();
        UpdateSide();
    }

    /// <summary>
    /// Writes the changes to the mod as one: into the mod's own record of it, if it defines it, else into its patch of it, in its
    /// records file (<see cref="RecordsFile"/>), merged with what the patch already sets; a picture from a file as the mod's own image
    /// beside it.
    /// </summary>
    /// <summary>The record as the plugins merge it without the mod, or null where they can't load without it.</summary>
    private JsonObject? Beneath(RecordId id, string mod)
    {
        try
        {
            return Catalog.Plugins.Without(mod).GetJson(id);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private void Save() => _ = SaveAsync();

    private async Task SaveAsync()
    {
        var id = Id;
        // The record beneath the mod, loaded off the main thread while the mod to save to is known and doesn't define the record.
        JsonObject? beneath = null;
        var looked = false;
        if (CurrentMod.Id is { } mod && Catalog.Plugins.DefinedBy(id) != mod)
        {
            (_save.Disabled, _discard.Disabled) = (true, true);
            beneath = await Task.Run(() => Beneath(id, mod));
            looked = true;
            if (!IsInsideTree())
                return;
            UpdateCount();
            // The mod changed while the plugins loaded: then nothing's written, and the changes stay held.
            if (CurrentMod.Id != mod)
                return;
        }
        // What's held now: a value typed while the plugins loaded counts too.
        var changes = new Dictionary<string, JsonNode?>(_changes);
        if (changes.Count == 0 && _png is null)
            return;
        var png = _png;
        var name = changes.TryGetValue("name", out var renamed) && (string?)renamed is { Length: > 0 } n ? n : RecordName;
        PluginFiles.Write(this, Catalog.Plugins, name, folder =>
        {
            if (png is not null)
            {
                var image = folder.Own(RecordTypes.Image, $"{Noun}_{id.Key.ToLowerInvariant()}");
                folder.WriteAsset(image, "png", png);
                changes[PictureFieldName] = image.ToString();
            }
            // The mod's own record, or its patch that wins, wherever it is, else a new patch in the records file of its kind.
            var file = folder.FileDefining(id) ?? folder.FilePatching(id) ?? RecordsFile;
            var record = folder.Record(file, id) ?? new JsonObject { ["patch"] = id.ToString() };
            // A patch sets only what differs from the record beneath the mod: a value put back as it was leaves the patch, and a
            // patch left with nothing goes, so the record is the mod's no more.
            if (record["patch"] is null)
                beneath = null;
            else if (!looked)
                beneath = Beneath(id, folder.Id);
            foreach (var (field, value) in changes)
            {
                var patched = Patched(field, value, beneath, record[field], record["patch"] is not null);
                if (beneath is not null && JsonNode.DeepEquals(patched, beneath[field]))
                    record.Remove(field);
                else
                    record[field] = patched?.DeepClone();
            }
            if (record.Count == 1 && record["patch"] is not null)
                folder.RemoveRecord(file, id);
            else
                folder.WriteRecord(file, record);
            // Saved: nothing's held any more, so the reload after doesn't carry it over.
            _changes.Clear();
            (_png, _pngText) = (null, null);
            UpdateCount();
        }, _reload);
    }
}

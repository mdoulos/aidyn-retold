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
/// An armor's or a shield's page, all of it on one page as cards, with no tabs (editor.md#an-armors-page): the armor itself, its
/// picture and values to change, held until <b>Save</b> writes them to the mod as one change (<see cref="PluginFiles.Write"/>); its
/// details as the game shows them; what names it; and every field as the plugins merge it. Beside them, a shield's model, the scene
/// the game hangs on a body's arm, in a panel of its own the page's height, with its textures under it.
/// </summary>
public partial class ArmorPage : HSplitContainer
{
    private const float LabelWidth = 260, PictureSize = 64;
    // The picture dialog's tiles, nine across, 6 apart, as wide as its words, its edges as far from the first and the last.
    private const int PictureTile = 56, PictureColumns = 9, PictureGap = 6, PictureGrid = PictureColumns * PictureTile + (PictureColumns - 1) * PictureGap;
    // How far the grid's scroll reaches into the dialog's right margin, for its bar, which is about 4 wide.
    private const int DialogBarRoom = 8;

    private readonly PluginCatalog _catalog;
    private readonly ArmorRecord _armor;
    private readonly Item _item;
    private readonly Func<Task> _reload;
    private readonly Dictionary<string, JsonNode?> _changes = [];
    private readonly VBoxContainer _rows = new();
    private readonly Label _count = EditorStyle.DimLabel();
    private readonly Button _discard = new() { Text = "Discard", Disabled = true, TooltipText = "Put every value back as it is" };
    private readonly Button _save = new() { Text = "Save", ThemeTypeVariation = "PrimaryButton", Disabled = true, TooltipText = "Save the changes to your mod" };
    private TextureRect? _picture;
    private Label? _pictureId;
    // A picture from a file, which saving writes as the mod's own image and points the armor at.
    private byte[]? _png;

    /// <param name="models">The models to draw a shield's from, in the panel beside the cards; none, no panel.</param>
    /// <param name="usage">Which models use which textures, for the shield's textures, built once for the plugins.</param>
    public ArmorPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, ArmorRecord armor,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
    {
        (_catalog, _armor, _item, _reload) = (catalog, armor, item, reload);
        var cards = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(column);
        cards.AddChild(margin);
        AddChild(cards);
        if (armor.Model is { } model && models is not null)
            AddChild(ModelPanel(model, models, open, usage));

        column.AddChild(EditCard());
        column.AddChild(InGame(game, rules, items));
        var usedBy = references.Select(r => r.From).Distinct().Count();
        column.AddChild(new Section("Used by", UsedByList.Create(catalog, references, open), foldable: false, flush: true)
        {
            Note = usedBy == 0 ? "" : $"{usedBy:N0} {(usedBy == 1 ? "record" : "records")}",
        });
        var fields = new RecordFieldsView { ThemeTypeVariation = "FlushTree", ColumnTitlesVisible = false };
        fields.Show(json, field => catalog.Plugins.Setters(armor.Id, field), other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel,
            other => catalog.Find(other)?.Summary);
        fields.Opened += open;
        column.AddChild(new Section("All fields", new FittedTable(fields, 12), flush: true) { Note = "as the plugins merge them", Open = false });
    }

    /// <summary>
    /// The shield's model (a scene, <c>aidyn:scene/0x…</c>, not an actor: it has no states), in a panel beside the cards: a card the
    /// panel's height, the view framed close to its edges, turned by dragging and zoomed by scrolling, with <b>Export Model</b>,
    /// <b>Replace Model</b> and <b>Open Model</b> in a row under it; and under that the model's textures, which export and replace as
    /// a model's page's do (<see cref="TexturesSection"/>, reading the model's file with no view).
    /// </summary>
    private Control ModelPanel(RecordId model, AidynRetold.Views.Models models, Action<RecordId> open, Func<Task<TextureUsage>> usage)
    {
        var path = _catalog.Plugins.FindAsset(model, "glb");
        var glb = path is null ? null : ReadGlb(path);
        TreeExiting += () => glb?.Dispose();
        var ctx = new ModelContext
        {
            Catalog = _catalog, Scene = model, Open = open, Reload = _reload, Usage = usage, Models = models, ShowOriginal = _ => { }, Glb = glb,
        };

        var thumb = new ModelThumb { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 280), TooltipText = "Drag to turn it, scroll to zoom" };
        // Framed once it's in the tree, where its bounds can be measured.
        thumb.Ready += () => thumb.Show(_catalog.Plugins, models, model);
        var framed = new PanelContainer { ThemeTypeVariation = "Frame", SizeFlagsVertical = SizeFlags.ExpandFill };
        framed.AddChild(thumb);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        body.AddChild(framed);
        var go = new Button { Text = "Open Model", TooltipText = $"Open {model}: its parts, and its original where a mod replaces it" };
        go.Pressed += () => open(model);
        body.AddChild(new ButtonRows(3, [.. ModelSection.FileButtons(this, ctx), go]));
        // The view stands 8 in from the card's edges, closer than a card's body pads.
        var inset = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
            inset.AddThemeConstantOverride($"margin_{side}", 8);
        inset.AddChild(body);
        var provider = path is null ? null : _catalog.Plugins.PluginWith(path);
        var modelCard = new Section("Model", inset, foldable: false, flush: true, fill: true)
        {
            Note = provider is null ? model.Key : $"{model.Key} · {EditorStyle.PluginName(provider)}",
        };

        var column = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        column.AddChild(modelCard);
        if (glb is not null)
        {
            var textures = new TexturesSection(ctx);
            column.AddChild(new Section("Textures", textures) { Note = textures.Summary });
        }
        // The panel scrolls when its cards are taller than the page, and the model's card takes what height is left when they aren't.
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(column);
        var panel = new ScrollContainer { CustomMinimumSize = new Vector2(400, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(margin);
        return panel;
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

    /// <summary>
    /// The armor's details exactly as the game shows them when C-down is held on it (<see cref="MenuDetailsBuilder.Item"/>, the
    /// menus' own): its icon and name, then the game's own words and numbers, and its totals for the editor's first party member,
    /// now and with it on. Armor and weapons have no description in the game; their details are these lines.
    /// </summary>
    private Section InGame(EditorGame? game, CharacterRules? rules, ItemCatalog items)
    {
        var plugins = _catalog.Plugins;
        var wearer = rules is null ? null : EditorCharacters.CopyParty(plugins, rules, game, items).Members.FirstOrDefault();
        var details = MenuDetailsBuilder.Item(new ItemInstance(_item), wearer, MenuText.From(plugins), plugins.TryGet<SpellRecord>, items);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        box.AddThemeConstantOverride("separation", 6);
        if (details is null)
        {
            box.AddChild(EditorStyle.DimLabel("The game shows no details for it."));
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
        // The totals are the member's the details are shown for: the editor's first, in its game.
        var who = wearer is null ? null : game is { IsNewGame: false } ? wearer.Name : $"{wearer.Name}, new game";
        return new Section("In the game", box, foldable: false) { Note = who is null || details?.Footer.Count is null or 0 ? "" : $"totals for {who}" };
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

    /// <summary>The armor's card: its title with the changes and Save at its right, then a row for each value.</summary>
    private Control EditCard()
    {
        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        var head = new PanelContainer { ThemeTypeVariation = "CardHead" };
        var row = EditorStyle.Row(8);
        row.AddChild(new Label { Text = _armor.Slot == ArmorSlot.Shield ? "Shield" : "Armor", ThemeTypeVariation = "CardTitleLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill });
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
        var a = _armor;
        AddRow("Picture", PictureField(), "Drawn wherever the game shows it: the inventory, a shop, the silhouette's slot. A larger picture is drawn into the original's size.");
        var name = new LineEdit { Text = a.Name, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        name.TextChanged += text => Change("name", text, a.Name);
        AddRow("Name", name, "What the menus, the shops and the conversations call it.");
        var slot = new OptionButton { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        slot.AddItem("Body", 0);
        slot.AddItem("Shield", 1);
        slot.Select(a.Slot == ArmorSlot.Shield ? 1 : 0);
        slot.ItemSelected += i => Change("slot", i == 1 ? "Shield" : "Body", a.Slot == ArmorSlot.Shield ? "Shield" : "Body");
        AddRow("Slot", slot, "Where it's worn: on the body, or on the arm as a shield.");
        AddRow("Price", Number("price", a.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        AddRow("Protection", Number("protection", a.Protection, 0, 255), "What wearing it adds to protection from damage.");
        AddRow("Defense", Number("defense", a.Defense, 0, 255), "A shield's defense, which combat reads; 0 on body armor.");
        AddRow("Dexterity", Number("dexterity", a.Dexterity, -128, 127), "What wearing it adds to modified Dexterity: a penalty, or 0.");
        AddRow("Stealth", Number("stealth", a.Stealth, -128, 127), "What wearing it adds to modified Stealth.");
        AddRow("Experience bonus", Number("expBonus", a.ExpBonus, 0, 255), "Read into the game's memory with the rest; what reads it isn't known.");
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
        string Name(RecordId id) => (string?)_catalog.Plugins.GetJson(id)?["name"] ?? id.Key;
        var b = _item.Bonuses;
        if (b.Aspect != Aspect.None)
            yield return $"only for the {b.Aspect} aspect";
        if (b.Stat is { } stat)
            yield return $"{stat.Amount:+0;-0} {Name(stat.Stat)}";
        if (b.Skill is { } skill)
            yield return $"{skill.Amount:+0;-0} {Name(skill.Skill)}";
        if (b.Spell is { } spell)
            yield return $"casts {Name(spell.Spell)} at level {spell.Level}";
        if (b.Enchantment is { } enchantment)
            yield return $"{Name(enchantment.Spell)} at level {enchantment.Level} while worn";
        if (b.Resistance is { } resistance)
            yield return $"takes {resistance.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture)} × {resistance.Element} damage";
    }

    private void AddRow(string label, Control field, string help)
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
    }

    private SpinBox Number(string field, int value, int min, int max, string suffix = "")
    {
        var spin = new SpinBox
        {
            MinValue = min, MaxValue = max, Step = 1, Rounded = true, Value = value, Suffix = suffix.Trim(),
            CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SelectAllOnFocus = true,
        };
        spin.ValueChanged += v => Change(field, (int)v, value);
        return spin;
    }

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
        ShowPicture(Texture(_armor.Icon), _armor.Icon.ToString());
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
    private Texture2D? Texture(RecordId image) =>
        _catalog.Find(image)?.Asset("png") is { } png && Png.Load(png.Path) is { } data ? ImageTexture.CreateFromImage(Png.Mipmapped(data)) : null;

    /// <summary>
    /// The pictures to pick from: every picture the game's weapons, armor and items have, and a PNG of one's own, which saving writes
    /// into the mod as its own image.
    /// </summary>
    public void PickPicture()
    {
        var icons = _catalog.Plugins.Ids.Where(i => i.Type is RecordTypes.Armor or RecordTypes.Weapon or RecordTypes.Item)
            .Select(i => (string?)_catalog.Plugins.GetJson(i)?["icon"]).OfType<string>().Distinct()
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
                Change("icon", icon.ToString(), _armor.Icon.ToString());
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
            Title = "A picture for the armor", Filters = ["*.png ; PNG pictures"],
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
            Change("icon", "file:" + path, _armor.Icon.ToString());
        };
        files.VisibilityChanged += () =>
        {
            if (!files.Visible)
                files.QueueFree();
        };
        GetTree().Root.AddChild(files);
        files.PopupCentered(new Vector2I(800, 500));
    }

    /// <summary>Holds a field's new value, or lets it go when it's back to the record's.</summary>
    private void Change(string field, JsonNode? value, JsonNode? original)
    {
        if (JsonNode.DeepEquals(value, original))
            _changes.Remove(field);
        else
            _changes[field] = value;
        UpdateCount();
    }

    private void UpdateCount()
    {
        var n = _changes.Count;
        _count.Text = n == 0 ? "" : n == 1 ? "1 change" : $"{n} changes";
        _discard.Disabled = _save.Disabled = n == 0;
    }

    /// <summary>Holds new values as if typed into the rows, for scripts (<c>--armor-change</c>): a number as a number, else text.</summary>
    public void ChangeFor(IEnumerable<(string Field, string Value)> pairs)
    {
        var json = _catalog.Plugins.GetJson(_armor.Id);
        foreach (var (field, value) in pairs)
            Change(field, int.TryParse(value, out var n) ? n : value, json?[field]?.DeepClone());
    }

    /// <summary>Saves what's held, as Save does (<c>--armor-save 1</c>).</summary>
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
    }

    /// <summary>
    /// Writes the changes to the mod as one: into the mod's own record of the armor, if it defines it, else into its patch of it, in
    /// <c>records/armor.json</c>, merged with what the patch already sets; a picture from a file as the mod's own image beside it.
    /// </summary>
    private void Save()
    {
        var changes = new Dictionary<string, JsonNode?>(_changes);
        var png = _png;
        var id = _armor.Id;
        var name = changes.TryGetValue("name", out var renamed) && (string?)renamed is { Length: > 0 } n ? n : _armor.Name;
        PluginFiles.Write(this, _catalog.Plugins, name, folder =>
        {
            if (png is not null)
            {
                var image = folder.Own(RecordTypes.Image, $"armor_{id.Key.ToLowerInvariant()}");
                folder.WriteAsset(image, "png", png);
                changes["icon"] = image.ToString();
            }
            var file = folder.FileDefining(id) ?? "armor.json";
            var record = folder.Record(file, id) ?? new JsonObject { ["patch"] = id.ToString() };
            foreach (var (field, value) in changes)
                record[field] = value?.DeepClone();
            folder.WriteRecord(file, record);
        }, _reload);
    }
}

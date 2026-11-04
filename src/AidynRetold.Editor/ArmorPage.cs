using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// An armor's or a shield's page, all of it on one page as cards, with no tabs (editor.md#an-armors-page): the armor itself, its
/// picture and values to change, held until <b>Save</b> writes them to the mod as one change (<see cref="PluginFiles.Write"/>); what
/// wearing it does on the editor's party; what names it; and every field as the plugins merge it.
/// </summary>
public partial class ArmorPage : ScrollContainer
{
    private const float LabelWidth = 260, PictureSize = 64;

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

    public ArmorPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, ArmorRecord armor,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload)
    {
        (_catalog, _armor, _item, _reload) = (catalog, armor, item, reload);
        HorizontalScrollMode = ScrollMode.Disabled;
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 16);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        margin.AddChild(column);
        AddChild(margin);

        column.AddChild(EditCard());
        // What wearing it does, the item page's own, without its header, which the card above stands for.
        var preview = new ItemPanel(catalog, game, rules, items, item, open, header: false);
        column.AddChild(new Section("In the game", preview.TakeBody(), foldable: false));
        preview.Visible = false;
        column.AddChild(preview);
        var usedBy = references.Select(r => r.From).Distinct().Count();
        column.AddChild(new Section("Used by", UsedByList.Create(catalog, references, open), foldable: false, flush: true)
        {
            Note = usedBy == 0 ? "" : $"{usedBy:N0} {(usedBy == 1 ? "record" : "records")}",
        });
        var fields = new RecordFieldsView { CustomMinimumSize = new Vector2(0, 360), ThemeTypeVariation = "FlushTree" };
        fields.Show(json, field => catalog.Plugins.Setters(armor.Id, field), other => catalog.Find(other) is not null || other.Type == RecordTypes.Voxel,
            other => catalog.Find(other)?.Summary);
        fields.Opened += open;
        column.AddChild(new Section("All fields", fields, flush: true) { Note = "as the plugins merge them", Open = false });
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
        var grid = new GridContainer { Columns = 8 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        EditorDialog? dialog = null;
        foreach (var icon in icons)
        {
            var texture = Texture(icon);
            if (texture is null)
                continue;
            var button = new Button
            {
                Icon = texture, ExpandIcon = true, IconAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(52, 52),
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
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), HorizontalScrollMode = ScrollMode.Disabled };
        scroll.AddChild(grid);
        dialog = new EditorDialog("Change the picture",
            [.. EditorDialog.Paragraphs("Pick one of the pictures the game's weapons, armor and items have, or a PNG of your own, which saving puts in your mod.", 512), scroll],
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("From a File", DialogButtonKind.Secondary, () =>
                {
                    dialog!.Close();
                    PickFile();
                }, Closes: false),
            ], width: 560);
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

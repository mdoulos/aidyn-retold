using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The page of a record whose kind has no page of its own (editor.md#a-records-page): an <see cref="EditPage"/> whose card is a row for
/// each of the record's fields, typed by the record class the plugins read it as: a number in a box, a switch, text, one of an enum's
/// names, a record as a link with a picker over the records of its type, a list of records as links, a list of numbers in boxes, a
/// list of small records (a shop's stock, a loot table's drops, a recipe's ingredients) as rows to add to and take from, and a nested
/// record of plain values as rows of its own. What it can't edit, it says so and leaves to All fields. <see cref="PageFacts"/> says
/// what the fields of the well-known kinds mean, and their bounds.
/// </summary>
public partial class RecordPage : EditPage
{
    private readonly RecordId _id;
    private readonly JsonObject _json;
    private readonly Type? _class;
    private readonly PageFacts.Kind _facts;
    private readonly Control? _window;

    /// <param name="images">The images the record names, for a card of them; none for a kind whose picture row shows its one image.</param>
    /// <param name="window">The game's own window on the record, if its kind has one (a shop's), for a card after the fields.</param>
    public RecordPage(PluginCatalog catalog, RecordId id, JsonObject json, IReadOnlyList<RecordReference> references, QuestIndex? quests,
        IReadOnlyList<ImageGallery.Item> images, Control? window, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, open, reload, models, usage)
    {
        (_id, _json, _window) = (id, json, window);
        _class = RecordTypes.TryInfo(id.Type)?.Class;
        _facts = PageFacts.For(id.Type);
        var cards = new List<Control>();
        if (window is not null)
            cards.Add(new Section("In the game", window, foldable: false) { Note = "the game's own window" });
        if (images.Count > 0 && _facts.Picture is null)
            cards.Add(new Section("Images", ImageGallery.Flow(images)) { Note = images.Count == 1 ? "1 image" : $"{images.Count} images" });
        if (quests is not null && QuestIndex.Covers(id))
        {
            var atlas = new AtlasView();
            atlas.Show(catalog, quests, id, open);
            if (atlas.Count > 0)
                cards.Add(new Section("Atlas", atlas, flush: true) { Note = $"{atlas.Count:N0} {(atlas.Count == 1 ? "entry" : "entries")}" });
            else
                atlas.QueueFree();
        }
        Build(json, references, cards);
    }

    /// <summary>The game's window on the record, if the page shows one: a shop's barter window.</summary>
    public Control? Window => _window;

    protected override RecordId Id => _id;

    protected override string RecordName => Text(_json["name"]) ?? Text(_json["title"]) ?? Catalog.Find(_id)?.Summary ?? _id.Key;

    protected override bool ShowsName => _json["name"] is JsonValue;

    protected override RecordId? Icon => _facts.Picture is { } pictureField && Text(_json[pictureField]) is { } text && RecordId.TryParse(text, out var icon) ? icon : null;

    protected override bool ShowsPicture => _facts.Picture is not null;

    protected override string PictureFieldName => _facts.Picture ?? "icon";

    protected override string PictureHelp => _facts.PictureHelp;

    protected override string PictureDialogText => $"Pick one of the pictures the game's {_facts.Plural} have, or a PNG of your own, which saving puts in your mod.";

    protected override IEnumerable<RecordId> PictureSources() => _facts.Picture is not { } pictureField ? []
        : Catalog.Plugins.Ids.Where(i => i.Type == _id.Type).Select(i => Text(Catalog.Plugins.GetJson(i)?[pictureField])).OfType<string>()
            .Select(t => RecordId.TryParse(t, out var r) ? r : (RecordId?)null).OfType<RecordId>();

    protected override RecordId? RecordModel => null;

    protected override string CardTitle => _facts.Singular;

    protected override string RecordsFile => PageFacts.FileOf(_id.Type);

    protected override string Noun => _facts.Singular.ToLowerInvariant();

    // ---- The rows ----------------------------------------------------------------------------------------------------------------

    protected override void AddValueRows()
    {
        foreach (var (field, value) in _json)
        {
            if (field is "id" or "patch" || field == _facts.Picture || (field == "name" && ShowsName) || _facts.Hidden.Contains(field))
                continue;
            AddFieldRow(field, value);
        }
    }

    /// <summary>A field's row by its class's property, or by its JSON where the class has none.</summary>
    private void AddFieldRow(string field, JsonNode? value)
    {
        var fact = _facts.Field(field);
        var label = fact.Label ?? Words(Pascal(field));
        var property = _class?.GetProperty(Pascal(field), BindingFlags.Public | BindingFlags.Instance);
        var type = property?.PropertyType;
        var underlying = type is null ? null : Nullable.GetUnderlyingType(type) ?? type;
        var original = value?.DeepClone();
        switch (value)
        {
            case JsonValue v when underlying?.IsEnum == true:
                AddRow(label, EnumChoice(field, underlying, Text(v) ?? ""), fact.Help);
                break;
            case JsonValue v when v.TryGetValue(out bool on):
                AddRow(label, Switch(field, on, label), fact.Help);
                break;
            case JsonValue v when v.TryGetValue(out int n) && (underlying is null || underlying == typeof(int) || underlying == typeof(uint) || underlying == typeof(byte) || underlying == typeof(short)):
                AddRow(label, Number(field, n, fact.Min ?? (n < 0 ? -32768 : 0), fact.Max ?? Math.Max(65535, n)), fact.Help);
                break;
            case JsonValue v when v.TryGetValue(out double d):
                AddRow(label, Decimal(field, d), fact.Help);
                break;
            case JsonValue v when Text(v) is { } text && RecordId.TryParse(text, out var linked) && underlying == typeof(RecordId):
                AddRow(label, RecordField(field, linked, linked.Type, nullable: type != underlying), fact.Help);
                break;
            case JsonValue v when Text(v) is { } text:
                AddRow(label, text.Length > 80 || field is "description" or "text" ? Paragraph(field, text) : TextField(field, text), fact.Help);
                break;
            case JsonArray a when a.Count > 0 && a.All(e => e is JsonValue ev && ev.TryGetValue(out int _)) && a.Count <= 64:
                AddRow(label, NumberList(field, a, fact), fact.Help);
                break;
            case JsonArray a when a.All(e => e is JsonValue ev && Text(ev) is { } t && RecordId.TryParse(t, out _)):
                AddRow(label, RecordList(field, a, ElementType(property)), fact.Help);
                break;
            case JsonArray a when ElementClass(property) is { IsSealed: true } element && element.GetProperties() is { Length: > 0 } ps && ps.All(p => IsPlain(p.PropertyType)):
                AddRow(label, RowList(field, a, element, property!), fact.Help);
                break;
            case JsonArray a:
                AddRow(label, EditorStyle.DimLabel(a.Count == 0 ? "None." : $"{a.Count} {(a.Count == 1 ? "item" : "items")}, which All fields below lists; the page doesn't edit them."), fact.Help);
                break;
            case JsonObject o when underlying is not null && !underlying.IsAssignableTo(typeof(System.Collections.IEnumerable)) && o.All(f => f.Value is JsonValue or JsonArray or null) && underlying.GetProperties().All(p => IsPlain(p.PropertyType) || IsPlainList(p.PropertyType)):
                AddRow(label, NestedRows(field, o, underlying), fact.Help);
                break;
            case JsonObject o:
                AddRow(label, EditorStyle.DimLabel($"{o.Count} fields, which All fields below lists; the page doesn't edit them."), fact.Help);
                break;
            case null:
                if (underlying == typeof(RecordId))
                    AddRow(label, RecordField(field, null, PageFacts.TypeOf(_id.Type, field) ?? "", nullable: true), fact.Help);
                else
                    AddRow(label, EditorStyle.DimLabel("None."), fact.Help);
                break;
            default:
                AddRow(label, EditorStyle.DimLabel(value.ToJsonString()), fact.Help);
                break;
        }
    }

    private static string Pascal(string field) => field.Length == 0 ? field : char.ToUpperInvariant(field[0]) + field[1..];

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static bool IsPlain(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u == typeof(int) || u == typeof(uint) || u == typeof(float) || u == typeof(double) || u == typeof(bool) || u == typeof(string) || u == typeof(byte) || u == typeof(short)
            || u == typeof(RecordId) || u.IsEnum;
    }

    /// <summary>A list of plain values: numbers, text, records.</summary>
    private static bool IsPlainList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) && IsPlain(t.GetGenericArguments()[0]);

    /// <summary>
    /// What a link to a record says: its summary, where that names it; a flag by its number, with its label where it has one; an image
    /// by its key and size.
    /// </summary>
    private string Describe(RecordId id, bool withKey)
    {
        var summary = Catalog.Find(id)?.Summary ?? "";
        return id.Type switch
        {
            RecordTypes.Flag => summary.Length > 0 && !summary.StartsWith("used by", StringComparison.Ordinal) && !summary.StartsWith("unused", StringComparison.Ordinal)
                ? $"flag {id.Key}: {summary}" : $"flag {id.Key}",
            RecordTypes.Image or RecordTypes.Texture => summary.Length > 0 ? $"{id.Type} {id.Key}, {summary}" : $"{id.Type} {id.Key}",
            _ => summary.Length == 0 ? id.ToString() : withKey ? $"{summary}  ({id.Key})" : summary,
        };
    }

    private static Type? ElementClass(PropertyInfo? property)
    {
        var element = ElementType(property);
        return element is { IsClass: true } && element != typeof(string) && element != typeof(RecordId) ? element : null;
    }

    private static Type? ElementType(PropertyInfo? property)
    {
        var t = property?.PropertyType;
        if (t is null)
            return null;
        var generic = t.IsGenericType ? t : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return generic?.GetGenericArguments().FirstOrDefault();
    }

    // ---- Controls ----------------------------------------------------------------------------------------------------------------

    /// <summary>One of an enum's names, the enum found at run time, held as <paramref name="field"/>.</summary>
    private OptionButton EnumChoice(string field, Type enumType, string current)
    {
        var names = Enum.GetNames(enumType);
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        foreach (var name in names)
            choice.AddItem(Words(name));
        var shown = Text(Shown(field, current)) ?? current;
        choice.Select(Math.Max(Array.IndexOf(names, shown), 0));
        choice.ItemSelected += i => Change(field, names[(int)i], current);
        return choice;
    }

    /// <summary>A number with a fraction, held as <paramref name="field"/>.</summary>
    private SpinBox Decimal(string field, double value)
    {
        var spin = new SpinBox
        {
            MinValue = -1e6, MaxValue = 1e6, Step = 0.01, UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin, SelectAllOnFocus = true,
            Value = Shown(field, value) is JsonValue held && held.TryGetValue(out double d) ? d : value,
        };
        spin.ValueChanged += v => Change(field, v, value);
        return spin;
    }

    /// <summary>A line of text, held as <paramref name="field"/>.</summary>
    private LineEdit TextField(string field, string value)
    {
        var edit = new LineEdit { Text = Text(Shown(field, value)) ?? value, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        edit.TextChanged += text => Change(field, text, value);
        return edit;
    }

    /// <summary>A record: its link, and <b>Change</b>, which picks another of its type, or none where it may be none.</summary>
    private Control RecordField(string field, RecordId? value, string type, bool nullable)
    {
        var row = EditorStyle.Row(12);
        var now = Text(Shown(field, value?.ToString())) is { } text && RecordId.TryParse(text, out var held) ? held : (RecordId?)null;
        if (now is { } id)
        {
            var link = new Button { Text = Describe(id, withKey: true), ThemeTypeVariation = "LinkButton", TooltipText = $"{id}: open it" };
            link.Pressed += () => Open(id);
            row.AddChild(link);
        }
        else
        {
            row.AddChild(EditorStyle.DimLabel("None"));
        }
        if (type.Length > 0)
        {
            var change = new Button { Text = "Change", TooltipText = $"One of the game's {TypeGroups.Name(type).ToLowerInvariant()}" + (nullable ? ", or none" : "") };
            change.Pressed += () => PickRecord(type, nullable, now, picked => Change(field, picked?.ToString(), value?.ToString()));
            row.AddChild(change);
        }
        return row;
    }

    /// <summary>A list of records as links, with <b>Add</b> and each one's <b>×</b>; the list is held whole.</summary>
    private Control RecordList(string field, JsonArray value, Type? element)
    {
        var type = PageFacts.TypeOf(_id.Type, field) ?? (value.Count > 0 && Text(value[0]) is { } first && RecordId.TryParse(first, out var f) ? f.Type : "");
        var now = (TryChanged(field, out var held) && held is JsonArray h ? h : value).Select(Text).OfType<string>()
            .Select(t => RecordId.TryParse(t, out var id) ? id : (RecordId?)null).OfType<RecordId>().ToList();
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 6);
        flow.AddThemeConstantOverride("v_separation", 6);
        void Hold(List<RecordId> list)
        {
            Change(field, new JsonArray([.. list.Select(i => (JsonNode?)i.ToString())]), value.DeepClone());
            BuildRows();
        }
        foreach (var id in now)
        {
            var chip = EditorStyle.Row(2);
            var link = new Button { Text = Describe(id, withKey: false), ThemeTypeVariation = "LinkButton", TooltipText = $"{id}: open it" };
            link.Pressed += () => Open(id);
            var remove = new Button { Text = "×", ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = "Take it off the list" };
            remove.Pressed += () => Hold([.. now.Where(i => i != id)]);
            chip.AddChild(link);
            chip.AddChild(remove);
            flow.AddChild(chip);
        }
        if (type.Length > 0)
        {
            var add = new Button { Text = "Add", TooltipText = $"One of the game's {TypeGroups.Name(type).ToLowerInvariant()}" };
            add.Pressed += () => PickRecord(type, false, null, picked =>
            {
                if (picked is { } p && !now.Contains(p))
                    Hold([.. now, p]);
            });
            flow.AddChild(add);
        }
        else if (now.Count == 0)
        {
            flow.AddChild(EditorStyle.DimLabel("None."));
        }
        if (now.Count <= 36)
            return flow;
        flow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 200), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(flow);
        return scroll;
    }

    /// <summary>A short list of whole numbers, a box each, held whole.</summary>
    private Control NumberList(string field, JsonArray value, PageFacts.FieldFact fact)
    {
        var now = (TryChanged(field, out var held) && held is JsonArray h ? h : value).Select(e => e is JsonValue v && v.TryGetValue(out int n) ? n : 0).ToList();
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        // Wide enough for the widest of them, so none is cut.
        var width = Math.Max(96, 48 + 9 * now.Max(n => n.ToString(CultureInfo.InvariantCulture).Length));
        for (var i = 0; i < now.Count; i++)
        {
            var index = i;
            var spin = new SpinBox
            {
                MinValue = fact.Min ?? (now[i] < 0 ? -32768 : 0), MaxValue = fact.Max ?? Math.Max(65535, now[i]), Step = 1, Rounded = true, Value = now[i],
                UpdateOnTextChanged = true, SelectAllOnFocus = true, CustomMinimumSize = new Vector2(width, 0),
            };
            spin.ValueChanged += v =>
            {
                now[index] = (int)v;
                Change(field, new JsonArray([.. now.Select(n => (JsonNode?)n)]), value.DeepClone());
            };
            row.AddChild(spin);
        }
        return row;
    }

    /// <summary>
    /// A list of small records, a row each with a control for each of the element's fields, <b>×</b> to take a row off and <b>Add</b>
    /// for one more. The list is held whole; a keyed list's rows taken off are removed by key as it's saved (<see cref="Patched"/>).
    /// </summary>
    private Control RowList(string field, JsonArray value, Type element, PropertyInfo property)
    {
        var rows = (TryChanged(field, out var held) && held is JsonArray h ? h : value).OfType<JsonObject>().Select(o => (JsonObject)o.DeepClone()).ToList();
        var properties = element.GetProperties();
        var grid = new GridContainer { Columns = properties.Length + 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 4);
        void Hold()
        {
            Change(field, new JsonArray([.. rows.Select(r => (JsonNode?)r.DeepClone())]), value.DeepClone());
            BuildRows();
        }
        foreach (var p in properties)
            grid.AddChild(new Label { Text = Words(p.Name), ThemeTypeVariation = "SubheadLabel" });
        grid.AddChild(new Control());
        foreach (var row in rows)
        {
            foreach (var p in properties)
            {
                var key = Camel(p.Name);
                var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                var current = row[key];
                if (type == typeof(RecordId))
                {
                    var id = Text(current) is { } t && RecordId.TryParse(t, out var r) ? r : (RecordId?)null;
                    var pick = new Button
                    {
                        Text = id is { } i ? Describe(i, withKey: false) : "None", SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left,
                        ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, TooltipText = id is { } tip ? $"{tip}: click to change it" : "Click to pick one",
                    };
                    var recordType = PageFacts.TypeOf(_id.Type, $"{field}.{key}") ?? id?.Type ?? "";
                    pick.Pressed += () => PickRecord(recordType, p.PropertyType != type, id, picked =>
                    {
                        row[key] = picked?.ToString();
                        Hold();
                    });
                    grid.AddChild(pick);
                }
                else if (type == typeof(bool))
                {
                    var box = new CheckBox { ButtonPressed = current is JsonValue bv && bv.TryGetValue(out bool on) && on };
                    box.Toggled += on =>
                    {
                        row[key] = on;
                        Hold();
                    };
                    grid.AddChild(box);
                }
                else if (type == typeof(float) || type == typeof(double))
                {
                    var spin = new SpinBox { MinValue = -1e6, MaxValue = 1e6, Step = 0.01, Value = current is JsonValue dv && dv.TryGetValue(out double d) ? d : 1, UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(96, 0) };
                    spin.ValueChanged += v =>
                    {
                        row[key] = v;
                        Change(field, new JsonArray([.. rows.Select(r => (JsonNode?)r.DeepClone())]), value.DeepClone());
                    };
                    grid.AddChild(spin);
                }
                else if (type.IsEnum)
                {
                    var names = Enum.GetNames(type);
                    var choice = new OptionButton();
                    foreach (var name in names)
                        choice.AddItem(Words(name));
                    choice.Select(Math.Max(Array.IndexOf(names, Text(current) ?? ""), 0));
                    choice.ItemSelected += i =>
                    {
                        row[key] = names[(int)i];
                        Hold();
                    };
                    grid.AddChild(choice);
                }
                else if (type == typeof(string))
                {
                    var edit = new LineEdit { Text = Text(current) ?? "", CustomMinimumSize = new Vector2(120, 0) };
                    edit.TextChanged += t =>
                    {
                        row[key] = t;
                        Change(field, new JsonArray([.. rows.Select(r => (JsonNode?)r.DeepClone())]), value.DeepClone());
                    };
                    grid.AddChild(edit);
                }
                else
                {
                    var fact = _facts.Field($"{field}.{key}");
                    var n = current is JsonValue nv && nv.TryGetValue(out int got) ? got : 0;
                    var spin = new SpinBox { MinValue = fact.Min ?? 0, MaxValue = fact.Max ?? Math.Max(65535, n), Step = 1, Rounded = true, Value = n, UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(96, 0) };
                    spin.ValueChanged += v =>
                    {
                        row[key] = (int)v;
                        Change(field, new JsonArray([.. rows.Select(r => (JsonNode?)r.DeepClone())]), value.DeepClone());
                    };
                    grid.AddChild(spin);
                }
            }
            var remove = new Button { Text = "×", ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None, TooltipText = "Take the row off" };
            remove.Pressed += () =>
            {
                rows.Remove(row);
                Hold();
            };
            grid.AddChild(remove);
        }
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        if (rows.Count > 0)
            column.AddChild(grid);
        else
            column.AddChild(EditorStyle.DimLabel("None."));
        var add = new Button { Text = "Add", SizeFlagsHorizontal = SizeFlags.ShrinkBegin, TooltipText = "One more row" };
        add.Pressed += () =>
        {
            var fresh = new JsonObject();
            foreach (var p in properties)
            {
                var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                var key = Camel(p.Name);
                if (type == typeof(RecordId))
                    fresh[key] = null;
                else if (type == typeof(bool))
                    fresh[key] = false;
                else if (type == typeof(float) || type == typeof(double))
                    fresh[key] = 1.0;
                else if (type.IsEnum)
                    fresh[key] = Enum.GetNames(type)[0];
                else if (type == typeof(string))
                    fresh[key] = "";
                else
                    fresh[key] = _facts.Field($"{field}.{key}").Default ?? 0;
            }
            // The row's record first, where it has one, or the row stands empty.
            var first = properties.FirstOrDefault(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(RecordId));
            if (first is not null && PageFacts.TypeOf(_id.Type, $"{field}.{Camel(first.Name)}") is { } recordType)
            {
                PickRecord(recordType, false, null, picked =>
                {
                    if (picked is null)
                        return;
                    fresh[Camel(first.Name)] = picked.ToString();
                    rows.Add(fresh);
                    Hold();
                });
            }
            else
            {
                rows.Add(fresh);
                Hold();
            }
        };
        column.AddChild(add);
        return column;
    }

    /// <summary>A nested record of plain values, a row each under the field's own; the object is held whole.</summary>
    private Control NestedRows(string field, JsonObject value, Type cls)
    {
        var held = TryChanged(field, out var now) && now is JsonObject h ? (JsonObject)h.DeepClone() : (JsonObject)value.DeepClone();
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 6);
        void Hold()
        {
            Change(field, (JsonObject)held.DeepClone(), value.DeepClone());
        }
        foreach (var (key, inner) in held)
        {
            var p = cls.GetProperty(Pascal(key), BindingFlags.Public | BindingFlags.Instance);
            var type = p is null ? null : Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var fact = _facts.Field($"{field}.{key}");
            grid.AddChild(new Label { Text = fact.Label ?? Words(Pascal(key)), TooltipText = fact.Help });
            switch (inner)
            {
                case JsonValue v when type?.IsEnum == true:
                    var names = Enum.GetNames(type);
                    var choice = new OptionButton();
                    foreach (var name in names)
                        choice.AddItem(Words(name));
                    choice.Select(Math.Max(Array.IndexOf(names, Text(v) ?? ""), 0));
                    choice.ItemSelected += i =>
                    {
                        held[key] = names[(int)i];
                        Hold();
                    };
                    grid.AddChild(choice);
                    break;
                case JsonValue v when v.TryGetValue(out bool on):
                    var box = new CheckBox { ButtonPressed = on };
                    box.Toggled += b =>
                    {
                        held[key] = b;
                        Hold();
                    };
                    grid.AddChild(box);
                    break;
                case JsonValue v when v.TryGetValue(out int n) && (type is null || type == typeof(int) || type == typeof(uint)):
                    var spin = new SpinBox { MinValue = fact.Min ?? (n < 0 ? -32768 : 0), MaxValue = fact.Max ?? Math.Max(65535, n), Step = 1, Rounded = true, Value = n, UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                    spin.ValueChanged += d =>
                    {
                        held[key] = (int)d;
                        Hold();
                    };
                    grid.AddChild(spin);
                    break;
                case JsonValue v when v.TryGetValue(out double d):
                    var dec = new SpinBox { MinValue = -1e6, MaxValue = 1e6, Step = 0.01, Value = d, UpdateOnTextChanged = true, CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                    dec.ValueChanged += x =>
                    {
                        held[key] = x;
                        Hold();
                    };
                    grid.AddChild(dec);
                    break;
                case JsonValue v when Text(v) is { } text && RecordId.TryParse(text, out var linked):
                    var link = new Button { Text = Describe(linked, withKey: true), ThemeTypeVariation = "LinkButton", SizeFlagsHorizontal = SizeFlags.ShrinkBegin, TooltipText = $"{linked}: open it" };
                    link.Pressed += () => Open(linked);
                    grid.AddChild(link);
                    break;
                case JsonValue v when Text(v) is { } text:
                    var edit = new LineEdit { Text = text, CustomMinimumSize = new Vector2(240, 0), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
                    edit.TextChanged += t =>
                    {
                        held[key] = t;
                        Hold();
                    };
                    grid.AddChild(edit);
                    break;
                case JsonArray a when a.Count > 0 && a.All(e => e is JsonValue ev && (ev.TryGetValue(out int _) || ev.TryGetValue(out double _))):
                    var numbers = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    numbers.AddThemeConstantOverride("h_separation", 6);
                    numbers.AddThemeConstantOverride("v_separation", 6);
                    var whole = a.All(e => e is JsonValue ev && ev.TryGetValue(out int _));
                    for (var i = 0; i < a.Count; i++)
                    {
                        var index = i;
                        var number = (JsonValue)a[i]!;
                        var one = new SpinBox
                        {
                            MinValue = whole ? (fact.Min ?? 0) : -1e6, MaxValue = whole ? (fact.Max ?? 65535) : 1e6, Step = whole ? 1 : 0.01, Rounded = whole,
                            Value = number.TryGetValue(out double x) ? x : 0, UpdateOnTextChanged = true, SelectAllOnFocus = true, CustomMinimumSize = new Vector2(84, 0),
                        };
                        one.ValueChanged += x =>
                        {
                            ((JsonArray)held[key]!)[index] = whole ? (int)x : x;
                            Hold();
                        };
                        numbers.AddChild(one);
                    }
                    grid.AddChild(numbers);
                    break;
                case JsonArray a when a.All(e => Text(e) is { } t && RecordId.TryParse(t, out _)):
                    var links = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    links.AddThemeConstantOverride("h_separation", 6);
                    foreach (var e in a)
                    {
                        var target = RecordId.Parse(Text(e)!);
                        var one = new Button { Text = Describe(target, withKey: false), ThemeTypeVariation = "LinkButton", TooltipText = $"{target}: open it" };
                        one.Pressed += () => Open(target);
                        links.AddChild(one);
                    }
                    if (a.Count == 0)
                        links.AddChild(EditorStyle.DimLabel("None."));
                    grid.AddChild(links);
                    break;
                case JsonArray a:
                    grid.AddChild(EditorStyle.DimLabel($"{a.Count} {(a.Count == 1 ? "item" : "items")}, which All fields below lists."));
                    break;
                default:
                    grid.AddChild(EditorStyle.DimLabel(inner?.ToJsonString() ?? "None"));
                    break;
            }
        }
        return grid;
    }

    /// <summary>
    /// The dialog that picks a record of a type: every record the plugins have of it, by its summary, filtered as the box is typed in,
    /// and none where the field may be none. A double click or <b>Use</b> takes the one picked.
    /// </summary>
    private void PickRecord(string type, bool nullable, RecordId? current, Action<RecordId?> use)
    {
        var entries = Catalog.Entries.Where(e => e.Id.Type == type).ToList();
        var list = new Tree { ThemeTypeVariation = "FlushTree", Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row };
        list.SetColumnExpand(0, false);
        list.SetColumnCustomMinimumWidth(0, 170);
        list.SetColumnClipContent(1, true);
        var filter = new LineEdit { PlaceholderText = "Filter", ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        TreeItem? selected = null;
        void Fill()
        {
            list.Clear();
            selected = null;
            var root = list.CreateItem();
            var wanted = filter.Text.Trim();
            if (nullable && wanted.Length == 0)
            {
                var none = list.CreateItem(root);
                none.SetText(0, "None");
                none.SetCustomColor(0, EditorStyle.Dim);
                none.SetMetadata(0, "");
                if (current is null)
                    selected = none;
            }
            foreach (var entry in entries)
            {
                if (wanted.Length > 0 && !entry.Id.ToString().Contains(wanted, StringComparison.OrdinalIgnoreCase) && !entry.Summary.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                    continue;
                var row = list.CreateItem(root);
                row.SetText(0, entry.Id.Namespace == RecordId.RomNamespace ? $"{entry.Id.Type}/{entry.Id.Key}" : entry.Id.ToString());
                row.SetCustomColor(0, EditorStyle.Dim);
                row.SetText(1, entry.Summary);
                row.SetTooltipText(1, entry.Summary);
                row.SetMetadata(0, entry.Id.ToString());
                if (entry.Id == current)
                    selected = row;
            }
            if (selected is not null)
            {
                selected.Select(0);
                list.ScrollToItem(selected);
            }
        }
        filter.TextChanged += _ => Fill();
        var box = new PanelContainer { ThemeTypeVariation = "ListBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(new FittedTable(list, 12, inBox: true));
        RecordId? Picked() => list.GetSelected()?.GetMetadata(0).AsString() is { Length: > 0 } text && RecordId.TryParse(text, out var id) ? id : null;
        EditorDialog? dialog = null;
        void Use()
        {
            if (!nullable && Picked() is null)
                return;
            use(Picked());
            dialog?.Close();
        }
        list.ItemActivated += Use;
        list.Ready += Fill;
        dialog = new EditorDialog($"Pick {(TypeGroups.One(type) ?? TypeGroups.Name(type).TrimEnd('s')).ToLowerInvariant()}", [filter, box],
            [new DialogButton("Cancel", DialogButtonKind.Cancel), new DialogButton("Use", DialogButtonKind.Primary, Use, Closes: false)],
            width: PictureGrid + 2 * EditorDialog.Inset);
        dialog.Open(this);
        filter.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>A keyed list's elements taken off are removed by their key as the patch is written, as the merge asks.</summary>
    protected override JsonNode? Patched(string field, JsonNode? value)
    {
        if (value is not JsonArray held || _class?.GetProperty(Pascal(field))?.GetCustomAttribute<KeyedListAttribute>() is not { Keys: { Count: > 0 } keys }
            || _json[field] is not JsonArray original)
            return value;
        var key = keys[0];
        var kept = held.OfType<JsonObject>().Select(o => o[key]?.ToJsonString()).ToHashSet();
        var patch = new JsonArray([.. held.Select(e => e?.DeepClone())]);
        foreach (var o in original.OfType<JsonObject>())
        {
            if (o[key] is { } k && !kept.Contains(k.ToJsonString()))
                patch.Add(new JsonObject { [key] = k.DeepClone(), [MergePatch.RemoveField] = true });
        }
        return patch;
    }
}

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
/// An armor's, a shield's, a weapon's or an item's page (editor.md#an-items-page): an <see cref="EditPage"/> whose card is the item's
/// picture, name and values, with what it gives beyond them, and whose own card is its details as the game shows them.
/// <see cref="ArmorPage"/>, <see cref="ShieldPage"/>, <see cref="WeaponPage"/> and <see cref="ItemPage"/> give their own rows, and say
/// whether a model goes with it and where the game draws it.
/// </summary>
public abstract partial class ItemEditPage : EditPage
{
    protected readonly Item Item;

    protected ItemEditPage(PluginCatalog catalog, Item item, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, open, reload, models, usage)
    {
        Item = item;
    }

    /// <summary>Its name as the page has it now, changed or not.</summary>
    public string ItemName => CurrentName;

    protected override string PictureHelp =>
        "Drawn wherever the game shows it: the inventory, a shop, the silhouette's slot. A PNG from a file is fitted to the original's size, as the game lays a picture out at its own.";

    protected override string PictureDialogText => "Pick one of the pictures the game's weapons, armor and items have, or a PNG of your own, which saving puts in your mod.";

    /// <summary>Every picture the game's weapons, armor, shields and items have.</summary>
    protected override IEnumerable<RecordId> PictureSources() =>
        Catalog.Plugins.Ids.Where(i => i.Type is RecordTypes.Armor or RecordTypes.Shield or RecordTypes.Weapon or RecordTypes.Item)
            .Select(i => (string?)Catalog.Plugins.GetJson(i)?["icon"]).OfType<string>()
            .Select(t => RecordId.TryParse(t, out var r) ? r : (RecordId?)null).OfType<RecordId>();

    /// <summary>Builds the page: its card, the panel beside it, its details in the game, what names it and its fields.</summary>
    protected void Build(EditorGame? game, CharacterRules? rules, ItemCatalog items, JsonObject json, IReadOnlyList<RecordReference> references) =>
        Build(json, references, [InGame(game, rules, items)]);

    /// <summary>What it gives beyond the values the rows change: its aspect, a stat or skill, a spell, a resistance.</summary>
    protected override void AddLastRows()
    {
        var bonuses = Gives().ToList();
        AddRow("What it gives", new Label
        {
            Text = bonuses.Count == 0 ? "Nothing beyond the values above." : string.Join("; ", bonuses.Select(b => char.ToUpperInvariant(b[0]) + b[1..])) + ".",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }, "Its aspect, resistances and stat bonus, which All fields below lists.");
    }

    private IEnumerable<string> Gives()
    {
        string Name(RecordId id) => (string?)Catalog.Plugins.GetJson(id)?["name"] ?? id.Key;
        var b = Item.Bonuses;
        if (Aspects.Enabled && b.Aspect != Aspect.None)
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
        var totals = (Item.Armor is not null || Item.Shield is not null) && details?.Footer.Count > 0;
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
}

using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Editor;

/// <summary>
/// An item's page (editor.md#an-items-page): what it is, its price, and by its kind, a potion's, a scroll's or another item's
/// description, which the game's details show, or worn gear's damage, protection and the Strength and Intelligence it needs, which they
/// show instead; and its experience multiplier. Its kind changes only among the worn kinds, as a potion's effects, a scroll's spell
/// and a key's flags go with theirs. An item with a model shows it in the panel beside the cards, to look at and to edit its textures;
/// the game draws no item's model in battle, so it isn't changed here.
/// </summary>
public partial class ItemPage : ItemEditPage
{
    private static readonly ItemKind[] Worn =
        [ItemKind.Helmet, ItemKind.Cloak, ItemKind.Gloves, ItemKind.Ring, ItemKind.Wand, ItemKind.Belt, ItemKind.Boots, ItemKind.Amulet];

    private readonly ItemRecord _record;

    public ItemPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, ItemRecord record,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, item, open, reload, models, usage)
    {
        _record = record;
        Build(game, rules, items, json, references);
    }

    protected override RecordId Id => _record.Id;

    protected override string RecordName => _record.Name;

    protected override RecordId Icon => _record.Icon;

    protected override RecordId? RecordModel => _record.Model;

    protected override string CardTitle => _record.Kind switch
    {
        ItemKind.Misc => _record.KeyItem is null ? "Item" : "Key item",
        ItemKind.Key => "Key",
        var kind => Words(kind.ToString()),
    };

    protected override string RecordsFile => "items.json";

    protected override string Noun => "item";

    /// <summary>Only an item with a model shows it, to look at and edit its textures; none is changed, as the game draws none in battle.</summary>
    protected override bool ShowsModel => _record.Model is not null;

    protected override bool ShowsModelRow => false;

    private bool IsWorn => Worn.Contains(_record.Kind);

    /// <summary>Whether the game's details show its description rather than numbers: a potion's, a scroll's or another item's, a key item's too; not a key's.</summary>
    private bool ShowsDescription => _record.Kind is ItemKind.Potion or ItemKind.Scroll or ItemKind.Misc;

    protected override void AddValueRows()
    {
        var r = _record;
        if (IsWorn)
            AddRow("Kind", Choice("kind", r.Kind, Worn), "What it is, which decides where it's worn. Two rings can be worn, and a wand's slot holds the rope, the banner and the instruments too.");
        else
            AddRow("Kind", EditorStyle.DimLabel(CardTitle, 13), "What it is. It changes only among the worn kinds: a potion's effects, a scroll's spell and a key's flags go with theirs.");
        AddRow("Price", Number("price", r.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        if (ShowsDescription || r.Description is not null)
        {
            AddRow("Description", Paragraph("description", r.Description), ShowsDescription
                ? "What the game's details say of it when C-down is held on it, wrapped to their width."
                : "Kept in the game's text, but its details show its numbers instead, so the game doesn't show it.");
        }
        if (IsWorn)
        {
            AddRow("Damage", Number("damage", r.Damage, -128, 127), "What wearing it adds to the wearer's damage.");
            AddRow("Protection", Number("protection", r.Protection, -128, 127), "What wearing it adds to the wearer's protection.");
            AddRow("Strength needed", Number("requiredStrength", r.RequiredStrength, 0, 255), "Wearing it needs at least this base Strength: the details' Min. Strength.");
            AddRow("Intelligence needed", Number("requiredIntelligence", r.RequiredIntelligence, 0, 255), "Wearing it needs at least this base Intelligence: the details' Min. Intelligence.");
        }
        AddRow("Experience multiplier", Number("expMultiplier", r.ExpMultiplier, 0, 65535), "Read into the game's memory with the rest; what reads it isn't known.");
    }
}

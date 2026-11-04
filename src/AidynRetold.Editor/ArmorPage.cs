using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Editor;

/// <summary>
/// An armor's or a shield's page (editor.md#an-items-page): its slot, a shield's model, its price, protection, defense, Dexterity,
/// Stealth and experience bonus. A shield's model, the scene the game hangs on a body's arm in battle (<c>Combatant.Holding</c>), is
/// in the panel beside the cards, which follows the slot as it's changed; body armor has none, as the game draws it as the body's outfit.
/// </summary>
public partial class ArmorPage : ItemEditPage
{
    private readonly ArmorRecord _armor;

    /// <param name="models">The models to draw a shield's from, in the panel beside the cards; none, no panel.</param>
    /// <param name="usage">Which models use which textures, for the shield's textures, built once for the plugins.</param>
    public ArmorPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, ArmorRecord armor,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, item, open, reload, models, usage)
    {
        _armor = armor;
        Build(game, rules, items, json, references);
    }

    protected override RecordId Id => _armor.Id;

    protected override string RecordName => _armor.Name;

    protected override RecordId Icon => _armor.Icon;

    protected override RecordId? RecordModel => _armor.Model;

    protected override string CardTitle => _armor.Slot == ArmorSlot.Shield ? "Shield" : "Armor";

    protected override string RecordsFile => "armor.json";

    protected override string Noun => "armor";

    /// <summary>Whether it's a shield, as its Slot reads now, changed or not.</summary>
    private bool IsShield => TryChanged("slot", out var slot) ? (string?)slot == "Shield" : _armor.Slot == ArmorSlot.Shield;

    protected override bool ShowsModel => IsShield;

    protected override bool ChangesModelPanel(string field) => field == "slot";

    protected override string ModelPlace => "on the arm in battle";

    protected override string ModelOwners => "the game's shields";

    protected override IEnumerable<(RecordId Model, string Name, RecordId Of)> ModelUsers() =>
        Catalog.Plugins.All<ArmorRecord>(RecordTypes.Armor).Where(a => a.Model is not null).Select(a => (a.Model!.Value, a.Name, a.Id));

    protected override string NoModelText => "No model. The game hangs nothing on the arm for a shield without one, so in battle it shows as no shield at all.";

    protected override void AddValueRows()
    {
        var a = _armor;
        AddRow("Slot", Choice("slot", a.Slot, [ArmorSlot.Body, ArmorSlot.Shield]), "Where it's worn: on the body, or on the arm as a shield.");
        AddModelRow("What the game hangs on the arm in battle, which the panel beside shows. Only a shield has one.");
        AddRow("Price", Number("price", a.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        AddRow("Protection", Number("protection", a.Protection, 0, 255), "What wearing it adds to protection from damage.");
        AddRow("Defense", Number("defense", a.Defense, 0, 255), "A shield's defense, which combat reads; 0 on body armor.");
        AddRow("Dexterity", Number("dexterity", a.Dexterity, -128, 127), "What wearing it adds to modified Dexterity: a penalty, or 0.");
        AddRow("Stealth", Number("stealth", a.Stealth, -128, 127), "What wearing it adds to modified Stealth.");
        AddRow("Experience bonus", Number("expBonus", a.ExpBonus, 0, 255), "Read into the game's memory with the rest; what reads it isn't known.");
    }
}

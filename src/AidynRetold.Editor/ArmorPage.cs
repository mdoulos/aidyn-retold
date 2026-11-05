using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Editor;

/// <summary>
/// Body armor's page (editor.md#an-items-page): its price, protection, Dexterity, Stealth and experience bonus. It has no model panel,
/// as the game draws armor as the body's outfit. A shield's page is <see cref="ShieldPage"/>.
/// </summary>
public partial class ArmorPage : ItemEditPage
{
    private readonly ArmorRecord _armor;

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

    protected override RecordId? Icon => _armor.Icon;

    protected override RecordId? RecordModel => null;

    protected override string CardTitle => "Armor";

    protected override string RecordsFile => "armor.json";

    protected override string Noun => "armor";

    protected override void AddValueRows()
    {
        var a = _armor;
        AddRow("Price", Number("price", a.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        AddRow("Protection", Number("protection", a.Protection, 0, 255), "What wearing it adds to protection from damage.");
        AddRow("Dexterity", Number("dexterity", a.Dexterity, -128, 127), "What wearing it adds to modified Dexterity: a penalty, or 0.");
        AddRow("Stealth", Number("stealth", a.Stealth, -128, 127), "What wearing it adds to modified Stealth.");
        AddRow("Experience bonus", Number("expBonus", a.ExpBonus, 0, 255), "Read into the game's memory with the rest; what reads it isn't known.");
    }
}

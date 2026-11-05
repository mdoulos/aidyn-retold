using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Editor;

/// <summary>
/// A shield's page (editor.md#an-items-page): its model, price, protection, defense, Dexterity, Stealth and experience bonus. Its model,
/// the scene the game hangs on a body's arm in battle (<c>Combatant.Holding</c>), is in the panel beside the cards.
/// </summary>
public partial class ShieldPage : ItemEditPage
{
    private readonly ShieldRecord _shield;

    /// <param name="models">The models to draw the shield's from, in the panel beside the cards; none, no panel.</param>
    /// <param name="usage">Which models use which textures, for the shield's textures, built once for the plugins.</param>
    public ShieldPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, ShieldRecord shield,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, item, open, reload, models, usage)
    {
        _shield = shield;
        Build(game, rules, items, json, references);
    }

    protected override RecordId Id => _shield.Id;

    protected override string RecordName => _shield.Name;

    protected override RecordId? Icon => _shield.Icon;

    protected override RecordId? RecordModel => _shield.Model;

    protected override string CardTitle => "Shield";

    protected override string RecordsFile => "shields.json";

    protected override string Noun => "shield";

    protected override bool ShowsModel => true;

    protected override string ModelPlace => "on the arm in battle";

    protected override string ModelOwners => "the game's shields";

    protected override IEnumerable<(RecordId Model, string Name, RecordId Of)> ModelUsers() =>
        Catalog.Plugins.All<ShieldRecord>(RecordTypes.Shield).Where(s => s.Model is not null).Select(s => (s.Model!.Value, s.Name, s.Id));

    protected override string NoModelText => "No model. The game hangs nothing on the arm for a shield without one, so in battle it shows as no shield at all.";

    protected override void AddValueRows()
    {
        var s = _shield;
        AddModelRow("What the game hangs on the arm in battle, which the panel beside shows.");
        AddRow("Price", Number("price", s.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        AddRow("Protection", Number("protection", s.Protection, 0, 255), "What bearing it adds to protection from damage.");
        AddRow("Defense", Number("defense", s.Defense, 0, 255), "Its defense, which combat reads.");
        AddRow("Dexterity", Number("dexterity", s.Dexterity, -128, 127), "What bearing it adds to modified Dexterity: a penalty, or 0.");
        AddRow("Stealth", Number("stealth", s.Stealth, -128, 127), "What bearing it adds to modified Stealth.");
        AddRow("Experience bonus", Number("expBonus", s.ExpBonus, 0, 255), "Read into the game's memory with the rest; what reads it isn't known.");
    }
}

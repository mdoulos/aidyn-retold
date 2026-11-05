using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Editor;

/// <summary>
/// A weapon's page (editor.md#an-items-page): its class, which is the skill wielding it needs, its model, its price, damage, hit,
/// range, damage's element, the Strength it needs, its attack animation, whether it comes off once equipped, and its experience
/// modifier. Its model, the scene the game puts in a body's hand in battle (<c>Combatant.Holding</c>; a bow's arrow in the hand and the
/// bow in the arm), is in the panel beside the cards; a bite's, a claw's and the other natural weapons' have none.
/// </summary>
public partial class WeaponPage : ItemEditPage
{
    private readonly WeaponRecord _weapon;

    public WeaponPage(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, WeaponRecord weapon,
        JsonObject json, IReadOnlyList<RecordReference> references, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, item, open, reload, models, usage)
    {
        _weapon = weapon;
        Build(game, rules, items, json, references);
    }

    protected override RecordId Id => _weapon.Id;

    protected override string RecordName => _weapon.Name;

    protected override RecordId? Icon => _weapon.Icon;

    protected override RecordId? RecordModel => _weapon.Model;

    protected override string CardTitle => "Weapon";

    protected override string RecordsFile => "weapons.json";

    protected override string Noun => "weapon";

    protected override bool ShowsModel => true;

    protected override string ModelPlace => "in the hand in battle";

    protected override string ModelOwners => "the game's weapons";

    protected override IEnumerable<(RecordId Model, string Name, RecordId Of)> ModelUsers() =>
        Catalog.Plugins.All<WeaponRecord>(RecordTypes.Weapon).Where(w => w.Model is not null).Select(w => (w.Model!.Value, w.Name, w.Id));

    protected override string NoModelText =>
        "No model. The game puts nothing in the hand for a weapon without one, as for a bite or a claw, which the body's own model strikes with.";

    protected override void AddValueRows()
    {
        var w = _weapon;
        AddRow("Class", Choice("class", w.Class, Enum.GetValues<WeaponClass>()), "Its weapon skill: wielding it needs that skill above 0, and the details name it.");
        AddModelRow("What the game puts in the hand in battle, which the panel beside shows: a bow's arrow, with the bow in the other hand.");
        AddRow("Price", Number("price", w.Price, 0, 65535, " gold"), "What a shop asks for it, before the barter.");
        AddRow("Damage", Number("damage", w.Damage, 0, 255), "Its damage, which combat reads: the details' Damage.");
        AddRow("Hit", Number("hit", w.Hit, 0, 255), "Combat reads it: the details' Base Hit.");
        AddRow("Range", Number("range", w.Range, 0, 255), "Its reach in combat.");
        AddRow("Damage type", Choice("damageType", w.DamageType, Enum.GetValues<Element>()), "The element of its damage, which a target's resistances read.");
        AddRow("Strength needed", Number("requiredStrength", w.RequiredStrength, 0, 255), "Wielding it needs at least this base Strength: the details' Min. Strength.");
        AddRow("Attack animation", Number("animation", w.Animation, 0, 255), "Which attack animation it uses.");
        AddRow("Fixed", Switch("fixed", w.Fixed, "Can't be taken off once equipped"), "Once equipped, it stays: the Archmage Staff, Niesen's only weapon, is.");
        AddRow("Experience modifier", Number("expModifier", w.ExpModifier, 0, 255), "Read into the game's memory with the rest; what reads it isn't known.");
    }
}

using AidynRetold.Core.Combat;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Outfits and held section of an actor's model's inspector. The outfits (<see cref="ActorRecord.Outfits"/>), each with what it's
/// worn over, the one the character shown would wear picked to start with; picking one shows its meshes and hides the rest's. What
/// the body holds as it fights, a weapon and a shield to try, the character's own to start with, hung from the locators the game
/// hangs them from (<see cref="Combatant.Holding"/>, <see cref="Attachments.Find"/>): trek's bodies hold nothing. And the actor's
/// separate head, Alaron's, on its locator, as the game draws it.
/// </summary>
public partial class HeldSection : VBoxContainer
{
    private readonly ModelContext _ctx;
    private readonly ActorRecord _actor;
    private readonly EntityRecord? _entity;
    private readonly OptionButton _weapon = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "The weapon drawn, as in battle" };
    private readonly OptionButton _shield = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "The shield on the arm, as in battle" };
    private readonly List<WeaponRecord> _weapons;
    private readonly List<ArmorRecord> _shields;
    private readonly List<Node3D> _held = [];
    private Node3D? _head;

    public HeldSection(ModelContext ctx, ActorRecord actor, EntityRecord? entity)
    {
        (_ctx, _actor, _entity) = (ctx, actor, entity);
        AddThemeConstantOverride("separation", 6);
        var plugins = ctx.Plugins;

        if (actor.Outfits is { Count: > 0 } outfits)
        {
            AddChild(EditorStyle.DimLabel("Outfit", 13));
            var group = new ButtonGroup();
            var worn = outfits.Index().FirstOrDefault(o => Meets(o.Item)).Index;
            for (var i = 0; i < outfits.Count; i++)
            {
                var outfit = outfits[i];
                var index = i;
                // Its first mesh names it; the rest are in its tooltip, so a long list doesn't widen the inspector.
                var name = outfit.Meshes.Count switch { 0 => "no meshes", 1 => outfit.Meshes[0], _ => $"{outfit.Meshes[0]} +{outfit.Meshes.Count - 1}" };
                var button = new CheckBox
                {
                    Text = $"{name} · {Condition(outfit)}", ButtonGroup = group, ButtonPressed = i == worn,
                    TooltipText = $"Its meshes: {string.Join(", ", outfit.Meshes)}", TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                    ClipText = true, CustomMinimumSize = new Vector2(280, 0),
                };
                button.Pressed += () => Dress(index);
                AddChild(button);
            }
            // The editor dresses the model itself, so the game's switch, which dresses it by a character's gear, goes.
            if (ctx.ViewOrThrow.Content is { } content)
                OutfitSwitch.Of(content)?.QueueFree();
            Dress(worn);
        }

        _weapons = [.. plugins.All<WeaponRecord>(RecordTypes.Weapon).Where(w => w.Model is not null).OrderBy(w => w.Name, StringComparer.Ordinal)];
        _shields = [.. plugins.All<ArmorRecord>(RecordTypes.Armor).Where(a => a.Model is not null).OrderBy(a => a.Name, StringComparer.Ordinal)];
        _weapon.AddItem("No weapon drawn", 0);
        for (var i = 0; i < _weapons.Count; i++)
            _weapon.AddItem(_weapons[i].Name, i + 1);
        _shield.AddItem("No shield", 0);
        for (var i = 0; i < _shields.Count; i++)
            _shield.AddItem(_shields[i].Name, i + 1);
        if (entity?.Weapons.FirstOrDefault(w => w is not null) is { } own && _weapons.FindIndex(w => w.Id == own) is >= 0 and var wi)
            _weapon.Select(wi + 1);
        if (entity?.Shield is { } shield && _shields.FindIndex(s => s.Id == shield) is >= 0 and var si)
            _shield.Select(si + 1);
        AddChild(Labelled("Weapon", _weapon));
        AddChild(Labelled("Shield", _shield));
        if (entity is { ShieldSkill: null })
            AddChild(Note($"{entity.Name} can never learn the shield skill, so the game draws no shield."));
        _weapon.ItemSelected += _ => Hold();
        _shield.ItemSelected += _ => Hold();
        Hold();

        if (actor.Head is { } head)
        {
            var shown = new CheckBox { Text = $"Head {head.Scene.Key}, on locator {head.Locator}", ButtonPressed = true, TooltipText = "The actor's separate head, as the game hangs it" };
            shown.Toggled += on =>
            {
                if (_head is not null)
                    _head.Visible = on;
            };
            AddChild(shown);
            PutHead(head);
        }
        TreeExiting += () =>
        {
            Clear();
            if (_head is not null && IsInstanceValid(_head))
                _head.QueueFree();
        };
    }

    /// <summary>What to say after the section's title.</summary>
    public string Summary => _actor.Outfits is { Count: > 0 } o ? $"{o.Count} outfits" : _entity?.Name ?? "";

    /// <summary>Whether the character shown meets an outfit's conditions, as <c>ActorOutfits.Meets</c> asks of a character's gear.</summary>
    private bool Meets(ActorOutfit outfit)
    {
        var gear = new[] { _entity?.Armor, _entity?.Shield }.Concat(_entity?.Weapons ?? []).OfType<RecordId>().ToList();
        var armored = _entity?.Armor is { } armor && _ctx.Plugins.TryGet<ArmorRecord>(armor) is { Protection: > 0 };
        return (outfit.Armored is not { } a || a == armored) && (outfit.Items is not { Count: > 0 } items || items.Any(gear.Contains));
    }

    private string Condition(ActorOutfit outfit)
    {
        var parts = new List<string>();
        if (outfit.Armored is { } a)
            parts.Add(a ? "over armour that protects" : "without armour that protects");
        if (outfit.Items is { Count: > 0 } items)
            parts.Add("with " + string.Join(" or ", items.Select(i => _ctx.Catalog.Find(i)?.Summary is { Length: > 0 } s ? s : i.ToString())));
        return parts.Count == 0 ? "otherwise" : string.Join(", ", parts);
    }

    /// <summary>Shows outfit <paramref name="index"/>'s meshes, and hides those the others name.</summary>
    private void Dress(int index)
    {
        if (_ctx.ViewOrThrow.Content is not { } content || _actor.Outfits is not { } outfits)
            return;
        var names = outfits.SelectMany(o => o.Meshes).ToHashSet();
        var worn = outfits[index].Meshes;
        foreach (var node in Nodes.FindAll<Node3D>(content))
        {
            var name = node.Name.ToString();
            if (names.Contains(name))
                node.Visible = worn.Contains(name);
        }
    }

    /// <summary>Hangs what's picked from the locators the game hangs it from.</summary>
    private void Hold()
    {
        Clear();
        if (_ctx.ViewOrThrow.Content is not { } content || _ctx.Models is not { } models)
            return;
        var weapon = _weapon.Selected > 0 ? _weapons[_weapon.Selected - 1] : null;
        var shield = _shield.Selected > 0 && _entity?.ShieldSkill is not null ? _shields[_shield.Selected - 1].Model : null;
        var rules = _ctx.Plugins.TryGet<CombatRecord>(RecordId.Decimal(RecordTypes.Combat, 0));
        var (hand, arm, _) = Combatant.Holding(_entity?.Id ?? _actor.Id, weapon, shield, rules);
        foreach (var held in new[] { hand, arm })
        {
            if (held is not { } h || Attachments.Find(content, h.Locator) is not { } on || models.Instantiate(h.Scene) is not { } item)
                continue;
            N64Material.SetLighting(item, N64Material.Lighting.Body);
            on.AddChild(item);
            _held.Add(item);
        }
    }

    private void Clear()
    {
        foreach (var item in _held.Where(IsInstanceValid))
            item.QueueFree();
        _held.Clear();
    }

    /// <summary>The actor's head on its locator's node, as <c>Models.InstantiateActor</c> hangs it.</summary>
    private void PutHead(ActorHead head)
    {
        if (_ctx.ViewOrThrow.Content is not { } content || _ctx.Models is not { } models)
            return;
        var locator = AidynRetold.Plugins.Assets.SceneNames.Locator(head.Locator);
        if (Nodes.FindAll<Node3D>(content).FirstOrDefault(n => n.Name == locator) is not { } on || models.Instantiate(head.Scene) is not { } node)
            return;
        N64Material.SetLighting(node, N64Material.Lighting.Body);
        on.AddChild(node);
        _head = node;
    }

    private static Control Labelled(string name, Control control)
    {
        var row = EditorStyle.Row(6);
        var label = EditorStyle.DimLabel(name);
        label.CustomMinimumSize = new Vector2(56, 0);
        row.AddChild(label);
        row.AddChild(control);
        return row;
    }

    private static Label Note(string text)
    {
        var label = EditorStyle.DimLabel(text, 13);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(300, 0);
        return label;
    }
}

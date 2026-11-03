using AidynRetold.Core;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The inspector beside a model's view (<see cref="ModelView.Inspector"/>): who the model is shown as, when it's an actor's that
/// characters wear, and its sections, each folding: the model's file, its textures, its parts, its outfits and what it holds, and the
/// body of the character it's shown as.
/// </summary>
public partial class ModelInspector : VBoxContainer
{
    private readonly ModelView _view;
    private readonly PluginCatalog _catalog;
    private readonly Action<RecordId> _open;
    private readonly List<Wearer> _wearers;
    private readonly OptionButton _shownAs = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "The character whose body, held items and strike the sections show" };
    private readonly VBoxContainer _sections = new();

    /// <summary>A character whose body is the actor: an entity, or a dialogue entity, which has only a body.</summary>
    public sealed record Wearer(RecordId Id, string Name, EntityBody Body, EntityRecord? Entity);

    public ModelInspector(PluginCatalog catalog, ModelView view, RecordId scene, RecordId? actor, Action<RecordId> open)
    {
        (_catalog, _view, _open, Scene, ActorId) = (catalog, view, open, scene, actor);
        AddThemeConstantOverride("separation", 6);
        _sections.AddThemeConstantOverride("separation", 6);
        _wearers = actor is { } a ? WearersOf(catalog.Plugins, a) : [];
        if (_wearers.Count > 0)
        {
            var row = EditorStyle.Row(6);
            row.AddChild(EditorStyle.DimLabel("Shown as"));
            foreach (var w in _wearers)
                _shownAs.AddItem($"{w.Name} ({w.Id.Type} {w.Id.Key})");
            row.AddChild(_shownAs);
            var go = new Button { Text = "Open", TooltipText = "Open the character's page" };
            go.Pressed += () => _open(Shown!.Id);
            row.AddChild(go);
            AddChild(row);
            _shownAs.ItemSelected += _ => Build();
        }
        AddChild(_sections);
        Build();
    }

    public RecordId Scene { get; }
    public RecordId? ActorId { get; }

    /// <summary>The character the model is shown as, or null for a model no character wears.</summary>
    public Wearer? Shown => _wearers.Count == 0 ? null : _wearers[Math.Max(_shownAs.Selected, 0)];

    /// <summary>Every entity and dialogue entity whose body is <paramref name="actor"/>, entities first, each by its ID.</summary>
    public static List<Wearer> WearersOf(PluginSet plugins, RecordId actor) =>
    [
        .. plugins.All<EntityRecord>(RecordTypes.Entity).Where(e => e.Body?.Actor == actor).OrderBy(e => e.Id.ToString(), StringComparer.Ordinal)
            .Select(e => new Wearer(e.Id, e.Name, e.Body!, e)),
        .. plugins.All<DbRecord>(RecordTypes.DialogEntity).Where(d => d.Body?.Actor == actor).OrderBy(d => d.Id.ToString(), StringComparer.Ordinal)
            .Select(d => new Wearer(d.Id, d.Name, d.Body!, null)),
    ];

    /// <summary>The sections for the model and the character it's shown as, afresh.</summary>
    private void Build()
    {
        foreach (var child in _sections.GetChildren())
        {
            _sections.RemoveChild(child);
            child.QueueFree();
        }
        if (Shown is { } wearer)
        {
            var alaron = wearer.Id == GameState.Alaron ? null : _catalog.Plugins.TryGet<EntityRecord>(GameState.Alaron)?.Body;
            var body = new Section("Body", new BodySection(_view, wearer.Body, alaron)) { Note = wearer.Name };
            _sections.AddChild(body);
        }
    }
}

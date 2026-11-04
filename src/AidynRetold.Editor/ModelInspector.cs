using AidynRetold.Core;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The inspector beside a model's view (<see cref="ModelView.Inspector"/>): who the model is shown as, when it's an actor's that
/// characters wear, and its sections, cards that fold, 16 apart: what names the record, first; the model's file, its textures, its
/// outfits and what it holds; the record's fields; and for an actor the body of the character it's shown as, then its parts, last,
/// which take what height is left (a model's parts follow its textures). The record's own cards (<see cref="AddSection"/>) are the
/// page's, here rather than in tabs.
/// </summary>
public partial class ModelInspector : VBoxContainer
{
    private readonly ModelContext _ctx;
    private readonly List<Wearer> _wearers;
    private readonly OptionButton _shownAs = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "The character whose body, held items and strike the sections show" };
    // What names the record, over the model's cards.
    private readonly VBoxContainer _first = new();
    private readonly VBoxContainer _sections = new();
    private readonly VBoxContainer _extras = new();
    // An actor's body and parts, last of all, under the record's fields, the parts taking the height that's left.
    private readonly VBoxContainer _last = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private static readonly Dictionary<string, bool> Folded = [];

    /// <summary>A character whose body is the actor: an entity, or a dialogue entity, which has only a body.</summary>
    public sealed record Wearer(RecordId Id, string Name, EntityBody Body, EntityRecord? Entity);

    public ModelInspector(ModelContext ctx)
    {
        _ctx = ctx;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 16);
        foreach (var box in new[] { _first, _sections, _extras, _last })
            box.AddThemeConstantOverride("separation", 16);
        _wearers = ctx.ActorId is { } a ? WearersOf(ctx.Plugins, a) : [];
        if (_wearers.Count > 0)
        {
            var row = EditorStyle.Row(6);
            row.AddChild(EditorStyle.DimLabel("Shown as"));
            foreach (var w in _wearers)
                _shownAs.AddItem($"{w.Name} ({w.Id.Type} {w.Id.Key})");
            row.AddChild(_shownAs);
            var go = new Button { Text = "Open", TooltipText = "Open the character's page" };
            go.Pressed += () => ctx.Open(Shown!.Id);
            row.AddChild(go);
            AddChild(row);
            _shownAs.ItemSelected += _ => Build();
        }
        AddChild(_first);
        AddChild(_sections);
        AddChild(_extras);
        AddChild(_last);
        Build();
    }

    /// <summary>
    /// A section of the record's own, which stays when the character it's shown as changes: what names it, over the model's cards
    /// when <paramref name="first"/>, or its fields, after them. Folded as it was last left.
    /// </summary>
    public void AddSection(string name, Control content, string note, bool flush = false, bool first = false)
    {
        var section = new Section(name, content, flush: flush) { Note = note, Open = Folded.GetValueOrDefault(name, true) };
        section.Toggled += open => Folded[name] = open;
        (first ? _first : _extras).AddChild(section);
    }

    /// <summary>The Parts section, for <c>--part</c>.</summary>
    public PartsSection? Parts => All.Select(s => s.Content).OfType<PartsSection>().FirstOrDefault();

    /// <summary>Every section, in order.</summary>
    private IEnumerable<Section> All => _first.GetChildren().Concat(_sections.GetChildren()).Concat(_extras.GetChildren()).Concat(_last.GetChildren()).OfType<Section>();

    /// <summary>Opens the sections named, by their titles in any case, and folds the rest (<c>--open</c>).</summary>
    public void OpenOnly(IReadOnlyCollection<string> names)
    {
        foreach (var section in All)
            section.Open = names.Contains(section.Title, StringComparer.OrdinalIgnoreCase);
    }

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

    /// <summary>The sections for the model and the character it's shown as, afresh, each folded as it was last left.</summary>
    private void Build()
    {
        foreach (var child in _sections.GetChildren().Concat(_last.GetChildren()))
        {
            child.GetParent().RemoveChild(child);
            child.QueueFree();
        }
        // The timeline's marks are the character's: its footsteps, its sounds and its strike.
        if (_ctx.ViewOrThrow.Actor is { } actor)
        {
            var shown = Shown;
            _ctx.ViewOrThrow.Marks = (clip, length) => TimelineMarks.For(_ctx.Plugins, actor, _ctx.Scene, shown, clip, length);
            _ctx.ViewOrThrow.RefreshMarks();
        }
        var model = new ModelSection(_ctx);
        Add("Model", model, model.Summary);
        var textures = new TexturesSection(_ctx);
        Add("Textures", textures, textures.Summary);
        var isActor = _ctx.ActorId is not null;
        // A model's parts follow its textures; an actor's are the last card, under its fields and the body over them.
        var parts = new PartsSection(_ctx);
        if (!isActor)
            Add("Parts", parts, parts.Summary, flush: true);
        if (_ctx.ViewOrThrow.Actor is { } dressed)
        {
            var held = new HeldSection(_ctx, dressed, Shown?.Entity);
            Add("Outfits and held", held, held.Summary);
        }
        if (Shown is { } wearer)
        {
            var alaron = wearer.Id == GameState.Alaron ? null : _ctx.Plugins.TryGet<EntityRecord>(GameState.Alaron)?.Body;
            Add("Body", new BodySection(_ctx.ViewOrThrow, wearer.Body, alaron), wearer.Name, last: true);
        }
        if (isActor)
            Add("Parts", parts, parts.Summary, flush: true, last: true, fill: true);
    }

    /// <param name="fill">Whether the card takes the height left under the rest, as the last card's table does.</param>
    private void Add(string name, Control content, string note, bool flush = false, bool last = false, bool fill = false)
    {
        var section = new Section(name, content, flush: flush, fill: fill) { Note = note, Open = Folded.GetValueOrDefault(name, true) };
        section.Toggled += open => Folded[name] = open;
        (last ? _last : _sections).AddChild(section);
    }
}

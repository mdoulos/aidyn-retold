using System.Globalization;
using AidynRetold.Core.Actors;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The actor's states as a graph (<see cref="GraphEdit"/>): a card per state, its animation and length in its title and a row per
/// branch, each row's port joined to the state it leads to, orange where it interrupts. The cards stand in columns by how many
/// branches from the initial state reach them, the states it never reaches in the last. Picking a card picks its state, as a row
/// of the table does.
/// </summary>
public partial class ActorPanel
{
    private const float ColumnWidth = 300, RowGap = 24;
    private static Color Plain => EditorTheme.Current.Dim;

    private readonly GraphEdit _graph = new()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false, MinimapEnabled = true, ShowGrid = false, ZoomMin = 0.15f, RightDisconnects = false,
        // Its menu would cover the first card; the wheel with Ctrl zooms, and the minimap finds the way.
        ShowMenu = false, Zoom = 0.75f,
    };

    /// <summary>The panel's height for the table, and the more the graph needs, which the view's split gives up.</summary>
    private const float TableHeight = 340, GraphHeight = 440;
    private readonly Button _tableButton = new() { Text = "Table", ToggleMode = true, ButtonPressed = true };
    private readonly Button _graphButton = new() { Text = "Graph", ToggleMode = true };
    private readonly Dictionary<int, GraphNode> _cards = [];
    private ActorRecord? _actor;
    private Func<int, int>? _length;
    private bool _graphBuilt;

    /// <summary>Table and Graph, which the model view puts in the timeline's row (<see cref="ViewButtons"/>).</summary>
    private void BuildViewButtons()
    {
        var group = new ButtonGroup();
        _tableButton.ButtonGroup = _graphButton.ButtonGroup = group;
        _tableButton.Pressed += () => ShowGraph(false);
        _graphButton.Pressed += () => ShowGraph(true);
        ViewButtons.AddChild(_tableButton);
        ViewButtons.AddChild(_graphButton);
        _graph.NodeSelected += node =>
        {
            if (!_selecting && node is GraphNode card && card.HasMeta("state"))
                StateClicked?.Invoke(card.GetMeta("state").AsInt32());
        };
    }

    /// <summary>Keeps the actor for the graph, which is built when first shown.</summary>
    private void KeepForGraph(ActorRecord actor, Func<int, int> length)
    {
        (_actor, _length, _graphBuilt) = (actor, length, false);
        if (_graph.Visible)
            BuildGraph();
    }

    private void ShowGraph(bool on)
    {
        _graph.Visible = on;
        _states.Visible = !on;
        CustomMinimumSize = new Vector2(0, on ? GraphHeight : TableHeight);
        if (on && !_graphBuilt)
            BuildGraph();
    }

    private void BuildGraph()
    {
        _graph.ClearConnections();
        foreach (var card in _cards.Values)
            card.QueueFree();
        _cards.Clear();
        if (_actor is not { } actor || _length is not { } length)
            return;
        _graphBuilt = true;

        // Columns by how many branches from the initial state reach a state.
        var depth = new Dictionary<int, int> { [actor.InitialState] = 0 };
        var queue = new Queue<int>([actor.InitialState]);
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var b in actor.States[at].Branches)
            {
                if (b.State >= 0 && b.State < actor.States.Count && depth.TryAdd(b.State, depth[at] + 1))
                    queue.Enqueue(b.State);
            }
        }
        var last = depth.Count == 0 ? 0 : depth.Values.Max() + 1;
        var heights = new Dictionary<int, float>();
        for (var n = 0; n < actor.States.Count; n++)
        {
            var state = actor.States[n];
            var card = new GraphNode
            {
                Title = $"{n}{(n == actor.InitialState ? " · initial" : "")} · {state.Animation.Key} · {length(n)} ticks", Name = $"state {n}",
                CustomMinimumSize = new Vector2(ColumnWidth - 40, 0), Resizable = false,
            };
            card.SetMeta("state", n);
            var into = EditorStyle.DimLabel("in", 12);
            card.AddChild(into);
            card.SetSlot(0, true, 0, Plain, false, 0, Plain);
            for (var i = 0; i < state.Branches.Count; i++)
            {
                var b = state.Branches[i];
                var row = new Label { Text = Branch(b), HorizontalAlignment = HorizontalAlignment.Right };
                row.AddThemeFontSizeOverride("font_size", 13);
                if (b.Interrupts)
                    row.AddThemeColorOverride("font_color", Interrupting);
                card.AddChild(row);
                card.SetSlot(i + 1, false, 0, Plain, true, 0, b.Interrupts ? Interrupting : Plain);
            }
            var column = depth.GetValueOrDefault(n, last);
            var y = heights.GetValueOrDefault(column);
            card.PositionOffset = new Vector2(column * ColumnWidth, y);
            heights[column] = y + 44 + (state.Branches.Count + 1) * 22 + RowGap;
            _graph.AddChild(card);
            _cards[n] = card;
        }
        Join();
    }

    /// <summary>Joins each branch's port to its state's.</summary>
    private void Join()
    {
        if (_actor is not { } actor || !_graphBuilt)
            return;
        _graph.ClearConnections();
        for (var n = 0; n < actor.States.Count; n++)
        {
            var branches = actor.States[n].Branches;
            for (var i = 0; i < branches.Count; i++)
            {
                if (_cards.TryGetValue(branches[i].State, out var to))
                    _graph.ConnectNode(_cards[n].Name, i, to.Name, 0);
            }
        }
    }

    /// <summary>Picks the card of the state the actor is in, while the graph shows.</summary>
    private void MarkCard(int state)
    {
        if (!_graph.Visible || !_cards.TryGetValue(state, out var card) || card.Selected)
            return;
        _selecting = true;
        foreach (var other in _cards.Values)
            other.Selected = other == card;
        _selecting = false;
    }
}

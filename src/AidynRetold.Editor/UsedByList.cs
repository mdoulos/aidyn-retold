using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// What names a record, as a card on a one-page record shows it, filling the card to its edges: a table of the records and their
/// summaries, with no heads, which say no more than the rows do, double-clicked to open one.
/// </summary>
public static class UsedByList
{
    /// <summary>The records naming it, one row each, with their summaries; a line saying so when nothing does.</summary>
    public static Control Create(PluginCatalog catalog, IReadOnlyList<RecordReference> references, Action<RecordId> open)
    {
        var groups = references.GroupBy(r => r.From).ToList();
        if (groups.Count == 0)
        {
            // Inset as a card's body is, as the card it's in is flush.
            var inset = new MarginContainer();
            foreach (var (side, by) in new[] { ("left", 24), ("right", 24), ("top", 18), ("bottom", 18) })
                inset.AddThemeConstantOverride($"margin_{side}", by);
            inset.AddChild(EditorStyle.DimLabel("Nothing names it."));
            return inset;
        }
        // A row is its text 13 high with the theme's 8 above and below; up to nine show, and only more scroll.
        var tree = new Tree
        {
            ThemeTypeVariation = "FlushTree",
            Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, Math.Min(groups.Count * 34 + 4, 9 * 34 + 4)), ScrollVerticalEnabled = groups.Count > 9,
        };
        tree.SetColumnExpand(0, false);
        tree.SetColumnCustomMinimumWidth(0, 190);
        tree.SetColumnClipContent(1, true);
        var root = tree.CreateItem();
        foreach (var group in groups)
        {
            var row = tree.CreateItem(root);
            var id = group.Key;
            row.SetText(0, id.Namespace == RecordId.RomNamespace ? $"{id.Type}/{id.Key}" : id.ToString());
            var paths = group.Select(r => r.Path).ToList();
            row.SetTooltipText(0, $"{id}, by {(paths.Count <= 6 ? string.Join(", ", paths) : $"{string.Join(", ", paths.Take(6))} and {paths.Count - 6} more")}");
            row.SetText(1, catalog.Find(id)?.Summary ?? "");
            row.SetMetadata(0, id.ToString());
        }
        tree.ItemActivated += () =>
        {
            if (tree.GetSelected()?.GetMetadata(0).AsString() is { } text && RecordId.TryParse(text, out var id))
                open(id);
        };
        return tree;
    }
}

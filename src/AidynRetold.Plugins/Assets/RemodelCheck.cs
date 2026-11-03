using System.Text.Json;

namespace AidynRetold.Plugins.Assets;

/// <summary>How a check of a replacement model came out.</summary>
public enum CheckLevel
{
    /// <summary>It keeps what the game needs.</summary>
    Pass,
    /// <summary>It works, but something the original had is gone or changed, which may show.</summary>
    Warn,
    /// <summary>The game can't play it as the original: a part, an animation or a locator it needs is missing.</summary>
    Fail,
}

/// <summary>One thing a replacement model was checked for, and what was found.</summary>
public sealed record CheckResult(CheckLevel Level, string What, string Detail);

/// <summary>
/// What a model that replaces another must keep for the game to play it as the original (docs/replacing-graphics.md#models),
/// checked against the original's glTF: the root part an actor moves by, every animation by its name with its length in ticks
/// (the game times its states by them), the locators that what's held, the head and the spells' effects hang from, the lights
/// and camera, and the model's extras. It also says whether part names are new that the game would read as parts, and whether
/// its textures keep their numbers, which the plugins' texture PNGs replace by (ADR 0044). It reads the files' JSON alone.
/// </summary>
public static class RemodelCheck
{
    private static readonly string[] ReservedPrefixes = ["transform ", "locator ", "light ", "emitter "];

    /// <param name="rootTransform">The actor's root transform, which it's moved by, or null for a model no actor animates.</param>
    public static IReadOnlyList<CheckResult> Compare(JsonElement original, JsonElement replacement, int? rootTransform)
    {
        var results = new List<CheckResult>();
        var originalNodes = NodeNames(original);
        var replacementNodes = NodeNames(replacement);

        if (rootTransform is { } root)
        {
            var name = SceneNames.Transform(root);
            results.Add(replacementNodes.Contains(name)
                ? new(CheckLevel.Pass, "Root part", $"{name} is there, which the game moves the character by.")
                : new(CheckLevel.Fail, "Root part", $"No node is named {name}: the game moves the character by it, and can't play the model without it."));
        }

        var before = Animations(original);
        var after = Animations(replacement);
        var missing = before.Keys.Where(n => !after.ContainsKey(n)).ToList();
        var unreadable = after.Where(a => a.Value is null).Select(a => a.Key).ToList();
        var lengths = before.Where(a => after.TryGetValue(a.Key, out var l) && l is not null && a.Value is not null && l != a.Value)
            .Select(a => $"{a.Key} ({a.Value} → {after[a.Key]})").ToList();
        if (missing.Count > 0)
            results.Add(new(CheckLevel.Fail, "Animations", $"{Count(missing.Count, "animation")} of the original's {before.Count} missing: {List(missing)}."));
        else if (before.Count > 0)
            results.Add(new(CheckLevel.Pass, "Animations", $"All {before.Count} of the original's are there."));
        if (unreadable.Count > 0)
            results.Add(new(CheckLevel.Fail, "Animation extras", $"{Count(unreadable.Count, "animation")} without the borg6 and length extras the game reads: {List(unreadable)}. Export with Custom Properties ticked."));
        if (lengths.Count > 0)
            results.Add(new(CheckLevel.Warn, "Animation lengths", $"{Count(lengths.Count, "animation")} changed length, which the game times its states by: {List(lengths)}."));
        if (after.Keys.Count(n => !before.ContainsKey(n)) is > 0 and var added)
            results.Add(new(CheckLevel.Pass, "New animations", $"{Count(added, "animation")} of its own, which the actor's states can play."));

        var locators = originalNodes.Where(n => SceneNames.TryLocator(n, out _)).Order(StringComparer.Ordinal).ToList();
        var lostLocators = locators.Where(n => !replacementNodes.Contains(n)).ToList();
        var lostAttachments = locators.Where(n => replacementNodes.Contains(n) && originalNodes.Contains(n + " attachment") && !replacementNodes.Contains(n + " attachment")).ToList();
        if (lostLocators.Count > 0)
            results.Add(new(CheckLevel.Fail, "Locators", $"Missing {List(lostLocators)}: what's held there, a head or a spell's effect has nowhere to go."));
        else if (locators.Count > 0)
            results.Add(new(CheckLevel.Pass, "Locators", $"All {locators.Count} are there."));
        if (lostAttachments.Count > 0)
            results.Add(new(CheckLevel.Warn, "Locator attachments", $"{List(lostAttachments.Select(l => l + " attachment"))} missing: a held item hangs from its locator unturned."));

        var lights = originalNodes.Where(n => SceneNames.TryLight(n, out _) || n == SceneNames.Camera).Where(n => !replacementNodes.Contains(n)).ToList();
        if (lights.Count > 0)
            results.Add(new(CheckLevel.Warn, "Lights and camera", $"Missing {List(lights)}, which the original had."));

        var extras = SceneExtrasKeys(original).Where(k => !SceneExtrasKeys(replacement).Contains(k)).ToList();
        if (extras.Count > 0)
            results.Add(new(CheckLevel.Warn, "Model extras", $"The original's {List(extras)} are gone. Export with Custom Properties ticked, or copy them across."));

        var stray = replacementNodes.Where(n => !originalNodes.Contains(n) && ReservedPrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal))
            && !n.EndsWith(" attachment", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        if (stray.Count > 0)
            results.Add(new(CheckLevel.Warn, "Part names", $"{List(stray)} aren't the original's, and the game reads them as parts, locators, lights or emitters."));

        var numbered = Images(replacement).Count(n => n is not null && SceneNames.TryImage(n, out _));
        results.Add(numbered > 0
            ? new(CheckLevel.Pass, "Textures", $"{Count(numbered, "texture")} keep their numbers, so the plugins' texture PNGs reach them.")
            : new(CheckLevel.Pass, "Textures", "Its textures are its own: the plugins' texture PNGs don't reach them."));
        return results;
    }

    /// <summary>Every node's name.</summary>
    private static HashSet<string> NodeNames(JsonElement root) =>
        root.TryGetProperty("nodes", out var nodes)
            ? [.. nodes.EnumerateArray().Select(n => n.TryGetProperty("name", out var name) ? name.GetString() : null).OfType<string>()]
            : [];

    /// <summary>Each animation by name, with its length in ticks from its extras, or null where they're missing.</summary>
    private static Dictionary<string, int?> Animations(JsonElement root)
    {
        var found = new Dictionary<string, int?>(StringComparer.Ordinal);
        if (!root.TryGetProperty("animations", out var animations))
            return found;
        foreach (var a in animations.EnumerateArray())
        {
            var name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null)
                continue;
            int? length = a.TryGetProperty("extras", out var e) && e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ExtrasKeys.Borg6, out _)
                && e.TryGetProperty(ExtrasKeys.Length, out var l) && l.TryGetInt32(out var ticks) ? ticks : null;
            found[name] = length;
        }
        return found;
    }

    private static IEnumerable<string?> Images(JsonElement root) =>
        root.TryGetProperty("images", out var images) ? images.EnumerateArray().Select(i => i.TryGetProperty("name", out var n) ? n.GetString() : null) : [];

    /// <summary>The keys of the glTF's scene's extras.</summary>
    private static HashSet<string> SceneExtrasKeys(JsonElement root)
    {
        var scene = root.TryGetProperty("scene", out var s) ? s.GetInt32() : 0;
        if (!root.TryGetProperty("scenes", out var scenes) || scene >= scenes.GetArrayLength() || !scenes[scene].TryGetProperty("extras", out var extras)
            || extras.ValueKind != JsonValueKind.Object)
            return [];
        return [.. extras.EnumerateObject().Select(p => p.Name)];
    }

    private static string Count(int n, string one) => $"{n} {(n == 1 ? one : one + "s")}";

    private static string List(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 5 ? string.Join(", ", list) : $"{string.Join(", ", list.Take(5))} and {list.Count - 5} more";
    }
}

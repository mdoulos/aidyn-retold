using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Model section of a model's inspector: what's in the model's file, the plugin it's from in the card's note (its ID is the view's
/// and the header's), and, when a plugin replaces it, a switch between the replacement and the original and the remodel check of the
/// one against the other (<see cref="RemodelCheck"/>). Its buttons export the file, replace it with another, checked first and
/// written into a plugin, and put the original back.
/// </summary>
public partial class ModelSection : VBoxContainer
{
    public ModelSection(ModelContext ctx)
    {
        // The file's line, then the buttons, set apart from it.
        AddThemeConstantOverride("separation", 12);
        var plugins = ctx.Plugins;
        var file = ctx.Original ? ctx.OriginalFile : ctx.File;
        var provider = file is null ? null : plugins.PluginWith(file);
        if (file is not null)
            AddChild(EditorStyle.DimLabel(Contents(file), 13));

        if (ctx.Replaced)
        {
            var which = new List<Button>();
            var group = new ButtonGroup();
            foreach (var (text, isOriginal) in new[] { ("Replacement", false), ("Original", true) })
            {
                var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, ButtonPressed = ctx.Original == isOriginal };
                button.Pressed += () =>
                {
                    if (ctx.Original != isOriginal)
                        ctx.ShowOriginal(isOriginal);
                };
                which.Add(button);
            }
            AddChild(new ButtonRows([.. which]));
        }

        var buttons = new List<Button>(FileButtons(this, ctx));
        if (ctx.Replaced && ctx.File is { } replacing && plugins.PluginWith(replacing) is { } replacer && replacer != RecordId.RomNamespace)
        {
            var back = new Button { Text = "Put Back Original", TooltipText = $"Take {replacer}'s model out, so the original shows again" };
            back.Pressed += () => PluginFiles.Remove(this, plugins, replacer, ctx.Scene, "glb", "the replacement model", ctx.Reload);
            buttons.Add(back);
        }
        AddChild(new ButtonRows([.. buttons]));

        if (ctx.Replaced && ctx.OriginalFile is { } originalFile && ctx.File is { } replacementFile)
        {
            var results = Check(ctx, originalFile, replacementFile);
            var check = new VBoxContainer();
            check.AddThemeConstantOverride("separation", 6);
            check.AddChild(EditorStyle.DimLabel("Remodel check, against the original", 13));
            check.AddChild(Results(results));
            AddChild(check);
            Summary = plugins.PluginWith(replacementFile) is { } by ? $"{EditorStyle.PluginName(by)} · {Verdict(results)}" : Verdict(results);
        }
        else
        {
            Summary = provider is null ? "" : EditorStyle.PluginName(provider);
        }
    }

    /// <summary>
    /// What to say after the section's title: the plugin the model's from, or, replaced, the plugin replacing it and how the remodel
    /// check came out.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// <b>Export Model</b> and <b>Replace Model</b> for a model, which a model's card and an armor's model panel both stand: the one
    /// saves its .glb, the other picks another, shows the remodel check of it and writes it into the mod.
    /// </summary>
    public static Button[] FileButtons(Node parent, ModelContext ctx)
    {
        var export = new Button { Text = "Export Model", TooltipText = "Save the model's .glb, to open in Blender" };
        export.Pressed += () => Export(parent, ctx);
        var replace = new Button { Text = "Replace Model", TooltipText = "Another .glb in its place, checked against the original, written into your mod" };
        replace.Pressed += () => Replace(parent, ctx);
        return [export, replace];
    }

    /// <summary>The root transform the check looks for: the actor's, for an actor's model.</summary>
    private static int? Root(ModelContext ctx) => ctx.ActorId is { } a ? ctx.Plugins.TryGet<AidynRetold.Plugins.Records.ActorRecord>(a)?.RootTransform : null;

    private static IReadOnlyList<CheckResult> Check(ModelContext ctx, string original, string replacement)
    {
        try
        {
            using var before = GlbReader.ReadJson(original);
            using var after = GlbReader.ReadJson(replacement);
            return RemodelCheck.Compare(before.RootElement, after.RootElement, Root(ctx));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return [new CheckResult(CheckLevel.Fail, "File", e.Message)];
        }
    }

    /// <summary>A line per check, its mark coloured by how it came out, the detail in its tooltip and under it.</summary>
    private static Control Results(IReadOnlyList<CheckResult> results)
    {
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 2);
        foreach (var r in results)
        {
            var row = EditorStyle.Row(6);
            var mark = new Label
            {
                Text = r.Level switch { CheckLevel.Pass => "✓", CheckLevel.Warn => "!", _ => "✗" }, CustomMinimumSize = new Vector2(14, 0),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            };
            mark.ThemeTypeVariation = r.Level switch { CheckLevel.Pass => "GoodLabel", CheckLevel.Warn => "ChangedLabel", _ => "ProblemLabel" };
            row.AddChild(mark);
            var text = new Label
            {
                Text = $"{r.What}: {r.Detail}", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(280, 0), TooltipText = r.Detail,
            };
            text.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(text);
            list.AddChild(row);
        }
        return list;
    }

    private static string Verdict(IReadOnlyList<CheckResult> results) =>
        results.Count(r => r.Level == CheckLevel.Fail) is > 0 and var fails ? $"check: {fails} failed"
        : results.Count(r => r.Level == CheckLevel.Warn) is > 0 and var warns ? $"check: {warns} to look at" : "check passed";

    /// <summary>What the model's file holds, in a line: its size, parts, animations and textures.</summary>
    private static string Contents(string path)
    {
        try
        {
            using var json = GlbReader.ReadJson(path);
            int Count(string key) => json.RootElement.TryGetProperty(key, out var list) ? list.GetArrayLength() : 0;
            return $"{new FileInfo(path).Length / 1024.0:N0} KB · {Count("nodes")} nodes · {Count("animations")} animations · {Count("images")} images";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            return e.Message;
        }
    }

    private static void Export(Node parent, ModelContext ctx)
    {
        if ((ctx.Original ? ctx.OriginalFile : ctx.File) is not { } path)
            return;
        PluginFiles.Save(parent, "Save the model's glTF", $"{ctx.Scene.Key}.glb", "*.glb ; glTF binaries", to =>
        {
            try
            {
                File.Copy(path, to, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                PluginFiles.Tell(parent, "Couldn't save it", e.Message);
            }
        });
    }

    /// <summary>Picks a .glb, shows the remodel check of it against the original, and writes it into a plugin if asked to.</summary>
    private static void Replace(Node parent, ModelContext ctx) =>
        PluginFiles.Open(parent, "A model to put in this one's place", "*.glb ; glTF binaries", path =>
        {
            var results = ctx.OriginalFile is { } original ? Check(ctx, original, path) : [];
            var fails = results.Count(r => r.Level == CheckLevel.Fail);
            EditorDialog.Confirm(parent, "Replace the model",
                $"{Path.GetFileName(path)}, checked against the original. " + (fails > 0 ? "Where it failed, the game can't play it as the original." : "It keeps what the game needs."),
                fails > 0 ? "Replace anyway" : "Replace", () =>
                {
                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(path);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        PluginFiles.Tell(parent, "Couldn't read it", e.Message);
                        return;
                    }
                    PluginFiles.Write(parent, ctx.Plugins, $"the new model for {ctx.Scene.Key}", folder => folder.WriteAsset(ctx.Scene, "glb", bytes), ctx.Reload);
                }, extra: Results(results), width: 580);
        });
}

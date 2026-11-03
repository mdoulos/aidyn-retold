using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Model section of a model's inspector: which plugin's file it is, what's in it, and, when a plugin replaces it, a switch
/// between the replacement and the original and the remodel check of the one against the other (<see cref="RemodelCheck"/>). Its
/// buttons export the file, replace it with another, checked first and written into a plugin, and put the original back.
/// </summary>
public partial class ModelSection : VBoxContainer
{
    private readonly ModelContext _ctx;

    public ModelSection(ModelContext ctx)
    {
        _ctx = ctx;
        AddThemeConstantOverride("separation", 6);
        var plugins = ctx.Plugins;
        var file = ctx.Original ? ctx.OriginalFile : ctx.File;
        var provider = file is null ? null : plugins.PluginWith(file);
        var original = ctx.OriginalFile is { } o ? plugins.PluginWith(o) : null;

        var from = EditorStyle.Row(4);
        from.AddChild(EditorStyle.DimLabel(ctx.Scene.ToString(), 13));
        if (original is not null)
            from.AddChild(EditorStyle.Chip(original, "gives the original model"));
        if (ctx.Replaced && provider is not null && provider != original)
        {
            from.AddChild(EditorStyle.DimLabel("›"));
            from.AddChild(EditorStyle.Chip(plugins.PluginWith(ctx.File!)!, "replaces the model", EditorStyle.Changed));
        }
        AddChild(from);
        if (file is not null)
            AddChild(EditorStyle.DimLabel(Contents(file), 13));

        if (ctx.Replaced)
        {
            var which = EditorStyle.Row(0);
            var group = new ButtonGroup();
            foreach (var (text, isOriginal) in new[] { ("Replacement", false), ("Original", true) })
            {
                var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, ButtonPressed = ctx.Original == isOriginal, CustomMinimumSize = new Vector2(110, 0) };
                button.Pressed += () =>
                {
                    if (ctx.Original != isOriginal)
                        ctx.ShowOriginal(isOriginal);
                };
                which.AddChild(button);
            }
            AddChild(which);
        }

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 6);
        var export = new Button { Text = "Export model…", TooltipText = "Save the model's .glb, to open in Blender" };
        export.Pressed += Export;
        buttons.AddChild(export);
        var replace = new Button { Text = "Replace model…", TooltipText = "Another .glb in its place, checked against the original, written into a plugin" };
        replace.Pressed += Replace;
        buttons.AddChild(replace);
        if (ctx.Replaced && ctx.File is { } replacing && plugins.PluginWith(replacing) is { } replacer && replacer != RecordId.RomNamespace)
        {
            var back = new Button { Text = "Put back the original", TooltipText = $"Take {replacer}'s model out, so the original shows again" };
            back.Pressed += () => PluginFiles.Remove(this, plugins, replacer, ctx.Scene, "glb", "the replacement model", ctx.Reload);
            buttons.AddChild(back);
        }
        AddChild(buttons);

        if (ctx.Replaced && ctx.OriginalFile is { } originalFile && ctx.File is { } replacementFile)
        {
            var results = Check(originalFile, replacementFile);
            AddChild(EditorStyle.DimLabel("Remodel check, against the original", 13));
            AddChild(Results(results));
            Summary = Verdict(results);
        }
        else
        {
            Summary = provider ?? "";
        }
    }

    /// <summary>What to say after the section's title: where the model's from, or how the remodel check came out.</summary>
    public string Summary { get; }

    /// <summary>The root transform the check looks for: the actor's, for an actor's model.</summary>
    private int? Root => _ctx.ActorId is { } a ? _ctx.Plugins.TryGet<AidynRetold.Plugins.Records.ActorRecord>(a)?.RootTransform : null;

    private IReadOnlyList<CheckResult> Check(string original, string replacement)
    {
        try
        {
            using var before = GlbReader.ReadJson(original);
            using var after = GlbReader.ReadJson(replacement);
            return RemodelCheck.Compare(before.RootElement, after.RootElement, Root);
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
            mark.AddThemeColorOverride("font_color", r.Level switch { CheckLevel.Pass => EditorStyle.Good, CheckLevel.Warn => EditorStyle.Changed, _ => EditorStyle.Problem });
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

    private void Export()
    {
        if ((_ctx.Original ? _ctx.OriginalFile : _ctx.File) is not { } path)
            return;
        PluginFiles.Save(this, "Save the model's glTF", $"{_ctx.Scene.Key}.glb", "*.glb ; glTF binaries", to =>
        {
            try
            {
                File.Copy(path, to, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                PluginFiles.Tell(this, "Couldn't save it", e.Message);
            }
        });
    }

    /// <summary>Picks a .glb, shows the remodel check of it against the original, and writes it into a plugin if asked to.</summary>
    private void Replace() =>
        PluginFiles.Open(this, "A model to put in this one's place", "*.glb ; glTF binaries", path =>
        {
            var results = _ctx.OriginalFile is { } original ? Check(original, path) : [];
            var fails = results.Count(r => r.Level == CheckLevel.Fail);
            var dialog = new ConfirmationDialog
            {
                Title = "Replace the model", OkButtonText = fails > 0 ? "Replace anyway" : "Replace", MinSize = new Vector2I(560, 0),
            };
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(540, 0) };
            box.AddChild(new Label
            {
                Text = $"{Path.GetFileName(path)} against the original. " + (fails > 0 ? "The game can't play it as the original where it failed." : "It keeps what the game needs."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(540, 0),
            });
            box.AddChild(Results(results));
            dialog.AddChild(box);
            dialog.Confirmed += () =>
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    PluginFiles.Tell(this, "Couldn't read it", e.Message);
                    return;
                }
                PluginFiles.Write(this, _ctx.Plugins, $"the model in {_ctx.Scene}'s place", folder => folder.WriteAsset(_ctx.Scene, "glb", bytes), _ctx.Reload);
            };
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
        });
}

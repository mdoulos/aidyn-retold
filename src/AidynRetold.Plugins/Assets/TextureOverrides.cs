namespace AidynRetold.Plugins.Assets;

/// <summary>How far a plugin's texture reaches: one model, or every model that uses the texture.</summary>
public enum TextureReach
{
    /// <summary>A model texture (<see cref="RecordTypes.ModelTexture"/>): the texture on that model alone.</summary>
    Model,
    /// <summary>A texture's own PNG from a plugin after the one that first gave it: on every model that uses it.</summary>
    Everywhere,
}

/// <summary>A plugin's PNG that a model draws in place of the copy inside its file, and how far it reaches.</summary>
/// <param name="Plugin">The ID of the plugin it comes from.</param>
public sealed record TextureOverride(string Path, string Plugin, TextureReach Reach);

/// <summary>
/// Where a model's texture comes from (ADR 0044). Each model's file carries its own copy of every texture it uses, each material
/// naming its texture's Borg (<see cref="ExtrasKeys.Borg1"/>), and each image named for it (<see cref="SceneNames.Image"/>). A plugin
/// replaces one on a single model with a model texture, <c>assets/aidyn/modeltexture/0x29C8/0x29A9.png</c>, or on every model with
/// the texture's own PNG, <c>assets/aidyn/texture/0x29A9.png</c>, which the ROM plugin also has for every texture: only a later
/// plugin's counts. A model texture comes before a texture everywhere, and either before the model's own copy.
/// </summary>
public static class TextureOverrides
{
    /// <summary>The PNG that <paramref name="model"/> draws for <paramref name="texture"/> in place of its own copy, or null for its own.</summary>
    public static TextureOverride? Find(PluginSet plugins, RecordId model, int texture)
    {
        if (plugins.FindAsset(RecordId.ModelTexture(model, texture), "png") is { } own)
            return new TextureOverride(own, plugins.PluginWith(own) ?? "?", TextureReach.Model);
        var everywhere = RecordId.Hex(RecordTypes.Texture, texture);
        if (plugins.FindAsset(everywhere, "png") is { } path && path != plugins.FindOriginalAsset(everywhere, "png"))
            return new TextureOverride(path, plugins.PluginWith(path) ?? "?", TextureReach.Everywhere);
        return null;
    }
}

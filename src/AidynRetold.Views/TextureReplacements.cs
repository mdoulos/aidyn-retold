using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using Godot;

namespace AidynRetold.Views;

/// <summary>
/// A model's textures from the plugins' PNGs, as it loads (ADR 0044, <see cref="TextureOverrides"/>): a model texture for that model,
/// else a texture's PNG from a plugin after the ROM plugin, else nothing, so the model's own copy is drawn. The PNG can be any size:
/// a model's texture coordinates are shares of the image, and the shader works out its texel from the texture it's given. A PNG is
/// read once, until its file is written again.
/// </summary>
public static class TextureReplacements
{
    private static readonly Dictionary<(string Path, DateTime Written), Texture2D?> Loaded = [];
    private static readonly Lock Gate = new();

    /// <summary>For <see cref="GlbFile.Load"/>: the texture a plugin gives <paramref name="model"/> for a Borg1, or null for its own copy.</summary>
    public static Func<int, Texture2D?> For(PluginSet plugins, RecordId model) =>
        borg1 => TextureOverrides.Find(plugins, model, borg1) is { } found ? Load(found.Path) : null;

    /// <summary>Forgets the textures read, which <see cref="Models.Dispose"/> does, so none is held at exit and a reload reads them afresh.</summary>
    public static void Clear()
    {
        lock (Gate)
            Loaded.Clear();
    }

    /// <summary>A PNG as a texture with mipmaps, as the model's own are made; null, with a warning, for a file that isn't one.</summary>
    public static Texture2D? Load(string path)
    {
        var key = (path, File.GetLastWriteTimeUtc(path));
        lock (Gate)
        {
            if (Loaded.TryGetValue(key, out var known))
                return known;
        }
        Texture2D? texture = null;
        if (Png.Load(path) is { } image)
        {
            if (!image.HasMipmaps())
                image.GenerateMipmaps();
            texture = ImageTexture.CreateFromImage(image);
        }
        else
        {
            GD.PushWarning($"{path} isn't a PNG Godot can read, so the model's own texture is drawn.");
        }
        lock (Gate)
            Loaded[key] = texture;
        return texture;
    }
}

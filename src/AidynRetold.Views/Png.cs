using AidynRetold.Core.Ui;
using Godot;

namespace AidynRetold.Views;

/// <summary>PNG files from the plugins' assets, as Godot images and textures.</summary>
public static class Png
{
    /// <summary>The image in a PNG file, or null if Godot can't read it.</summary>
    public static Image? Load(string path)
    {
        var image = new Image();
        return image.LoadPngFromBuffer(File.ReadAllBytes(path)) == Error.Ok ? image : null;
    }

    /// <summary>A texture of the image in a PNG file, or null if Godot can't read it.</summary>
    public static ImageTexture? Texture(string path) => Load(path) is { } image ? ImageTexture.CreateFromImage(image) : null;

    /// <summary>
    /// An image with mipmaps made by coverage (<see cref="CoverageMipmaps"/>), so the colour its clear pixels hold never reaches its
    /// edges at the smaller sizes, where Godot's own left dark smudges on the icons' edges (Nov 2, 2026). It changes
    /// <paramref name="data"/> to RGBA.
    /// </summary>
    public static Image Mipmapped(Image data)
    {
        data.Convert(Image.Format.Rgba8);
        data.ClearMipmaps();
        var (width, height) = (data.GetWidth(), data.GetHeight());
        return Image.CreateFromData(width, height, true, Image.Format.Rgba8, CoverageMipmaps.Chain(data.GetData(), width, height));
    }
}

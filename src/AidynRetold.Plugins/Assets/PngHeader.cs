using System.Buffers.Binary;

namespace AidynRetold.Plugins.Assets;

/// <summary>What a PNG asset's header says, read without decoding it.</summary>
public static class PngHeader
{
    /// <summary>A PNG's width and height in pixels, from its header, or null if it isn't a PNG.</summary>
    public static (int Width, int Height)? Size(string path)
    {
        Span<byte> header = stackalloc byte[24];
        using var file = File.OpenRead(path);
        if (file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length || header[12..16].SequenceCompareTo("IHDR"u8) != 0)
            return null;
        return Size(header);
    }

    /// <summary>A PNG's width and height from its bytes, or null if they aren't a PNG's.</summary>
    public static (int Width, int Height)? Size(ReadOnlySpan<byte> png) =>
        png.Length < 24 || png[12..16].SequenceCompareTo("IHDR"u8) != 0
            ? null
            : (BinaryPrimitives.ReadInt32BigEndian(png[16..]), BinaryPrimitives.ReadInt32BigEndian(png[20..]));
}

using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace AidynRetold.TestSupport;

/// <summary>Binary glTF files for tests to take apart and put back.</summary>
public static class TestGlb
{
    /// <summary>The file's JSON chunk, as a node to edit.</summary>
    public static JsonObject Json(byte[] glb)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        return JsonNode.Parse(glb.AsSpan(20, length))!.AsObject();
    }

    /// <summary><paramref name="glb"/> with its JSON chunk replaced by <paramref name="json"/> and its binary chunk kept, as a tool that edited the file would write it.</summary>
    public static byte[] WithJson(byte[] glb, JsonObject json)
    {
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        var binary = glb.AsSpan(20 + jsonLength).ToArray();
        var text = Encoding.UTF8.GetBytes(json.ToJsonString());
        var padded = (text.Length + 3) / 4 * 4;
        var result = new byte[20 + padded + binary.Length];
        glb.AsSpan(0, 12).CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), result.Length);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(12), padded);
        glb.AsSpan(16, 4).CopyTo(result.AsSpan(16));
        text.CopyTo(result, 20);
        result.AsSpan(20 + text.Length, padded - text.Length).Fill((byte)' ');
        binary.CopyTo(result, 20 + padded);
        return result;
    }
}

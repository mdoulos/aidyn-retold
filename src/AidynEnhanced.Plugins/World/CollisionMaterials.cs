using System.Globalization;
using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.Assets;

namespace AidynEnhanced.Plugins.World;

/// <summary>
/// How a chunk's export names the materials of its collision mesh, so that a surface, a trigger or a toggle is a
/// material a modeller assigns to faces, and how the import reads them back. A name is <c>collision</c>, the surface's
/// name (<see cref="SurfaceNames"/>), then <c>trigger n</c> or <c>toggle n</c> for a triangle with that role and target,
/// or <c>pak</c> for one only there with the Expansion Pak: <c>collision Grass</c>, <c>collision Player and camera</c>,
/// <c>collision Stone trigger 12</c>, <c>collision Grass pak</c>. A material named otherwise is read as Default collision,
/// and the import says so.
/// </summary>
public static class CollisionMaterials
{
    public const string Prefix = "collision";
    private const string Trigger = "trigger", Toggle = "toggle", Pak = "pak";

    /// <summary>What a material stands for: the attributes shared by the triangles it's assigned to.</summary>
    public readonly record struct Kind(int Surface, CollisionRole Role, int Target)
    {
        public static Kind Of(CollisionTriangle t) => new(t.Surface, t.Role, t.Role is CollisionRole.Trigger or CollisionRole.Toggle ? t.Target : 0);
    }

    /// <summary>The material name of a kind.</summary>
    public static string Name(Kind kind)
    {
        var name = Prefix + " " + SurfaceNames.Of(kind.Surface);
        return kind.Role switch
        {
            CollisionRole.Trigger => $"{name} {Trigger} {kind.Target.ToString(CultureInfo.InvariantCulture)}",
            CollisionRole.Toggle => $"{name} {Toggle} {kind.Target.ToString(CultureInfo.InvariantCulture)}",
            CollisionRole.ExpansionPak => $"{name} {Pak}",
            _ => name,
        };
    }

    /// <summary>
    /// Reads a material name back. A copy's suffix (<c>.001</c>, as Blender numbers a duplicate) is ignored. Returns false for
    /// a name that isn't a collision material's, which the import reads as <see cref="Default"/>.
    /// </summary>
    public static bool TryParse(string name, out Kind kind)
    {
        kind = Default;
        var text = StripCopySuffix(name).Trim();
        if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        text = text[Prefix.Length..].Trim();
        var role = CollisionRole.Normal;
        var target = 0;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 1 && words[^1].Equals(Pak, StringComparison.OrdinalIgnoreCase))
        {
            role = CollisionRole.ExpansionPak;
            words = words[..^1];
        }
        else if (words.Length >= 2 && int.TryParse(words[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && (words[^2].Equals(Trigger, StringComparison.OrdinalIgnoreCase) || words[^2].Equals(Toggle, StringComparison.OrdinalIgnoreCase)))
        {
            role = words[^2].Equals(Trigger, StringComparison.OrdinalIgnoreCase) ? CollisionRole.Trigger : CollisionRole.Toggle;
            target = n;
            words = words[..^2];
        }
        var surfaceName = string.Join(' ', words);
        int surface;
        if (surfaceName.Length == 0)
        {
            surface = 0;
        }
        else if (SurfaceNames.All.Select((s, i) => (s, i)).FirstOrDefault(p => p.s.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) is { s: not null } found)
        {
            surface = found.i;
        }
        else if (surfaceName.StartsWith("Surface ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(surfaceName.AsSpan("Surface ".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            surface = number;
        }
        else
        {
            return false;
        }
        kind = new Kind(surface, role, target);
        return true;
    }

    /// <summary>Default collision: surface 0, no role.</summary>
    public static Kind Default => new(0, CollisionRole.Normal, 0);

    /// <summary>A name without the <c>.001</c> a copy gets.</summary>
    public static string StripCopySuffix(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 && name.AsSpan(dot + 1).ToArray().All(char.IsAsciiDigit) ? name[..dot] : name;
    }

    /// <summary>
    /// The glTF material of a kind: named for it, two-sided and coloured as the editor colours the surface, so the mesh
    /// reads in a modeller, with the kind in its extras for tools.
    /// </summary>
    public static JsonObject Material(Kind kind)
    {
        var (r, g, b) = Colour(kind);
        return new JsonObject
        {
            ["name"] = Name(kind),
            ["doubleSided"] = true,
            ["pbrMetallicRoughness"] = new JsonObject
            {
                ["baseColorFactor"] = new JsonArray(Linear(r), Linear(g), Linear(b), 1f),
                ["metallicFactor"] = 0f,
                ["roughnessFactor"] = 1f,
            },
            ["extras"] = new JsonObject { ["surface"] = kind.Surface, ["role"] = kind.Role.ToString(), ["target"] = kind.Target },
        };
    }

    /// <summary>The editor's colour for a surface (<c>CollisionLayer</c>), as on-screen sRGB, 0–1; a trigger is tinted magenta and a toggle orange, as there.</summary>
    public static (float R, float G, float B) Colour(Kind kind)
    {
        var html = kind.Role switch
        {
            CollisionRole.Trigger => "#ff4dd2",
            CollisionRole.Toggle => "#ff9f43",
            CollisionRole.ExpansionPak => "#ffe14d",
            _ => kind.Surface >= 0 && kind.Surface < SurfaceColours.Length ? SurfaceColours[kind.Surface] : null,
        };
        if (html is null)
            return Hsv(kind.Surface * 0.13f % 1, 0.7f, 0.85f);
        return (Convert.ToInt32(html[1..3], 16) / 255f, Convert.ToInt32(html[3..5], 16) / 255f, Convert.ToInt32(html[5..7], 16) / 255f);
    }

    private static readonly string[] SurfaceColours =
    [
        "#777777", "#b0b0b8", "#ff4d4d", "#ffffff", "#5cb85c", "#e8d18a", "#7a5c3a", "#ff8c1a", "#6b6b8f",
        "#b07a45", "#8f5a2e", "#9aa4ad", "#76828c", "#56606a", "#3d8bff",
    ];

    private static (float, float, float) Hsv(float h, float s, float v)
    {
        var i = (int)(h * 6) % 6;
        var f = h * 6 - MathF.Floor(h * 6);
        float p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        return i switch { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
    }

    private static float Linear(float srgb) => srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
}

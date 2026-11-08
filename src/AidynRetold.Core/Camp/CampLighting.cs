using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Camp;

/// <summary>A light that casts a figure's shadow at camp: the sun, along its way, or the fire, out from it.</summary>
public enum CampLightKind
{
    Sun,
    Fire,
}

/// <summary>A shadow a light casts from a figure, and how dark it is: the share of the figure's light that light gives, 0 to 1.</summary>
public sealed record CampShadowCaster(CampLightKind Light, float Darkness);

/// <summary>
/// Which lights cast the figures' shadows at camp, and how dark, the user's design (Nov 7, 2026): the shadows match the scene, the biome
/// and the time of day, as its lights do (<see cref="CampsiteRecord.LightAt"/>). A shadow is where one light doesn't reach, so it's as dark
/// as that light's share of all the light on the figure, the ambient's included: in the forest's day the sun gives about half and its
/// shadow is about half dark; at night, with no sun, the fire gives most and casts its own, out from it. A light too faint against the
/// rest to be noticed casts none (<see cref="Faintest"/>).
/// </summary>
public static class CampLighting
{
    /// <summary>The share under which a light casts no shadow, and over which its shadow is all there; between, it fades in.</summary>
    public const float Faintest = 0.08f, Noticed = 0.16f;

    /// <summary>A colour's brightness, 0 to 1, as the eye weighs it.</summary>
    public static float Luminance(Rgba color) => (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255;

    /// <summary>How much light the sun gives a figure: its strength times its colour's brightness, none where it's down or there's none.</summary>
    public static float Sunlight(CampSun? sun) => sun is { Y: > 0 } up ? Math.Max(0, up.Strength) * Luminance(up.Color) : 0;

    /// <summary>
    /// The shadows a figure casts and how dark: the sun's and the fire's, each its share of the ambient, the sun and the fire on it
    /// together, faded out as it falls under what's noticed; the strongest first.
    /// </summary>
    /// <param name="ambient">The ambient light on the figure (<see cref="Luminance"/> of the campsite's).</param>
    /// <param name="sun">The sun's light on it (<see cref="Sunlight"/>).</param>
    /// <param name="fire">The fire's light on it where it stands, after its fall-off with the distance.</param>
    public static IReadOnlyList<CampShadowCaster> Casters(float ambient, float sun, float fire)
    {
        var total = Math.Max(0, ambient) + Math.Max(0, sun) + Math.Max(0, fire);
        if (total <= 0)
            return [];
        var casters = new List<CampShadowCaster>();
        foreach (var (kind, light) in new[] { (CampLightKind.Sun, sun), (CampLightKind.Fire, fire) })
        {
            var share = Math.Max(0, light) / total;
            var shown = Smooth((share - Faintest) / (Noticed - Faintest));
            if (shown > 0)
                casters.Add(new CampShadowCaster(kind, share * shown));
        }
        return [.. casters.OrderByDescending(c => c.Darkness)];
    }

    private static float Smooth(float x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }
}

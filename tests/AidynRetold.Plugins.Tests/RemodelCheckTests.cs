using System.Text.Json.Nodes;
using AidynRetold.Plugins.Assets;

namespace AidynRetold.Plugins.Tests;

/// <summary>What <see cref="RemodelCheck"/> says of a replacement model against the original it replaces.</summary>
public sealed class RemodelCheckTests
{
    /// <summary>A model's glTF JSON: these node names (each locator with its attachment), these animations with their lengths, and image names.</summary>
    private static System.Text.Json.JsonElement Model(string[] nodes, (string Name, int? Length)[] animations, string[]? images = null, bool extras = true)
    {
        var gltf = new GltfBuilder();
        var roots = nodes.Select(n => gltf.AddNode(new JsonObject { ["name"] = n })).ToList();
        foreach (var (name, length) in animations)
        {
            var animation = new JsonObject { ["name"] = name, ["channels"] = new JsonArray(), ["samplers"] = new JsonArray() };
            if (length is { } l)
                animation["extras"] = new JsonObject { [ExtrasKeys.Borg6] = int.Parse(name[6..]), [ExtrasKeys.Length] = l };
            gltf.AddAnimation(animation);
        }
        foreach (var image in images ?? [])
            gltf.AddImage(new JsonObject { ["name"] = image, ["uri"] = "x.png" });
        var glb = gltf.ToGlb(roots, extras ? new JsonObject { [ExtrasKeys.AmbientLight] = new JsonArray(1, 1, 1) } : null);
        return GlbReader.Parse(glb).Root.Clone();
    }

    private static readonly System.Text.Json.JsonElement Original = Model(
        ["transform 2", "transform 3", "locator 0", "locator 0 attachment", "locator 2", "locator 2 attachment", "light 0"],
        [("borg6 100", 25), ("borg6 101", 33)], ["borg1 10665"]);

    private static CheckLevel Level(IReadOnlyList<CheckResult> results, string what) => results.Single(r => r.What == what).Level;

    [Fact]
    public void AReshapedCopyKeepsEverythingAndPasses()
    {
        var results = RemodelCheck.Compare(Original, Original, rootTransform: 2);
        Assert.All(results, r => Assert.Equal(CheckLevel.Pass, r.Level));
        Assert.Contains("1 texture keep", results.Single(r => r.What == "Textures").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ARemodelWithoutTheRootAnAnimationOrALocatorFails()
    {
        var remodel = Model(["transform 3", "locator 0", "locator 0 attachment", "light 0"], [("borg6 100", 25)]);
        var results = RemodelCheck.Compare(Original, remodel, rootTransform: 2);
        Assert.Equal(CheckLevel.Fail, Level(results, "Root part"));
        Assert.Equal(CheckLevel.Fail, Level(results, "Animations"));
        Assert.Contains("borg6 101", results.Single(r => r.What == "Animations").Detail, StringComparison.Ordinal);
        Assert.Equal(CheckLevel.Fail, Level(results, "Locators"));
        Assert.Contains("locator 2", results.Single(r => r.What == "Locators").Detail, StringComparison.Ordinal);
        Assert.Contains("own", results.Single(r => r.What == "Textures").Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void LostExtrasChangedLengthsAndStrayPartNamesAreSaid()
    {
        var remodel = Model(["transform 2", "transform 3", "locator 0", "locator 2", "locator 2 attachment", "transform 9", "outfit plain"],
            [("borg6 100", 30), ("borg6 101", null), ("borg6 1", 20)], ["borg1 10665"], extras: false);
        var results = RemodelCheck.Compare(Original, remodel, rootTransform: 2);
        Assert.Equal(CheckLevel.Pass, Level(results, "Root part"));
        Assert.Equal(CheckLevel.Pass, Level(results, "Animations"));
        Assert.Equal(CheckLevel.Fail, Level(results, "Animation extras"));
        Assert.Equal(CheckLevel.Warn, Level(results, "Animation lengths"));
        Assert.Contains("borg6 100 (25 → 30)", results.Single(r => r.What == "Animation lengths").Detail, StringComparison.Ordinal);
        Assert.Equal(CheckLevel.Pass, Level(results, "New animations"));
        Assert.Equal(CheckLevel.Warn, Level(results, "Locator attachments"));
        Assert.Equal(CheckLevel.Warn, Level(results, "Lights and camera"));
        Assert.Equal(CheckLevel.Warn, Level(results, "Model extras"));
        // A new mesh's node named outside the game's parts is fine; one named like a part isn't.
        Assert.Equal("transform 9 aren't the original's, and the game reads them as parts, locators, lights or emitters.",
            results.Single(r => r.What == "Part names").Detail);
    }

    [Fact]
    public void AModelNoActorAnimatesIsntCheckedForARoot() =>
        Assert.DoesNotContain(RemodelCheck.Compare(Original, Original, rootTransform: null), r => r.What == "Root part");
}

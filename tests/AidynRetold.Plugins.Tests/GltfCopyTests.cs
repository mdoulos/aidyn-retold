using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.World;

namespace AidynRetold.Plugins.Tests;

public sealed class GltfCopyTests
{
    /// <summary>A file with a parent node over a lit child mesh with a texture, a camera, a light, and an animation that moves the child.</summary>
    internal static byte[] SampleGlb()
    {
        var gltf = new GltfBuilder();
        var png = TestPng.Encode(2, 2, 2, [255, 0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0]);
        var image = gltf.AddPng(png, "borg1 7");
        var sampler = gltf.AddSampler(9728, 9728, 10497, 33648);
        var texture = gltf.AddTexture(image, sampler);
        var material = gltf.AddMaterial(new JsonObject
        {
            ["name"] = "m",
            ["pbrMetallicRoughness"] = new JsonObject { ["baseColorTexture"] = new JsonObject { ["index"] = texture } },
            ["extras"] = new JsonObject { [ExtrasKeys.Borg1] = 7 },
        });
        var mesh = gltf.AddMesh(new JsonObject
        {
            ["name"] = "mesh",
            ["primitives"] = new JsonArray(new JsonObject
            {
                ["attributes"] = new JsonObject
                {
                    ["POSITION"] = gltf.AddPositions([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)]),
                    ["TEXCOORD_0"] = gltf.AddVectors([new Vector2(0, 0), new(1, 0), new(0, 1)]),
                },
                ["indices"] = gltf.AddIndices([0, 1, 2], 3),
                ["material"] = material,
                ["extras"] = new JsonObject { ["displayList"] = 3 },
            }),
        });
        var camera = gltf.AddCamera(new JsonObject { ["type"] = "perspective", ["perspective"] = new JsonObject { ["yfov"] = 0.7, ["znear"] = 0.1 } });
        var light = gltf.AddLight(new JsonObject { ["type"] = "directional", ["color"] = new JsonArray(1, 0.5, 0.25) });
        var child = gltf.AddNode(new JsonObject
        {
            ["name"] = "transform 1",
            ["mesh"] = mesh,
            ["translation"] = new JsonArray(1, 2, 3),
            ["extras"] = new JsonObject { [ExtrasKeys.Transform] = 1 },
        });
        var lightNode = gltf.AddNode(new JsonObject { ["name"] = "light 0", ["extensions"] = new JsonObject { ["KHR_lights_punctual"] = new JsonObject { ["light"] = light } } });
        var cameraNode = gltf.AddNode(new JsonObject { ["name"] = "camera", ["camera"] = camera });
        var parent = gltf.AddNode(new JsonObject { ["name"] = "transform 0", ["children"] = new JsonArray(child, lightNode) });
        gltf.AddAnimation(new JsonObject
        {
            ["name"] = "borg6 5",
            ["channels"] = new JsonArray(
                new JsonObject { ["sampler"] = 0, ["target"] = new JsonObject { ["node"] = child, ["path"] = "translation" } },
                new JsonObject { ["sampler"] = 1, ["target"] = new JsonObject { ["node"] = cameraNode, ["path"] = "translation" } }),
            ["samplers"] = new JsonArray(
                new JsonObject { ["input"] = gltf.AddTimes([0, 1]), ["output"] = gltf.AddKeyValues([new Vector3(0, 0, 0), new Vector3(1, 1, 1)]) },
                new JsonObject { ["input"] = gltf.AddTimes([0, 1]), ["output"] = gltf.AddKeyValues([new Vector3(0, 0, 0), new Vector3(2, 2, 2)]) }),
            ["extras"] = new JsonObject { [ExtrasKeys.Borg6] = 5, [ExtrasKeys.Length] = 60 },
        });
        return gltf.ToGlb([parent, cameraNode], new JsonObject { [ExtrasKeys.AmbientLight] = new JsonArray(0.1, 0.2, 0.3) });
    }

    [Fact]
    public void Copies_a_subtree_with_what_it_uses_and_renumbers_everything()
    {
        using var source = GlbReader.Parse(SampleGlb());
        var into = new GltfBuilder();
        // Something already in the target, so that every index has to move.
        into.AddNode(new JsonObject { ["name"] = "first" });
        into.AddMaterial(new JsonObject { ["name"] = "taken" });
        into.AddAccessor(new JsonObject { ["componentType"] = 5126, ["count"] = 0, ["type"] = "SCALAR" });

        var parentIndex = GltfCopy.SceneRoots(source)[0];
        var roots = GltfCopy.Copy(source, into, [parentIndex]);
        var holder = into.AddNode(new JsonObject { ["name"] = "holder", ["children"] = new JsonArray(roots.Select(r => (JsonNode)r).ToArray()) });

        using var copy = GlbReader.Parse(into.ToGlb([0, holder]));
        var root = copy.Root;
        var nodes = root.GetProperty("nodes");
        var parent = nodes[roots[0]];
        Assert.Equal("transform 0", parent.GetProperty("name").GetString());
        var children = parent.GetProperty("children").EnumerateArray().Select(c => c.GetInt32()).ToArray();
        Assert.Equal(2, children.Length);
        var child = nodes[children[0]];
        Assert.Equal("transform 1", child.GetProperty("name").GetString());
        Assert.Equal(1, child.GetProperty("extras").GetProperty(ExtrasKeys.Transform).GetInt32());
        Assert.Equal(new[] { 1f, 2f, 3f }, child.GetProperty("translation").EnumerateArray().Select(v => v.GetSingle()).ToArray());

        // The mesh, its material and texture came along, with their extras, and the geometry reads back.
        var mesh = root.GetProperty("meshes")[child.GetProperty("mesh").GetInt32()];
        var primitive = mesh.GetProperty("primitives")[0];
        Assert.Equal(3, primitive.GetProperty("extras").GetProperty("displayList").GetInt32());
        var positions = copy.Floats(primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32());
        Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }, positions);
        Assert.Equal(new[] { 0, 1, 2 }, copy.Indices(primitive.GetProperty("indices").GetInt32()));
        var material = root.GetProperty("materials")[primitive.GetProperty("material").GetInt32()];
        Assert.Equal("m", material.GetProperty("name").GetString());
        Assert.Equal(7, material.GetProperty("extras").GetProperty(ExtrasKeys.Borg1).GetInt32());
        var texture = root.GetProperty("textures")[material.GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32()];
        var image = root.GetProperty("images")[texture.GetProperty("source").GetInt32()];
        Assert.Equal("borg1 7", image.GetProperty("name").GetString());
        Assert.Equal(TestPng.Encode(2, 2, 2, [255, 0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0]), copy.BufferViewBytes(image.GetProperty("bufferView").GetInt32()));
        Assert.Equal(33648, root.GetProperty("samplers")[texture.GetProperty("sampler").GetInt32()].GetProperty("wrapT").GetInt32());

        // The light under the parent came, the camera that wasn't under it didn't.
        var light = nodes[children[1]].GetProperty("extensions").GetProperty("KHR_lights_punctual").GetProperty("light").GetInt32();
        Assert.Equal(0.5f, root.GetProperty("extensions").GetProperty("KHR_lights_punctual").GetProperty("lights")[light].GetProperty("color")[1].GetSingle());
        Assert.False(root.TryGetProperty("cameras", out _));
        Assert.Contains("KHR_lights_punctual", root.GetProperty("extensionsUsed").EnumerateArray().Select(e => e.GetString()));

        // The animation is cut to the channel that moves a copied node, pointing at the new node.
        var animation = root.GetProperty("animations")[0];
        Assert.Equal("borg6 5", animation.GetProperty("name").GetString());
        var channels = animation.GetProperty("channels");
        Assert.Equal(1, channels.GetArrayLength());
        Assert.Equal(children[0], channels[0].GetProperty("target").GetProperty("node").GetInt32());
        Assert.Equal(1, animation.GetProperty("samplers").GetArrayLength());
        var output = copy.Floats(animation.GetProperty("samplers")[0].GetProperty("output").GetInt32());
        Assert.Equal(new float[] { 0, 0, 0, 1, 1, 1 }, output);
    }

    [Fact]
    public void Copies_from_one_file_share_meshes_and_renaming_applies_to_every_node()
    {
        using var source = GlbReader.Parse(SampleGlb());
        var into = new GltfBuilder();
        var copier = new GltfCopy(source, into, name => name + ".001");
        var first = copier.Copy(copier.SceneRoots());
        var second = copier.Copy(copier.SceneRoots());
        using var copy = GlbReader.Parse(into.ToGlb([.. first, .. second]));
        var root = copy.Root;
        Assert.Equal(1, root.GetProperty("meshes").GetArrayLength());
        Assert.Equal(1, root.GetProperty("materials").GetArrayLength());
        Assert.Equal(1, root.GetProperty("images").GetArrayLength());
        Assert.Equal(2, root.GetProperty("animations").GetArrayLength());
        Assert.Equal(8, root.GetProperty("nodes").GetArrayLength());
        Assert.All(root.GetProperty("nodes").EnumerateArray(), n => Assert.EndsWith(".001", n.GetProperty("name").GetString()));
        Assert.NotEqual(first[0], second[0]);
    }
}

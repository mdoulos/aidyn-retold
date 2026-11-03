using System.Numerics;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using AidynRetold.Plugins.World;

namespace AidynRetold.Plugins.Tests;

public sealed class CollisionMaterialsTests
{
    [Theory]
    [InlineData(4, CollisionRole.Normal, 0, "collision Grass")]
    [InlineData(1, CollisionRole.Normal, 0, "collision Player and camera")]
    [InlineData(11, CollisionRole.Trigger, 12, "collision Stone trigger 12")]
    [InlineData(0, CollisionRole.Toggle, 3, "collision Default toggle 3")]
    [InlineData(25, CollisionRole.ExpansionPak, 0, "collision Trans stone huge echo pak")]
    [InlineData(30, CollisionRole.Normal, 0, "collision Surface 30")]
    public void Names_round_trip(int surface, CollisionRole role, int target, string name)
    {
        var kind = new CollisionMaterials.Kind(surface, role, target);
        Assert.Equal(name, CollisionMaterials.Name(kind));
        Assert.True(CollisionMaterials.TryParse(name, out var parsed));
        Assert.Equal(kind, parsed);
    }

    [Theory]
    [InlineData("collision Grass.001", 4, CollisionRole.Normal, 0)]
    [InlineData("Collision grass", 4, CollisionRole.Normal, 0)]
    [InlineData("collision", 0, CollisionRole.Normal, 0)]
    [InlineData("collision Stone Trigger 7.003", 11, CollisionRole.Trigger, 7)]
    public void Reads_copies_and_cases(string name, int surface, CollisionRole role, int target)
    {
        Assert.True(CollisionMaterials.TryParse(name, out var kind));
        Assert.Equal(new CollisionMaterials.Kind(surface, role, target), kind);
    }

    [Theory]
    [InlineData("Material")]
    [InlineData("collision Marble")]
    [InlineData("")]
    public void Refuses_other_names(string name) => Assert.False(CollisionMaterials.TryParse(name, out _));

    [Fact]
    public void A_triangle_s_kind_keeps_the_target_only_where_the_role_reads_it()
    {
        var trigger = new CollisionTriangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, 4, CollisionRole.Trigger, CollisionFlags.None, 9);
        Assert.Equal(new CollisionMaterials.Kind(4, CollisionRole.Trigger, 9), CollisionMaterials.Kind.Of(trigger));
        var plain = trigger with { Role = CollisionRole.Normal };
        Assert.Equal(new CollisionMaterials.Kind(4, CollisionRole.Normal, 0), CollisionMaterials.Kind.Of(plain));
    }

    [Fact]
    public void The_material_is_named_coloured_and_two_sided()
    {
        var material = CollisionMaterials.Material(new CollisionMaterials.Kind(14, CollisionRole.Normal, 0));
        Assert.Equal("collision Water", material["name"]!.GetValue<string>());
        Assert.True(material["doubleSided"]!.GetValue<bool>());
        var colour = material["pbrMetallicRoughness"]!["baseColorFactor"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray();
        Assert.True(colour[2] > colour[0], "water is blue");
        Assert.Equal(14, material["extras"]!["surface"]!.GetValue<int>());
    }

    [Fact]
    public void A_scene_object_s_rotation_comes_back_from_its_orientation()
    {
        var random = new Random(7);
        for (var i = 0; i < 500; i++)
        {
            var rotation = new Vector3(Angle(random), Angle(random), Angle(random));
            if (i % 5 == 0)
                rotation.X = rotation.Z = 0; // most objects turn about Y alone
            var scene = new SceneObject(0, Vector3.Zero, 1, PakVariant.Any, new FlagGate(Ids.Flag(1)), [], 0, rotation, Vector3.One, default, SceneObjectFlags.None);
            var q = scene.Orientation();
            var back = scene with { Rotation = SceneObject.RotationOf(q) };
            var q2 = back.Orientation();
            // The same turn: q and −q are one rotation.
            var dot = MathF.Abs(Quaternion.Dot(q, q2));
            Assert.True(dot > 1 - 1e-4f, $"{rotation} came back as {back.Rotation}: {q} vs {q2}");
        }

        static float Angle(Random r) => (float)(r.NextDouble() * 2 * Math.PI - Math.PI);
    }

    [Fact]
    public void A_pitch_of_ninety_degrees_comes_back_as_a_turn()
    {
        var scene = new SceneObject(0, Vector3.Zero, 1, PakVariant.Any, new FlagGate(Ids.Flag(1)), [], 0, new Vector3(0.3f, MathF.PI / 2, 0.4f), Vector3.One, default, SceneObjectFlags.None);
        var q = scene.Orientation();
        var back = SceneObject.RotationOf(q);
        var q2 = (scene with { Rotation = back }).Orientation();
        Assert.True(MathF.Abs(Quaternion.Dot(q, q2)) > 1 - 1e-4f);
    }
}

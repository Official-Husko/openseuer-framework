using ObenFramework.World.Props;

namespace ObenCore.Tests;

public class PropRulesTests
{
    private static PropRecord Cube(string mod = "MyMod", string id = "crate") => new()
    {
        Mod = mod,
        Id = id,
        Scene = "Interior Player Tenement",
        Kind = WorldPropKind.Primitive,
        Alt = (int)WorldPrimitive.Cube,
    };

    [Fact]
    public void A_plain_cube_is_valid()
    {
        Assert.True(PropRules.Valid(Cube()));
    }

    [Theory]
    [InlineData(0.01f, true)]
    [InlineData(100f, true)]
    [InlineData(0.005f, false)]
    [InlineData(101f, false)]
    [InlineData(float.NaN, false)]
    public void Scale_must_stay_in_range(float axis, bool allowed)
    {
        Assert.Equal(allowed, PropRules.ScaleAllowed(new[] { 1f, axis, 1f }));
    }

    [Fact]
    public void Poses_must_be_finite_and_the_right_length()
    {
        Assert.True(PropRules.PoseAllowed(new[] { 1f, 2f, 3f }, new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 1f, 1f }));
        Assert.False(PropRules.PoseAllowed(new[] { 1f, float.PositiveInfinity, 3f }, new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 1f, 1f }));
        Assert.False(PropRules.PoseAllowed(new[] { 1f, 2f }, new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 1f, 1f }));
        Assert.False(PropRules.PoseAllowed(new[] { 1f, 2f, 3f }, null, new[] { 1f, 1f, 1f }));
    }

    [Theory]
    [InlineData("MyMod/crate", true)]
    [InlineData("My.Mod/crate_2-b", true)]
    [InlineData("crate", false)]
    [InlineData("/crate", false)]
    [InlineData("MyMod/", false)]
    [InlineData("MyMod/sub/crate", false)]
    [InlineData("MyMod/cr ate", false)]
    public void Model_names_are_mod_and_key(string name, bool valid)
    {
        Assert.Equal(valid, PropRules.SplitModelKey(name, out _, out _));
    }

    [Fact]
    public void Each_kind_checks_its_own_fields()
    {
        var model = Cube();
        model.Kind = WorldPropKind.Model;
        model.Key = "MyMod/crate";
        Assert.True(PropRules.Valid(model));

        var furniture = Cube();
        furniture.Kind = WorldPropKind.Furniture;
        Assert.False(PropRules.Valid(furniture));
        furniture.Key = "Red Metal Chair";
        Assert.True(PropRules.Valid(furniture));

        var item = Cube();
        item.Kind = WorldPropKind.Item;
        item.Alt = 0;
        Assert.False(PropRules.Valid(item));
        item.Alt = 12;
        Assert.True(PropRules.Valid(item));

        var shape = Cube();
        shape.Alt = 7;
        Assert.False(PropRules.Valid(shape));
    }

    [Fact]
    public void Lights_need_sensible_numbers()
    {
        Assert.Null(PropRules.LightProblem(WorldPropLight.Point, 5f, 1f, 30f));
        Assert.Contains("range", PropRules.LightProblem(WorldPropLight.Point, 0f, 1f, 30f));
        Assert.Contains("intensity", PropRules.LightProblem(WorldPropLight.Point, 5f, -1f, 30f));
        Assert.Contains("at least 1", PropRules.LightProblem(WorldPropLight.Spot, 5f, 1f, 0.5f));

        var light = Cube();
        light.Kind = WorldPropKind.Light;
        light.LightRange = 4f;
        light.LightIntensity = 2f;
        light.LightSpotAngle = 30f;
        Assert.True(PropRules.Valid(light));
        light.LightColor = new[] { 1f, 1f };
        Assert.False(PropRules.Valid(light));
    }

    [Fact]
    public void An_interact_key_needs_a_collider()
    {
        var prop = Cube();
        prop.InteractKey = "open";
        Assert.True(PropRules.Valid(prop));
        prop.Collider = false;
        Assert.False(PropRules.Valid(prop));
    }

    [Fact]
    public void Put_replaces_and_drop_removes()
    {
        var file = new PropFile();
        PropRules.Put(file, Cube());
        PropRules.Put(file, Cube(id: "lamp"));
        PropRules.Put(file, Cube());
        PropRules.Put(file, Cube("Other"));

        Assert.Equal(3, file.Props.Count);
        Assert.Equal(2, PropRules.Count(file, "MyMod"));
        Assert.NotNull(PropRules.Find(file, "MyMod", "lamp"));
        Assert.Equal(3, PropRules.InScene(file, "Interior Player Tenement").Count);
        Assert.Equal(1, PropRules.Drop(file, "MyMod", "lamp"));
        Assert.Equal(1, PropRules.Drop(file, "MyMod", null));
        Assert.Single(file.Props);
    }

    [Fact]
    public void A_mod_has_a_cap_but_may_always_replace()
    {
        var file = new PropFile();
        for (int i = 0; i < PropRules.MaxPerMod; i++)
            PropRules.Put(file, Cube(id: "p" + i));

        Assert.False(PropRules.HasRoom(file, "MyMod", "one-more"));
        Assert.True(PropRules.HasRoom(file, "MyMod", "p3"));
        Assert.True(PropRules.HasRoom(file, "Other", "first"));
    }

    [Fact]
    public void Clean_keeps_only_valid_unique_records()
    {
        var broken = Cube(id: "broken");
        broken.Scale = new[] { 0f, 1f, 1f };
        var nameless = Cube(id: "");
        var duplicate = Cube();
        duplicate.Alt = (int)WorldPrimitive.Sphere;
        var withNulls = Cube(id: "nulls");
        withNulls.Data = null;
        withNulls.Key = null;
        var file = new PropFile { Props = new List<PropRecord> { Cube(), broken, nameless, duplicate, null, withNulls } };

        var clean = PropRules.Clean(file);

        Assert.Equal(2, clean.Props.Count);
        Assert.Equal((int)WorldPrimitive.Sphere, PropRules.Find(clean, "MyMod", "crate").Alt);
        Assert.Equal("", PropRules.Find(clean, "MyMod", "nulls").Data);
        Assert.Empty(PropRules.Clean(null).Props);
    }
}

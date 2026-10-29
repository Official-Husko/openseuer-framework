using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenLoader.Config;
using UnityEngine;

namespace ObenCore.Tests
{
    public class UnityStructJsonTests
    {
        public class Holder
        {
            public Vector3 Spawn = new Vector3(1f, 2.5f, -3f);
            public Color Tint = new Color(1f, 0f, 0f, 1f);
            public Vector3? Maybe;
            public List<Vector3> Path = new() { new Vector3(1f, 0f, 0f) };
        }

        [Fact]
        public void WritesOnlyTheFields()
        {
            var json = JObject.Parse(JsonConvert.SerializeObject(new Holder(), JsonFile.Settings));

            Assert.Equal(new[] { "x", "y", "z" }, ((JObject)json["Spawn"]!).Properties().Select(p => p.Name));
            Assert.Equal(-3f, (float)json["Spawn"]!["z"]!);
            Assert.Equal(new[] { "r", "g", "b", "a" }, ((JObject)json["Tint"]!).Properties().Select(p => p.Name));
            Assert.Null(json["Maybe"]);
        }

        [Fact]
        public void RoundTrips()
        {
            var original = new Holder { Maybe = new Vector3(4f, 5f, 6f) };

            var copy = JsonConvert.DeserializeObject<Holder>(JsonConvert.SerializeObject(original, JsonFile.Settings), JsonFile.Settings)!;

            Assert.Equal(original.Spawn, copy.Spawn);
            Assert.Equal(original.Tint, copy.Tint);
            Assert.Equal(original.Maybe, copy.Maybe);
            Assert.Equal(original.Path, copy.Path);
        }

        [Fact]
        public void MissingFieldsAreZero()
        {
            var copy = JsonConvert.DeserializeObject<Holder>("{\"Spawn\": {\"x\": 7}}", JsonFile.Settings)!;

            Assert.Equal(new Vector3(7f, 0f, 0f), copy.Spawn);
        }

        [Fact]
        public void SnapshotsForTheChangeLog()
        {
            var before = ConfigValues.Snapshot(new Holder());
            var after = ConfigValues.Snapshot(new Holder { Spawn = new Vector3(0f, 0f, 0f) });

            Assert.StartsWith("Spawn ", ConfigValues.Changes(before, after));
        }

        [Fact]
        public void LeavesOtherTypesAlone()
        {
            var converter = new UnityStructJson();

            Assert.True(converter.CanConvert(typeof(Vector3)));
            Assert.True(converter.CanConvert(typeof(Vector3?)));
            Assert.False(converter.CanConvert(typeof(Rect)));
            Assert.False(converter.CanConvert(typeof(float)));
            Assert.False(converter.CanConvert(typeof(DateTime)));
        }
    }
}

// Stand-ins shaped like Unity's: public fields plus a property that gives the same type again, which plain
// Newtonsoft stops on as a loop.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3 normalized => this;
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
    }

    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public Color linear => this;
    }

    public struct Rect
    {
        private float _x;

        public Rect(float x) => _x = x;

        public float x => _x;
    }
}

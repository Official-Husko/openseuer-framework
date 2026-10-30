using System.Reflection;
using DevBridge.Tracing;
using ObenCore.Tests.Tracing.Samples;

namespace ObenCore.Tests.Tracing.Samples
{
    public static class SampleClock
    {
        public static int Hour => 13;

        public static float Speed { get; set; } = 1f;

        public static event Action Ticked;

        public static int Add(int a, int b) => a + b;

        public static T Echo<T>(T value) => value;

        public static ref int Slot(int[] values) => ref values[0];

        internal static void Hidden() { }

        public static void Tick() => Ticked?.Invoke();
    }

    public sealed class SampleTask
    {
        public string Title { get; set; } = "";

        public bool Start() => true;

        public override string ToString() => Title;
    }

    public sealed class SampleInfo
    {
        public void Refresh() { }
    }

    public sealed class SampleBox<T>
    {
        public void Put(T value) { }
    }

    internal static class SampleInternal
    {
        public static void Run() { }
    }

    public abstract class SampleBase
    {
        public abstract void Must();

        public virtual void May() { }
    }
}

namespace ObenCore.Tests.Tracing
{
    public class TraceTargetsTests
    {
        private static readonly Type[] Types =
        {
            typeof(SampleClock), typeof(SampleTask), typeof(SampleInfo), typeof(SampleBox<>), typeof(SampleInternal), typeof(SampleBase),
        };

        private static List<string> Names(string filter) =>
            TraceTargets.Select(Types, filter).Select(TraceFormat.Method).OrderBy(n => n).ToList();

        [Fact]
        public void PicksThePublicApi()
        {
            Assert.Equal(new[]
            {
                "SampleBase.May",
                "SampleClock.Add",
                "SampleClock.Hour",
                "SampleClock.Speed",
                "SampleClock.Speed=",
                "SampleClock.Tick",
                "SampleTask.Start",
                "SampleTask.Title=",
            }, Names(""));
        }

        [Theory]
        [InlineData("SampleTask", 2)]
        [InlineData("sampleclock, SampleTask", 7)]
        [InlineData("ObenCore.Tests.Tracing.Samples", 8)]
        [InlineData("Nothing", 0)]
        public void FilterMatchesNamesAndNamespaces(string filter, int count) => Assert.Equal(count, Names(filter).Count);

        [Fact]
        public void FilterSplitsOnCommas()
        {
            Assert.Equal(new[] { "Quest", "GlobalFlags" }, TraceTargets.Filter(" Quest , ,GlobalFlags "));
            Assert.Empty(TraceTargets.Filter("  "));
        }

        [Fact]
        public void TypesOfAnAssemblyIncludesTheSamples() =>
            Assert.Contains(typeof(SampleClock), TraceTargets.TypesOf(typeof(SampleClock).Assembly));

        [Fact]
        public void HandlersOfTracedAssembliesOnlyIncludingPrivateOnes()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var hidden = typeof(SampleClock).GetMethod("Hidden", flags)!;
            var add = typeof(SampleClock).GetMethod(nameof(SampleClock.Add))!;
            var echo = typeof(SampleClock).GetMethod(nameof(SampleClock.Echo))!;
            var start = typeof(SampleTask).GetMethod(nameof(SampleTask.Start))!;
            var foreign = typeof(Math).GetMethod("Abs", new[] { typeof(int) })!;
            var tracer = typeof(TraceTargets).GetMethod(nameof(TraceTargets.Filter))!;
            var traced = new[] { typeof(SampleClock).Assembly };
            var handlers = new[] { hidden, add, echo, start, foreign, tracer };

            Assert.Equal(new[] { hidden, add, start }, TraceTargets.Handlers(handlers, traced, ""));
            Assert.Equal(new[] { start }, TraceTargets.Handlers(handlers, traced, "SampleTask"));
            Assert.Empty(TraceTargets.Handlers(handlers, Array.Empty<Assembly>(), ""));
        }

        [Fact]
        public void NonPublicAddsInternalTypesAndMethods()
        {
            var names = TraceTargets.Select(Types, "", nonPublic: true).Select(TraceFormat.Method).ToList();
            Assert.Contains("SampleClock.Hidden", names);
            Assert.Contains("SampleInternal.Run", names);
            Assert.Contains("SampleClock.Add", names);
            // Compiler-made code, generic types and the instance getters still stay out.
            Assert.DoesNotContain(names, n => n.IndexOf('<') >= 0);
            Assert.DoesNotContain("SampleBox.Put", names);
            Assert.DoesNotContain("SampleTask.Title", names);
            Assert.DoesNotContain("SampleClock.Hidden", Names(""));
        }

        [Fact]
        public void TheTracerNeverTracesItself()
        {
            var own = TraceTargets.TypesOf(typeof(TraceTargets).Assembly).Where(t => t.Namespace == typeof(TraceTargets).Namespace);
            Assert.Empty(TraceTargets.Select(own, "", nonPublic: true));
        }

        [Fact]
        public void NoAccessorOfAnInstanceTypeButItsSetters()
        {
            var methods = TraceTargets.Select(new[] { typeof(SampleTask) }, "");
            Assert.DoesNotContain(methods, m => m.Name == "get_Title");
            Assert.Contains(methods, m => m.Name == "set_Title");
            Assert.DoesNotContain(methods, m => m.Name == nameof(ToString));
        }
    }
}

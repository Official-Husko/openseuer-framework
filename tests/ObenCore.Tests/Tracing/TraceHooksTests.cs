using System.Reflection;
using DevBridge.Tracing;

namespace ObenCore.Tests.Tracing
{
    public static class TracedSample
    {
        public static int Add(int a, int b) => a + b;

        public static void Fail(string why) => throw new InvalidOperationException(why);

        public static int Outer(int a) => Add(a, 1) * 2;
    }

    /// <summary>
    /// Runs the tracer's hooks the way Harmony calls them around a traced method. Harmony itself can't patch
    /// on the test runtime (.NET 10), so the hooks are called directly; in the game they are patched in.
    /// </summary>
    [Collection("tracer")]
    public class TraceHooksTests : IDisposable
    {
        private static readonly MethodInfo Add = typeof(TracedSample).GetMethod(nameof(TracedSample.Add))!;
        private static readonly MethodInfo Fail = typeof(TracedSample).GetMethod(nameof(TracedSample.Fail))!;
        private static readonly MethodInfo Outer = typeof(TracedSample).GetMethod(nameof(TracedSample.Outer))!;

        private readonly List<string> _warnings = new List<string>();
        private readonly List<MethodInfo> _patched = new List<MethodInfo>();

        public TraceHooksTests()
        {
            var methods = TraceTargets.Select(new[] { typeof(TracedSample) }, "");
            TraceHooks.Start(methods, Array.Empty<Assembly>(), new TraceBook(), _warnings.Add, _patched.Add);
        }

        public void Dispose() => TraceHooks.Stop();

        private static List<TraceRecord> Calls() =>
            TraceHooks.Book.Read(0, 100).Where(r => r.Kind == TraceBook.CallKind).ToList();

        private static TraceCall Enter(MethodInfo method, params object[] args)
        {
            TraceHooks.Prefix(method, args, out var state);
            return state;
        }

        private static void Leave(TraceCall state, object? result = null, Exception? error = null)
        {
            if (error == null)
                TraceHooks.Postfix(result, state);
            TraceHooks.Finalizer(error, state);
        }

        [Fact]
        public void PatchesEveryPickedMethod()
        {
            Assert.True(TraceHooks.Active);
            Assert.Equal(3, TraceHooks.Patched);
            Assert.Equal(3, _patched.Count);
            Assert.Null(TraceHooks.FirstRefusal);
        }

        [Fact]
        public void RecordsArgumentsResultAndCaller()
        {
            Leave(Enter(Add, 2, 3), 5);

            var call = Calls().Single();
            Assert.Equal("TracedSample.Add", call.Method);
            Assert.Equal("2, 3", call.Args);
            Assert.Equal("5", call.Result);
            Assert.Equal(0, call.Depth);
            Assert.Equal("ObenCore.Tests", call.From);
            Assert.True(call.Ms >= 0);
            Assert.Equal(0, call.Thread);
            Assert.Empty(_warnings);
        }

        [Fact]
        public void RecordsExceptions()
        {
            Leave(Enter(Fail, "broken"), error: new InvalidOperationException("broken"));
            var call = Calls().Single();
            Assert.StartsWith("InvalidOperationException: broken", call.Error);
            Assert.Null(call.Result);
            Assert.Equal(1, TraceHooks.Book.Top(1).Single().Errors);
        }

        [Fact]
        public void VoidMethodsReturnVoid()
        {
            var state = Enter(Fail, "fine");
            TraceHooks.Finalizer(null, state);
            Assert.Equal("void", Calls().Single().Result);
        }

        [Fact]
        public void NestedCallsAreDeeper()
        {
            var outer = Enter(Outer, 3);
            Leave(Enter(Add, 3, 1), 4);
            Leave(outer, 8);

            var calls = Calls();
            Assert.Equal(new[] { "TracedSample.Add", "TracedSample.Outer" }, calls.Select(c => c.Method));
            Assert.Equal(1, calls[0].Depth);
            Assert.Null(calls[0].From);
            Assert.Equal(0, calls[1].Depth);
        }

        [Fact]
        public void UntracedMethodsAreIgnored()
        {
            var other = typeof(Math).GetMethod("Abs", new[] { typeof(int) })!;
            Assert.Null(Enter(other, -1));
            Assert.Empty(Calls());
        }

        [Fact]
        public void NothingIsRecordedAfterStop()
        {
            TraceHooks.Stop();
            Assert.False(TraceHooks.Active);
            Assert.Null(Enter(Add, 1, 1));
            Assert.Empty(Calls());
        }
    }
}

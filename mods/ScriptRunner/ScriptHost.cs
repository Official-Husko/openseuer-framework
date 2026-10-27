using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ObenLoader;
using ObenLoader.Logging;

namespace ScriptRunner;

/// <summary>
/// Loads compiled scripts and runs their entry point: a static Run() or Run(ScriptScope) on any
/// type in the script. Keeps one scope per script name.
/// </summary>
internal sealed class ScriptHost : IDisposable
{
    private readonly Dictionary<string, ScriptScope> _scopes = new Dictionary<string, ScriptScope>(StringComparer.Ordinal);

    public IEnumerable<string> Active => _scopes.Keys;

    public sealed class Outcome
    {
        public object Result;
        public List<string> Log;
    }

    public Outcome Run(string name, byte[] image, byte[] symbols)
    {
        Reset(name);
        var assembly = LoadedAssemblies.LoadFromMemory(image, symbols);
        var entry = FindEntry(assembly)
            ?? throw new ArgumentException("The script has no static Run() or Run(ScriptScope) method.");

        var scope = new ScriptScope(name);
        _scopes[name] = scope;

        long before = LogHub.Buffer.LastSequence;
        object result;
        try
        {
            result = entry.Invoke(null, entry.GetParameters().Length == 1 ? new object[] { scope } : null);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            // Rethrown as is, so the stack trace still points into the script.
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
        if (result is IEnumerator routine)
        {
            scope.StartCoroutine(routine);
            result = "started as a coroutine";
        }

        var lines = LogHub.Buffer.Read(before, 200).Select(l => $"[{l.Level}] [{l.Source}] {l.Message}").ToList();
        return new Outcome { Result = result, Log = lines };
    }

    /// <summary>Undoes what a script set up. Returns false when nothing by that name is active.</summary>
    public bool Reset(string name)
    {
        if (!_scopes.TryGetValue(name, out var scope))
            return false;
        _scopes.Remove(name);
        scope.Dispose();
        return true;
    }

    public void Dispose()
    {
        foreach (var scope in _scopes.Values)
            scope.Dispose();
        _scopes.Clear();
    }

    private static MethodInfo FindEntry(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (method.Name != "Run")
                    continue;
                var ps = method.GetParameters();
                if (ps.Length == 0 || (ps.Length == 1 && ps[0].ParameterType == typeof(ScriptScope)))
                    return method;
            }
        }
        return null;
    }
}

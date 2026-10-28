namespace ObenTool.Scripting;

/// <summary>
/// Turns what the user typed into a compilable file. A whole file (anything with its own static
/// Run method) is used as it is. Otherwise the text becomes the body of Run(ScriptScope Scope):
/// a lone expression is returned, statements run and may return something themselves.
/// </summary>
public static class ScriptSource
{
    public static readonly string[] DefaultUsings =
    {
        "System", "System.Collections", "System.Collections.Generic", "System.Linq", "System.Reflection",
        "UnityEngine", "UnityEngine.SceneManagement", "HarmonyLib",
        "ObenLoader", "ObenFramework", "ObenFramework.Console", "ObenFramework.Events", "ObenFramework.Game",
        "ObenFramework.Reflection", "ObenFramework.Scenes", "ScriptRunner",
    };

    public static bool IsWholeFile(string code) =>
        code.Contains(" Run(") && code.Contains("static ") && code.Contains("class ");

    public static string Prepare(string code, string fileLabel)
    {
        if (IsWholeFile(code))
            return code;

        var (ownUsings, body) = SplitUsings(code.Trim());
        bool expression = !body.Contains(';') && !body.StartsWith('{');
        var usings = string.Join("\n", DefaultUsings.Select(u => $"using {u};").Concat(ownUsings));
        string inner = expression
            ? $"return (object)({body});"
            : body + "\n#line hidden\nreturn null;";

        // #line maps compile errors and stack traces back to the user's own line numbers.
        return $$"""
            {{usings}}
            using Object = UnityEngine.Object;

            public static class ObenScript
            {
                public static object Run(ScriptScope Scope)
                {
            #line 1 "{{fileLabel}}"
            {{inner}}
            #line default
                }
            }
            """;
    }

    /// <summary>
    /// Leading "using X;" lines of a snippet are directives, not statements, so they move above the
    /// generated class. A "using (...)" or "using var" statement stays where it is.
    /// </summary>
    private static (List<string> Usings, string Body) SplitUsings(string code)
    {
        var usings = new List<string>();
        var lines = code.Split('\n');
        int i = 0;
        for (; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
                continue;
            bool directive = line.StartsWith("using ") && line.EndsWith(";") && !line.StartsWith("using (") && !line.StartsWith("using var ");
            if (!directive)
                break;
            usings.Add(line);
        }
        // Blank lines keep the user's line numbers lined up with the #line directive.
        string body = new string('\n', i) + string.Join("\n", lines.Skip(i));
        return (usings, body.TrimEnd());
    }
}

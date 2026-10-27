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
        "ObenLoader", "ObenKit", "ObenKit.Console", "ObenKit.Reflection", "ObenKit.Scenes", "ScriptRunner",
    };

    public static bool IsWholeFile(string code) =>
        code.Contains(" Run(") && code.Contains("static ") && code.Contains("class ");

    public static string Prepare(string code, string fileLabel)
    {
        if (IsWholeFile(code))
            return code;

        string body = code.Trim();
        bool expression = !body.Contains(';') && !body.StartsWith('{');
        var usings = string.Join("\n", DefaultUsings.Select(u => $"using {u};"));
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
}

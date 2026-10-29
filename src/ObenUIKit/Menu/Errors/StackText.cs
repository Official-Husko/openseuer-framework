using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Errors;

/// <summary>The colors <see cref="StackText"/> marks things with, as TextMeshPro color values ("#RRGGBB").</summary>
internal sealed class StackColors
{
    public string Error = "#E05050";
    public string Caught = "#E0A040";
    public string Muted = "#808080";
    public string Text = "#D0D0D0";
    public string Method = "#E8D890";
    public string File = "#90C890";
    public string Number = "#90B8E8";
}

/// <summary>
/// Turns error titles and stack traces into colored rich text for the Errors page: the exception type in the
/// error's color, frames with the method, file and line picked out, frames in System, Unity or Harmony code
/// dimmed so the mod's own stand out, and the names the compiler makes up for lambdas, iterators and local
/// functions written the way they appear in the source. Knows Mono's frames ("at A.B.C (int x) [0x00013] in
/// path/File.cs:9") and Unity's ("A.B:C (int) (at path/File.cs:9)", or with a dot in exception traces).
/// Pure text work, unit tested.
/// </summary>
internal static class StackText
{
    private static readonly Regex MonoFrame = new Regex(
        @"^at\s+(?<method>(?:\(wrapper [^)]*\)\s*)?[^(]+?)\s*\((?<args>[^)]*)\)\s*(?:\[0x[0-9a-fA-F]+\]\s*)?(?:in\s+(?<file>.+?)(?::(?<line>\d+))?)?\s*$");

    // "A.B:C (int)" for log calls, "A.B.C (int) (at File.cs:9)" for an exception's frames; a frame needs the
    // colon or the "(at ...)", so a line like "Retry.Later (soon)" stays text.
    private static readonly Regex UnityFrame = new Regex(
        @"^(?<method>[^\s(]+[.:][^\s(]+)\s*\((?<args>[^)]*)\)\s*(?:\(at\s+(?<file>.+?)(?::(?<line>\d+))?\))?\s*$");

    // "System.InvalidOperationException: text", also after "--->" or "Rethrow as".
    private static readonly Regex ExceptionLine = new Regex(
        @"^(?<lead>--->\s*|Rethrow as\s+)?(?<type>(?:[A-Za-z_][\w`]*\.)*[A-Za-z_]\w*(?:Exception|Error))(?::\s*(?<message>.*))?$");

    // <Run>b__0_0 (lambda), <Run>g__Local|3_0 (local function), <Run>d__5 (iterator or async state machine).
    private static readonly Regex Generated = new Regex(@"<(?<outer>[^>]*)>(?<kind>[a-z])__(?<rest>[^.+:]*)");

    // A method Harmony patched: "(wrapper dynamic-method) Type.DMD<Type::Method>".
    private static readonly Regex Patched = new Regex(@"^\(wrapper dynamic-method\)\s*(?<owner>.*?)\.?DMD<(?:[^>:]*::)?(?<name>[^>]+)>$");

    private static readonly string[] LibraryPrefixes =
    {
        "System.", "Mono.", "UnityEngine.", "UnityEditor.", "Unity.", "TMPro.", "HarmonyLib.", "MonoMod.",
        "Newtonsoft.", "(wrapper",
    };

    /// <summary>
    /// A problem's first line in <paramref name="color"/>; for "Type: message" only the type, the message stays
    /// in the text color.
    /// </summary>
    public static string Title(string title, string color, StackColors colors)
    {
        var match = ExceptionLine.Match((title ?? "").Trim());
        if (!match.Success)
            return Paint(color, title ?? "");
        return ExceptionText(match, color, colors);
    }

    /// <summary>One line of a problem's details, colored by what it is.</summary>
    public static string Line(string raw, string errorColor, StackColors colors)
    {
        string line = (raw ?? "").Trim();
        if (line.Length == 0)
            return "";

        var frame = MonoFrame.Match(line);
        if (!frame.Success)
        {
            frame = UnityFrame.Match(line);
            if (frame.Success && frame.Groups["method"].Value.IndexOf(':') < 0 && !frame.Groups["file"].Success)
                frame = Match.Empty;
        }
        if (frame.Success)
            return Frame(frame, colors);

        if (line.StartsWith("(wrapper", StringComparison.Ordinal))
            return Paint(colors.Muted, line);
        if (line.StartsWith("---", StringComparison.Ordinal) && !line.StartsWith("--->", StringComparison.Ordinal))
            return Paint(colors.Muted, line);

        var exception = ExceptionLine.Match(line);
        if (exception.Success)
            return ExceptionText(exception, errorColor, colors);
        return Paint(colors.Muted, line);
    }

    /// <summary>
    /// The lines of a problem's details, trimmed and without empty ones. Mono writes an inner exception on its
    /// outer one's line ("... ---&gt; System.IOException: ..."); that gets a line of its own.
    /// </summary>
    public static List<string> Lines(string details)
    {
        var lines = new List<string>();
        foreach (string raw in (details ?? "").Split('\n'))
        {
            string rest = raw.Trim();
            int at;
            while ((at = rest.IndexOf(" ---> ", StringComparison.Ordinal)) > 0)
            {
                lines.Add(rest.Substring(0, at).TrimEnd());
                rest = rest.Substring(at + 1).Trim();
            }
            if (rest.Length > 0)
                lines.Add(rest);
        }
        return lines;
    }

    /// <summary>
    /// "Ns.Type+&lt;&gt;c.&lt;Run&gt;b__0_0" as "Ns.Type.Run (lambda)": the names the compiler gives closures,
    /// iterators and local functions, written as in the source.
    /// </summary>
    public static string Readable(string method)
    {
        string text = method.Trim();
        string note = null;
        var patched = Patched.Match(text);
        if (patched.Success)
        {
            string owner = patched.Groups["owner"].Value;
            text = (owner.Length > 0 ? owner + "." : "") + patched.Groups["name"].Value;
            note = "patched";
        }
        text = Regex.Replace(text.Replace("::", ".").Replace(':', '.'), @"`\d+", "");
        text = Regex.Replace(text.Replace("+<>c__DisplayClass", "+<>c#"), @"\+<>c(#\w*)?", "");
        text = Generated.Replace(text, m =>
        {
            switch (m.Groups["kind"].Value)
            {
                case "b":
                    note ??= "lambda";
                    return m.Groups["outer"].Value;
                case "g":
                    string local = m.Groups["rest"].Value;
                    int bar = local.IndexOf('|');
                    return m.Groups["outer"].Value + "." + (bar > 0 ? local.Substring(0, bar) : local);
                case "d":
                    note ??= "async or iterator";
                    return m.Groups["outer"].Value;
                default:
                    return m.Groups["outer"].Value;
            }
        });
        if (note == "async or iterator" && text.EndsWith(".MoveNext", StringComparison.Ordinal))
            text = text.Substring(0, text.Length - ".MoveNext".Length);
        text = text.Replace('+', '.').Replace("..", ".");
        return note == null ? text : $"{text} ({note})";
    }

    private static string Frame(Match frame, StackColors colors)
    {
        string method = frame.Groups["method"].Value;
        string readable = Readable(method);
        bool library = IsLibrary(readable) || (IsLibrary(method) && !readable.EndsWith("(patched)", StringComparison.Ordinal));

        // The note "(lambda)" stays after the name, outside the part that is split into type and method.
        string note = "";
        int space = readable.IndexOf(" (", StringComparison.Ordinal);
        if (space > 0)
        {
            note = readable.Substring(space);
            readable = readable.Substring(0, space);
        }

        int dot = readable.LastIndexOf('.');
        string owner = dot > 0 ? readable.Substring(0, dot + 1) : "";
        string name = dot > 0 ? readable.Substring(dot + 1) : readable;
        int typeDot = owner.Length > 1 ? owner.LastIndexOf('.', owner.Length - 2) : -1;
        string ns = typeDot > 0 ? owner.Substring(0, typeDot + 1) : "";
        string type = owner.Substring(ns.Length);

        var text = new StringBuilder();
        text.Append(Paint(colors.Muted, "at "));
        if (library)
        {
            text.Append(Paint(colors.Muted, readable + note + " (" + Args(frame.Groups["args"].Value) + ")"));
        }
        else
        {
            text.Append(Paint(colors.Muted, ns));
            text.Append(Paint(colors.Text, type));
            text.Append(Paint(colors.Method, name));
            text.Append(Paint(colors.Muted, note + " (" + Args(frame.Groups["args"].Value) + ")"));
        }

        string file = FileName(frame.Groups["file"].Value);
        if (file.Length > 0)
        {
            text.Append("  ");
            text.Append(Paint(library ? colors.Muted : colors.File, file));
            if (frame.Groups["line"].Success && frame.Groups["line"].Value != "0")
                text.Append(Paint(colors.Muted, ":")).Append(Paint(library ? colors.Muted : colors.Number, frame.Groups["line"].Value));
        }
        return text.ToString();
    }

    private static string ExceptionText(Match match, string color, StackColors colors)
    {
        var text = new StringBuilder();
        if (match.Groups["lead"].Success && match.Groups["lead"].Length > 0)
            text.Append(Paint(colors.Muted, match.Groups["lead"].Value.StartsWith("---") ? "Caused by " : "Rethrown as "));
        string type = match.Groups["type"].Value;
        int dot = type.LastIndexOf('.');
        text.Append(Paint(color, dot >= 0 ? type.Substring(dot + 1) : type));
        if (match.Groups["message"].Success && match.Groups["message"].Length > 0)
            text.Append(Paint(colors.Text, ": " + match.Groups["message"].Value));
        return text.ToString();
    }

    // "ObenUIKit.Controls.UiBuilder ui, int count" as "UiBuilder ui, int count".
    private static string Args(string args)
    {
        if (args.Length == 0)
            return "";
        var parts = args.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            int space = part.IndexOf(' ');
            string type = space > 0 ? part.Substring(0, space) : part;
            int dot = type.LastIndexOf('.');
            parts[i] = dot >= 0 ? type.Substring(dot + 1) + part.Substring(type.Length) : part;
        }
        return string.Join(", ", parts);
    }

    // Only the file name: a full path from the build machine says nothing, and "<a1b2...>" (no symbols) is dropped.
    private static string FileName(string path)
    {
        path = path.Trim();
        if (path.Length == 0 || path.StartsWith("<", StringComparison.Ordinal))
            return "";
        int slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    private static bool IsLibrary(string method)
    {
        foreach (string prefix in LibraryPrefixes)
        {
            if (method.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string Paint(string color, string text) => KitText.Paint(color, text);
}

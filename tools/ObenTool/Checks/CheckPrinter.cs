using Oben.Checks;
namespace ObenTool.Checks;

/// <summary>Prints the results of a check, grouped by mod, and works out the exit code.</summary>
public static class CheckPrinter
{
    /// <summary>Prints everything and returns the number of hooks that would fail (missing plus ambiguous).</summary>
    public static int Print(IReadOnlyList<ModReport> reports, bool verbose)
    {
        int failures = 0, warnings = 0, hooks = 0, dynamic = 0;
        foreach (var report in reports)
        {
            hooks += report.Checked;
            dynamic += report.Dynamic;
            int bad = report.Findings.Count(f => f.Severity != Severity.Warning);
            failures += bad;
            warnings += report.Findings.Count - bad;

            Write(bad > 0 ? ConsoleColor.Red : null, $"{report.Mod}: {report.Checked} hooks, {(bad > 0 ? bad + " broken" : "ok")}"
                + (report.Dynamic > 0 ? $", {report.Dynamic} not checkable" : ""));
            foreach (var finding in report.Findings)
            {
                var color = finding.Severity == Severity.Warning ? ConsoleColor.Yellow : ConsoleColor.Red;
                string label = finding.Severity.ToString().ToUpperInvariant();
                Write(color, $"  {label,-9} {finding.Class}");
                Write(color, $"            {finding.Hook}");
                Write(color, $"            {finding.Message}");
            }
            if (verbose)
            {
                foreach (string passed in report.Passed)
                    Write(null, "  ok        " + passed);
            }
        }

        Console.WriteLine();
        Write(failures > 0 ? ConsoleColor.Red : ConsoleColor.Green,
            $"{hooks} hooks in {reports.Count} assemblies: {failures} broken, {warnings} warnings, {dynamic} worked out at run time (not checked).");
        return failures;
    }

    private static void Write(ConsoleColor? color, string text)
    {
        var old = Console.ForegroundColor;
        if (color != null)
            Console.ForegroundColor = color.Value;
        Console.WriteLine(text);
        Console.ForegroundColor = old;
    }
}

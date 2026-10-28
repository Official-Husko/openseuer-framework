namespace ObenTool.Checks;

/// <summary>What the checks found out about one mod assembly.</summary>
public sealed class ModReport
{
    public string Mod { get; }
    public List<Finding> Findings { get; } = new();

    /// <summary>Every hook that was looked at and held up, for --verbose.</summary>
    public List<string> Passed { get; } = new();

    /// <summary>Hooks whose target is worked out at run time (TargetMethod, a variable as the name), so they can't be checked.</summary>
    public int Dynamic { get; set; }

    public ModReport(string mod)
    {
        Mod = mod;
    }

    public int Checked => Passed.Count + Findings.Count(f => f.Severity != Severity.Warning);

    public void Add(Severity severity, string cls, string hook, string message) =>
        Findings.Add(new Finding(severity, Mod, cls, hook, message));

    public void Ok(string cls, string hook) => Passed.Add($"{cls}: {hook}");

    /// <summary>Records the outcome of a lookup under the given hook.</summary>
    public void Record(Lookup lookup, string cls, string hook)
    {
        switch (lookup.Status)
        {
            case LookupStatus.Found:
                Ok(cls, hook);
                break;
            case LookupStatus.Ambiguous:
                Add(Severity.Ambiguous, cls, hook, lookup.Detail);
                break;
            default:
                Add(Severity.Missing, cls, hook, lookup.Detail);
                break;
        }
    }
}

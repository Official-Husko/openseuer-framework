namespace ObenTool.Checks;

public enum Severity
{
    /// <summary>The target is gone or no longer matches, so the hook would fail.</summary>
    Missing,
    /// <summary>More than one member matches, so the game would throw when the hook resolves it.</summary>
    Ambiguous,
    /// <summary>Worth a look but doesn't stop the hook (a type differs, an assembly couldn't be read).</summary>
    Warning,
}

/// <summary>One problem with a hook: which mod, which class in it, which target, and what is wrong.</summary>
public sealed record Finding(Severity Severity, string Mod, string Class, string Hook, string Message);

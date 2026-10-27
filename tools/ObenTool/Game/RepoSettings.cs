using System.Xml.Linq;

namespace ObenTool.Game;

/// <summary>
/// Reads the game folder from the repo's build/Local.props, so the tool and the build agree on
/// which install to use without setting anything twice.
/// </summary>
public static class RepoSettings
{
    public static string? GameDir()
    {
        string? props = FindLocalProps();
        if (props is null)
            return null;
        try
        {
            return XDocument.Load(props).Descendants("ObenGameDir").FirstOrDefault()?.Value.Trim();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static string? FindLocalProps()
    {
        foreach (string start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "build", "Local.props");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}

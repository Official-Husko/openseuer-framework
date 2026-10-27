using System.Text;
using ObenFramework.Console;
using ObenLoader;

namespace ObenFramework.Features;

/// <summary>The "features" console command: list every switchable feature, or turn one on or off.</summary>
internal static class FeatureCommand
{
    public static void Register(ObenMod framework) =>
        GameConsole.Register(framework, "features", "features [mod/feature on|off]", Run);

    private static string Run(string[] args)
    {
        if (args.Length == 0)
            return List();
        if (args.Length != 2 || (args[1] != "on" && args[1] != "off"))
            return "Usage: features mod/feature on|off";

        var feature = ModFeatures.Find(args[0]);
        if (feature == null)
            return $"No feature '{args[0]}'. Type features to see them all.";
        ModFeatures.SetEnabled(feature, args[1] == "on");
        return $"{feature.Name} is {(feature.Enabled ? "on" : "off")}.";
    }

    private static string List()
    {
        var text = new StringBuilder("Features (features mod/feature on|off):\n");
        foreach (var feature in ModFeatures.Features)
        {
            text.Append(feature.Enabled ? "<color=#7c7>on </color> " : "<color=#c77>off</color> ")
                .Append(ModFeatures.KeyOf(feature)).Append("  ").Append(feature.Name).Append('\n');
        }
        return text.ToString();
    }
}

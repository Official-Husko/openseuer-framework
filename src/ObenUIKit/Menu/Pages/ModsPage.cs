using System.Linq;
using ObenFramework.Features;
using ObenLoader.Mods;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Every installed mod with its version and state, whether it loads at start, and a switch for
/// each of its features. Feature switches act at once; loading at start applies on the next launch.
/// </summary>
internal static class ModsPage
{
    public static ModPage Create() => new ModPage("Mods", Build) { Order = 0 };

    private static void Build(UiBuilder ui)
    {
        ui.Live(() => ModRegistry.RestartNeeded ? "Restart the game to apply the changes to which mods load." : "", muted: true);

        foreach (var mod in ModRegistry.Mods.OrderBy(m => m.Manifest.DisplayName))
        {
            ui.Header($"{mod.Manifest.DisplayName}  {mod.Manifest.Version}");
            if (!string.IsNullOrEmpty(mod.Manifest.Description))
                ui.Label(mod.Manifest.Description, muted: true);
            if (mod.State != ModState.Loaded)
                ui.Label(StateText(mod), muted: true);

            var because = ModRegistry.AlwaysOnBecause(mod);
            if (because == mod)
                ui.Label("Always on.", muted: true);
            else if (because != null)
                ui.Label($"Always on, {because.Manifest.DisplayName} needs it.", muted: true);
            else
                ui.Toggle("Load when the game starts", ModRegistry.IsEnabled(mod.Id), on => ModRegistry.SetEnabled(mod.Id, on));

            foreach (var feature in ModFeatures.Of(mod))
            {
                ui.Toggle(feature.Name, feature.Enabled, on => ModFeatures.SetEnabled(feature, on));
                if (!string.IsNullOrEmpty(feature.Description))
                    ui.Label(feature.Description, muted: true);
            }
            ui.Space(16f);
        }
    }

    private static string StateText(ModInfo mod) => mod.State switch
    {
        ModState.Failed => "Couldn't load: " + mod.Error,
        ModState.Disabled => "Switched off.",
        _ => "Not loaded yet.",
    };
}

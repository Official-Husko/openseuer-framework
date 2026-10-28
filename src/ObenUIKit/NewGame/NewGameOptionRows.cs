// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Globalization;
using ObenFramework.NewGames;
using ObenUIKit.Controls;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// One kit control per new game option kind: toggles and choices as the game's cycles, small whole-number
/// ranges as a cycle of their numbers, larger ones and decimals as sliders snapped to the option's steps.
/// </summary>
internal static class NewGameOptionRows
{
    /// <summary>Whole-number options with at most this many values get a cycle instead of a slider.</summary>
    private const int MaxCycleValues = 21;

    public static void Add(UiBuilder ui, NewGameOption option)
    {
        string value = NewGameOptions.ScreenValue(option.ModId, option.Key) ?? option.Default;
        switch (option.Kind)
        {
            case NewGameOptionKind.Toggle:
                ui.Toggle(option.Title, value == "1", on => Choose(option, on ? "1" : "0"));
                break;
            case NewGameOptionKind.Choice:
                ui.Cycle(option.Title, option.Titles, Math.Max(0, Array.IndexOf(option.Ids, value)), i => Choose(option, option.Ids[i]));
                break;
            case NewGameOptionKind.Integer:
                AddInteger(ui, option, value);
                break;
            default:
                AddFloat(ui, option, value);
                break;
        }
        if (option.Description.Length > 0)
            ui.Label(option.Description, muted: true);
    }

    private static void AddInteger(UiBuilder ui, NewGameOption option, string value)
    {
        int current = int.Parse(value, CultureInfo.InvariantCulture);
        int count = option.Max - option.Min + 1;
        if (count <= MaxCycleValues)
        {
            var numbers = new string[count];
            for (int i = 0; i < count; i++)
                numbers[i] = (option.Min + i).ToString(CultureInfo.InvariantCulture);
            ui.Cycle(option.Title, numbers, current - option.Min, i => Choose(option, numbers[i]));
            return;
        }
        ui.Slider(option.Title, option.Min, option.Max, current,
            v => Choose(option, ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture)), "0", wholeNumbers: true);
    }

    private static void AddFloat(UiBuilder ui, NewGameOption option, string value)
    {
        float current = float.Parse(value, CultureInfo.InvariantCulture);
        ui.Slider(option.Title, option.FloatMin, option.FloatMax, current,
            v => Choose(option, v.ToString("R", CultureInfo.InvariantCulture)), Format(option.FloatStep));
    }

    // Enough decimals to tell two neighbouring steps apart.
    private static string Format(float step)
    {
        if (step >= 1f) return "0";
        if (step >= 0.1f) return "0.0";
        if (step >= 0.01f) return "0.00";
        return "0.000";
    }

    private static void Choose(NewGameOption option, string value)
    {
        // The option may have gone away with its mod while the window was open.
        if (NewGameOptions.Find(option.ModId, option.Key) == option)
            NewGameOptions.Choose(option.ModId, option.Key, value);
    }
}

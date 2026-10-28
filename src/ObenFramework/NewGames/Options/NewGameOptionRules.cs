// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Globalization;

namespace ObenFramework.NewGames;

/// <summary>
/// Builds and checks new game options. Mistakes in the call (bad ids, empty titles, a list that doesn't
/// line up) throw, so they show up while writing the mod; a default outside the range throws too.
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal static class NewGameOptionRules
{
    public const int MaxFloatSteps = 1000;

    public static NewGameOption Toggle(string modId, string key, string title, string description, bool defaultValue, NewGamePlacement placement)
    {
        Common(modId, key, title, placement);
        return new NewGameOption(modId, key, title, description, NewGameOptionKind.Toggle, placement, defaultValue ? "1" : "0");
    }

    public static NewGameOption Choice(string modId, string key, string title, string description, string[] ids, string[] titles,
        int defaultIndex, NewGamePlacement placement)
    {
        Common(modId, key, title, placement);
        if (ids == null || ids.Length == 0)
            throw new ArgumentException("A choice needs at least one id.", nameof(ids));
        if (titles == null || titles.Length != ids.Length)
            throw new ArgumentException("A choice needs one title for each id.", nameof(titles));
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.IsNullOrEmpty(ids[i]) || string.IsNullOrEmpty(titles[i]))
                throw new ArgumentException("Choice ids and titles can't be empty.", nameof(ids));
            if (Array.IndexOf(ids, ids[i]) != i)
                throw new ArgumentException($"The choice id '{ids[i]}' is listed twice.", nameof(ids));
        }
        if (defaultIndex < 0 || defaultIndex >= ids.Length)
            throw new ArgumentOutOfRangeException(nameof(defaultIndex), $"The default index must be from 0 to {ids.Length - 1}.");

        return new NewGameOption(modId, key, title, description, NewGameOptionKind.Choice, placement, ids[defaultIndex])
        {
            Ids = (string[])ids.Clone(),
            Titles = (string[])titles.Clone(),
        };
    }

    public static NewGameOption Integer(string modId, string key, string title, string description, int min, int max,
        int defaultValue, NewGamePlacement placement)
    {
        Common(modId, key, title, placement);
        if (min > max)
            throw new ArgumentException("min can't be greater than max.", nameof(min));
        if (defaultValue < min || defaultValue > max)
            throw new ArgumentOutOfRangeException(nameof(defaultValue), "The default must be from min to max.");
        return new NewGameOption(modId, key, title, description, NewGameOptionKind.Integer, placement, Text(defaultValue))
        {
            Min = min,
            Max = max,
        };
    }

    public static NewGameOption Float(string modId, string key, string title, string description, float min, float max,
        float step, float defaultValue, NewGamePlacement placement)
    {
        Common(modId, key, title, placement);
        if (!Finite(min) || !Finite(max) || !Finite(step) || !Finite(defaultValue))
            throw new ArgumentException("min, max, step and the default must be finite numbers.", nameof(min));
        if (min >= max)
            throw new ArgumentException("min must be less than max.", nameof(min));
        if (step <= 0f)
            throw new ArgumentException("step must be greater than zero.", nameof(step));

        float rawSteps = (max - min) / step;
        float tolerance = Tolerance(min, max, step);
        if (rawSteps > MaxFloatSteps + 0.0001f || Math.Abs(rawSteps - (float)Math.Round(rawSteps)) > tolerance)
            throw new ArgumentException($"(max - min) / step must be a whole number of steps, at most {MaxFloatSteps}.", nameof(step));
        if (defaultValue < min || defaultValue > max)
            throw new ArgumentOutOfRangeException(nameof(defaultValue), "The default must be from min to max.");
        float rawIndex = (defaultValue - min) / step;
        if (Math.Abs(rawIndex - (float)Math.Round(rawIndex)) > tolerance)
            throw new ArgumentOutOfRangeException(nameof(defaultValue), "The default must be min plus a whole number of steps.");

        var option = new NewGameOption(modId, key, title, description, NewGameOptionKind.Float, placement, "0")
        {
            FloatMin = min,
            FloatMax = max,
            FloatStep = step,
            FloatSteps = (int)Math.Round(rawSteps),
        };
        option.Default = Text(FloatAt(option, (int)Math.Round(rawIndex)));
        return option;
    }

    /// <summary>The value of step <paramref name="index"/>, clamped to the option's range.</summary>
    public static float FloatAt(NewGameOption option, int index)
    {
        if (index <= 0)
            return option.FloatMin;
        if (index >= option.FloatSteps)
            return option.FloatMax;
        return option.FloatMin + index * option.FloatStep;
    }

    /// <summary>The step nearest to <paramref name="value"/>, clamped to the option's range.</summary>
    public static int FloatIndex(NewGameOption option, float value)
    {
        int index = (int)Math.Round((value - option.FloatMin) / option.FloatStep);
        return Math.Max(0, Math.Min(option.FloatSteps, index));
    }

    /// <summary>The next value one step left or right, wrapping at the ends like the game's cycles do.</summary>
    public static string Step(NewGameOption option, string value, bool right)
    {
        value = option.Normalize(value);
        int delta = right ? 1 : -1;
        switch (option.Kind)
        {
            case NewGameOptionKind.Toggle:
                return value == "1" ? "0" : "1";
            case NewGameOptionKind.Choice:
                return option.Ids[Wrap(Array.IndexOf(option.Ids, value) + delta, 0, option.Ids.Length - 1)];
            case NewGameOptionKind.Integer:
                return Text(Wrap(int.Parse(value, CultureInfo.InvariantCulture) + delta, option.Min, option.Max));
            default:
                int index = FloatIndex(option, float.Parse(value, CultureInfo.InvariantCulture));
                return Text(FloatAt(option, Wrap(index + delta, 0, option.FloatSteps)));
        }
    }

    public static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Text(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static int Wrap(int value, int min, int max)
    {
        if (value < min)
            return max;
        return value > max ? min : value;
    }

    private static void Common(string modId, string key, string title, NewGamePlacement placement)
    {
        if (!OptionIds.IsValid(modId))
            throw new ArgumentException($"'{modId}' is not a usable mod id (1 to 64 letters, digits, '.', '_' or '-').", nameof(modId));
        if (!OptionIds.IsValidKey(key))
            throw new ArgumentException($"'{key}' is not a usable option key (1 to 64 letters, digits, '_' or '-').", nameof(key));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("An option needs a title.", nameof(title));
        if (placement != NewGamePlacement.BelowDifficulty && placement != NewGamePlacement.CustomPanel)
            throw new ArgumentOutOfRangeException(nameof(placement));
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static float Tolerance(float min, float max, float step) =>
        1e-5f * Math.Max(Math.Abs(min), Math.Abs(max)) / step + 0.0001f;
}

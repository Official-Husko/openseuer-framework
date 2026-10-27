using System;
using System.Collections.Generic;

namespace ObenFramework.Clock;

/// <summary>
/// The weather, as the game's WeatherManager and the console's change_weather see it: a list of named
/// profiles, one of which is active. Interiors have no weather, so check <see cref="IsAvailable"/>; the
/// other members throw <see cref="InvalidOperationException"/> without it.
/// </summary>
public static class GameWeather
{
    /// <summary>True when the current scene has a weather system with at least one profile.</summary>
    public static bool IsAvailable => WeatherManager.instance != null && WeatherManager.instance.WeatherCount() > 0;

    /// <summary>Number of weather profiles.</summary>
    public static int Count => Manager.WeatherCount();

    /// <summary>Index of the active weather, or -1 when the game has none selected.</summary>
    public static int CurrentIndex => Manager.ActiveWeatherIndex();

    /// <summary>Name of the active weather, or null when none is selected.</summary>
    public static string Current => Manager.WeatherName(CurrentIndex);

    /// <summary>Every weather name, in the order of their indices.</summary>
    public static List<string> Names()
    {
        var manager = Manager;
        int count = manager.WeatherCount();
        var names = new List<string>(count);
        for (int i = 0; i < count; i++)
            names.Add(manager.WeatherName(i));
        return names;
    }

    /// <summary>Finds a weather by name, ignoring case.</summary>
    public static bool TryFind(string name, out int index)
    {
        var manager = Manager;
        int count = manager.WeatherCount();
        for (int i = 0; i < count; i++)
        {
            string candidate = manager.WeatherName(i);
            if (candidate != null && string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                return true;
            }
        }
        index = -1;
        return false;
    }

    /// <summary>
    /// Switches to the weather with this name (or index as text, like the console). The change is
    /// immediate unless <paramref name="transitionSeconds"/> asks for a blend. Returns the weather's real name.
    /// </summary>
    public static string Set(string nameOrIndex, float transitionSeconds = 0f)
    {
        if (string.IsNullOrWhiteSpace(nameOrIndex))
            throw new ArgumentException("Give a weather name.", nameof(nameOrIndex));

        if (!(int.TryParse(nameOrIndex, out int index) && IsValid(index)) && !TryFind(nameOrIndex.Trim(), out index))
            throw new ArgumentException($"No weather '{nameOrIndex}'. Available: {string.Join(", ", Names())}.", nameof(nameOrIndex));
        return Set(index, transitionSeconds);
    }

    /// <summary>Switches to the weather at <paramref name="index"/>. Returns its name.</summary>
    public static string Set(int index, float transitionSeconds = 0f)
    {
        if (!IsValid(index))
            throw new ArgumentOutOfRangeException(nameof(index), $"Weather index must be 0 to {Count - 1}.");
        Manager.SetWeather(index, changeSpeed: true, Math.Max(0f, transitionSeconds));
        return Manager.WeatherName(index);
    }

    private static bool IsValid(int index) => index >= 0 && index < Count && Manager.WeatherName(index) != null;

    private static WeatherManager Manager
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("There is no weather here: the scene has no weather system (main menu or an interior).");
            return WeatherManager.instance;
        }
    }
}

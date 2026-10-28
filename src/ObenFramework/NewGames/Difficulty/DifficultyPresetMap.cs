// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.NewGames;

/// <summary>
/// The arithmetic behind difficulty presets: finding a level in the game's list, snapping a preset's
/// free number to the nearest step the game knows, and placing mod presets after the game's own levels
/// in the New Game screen's list. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class DifficultyPresetMap
{
    /// <summary>The position of <paramref name="wanted"/> in <paramref name="levels"/>, or -1.</summary>
    public static int IndexOf(int[] levels, int wanted)
    {
        if (levels == null)
            return -1;
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == wanted)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// The index of the step closest to <paramref name="value"/>; on an even gap the lower one wins.
    /// A value that is not a number takes the first step. -1 when there are no steps.
    /// </summary>
    public static int Nearest(float value, float[] steps)
    {
        if (steps == null || steps.Length == 0)
            return -1;
        if (float.IsNaN(value))
            return 0;
        int best = 0;
        float gap = Math.Abs(steps[0] - value);
        for (int i = 1; i < steps.Length; i++)
        {
            float other = Math.Abs(steps[i] - value);
            if (other < gap)
            {
                gap = other;
                best = i;
            }
        }
        return best;
    }

    /// <summary>The indexes of <paramref name="values"/> sorted by value; equal values keep their order.</summary>
    public static int[] OrderByValue(float[] values)
    {
        if (values == null)
            return Array.Empty<int>();
        int[] order = new int[values.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;
        for (int i = 1; i < order.Length; i++)
        {
            int key = order[i];
            int j = i - 1;
            while (j >= 0 && values[order[j]] > values[key])
            {
                order[j + 1] = order[j];
                j--;
            }
            order[j + 1] = key;
        }
        return order;
    }

    /// <summary>
    /// Keeps an index inside a list of <paramref name="count"/> levels: an index already inside stays, one
    /// outside goes to <paramref name="preferred"/> when that is inside, otherwise to the nearer end.
    /// </summary>
    public static int Clamp(int index, int count, int preferred)
    {
        if (count <= 0)
            return 0;
        if (index >= 0 && index < count)
            return index;
        if (preferred >= 0 && preferred < count)
            return preferred;
        return index < 0 ? 0 : count - 1;
    }

    /// <summary>True when <paramref name="index"/> points at one of the <paramref name="listed"/> mod presets after the game's <paramref name="vanilla"/> levels.</summary>
    public static bool IsListed(int index, int vanilla, int listed) =>
        listed > 0 && index >= vanilla && index - vanilla < listed;
}

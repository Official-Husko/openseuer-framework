using System;
using System.Collections.Generic;
using System.Globalization;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// The values a number without bounds cycles through: a ladder of round numbers, plus the current
/// value if it isn't on it.
/// </summary>
internal static class NumberSteps
{
    private static readonly double[] Whole =
    {
        0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 60, 75, 100, 150, 200, 250, 300, 400, 500,
        750, 1000, 1500, 2000, 2500, 5000, 10000, 25000, 50000, 100000,
    };

    private static readonly double[] Fraction =
    {
        0, 0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.75, 0.8, 0.9, 1, 1.25, 1.5, 2, 2.5, 3, 4, 5, 6, 8,
        10, 15, 20, 25, 30, 50, 75, 100, 150, 200, 300, 500, 1000,
    };

    public static double[] Around(double current, bool whole, out int index)
    {
        var values = new List<double>();
        foreach (double step in whole ? Whole : Fraction)
        {
            values.Add(step);
            if (current < 0 && step > 0)
                values.Add(-step);
        }
        if (!values.Exists(v => Same(v, current)))
            values.Add(current);
        values.Sort();

        index = values.FindIndex(v => Same(v, current));
        return values.ToArray();
    }

    // A float setting arrives widened (0.15f is 0.15000000596...), so the ladder's 0.15 has to count as the same value.
    private static bool Same(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(1.0, Math.Abs(b));

    public static string[] Labels(double[] values, bool whole)
    {
        var labels = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            labels[i] = values[i].ToString(whole ? "0" : "0.##", CultureInfo.InvariantCulture);
        return labels;
    }
}

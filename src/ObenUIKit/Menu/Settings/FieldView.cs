using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace ObenUIKit.Menu.Settings;

internal enum FieldKind
{
    Switch,
    Choice,
    Whole,
    Decimal,
    Text,
    Key,
    Color,
    Other,
}

/// <summary>What the page needs to know about one settings field, worked out once per settings class.</summary>
internal sealed class FieldView
{
    private static readonly Dictionary<Type, List<FieldView>> Cache = new Dictionary<Type, List<FieldView>>();

    public FieldInfo Field;
    public FieldKind Kind;
    public string Label;
    public string Description;
    public string Tooltip;
    public bool HasRange;
    public float Min, Max;

    /// <summary>Decimals shown and kept by a decimal slider.</summary>
    public int Decimals;

    public string[] ChoiceNames;
    public string[] ChoiceLabels;

    public static List<FieldView> For(Type type)
    {
        if (Cache.TryGetValue(type, out var views))
            return views;

        views = new List<FieldView>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.IsInitOnly || field.IsLiteral)
                continue;
            var setting = field.GetCustomAttribute<SettingAttribute>();
            if (setting != null && setting.Hidden)
                continue;
            views.Add(Make(field, setting));
        }
        Cache[type] = views;
        return views;
    }

    private static FieldView Make(FieldInfo field, SettingAttribute setting)
    {
        var view = new FieldView
        {
            Field = field,
            Kind = KindOf(field.FieldType),
            Label = setting?.Label ?? Prettify(field.Name),
            Description = setting?.Description ?? field.GetCustomAttribute<DescriptionAttribute>()?.Description,
            Tooltip = setting?.Tooltip,
        };
        if (view.Kind == FieldKind.Choice)
        {
            view.ChoiceNames = Enum.GetNames(field.FieldType);
            view.ChoiceLabels = Array.ConvertAll(view.ChoiceNames, Prettify);
        }
        if ((view.Kind == FieldKind.Whole || view.Kind == FieldKind.Decimal) && setting != null && setting.HasRange)
        {
            view.HasRange = true;
            view.Min = setting.Min;
            view.Max = setting.Max;
            view.Decimals = DecimalsFor(setting.Step, setting.Max - setting.Min);
        }
        return view;
    }

    private static FieldKind KindOf(Type type)
    {
        if (type == typeof(bool))
            return FieldKind.Switch;
        if (type.IsEnum && type != typeof(UnityEngine.KeyCode))
            return type.IsDefined(typeof(FlagsAttribute), false) ? FieldKind.Other : FieldKind.Choice;
        if (type == typeof(int) || type == typeof(long))
            return FieldKind.Whole;
        if (type == typeof(float) || type == typeof(double))
            return FieldKind.Decimal;
        if (type == typeof(UnityEngine.KeyCode))
            return FieldKind.Key;
        if (type == typeof(UnityEngine.Color))
            return FieldKind.Color;
        return type == typeof(string) ? FieldKind.Text : FieldKind.Other;
    }

    private static int DecimalsFor(float step, float range)
    {
        if (step > 0f)
        {
            for (int d = 0; d < 4; d++)
            {
                double scaled = step * Math.Pow(10, d);
                if (Math.Abs(scaled - Math.Round(scaled)) < 1e-4)
                    return d;
            }
            return 4;
        }
        return range <= 5f ? 2 : range <= 50f ? 1 : 0;
    }

    /// <summary>"PauseWhileOpen" becomes "Pause while open".</summary>
    internal static string Prettify(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '_')
                sb.Append(' ');
            else if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != '_')
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            else
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

namespace ObenUIKit.Menu.Settings;

internal enum FieldKind
{
    Switch,
    Choice,
    Pick,
    Whole,
    Decimal,
    Text,
    TextList,
    Key,
    Color,
    Flags,
    Vector,
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
    public string Group;
    public bool HasRange;
    public float Min, Max;

    /// <summary>Decimals shown and kept by a decimal slider.</summary>
    public int Decimals;

    /// <summary>Enum names (Choice, Flags) or the allowed strings (Pick).</summary>
    public string[] ChoiceNames;
    public string[] ChoiceLabels;

    /// <summary>The value of each flag, for Flags.</summary>
    public long[] FlagValues;

    /// <summary>How many numbers a vector has.</summary>
    public int VectorSize;

    /// <summary>Label, description, tooltip and group in one string, for the page's search.</summary>
    public string SearchText;

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
            if (setting != null && setting.Hidden || field.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false)
                continue;
            views.Add(Make(field, setting));
        }
        views = Grouped(views);
        Cache[type] = views;
        return views;
    }

    /// <summary>A settings class marked <c>[Browsable(false)]</c> stays off the page unless a mod registers it.</summary>
    public static bool HiddenClass(Type type) => type.GetCustomAttribute<BrowsableAttribute>()?.Browsable == false;

    /// <summary>"PauseWhileOpen" becomes "Pause while open".</summary>
    internal static string Prettify(string name) => FieldText.Prettify(name);

    /// <summary>Whether the field holds something else than its default.</summary>
    public bool Differs(object settings, object defaults)
    {
        object now = Field.GetValue(settings);
        object then = Field.GetValue(defaults);
        if (now is IEnumerable<string> a && then is IEnumerable<string> b)
            return FieldText.JoinList(a) != FieldText.JoinList(b);
        return !Equals(now, then);
    }

    // Ungrouped fields first, then each group's fields together, groups in the order they first appear.
    private static List<FieldView> Grouped(List<FieldView> views)
    {
        var ordered = views.FindAll(v => v.Group == null);
        foreach (var view in views)
        {
            if (view.Group != null && !ordered.Exists(v => v.Group == view.Group))
                ordered.AddRange(views.FindAll(v => v.Group == view.Group));
        }
        return ordered;
    }

    private static FieldView Make(FieldInfo field, SettingAttribute setting)
    {
        string group = setting?.Group ?? field.GetCustomAttribute<CategoryAttribute>()?.Category;
        var view = new FieldView
        {
            Field = field,
            Kind = KindOf(field.FieldType, setting),
            Label = setting?.Label ?? field.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? Prettify(field.Name),
            Description = setting?.Description ?? field.GetCustomAttribute<DescriptionAttribute>()?.Description,
            Tooltip = setting?.Tooltip,
            Group = string.IsNullOrEmpty(group) ? null : group,
        };
        view.SearchText = FieldText.SearchText(view.Label, view.Description, view.Tooltip, view.Group);

        switch (view.Kind)
        {
            case FieldKind.Choice:
                view.ChoiceNames = Enum.GetNames(field.FieldType);
                view.ChoiceLabels = Array.ConvertAll(view.ChoiceNames, Prettify);
                break;
            case FieldKind.Pick:
                view.ChoiceNames = setting.Choices;
                break;
            case FieldKind.Flags:
                ReadFlags(view, field.FieldType);
                break;
            case FieldKind.Vector:
                view.VectorSize = field.FieldType == typeof(Vector2) ? 2 : field.FieldType == typeof(Vector3) ? 3 : 4;
                break;
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

    private static FieldKind KindOf(Type type, SettingAttribute setting)
    {
        if (type == typeof(bool))
            return FieldKind.Switch;
        if (type == typeof(KeyCode))
            return FieldKind.Key;
        if (type.IsEnum)
            return type.IsDefined(typeof(FlagsAttribute), false) ? FieldKind.Flags : FieldKind.Choice;
        if (type == typeof(int) || type == typeof(long))
            return FieldKind.Whole;
        if (type == typeof(float) || type == typeof(double))
            return FieldKind.Decimal;
        if (type == typeof(Color))
            return FieldKind.Color;
        if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4))
            return FieldKind.Vector;
        if (type == typeof(string))
            return setting?.Choices != null && setting.Choices.Length > 0 ? FieldKind.Pick : FieldKind.Text;
        if (type == typeof(List<string>) || type == typeof(string[]))
            return FieldKind.TextList;
        return FieldKind.Other;
    }

    // The named flags with exactly one bit set; combinations like All are left out.
    private static void ReadFlags(FieldView view, Type type)
    {
        var names = new List<string>();
        var values = new List<long>();
        foreach (string name in Enum.GetNames(type))
        {
            long value = Convert.ToInt64(Enum.Parse(type, name));
            if (value != 0 && (value & (value - 1)) == 0)
            {
                names.Add(name);
                values.Add(value);
            }
        }
        view.ChoiceNames = names.ToArray();
        view.ChoiceLabels = Array.ConvertAll(view.ChoiceNames, Prettify);
        view.FlagValues = values.ToArray();
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
}

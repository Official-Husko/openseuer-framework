// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// The action buttons on the Settings page: those a mod registered with <see cref="ModActions"/> and the
/// <see cref="SettingActionAttribute"/> methods of its settings class. Each is a button with its description
/// under it and a line for the result of the last run; actions with a group share a card.
/// </summary>
internal static class ActionRows
{
    private sealed class MethodAction
    {
        public MethodInfo Method;
        public string Label;
        public string Description;
        public bool Confirm;
        public string Group;
    }

    /// <summary>One button to build, from either source.</summary>
    private struct Row
    {
        public string Key;
        public string Title;
        public string Description;
        public bool Confirm;
        public string Group;
        public Func<string> Run;
        public bool Rebuild;
    }

    private static readonly Dictionary<Type, List<MethodAction>> Cache = new Dictionary<Type, List<MethodAction>>();

    // The last result of each action, so it is still there after the page is built again.
    private static readonly Dictionary<string, string> Results = new Dictionary<string, string>();

    /// <summary>The methods of a settings class marked as actions, worked out once per class.</summary>
    public static bool HasMethods(Type type) => MethodsOf(type).Count > 0;

    public static bool FindsMethods(Type type, string query) =>
        MethodsOf(type).Exists(a => FieldText.Matches(query, FieldText.SearchText(a.Label, a.Description, a.Group)));

    /// <summary>Builds the action methods of a settings object (those the search finds, with a query). Returns how many.</summary>
    public static int BuildMethods(UiBuilder ui, SettingsSection section, object settings, string query = null)
    {
        var rows = new List<Row>();
        foreach (var action in MethodsOf(settings.GetType()))
        {
            if (query != null && !FieldText.Matches(query, FieldText.SearchText(action.Label, action.Description, action.Group)))
                continue;
            var method = action.Method;
            // The method may change fields shown above, so the page is built again after it ran.
            rows.Add(new Row
            {
                Key = section.OwnerId + "/" + action.Label,
                Title = action.Label,
                Description = action.Description,
                Confirm = action.Confirm,
                Group = query == null ? action.Group : null,
                Run = () =>
                {
                    object result = method.Invoke(settings, null);
                    section.Edited(deferred: false);
                    return result as string;
                },
                Rebuild = true,
            });
        }
        BuildAll(ui, rows);
        return rows.Count;
    }

    /// <summary>Builds registered actions (those the search finds, with a query). Returns how many.</summary>
    public static int BuildRegistered(UiBuilder ui, List<ModAction> actions, string query = null)
    {
        var rows = new List<Row>();
        foreach (var action in actions)
        {
            if (query != null && !FieldText.Matches(query, FieldText.SearchText(action.Title, action.Description, action.Group, action.OwnerName)))
                continue;
            rows.Add(new Row
            {
                Key = action.OwnerId + "/" + action.Title,
                Title = action.Title,
                Description = action.Description,
                Confirm = action.Confirm,
                Group = query == null ? action.Group : null,
                Run = action.Run,
            });
        }
        BuildAll(ui, rows);
        return rows.Count;
    }

    // Rows without a group first, then a card per group in the order the groups first appear.
    private static void BuildAll(UiBuilder ui, List<Row> rows)
    {
        foreach (var row in rows)
        {
            if (row.Group == null)
                Build(ui, row);
        }
        var groups = new List<string>();
        foreach (var row in rows)
        {
            if (row.Group != null && !groups.Contains(row.Group))
                groups.Add(row.Group);
        }
        foreach (string group in groups)
        {
            ui.Card(group, card =>
            {
                foreach (var row in rows)
                {
                    if (row.Group == group)
                        Build(card, row);
                }
            });
        }
    }

    private static void Build(UiBuilder ui, Row row)
    {
        LabelHandle result = null;
        void Run()
        {
            string text;
            try
            {
                text = row.Run();
                text = string.IsNullOrEmpty(text) ? "Done." : text;
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                text = "Failed: " + inner.Message;
                Runtime.KitRuntime.Current?.Kit.Log.Error($"The action '{row.Title}' failed", inner);
            }
            Results[row.Key] = text;
            if (row.Rebuild)
                ui.Window.Rebuild();
            else
                result.Text = text;
        }

        ui.Button(row.Title, () =>
        {
            if (row.Confirm)
                UiKit.Confirm(UIKitMod.Instance, row.Title, "Are you sure?", Run);
            else
                Run();
        });
        if (!string.IsNullOrEmpty(row.Description))
            ui.Label(row.Description, muted: true);
        result = ui.Label(Results.TryGetValue(row.Key, out var last) ? last : "", muted: true);
    }

    private static List<MethodAction> MethodsOf(Type type)
    {
        if (Cache.TryGetValue(type, out var actions))
            return actions;
        actions = new List<MethodAction>();
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            var mark = method.GetCustomAttribute<SettingActionAttribute>();
            if (mark == null || method.GetParameters().Length != 0)
                continue;
            actions.Add(new MethodAction
            {
                Method = method,
                Label = mark.Label ?? FieldView.Prettify(method.Name),
                Description = mark.Description,
                Confirm = mark.Confirm,
                Group = string.IsNullOrEmpty(mark.Group) ? null : mark.Group,
            });
        }
        Cache[type] = actions;
        return actions;
    }
}

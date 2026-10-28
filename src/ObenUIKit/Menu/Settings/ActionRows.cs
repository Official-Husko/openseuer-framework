// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// The action buttons on the Settings page: those a mod registered with <see cref="ModActions"/> and the
/// <see cref="SettingActionAttribute"/> methods of its settings class. Each is a button with its description
/// under it and a line for the result of the last run.
/// </summary>
internal static class ActionRows
{
    private sealed class MethodAction
    {
        public MethodInfo Method;
        public string Label;
        public string Description;
        public bool Confirm;
    }

    private static readonly Dictionary<Type, List<MethodAction>> Cache = new Dictionary<Type, List<MethodAction>>();

    // The last result of each action, so it is still there after the page is built again.
    private static readonly Dictionary<string, string> Results = new Dictionary<string, string>();

    /// <summary>The methods of a settings class marked as actions, worked out once per class.</summary>
    public static bool HasMethods(Type type) => MethodsOf(type).Count > 0;

    public static void BuildMethods(UiBuilder ui, SettingsSection section, object settings)
    {
        foreach (var action in MethodsOf(settings.GetType()))
        {
            var method = action.Method;
            // The method may change fields shown above, so the page is built again after it ran.
            Row(ui, section.OwnerId + "/" + action.Label, action.Label, action.Description, action.Confirm, () =>
            {
                object result = method.Invoke(settings, null);
                section.Edited(deferred: false);
                return result as string;
            }, rebuild: true);
        }
    }

    public static void BuildRegistered(UiBuilder ui, List<ModAction> actions)
    {
        foreach (var action in actions)
            Row(ui, action.OwnerId + "/" + action.Title, action.Title, action.Description, action.Confirm, action.Run);
    }

    private static void Row(UiBuilder ui, string key, string title, string description, bool confirm, Func<string> run,
        bool rebuild = false)
    {
        LabelHandle result = null;
        void Run()
        {
            string text;
            try
            {
                text = run();
                text = string.IsNullOrEmpty(text) ? "Done." : text;
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException t && t.InnerException != null ? t.InnerException : e;
                text = "Failed: " + inner.Message;
                Runtime.KitRuntime.Current?.Kit.Log.Error($"The action '{title}' failed", inner);
            }
            Results[key] = text;
            if (rebuild)
                ui.Window.Rebuild();
            else
                result.Text = text;
        }

        ui.Button(title, () =>
        {
            if (confirm)
                UiKit.Confirm(UIKitMod.Instance, title, "Are you sure?", Run);
            else
                Run();
        });
        if (!string.IsNullOrEmpty(description))
            ui.Label(description, muted: true);
        result = ui.Label(Results.TryGetValue(key, out var last) ? last : "", muted: true);
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
            });
        }
        Cache[type] = actions;
        return actions;
    }
}

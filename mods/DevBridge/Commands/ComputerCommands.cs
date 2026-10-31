using System;
using System.Collections;
using System.Reflection;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.World;

namespace DevBridge.Commands;

/// <summary>
/// The in-game computers through ObenFramework.World, and the apps registered with ObenUIKit. The bridge
/// doesn't depend on the kit, so the apps are reached by name in whichever kit copy is loaded last.
/// </summary>
internal static class ComputerCommands
{
    [BridgeCommand("computers.list", "Computers of the loaded scenes: title, scene, whether the player uses it, e-mail and file counts.",
        Params = new[] { "id?: one computer, with its e-mails and files" })]
    private static object ComputersList(JObject p)
    {
        string id = p.Str("id", null);
        var list = new JArray();
        foreach (var computer in Computers.All())
        {
            string computerId = Computers.IdOf(computer);
            if (id != null && computerId != id)
                continue;
            var entry = new JObject
            {
                ["id"] = computerId, ["title"] = computer.title, ["scene"] = computer.gameObject.scene.name,
                ["inUse"] = Computers.Current == computer, ["desktop"] = Computers.DesktopOf(computer) != null,
                ["emails"] = Computers.Emails(computer).Count, ["files"] = Computers.Files(computer).Count,
            };
            if (id != null)
            {
                var emails = new JArray();
                foreach (var doc in Computers.Emails(computer))
                    emails.Add(new JObject { ["title"] = doc.Title, ["from"] = doc.Sender, ["body"] = doc.Body });
                var files = new JArray();
                foreach (var doc in Computers.Files(computer))
                    files.Add(new JObject { ["name"] = doc.Title, ["body"] = doc.Body });
                entry["emailList"] = emails;
                entry["fileList"] = files;
            }
            list.Add(entry);
        }
        return new JObject { ["computers"] = list };
    }

    [BridgeCommand("computer.apps", "Apps registered for the in-game computers (ObenUIKit) and their windows on computer screens; opens one with 'open', adds or removes the kit's two demo apps with 'demo'.",
        Params = new[] { "open?: app id to open (an on-screen app opens on the computer in use)", "close?: app id to close", "demo?: true adds the demo apps, false removes them" })]
    private static object Apps(JObject p)
    {
        var type = KitType();
        if (type == null)
            return new JObject { ["kit"] = false, ["apps"] = new JArray() };

        if (p["demo"] != null)
            type.Assembly.GetType("ObenUIKit.Dev.KitDemoComputer").GetMethod("Show").Invoke(null, new object[] { p.Bool("demo", true) });
        if (p.Str("close", null) is string close)
            type.GetMethod("Close", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { close });
        string open = p.Str("open", null);
        bool? opened = null;
        if (open != null)
            opened = (bool)type.GetMethod("Open", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { open });

        var list = new JArray();
        var all = (IEnumerable)type.GetProperty("All", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var isOpen = type.GetMethod("IsOpen", BindingFlags.Public | BindingFlags.Static);
        foreach (var app in all)
        {
            var appType = app.GetType();
            string appId = (string)appType.GetProperty("Id").GetValue(app);
            list.Add(new JObject
            {
                ["id"] = appId, ["title"] = (string)appType.GetProperty("Title").GetValue(app),
                ["onScreen"] = (bool)appType.GetProperty("OnScreen").GetValue(app),
                ["icon"] = appType.GetProperty("Icon").GetValue(app) != null, ["open"] = (bool)isOpen.Invoke(null, new object[] { appId }),
            });
        }
        var windows = new JArray();
        foreach (var window in (IEnumerable)type.GetProperty("ScreenWindows", BindingFlags.Public | BindingFlags.Static).GetValue(null))
        {
            var windowType = window.GetType();
            windows.Add(new JObject
            {
                ["app"] = (string)windowType.GetProperty("AppId").GetValue(window), ["title"] = (string)windowType.GetProperty("Title").GetValue(window),
                ["minimized"] = (bool)windowType.GetProperty("IsMinimized").GetValue(window), ["size"] = windowType.GetProperty("Size").GetValue(window).ToString(),
            });
        }
        return new JObject { ["kit"] = true, ["opened"] = opened, ["apps"] = list, ["screenWindows"] = windows };
    }

    private static Type KitType()
    {
        Type found = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.GetName().Name.StartsWith("ObenUIKit", StringComparison.Ordinal))
                continue;
            var type = assembly.GetType("ObenUIKit.Computer.ComputerApps");
            if (type != null)
                found = type;
        }
        return found;
    }
}

// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ObenFramework.World;

/// <summary>A document on a computer: an e-mail or a file in its recycle bin.</summary>
public readonly struct ComputerDocument
{
    public ComputerDocument(string title, string sender, string body)
    {
        Title = title;
        Sender = sender;
        Body = body;
    }

    public string Title { get; }

    /// <summary>Who sent it (e-mails), or empty.</summary>
    public string Sender { get; }

    public string Body { get; }
}

/// <summary>The player sat down at a computer or got up from it.</summary>
public readonly struct ComputerUse
{
    public ComputerUse(InteractableComputer computer, bool inUse)
    {
        Computer = computer;
        InUse = inUse;
    }

    public InteractableComputer Computer { get; }
    public bool InUse { get; }
    public string Id => Computers.IdOf(Computer);
}

/// <summary>
/// The in-game computers: their e-mails and files, the desktop picture, which one the player is using and when
/// they sit down and get up. Apps on the desktop are in ObenUIKit (<c>ComputerApps</c>).
/// </summary>
public static class Computers
{
    private static readonly AccessTools.FieldRef<InteractableComputer, ComputerScreen> ScreenField =
        AccessTools.FieldRefAccess<InteractableComputer, ComputerScreen>("computerScreen");
    private static readonly AccessTools.FieldRef<InteractableComputer, List<Email>> EmailsField =
        AccessTools.FieldRefAccess<InteractableComputer, List<Email>>("emails");
    private static readonly AccessTools.FieldRef<InteractableComputer, List<ComputerFile>> FilesField =
        AccessTools.FieldRefAccess<InteractableComputer, List<ComputerFile>>("recycleBinFiles");
    private static readonly AccessTools.FieldRef<ComputerScreen, Image> WallpaperField =
        AccessTools.FieldRefAccess<ComputerScreen, Image>("DesktopWallpaper");

    /// <summary>The player sat down at a computer or got up from it (once the camera is back).</summary>
    public static readonly WorldEvent<ComputerUse> Used = new WorldEvent<ComputerUse>(nameof(Used));

    public static List<InteractableComputer> All() => ObjectIds.Active<InteractableComputer>();

    public static InteractableComputer Find(string id) => ObjectIds.Find<InteractableComputer>(id);

    public static string IdOf(InteractableComputer computer) => ObjectIds.IdOf(computer);

    /// <summary>The computer the player is using, or null.</summary>
    public static InteractableComputer Current => InteractableComputer.currentComputer != null ? InteractableComputer.currentComputer : null;

    /// <summary>The computer's screen (its desktop, taskbar and windows), or null.</summary>
    public static ComputerScreen ScreenOf(InteractableComputer computer) => computer != null ? ScreenField(computer) : null;

    /// <summary>The desktop: the wallpaper image the icons sit on. Null when the screen has none.</summary>
    public static RectTransform DesktopOf(InteractableComputer computer)
    {
        var screen = ScreenOf(computer);
        var wallpaper = screen != null ? WallpaperField(screen) : null;
        return wallpaper != null ? wallpaper.rectTransform : null;
    }

    public static List<ComputerDocument> Emails(InteractableComputer computer)
    {
        var list = new List<ComputerDocument>();
        var emails = computer != null ? EmailsField(computer) : null;
        if (emails == null)
            return list;
        foreach (var email in emails)
        {
            if (email != null)
                list.Add(new ComputerDocument(email.title ?? "", email.from ?? "", email.content ?? ""));
        }
        return list;
    }

    public static List<ComputerDocument> Files(InteractableComputer computer)
    {
        var list = new List<ComputerDocument>();
        var files = computer != null ? FilesField(computer) : null;
        if (files == null)
            return list;
        foreach (var file in files)
        {
            if (file != null)
                list.Add(new ComputerDocument(file.fileName ?? "", "", file.textFile != null ? file.textFile.content ?? "" : ""));
        }
        return list;
    }

    /// <summary>
    /// Shows another desktop picture until <paramref name="owner"/> unloads or the result is disposed, then puts
    /// the old one back. Not saved; the level's picture returns when it loads.
    /// </summary>
    public static IDisposable SetWallpaper(ITracker owner, InteractableComputer computer, Sprite picture)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (picture == null)
            throw new ArgumentNullException(nameof(picture));
        var screen = ScreenOf(computer);
        var image = screen != null ? WallpaperField(screen) : null;
        if (image == null)
            throw new ArgumentException("The computer has no desktop picture.", nameof(computer));
        var old = image.sprite;
        image.sprite = picture;
        var registration = Disposable.Create(() =>
        {
            if (image != null && image.sprite == picture)
                image.sprite = old;
        });
        owner.Track(registration);
        return registration;
    }

    [HarmonyPatch(typeof(InteractableComputer))]
    private static class UsePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(InteractableComputer.Use))]
        private static void SatDown(InteractableComputer __instance) => Raise(__instance, true);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InteractableComputer.OnStopUseEnd))]
        private static void GotUp(InteractableComputer __instance) => Raise(__instance, false);

        private static void Raise(InteractableComputer computer, bool inUse)
        {
            try
            {
                if (Used.HasSubscribers && WorldChanges.Active)
                    Used.Raise(new ComputerUse(computer, inUse));
            }
            catch (Exception e) { WorldLog.Failed("Computer hook", e); }
        }
    }
}

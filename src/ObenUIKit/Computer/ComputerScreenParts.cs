using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ObenUIKit.Computer;

/// <summary>
/// The parts of a computer's screen the kit copies and fills: its program window and taskbar prefabs, the
/// desktop's icon grid and the Recycle Bin icon. The screen is a world-space canvas measured in metres (0.36 by
/// 0.20 on the bank's laptop), so everything put on it is sized from the game's own pieces.
/// </summary>
internal static class ComputerScreenParts
{
    /// <summary>Metres per unit of an app's content: the whole screen is about 1800 by 1000 units.</summary>
    public const float ContentScale = 0.0002f;

    private static readonly AccessTools.FieldRef<ComputerScreen, Transform> ProgramTabsField =
        AccessTools.FieldRefAccess<ComputerScreen, Transform>("programTabs");
    private static readonly AccessTools.FieldRef<ComputerScreen, Transform> TaskbarField =
        AccessTools.FieldRefAccess<ComputerScreen, Transform>("taskbar");
    private static readonly AccessTools.FieldRef<ComputerScreen, GameObject> WindowPrefabField =
        AccessTools.FieldRefAccess<ComputerScreen, GameObject>("textFilePanelPrefab");
    private static readonly AccessTools.FieldRef<ComputerScreen, GameObject> TaskbarPrefabField =
        AccessTools.FieldRefAccess<ComputerScreen, GameObject>("taskbarProgramPrefab");
    private static readonly AccessTools.FieldRef<ComputerScreen, Image> RecycleBinIconField =
        AccessTools.FieldRefAccess<ComputerScreen, Image>("recycleBinIcon");
    private static readonly AccessTools.FieldRef<ComputerScreen, Sprite> TextFileIconField =
        AccessTools.FieldRefAccess<ComputerScreen, Sprite>("textFileIcon");

    /// <summary>Where the program windows go (the e-mail and recycle bin panels live there).</summary>
    public static Transform ProgramTabs(ComputerScreen screen) => ProgramTabsField(screen);

    public static Transform Taskbar(ComputerScreen screen) => TaskbarField(screen);

    /// <summary>The text file viewer's window: title bar, close and minimise buttons, a white body.</summary>
    public static GameObject WindowPrefab(ComputerScreen screen) => WindowPrefabField(screen);

    public static GameObject TaskbarPrefab(ComputerScreen screen) => TaskbarPrefabField(screen);

    /// <summary>The icon an app without its own gets: the screen's text file icon.</summary>
    public static Sprite DefaultIcon(ComputerScreen screen) => TextFileIconField(screen);

    /// <summary>The Recycle Bin's desktop icon (picture and title, opens on a double click), or null.</summary>
    public static GameObject DesktopIcon(ComputerScreen screen)
    {
        var image = RecycleBinIconField(screen);
        return image != null && image.transform.parent != null ? image.transform.parent.gameObject : null;
    }

    /// <summary>The desktop's icon grid, filled from the top left downwards; null when the screen has none.</summary>
    public static RectTransform Shortcuts(ComputerScreen screen)
    {
        var icon = DesktopIcon(screen);
        var desk = icon != null ? icon.transform.parent : null;
        var grid = desk != null ? desk.Find("Shortcuts") : null;
        return grid != null && grid.GetComponent<GridLayoutGroup>() != null ? (RectTransform)grid : null;
    }

    /// <summary>The font the screen's own texts use.</summary>
    public static TMP_FontAsset Font(ComputerScreen screen)
    {
        var text = screen.GetComponentInChildren<TextMeshProUGUI>(true);
        return text != null ? text.font : null;
    }

    /// <summary>
    /// Switches off a copied button's persistent calls into <paramref name="target"/> (the game's own handler on
    /// the original), keeping the rest, such as its click sound.
    /// </summary>
    public static void Mute(UnityEngine.UI.Button button, Object target)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            var called = button.onClick.GetPersistentTarget(i);
            if (called == null || called == target)
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }
    }
}

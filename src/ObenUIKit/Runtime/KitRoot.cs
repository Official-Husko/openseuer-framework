using ObenLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenUIKit.Runtime;

/// <summary>
/// The kit's persistent overlay canvas. Windows are children of it. Sorted above the game's HUD,
/// pause menu and loading screen, and below the developer console so F1 still shows on top.
/// </summary>
internal sealed class KitRoot
{
    public const int SortingOrder = 9;
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private readonly ObenMod _kit;

    public GameObject Root { get; }
    public RectTransform Rect { get; }

    public KitRoot(ObenMod kit)
    {
        _kit = kit;
        Root = kit.CreateObject("Root");
        Root.layer = LayerMask.NameToLayer("UI");
        var canvas = Root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 1f;
        Root.AddComponent<GraphicRaycaster>();
        Rect = (RectTransform)Root.transform;
    }

    /// <summary>Makes sure clicks reach the kit: the game's scenes have an EventSystem, but just in case.</summary>
    public void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        var go = _kit.CreateObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}

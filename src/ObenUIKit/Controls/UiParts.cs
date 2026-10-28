using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Low-level pieces every control is made of: rect children, themed text, images, layout sizes.</summary>
internal static class UiParts
{
    public static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static TextMeshProUGUI Text(Transform parent, UiTheme theme, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rect = Child(parent, "Text");
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (theme.Font != null)
            tmp.font = theme.Font;
        if (theme.FontMaterial != null)
            tmp.fontSharedMaterial = theme.FontMaterial;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
    {
        var image = Child(parent, name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    public static LayoutElement Layout(Component target)
    {
        var layout = target.gameObject.GetComponent<LayoutElement>();
        return layout != null ? layout : target.gameObject.AddComponent<LayoutElement>();
    }

    public static LayoutElement Height(Component target, float height)
    {
        var layout = Layout(target);
        layout.minHeight = height;
        layout.preferredHeight = height;
        return layout;
    }

    public static LayoutElement Width(Component target, float width, float flexible = 0f)
    {
        var layout = Layout(target);
        layout.preferredWidth = width;
        layout.flexibleWidth = flexible;
        return layout;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Computer;

/// <summary>
/// The pieces <see cref="ComputerUi"/> draws with, in the look of the computer's own windows: near-black text,
/// the title bar's pale grey-blue for buttons and its grey for their edges, white text fields.
/// </summary>
internal static class ComputerUiParts
{
    public static readonly Color Ink = new Color(0.08f, 0.08f, 0.08f, 1f);
    private static readonly Color Muted = new Color(0.45f, 0.47f, 0.48f, 1f);
    private static readonly Color Face = new Color(0.878f, 0.910f, 0.922f, 1f);
    private static readonly Color Edge = new Color(0.533f, 0.565f, 0.565f, 1f);
    private const float ControlHeight = 56f;
    private const float EdgeWidth = 2f;

    /// <summary>A scrolling column filling <paramref name="content"/>; returns where items go.</summary>
    public static RectTransform ScrollList(RectTransform content)
    {
        // A clear image so the wheel scrolls over gaps too, and a mask so long lists stay inside the window.
        var catcher = content.gameObject.AddComponent<Image>();
        catcher.color = Color.clear;
        content.gameObject.AddComponent<RectMask2D>();

        var list = Child(content, "Items");
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.offsetMin = list.offsetMax = Vector2.zero;
        var column = list.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(24, 24, 20, 20);
        column.spacing = 12f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = content.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = content;
        scroll.content = list;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        return list;
    }

    public static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string text, float size, Color color, FontStyles style)
    {
        var tmp = Child(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            tmp.font = font;
        tmp.text = text ?? "";
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static UnityEngine.UI.Button Button(Transform parent, TMP_FontAsset font, string label)
    {
        var face = Framed(parent, "Button", Face);
        var button = face.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = face;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.78f, 0.8f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.05f;
        button.colors = colors;
        var text = Text(face.transform, font, label, 28f, Ink, FontStyles.Normal);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 8f);
        return button;
    }

    public static TMP_InputField Field(Transform parent, TMP_FontAsset font, string value, string placeholder)
    {
        var box = Framed(parent, "Field", Color.white);
        // Built inactive so the input field finds its text parts when it starts.
        box.gameObject.SetActive(false);
        var area = Stretch(Child(box.transform, "Text Area"), 12f);
        area.gameObject.AddComponent<RectMask2D>();
        var hint = Text(area, font, placeholder, 28f, Muted, FontStyles.Italic);
        var text = Text(area, font, "", 28f, Ink, FontStyles.Normal);
        foreach (var part in new[] { hint, text })
        {
            part.alignment = TextAlignmentOptions.MidlineLeft;
            part.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(part.rectTransform, 0f);
        }
        text.richText = false;

        var input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = hint;
        input.targetGraphic = box;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.customCaretColor = true;
        input.caretColor = Ink;
        input.caretWidth = 3;
        input.selectionColor = new Color(0.6f, 0.75f, 1f, 0.5f);
        input.SetTextWithoutNotify(value ?? "");
        box.gameObject.SetActive(true);
        return input;
    }

    public static Image Picture(Transform parent, Sprite sprite, float height)
    {
        var image = Child(parent, "Picture").gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return image;
    }

    public static void Space(Transform parent, float height) =>
        Child(parent, "Space").gameObject.AddComponent<LayoutElement>().minHeight = height;

    public static RectTransform Row(Transform parent)
    {
        var row = Child(parent, "Row");
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return row;
    }

    // An edge-coloured box with the face inset by the edge width; returns the face (the part that's clicked).
    private static Image Framed(Transform parent, string name, Color face)
    {
        var frame = Child(parent, name).gameObject.AddComponent<Image>();
        frame.color = Edge;
        frame.raycastTarget = false;
        var layout = frame.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = ControlHeight;
        var inner = Stretch(Child(frame.transform, "Face"), EdgeWidth).gameObject.AddComponent<Image>();
        inner.color = face;
        return inner;
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }
}

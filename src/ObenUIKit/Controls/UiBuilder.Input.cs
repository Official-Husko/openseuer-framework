using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Controls that take keys: a text field and a key binding row.</summary>
public sealed partial class UiBuilder
{
    /// <summary>
    /// A line of text to type. <paramref name="onChange"/> runs on every edit and <paramref name="onEnd"/>
    /// when the player leaves the field (Enter, Esc or a click elsewhere), both with the current text.
    /// Esc leaves the field without undoing what was typed and does not close the window. Game keys
    /// stay locked while typing, as they do for any open window.
    /// </summary>
    public FieldHandle TextField(string label, string value, Action<string> onChange, Action<string> onEnd = null)
    {
        Header(label);
        var kitRow = OptionRow(label);

        // Built inactive so the input field finds its text parts when it starts.
        var box = UiParts.Image(kitRow.Rect, "Field", null, Theme.FieldBackground, raycast: true);
        box.gameObject.SetActive(false);
        var rect = box.rectTransform;
        rect.anchorMin = new Vector2(0.03f, 0.12f);
        rect.anchorMax = new Vector2(0.97f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var area = UiParts.Stretch(UiParts.Child(box.transform, "Text Area"), 12f, 4f, 12f, 4f);
        area.gameObject.AddComponent<RectMask2D>();
        var text = UiParts.Text(area, Theme, "", Theme.TextSize, Theme.Text, TextAlignmentOptions.MidlineLeft);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = false;
        UiParts.Stretch(text.rectTransform);

        var input = box.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.targetGraphic = box;
        input.transition = Selectable.Transition.None;
        input.navigation = new Navigation { mode = Navigation.Mode.None };
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.restoreOriginalTextOnEscape = false;
        input.customCaretColor = true;
        input.caretColor = Theme.Text;
        input.caretWidth = Mathf.RoundToInt(Theme.CaretWidth);
        input.caretBlinkRate = Theme.CaretBlinkRate;
        input.selectionColor = Theme.FieldSelection;
        input.SetTextWithoutNotify(value ?? "");
        input.onValueChanged.AddListener(v => Guard(label, () => onChange?.Invoke(v)));
        input.onEndEdit.AddListener(v => Guard(label, () => onEnd?.Invoke(v)));

        var focus = box.gameObject.AddComponent<KitField>();
        focus.Init(input);
        box.gameObject.SetActive(true);

        Register(kitRow, null, () => { _kit.Sounds.Press(); focus.Focus(); });
        return new FieldHandle(input, focus);
    }

    /// <summary>
    /// A key binding row: click it (or press Enter on it) and then press the new key. Esc cancels.
    /// Mouse buttons and pad buttons can't be bound.
    /// </summary>
    public KeyBindHandle KeyBind(string label, KeyCode key, Action<KeyCode> onChange)
    {
        Header(label);
        var kitRow = OptionRow(label);
        var text = UiParts.Text(kitRow.Rect, Theme, "", Theme.TextSize, Theme.Text);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(text.rectTransform, 24f, 0f, 24f, 0f);

        var theme = Theme;
        var handle = new KeyBindHandle(text, key);
        var capture = kitRow.gameObject.AddComponent<KitKeyCapture>();
        capture.Init(
            picked: k =>
            {
                handle.Set(k);
                Guard(label, () => onChange?.Invoke(k));
            },
            waiting: on =>
            {
                handle.Waiting(on);
                if (text != null)
                    text.color = on ? theme.TextHover : theme.Text;
            });

        void Begin()
        {
            _kit.Sounds.Press();
            capture.Begin();
        }
        Register(kitRow, null, Begin, Begin);
        return handle;
    }
}

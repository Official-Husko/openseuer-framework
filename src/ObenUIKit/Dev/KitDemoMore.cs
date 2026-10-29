using ObenUIKit.Controls;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Dev;

/// <summary>The demo window's "More" tab: titles, button looks, title-bar buttons and Resets, chords, scroll areas.</summary>
internal static class KitDemoMore
{
    private static float _volume = 0.5f;
    private static KeyChord _shortcut = new KeyChord(KeyCode.F7, KeyModifiers.Ctrl);

    public static void Build(UiBuilder ui)
    {
        ui.Title("A title for a section");
        var note = ui.Label("A label in another color and size.");
        note.Color = new Color(0.55f, 0.85f, 0.5f);
        note.Size = 20f;

        ui.Row(r =>
        {
            r.Button("Plain", () => UiKit.Toast("Plain"));
            r.Button("Quiet", ButtonStyle.Quiet, () => UiKit.Toast("Quiet"));
            r.Button("Box", ButtonStyle.Box, () => UiKit.Toast("Box"));
        }, TextAnchor.MiddleLeft);
        ui.Button("The main action", ButtonStyle.Box, () => UiKit.Toast("Main action"));

        SliderHandle volume = null;
        volume = ui.Slider("Volume", 0f, 1f, _volume, v => _volume = v, "0.00");
        ui.Resettable(() => Mathf.Abs(_volume - 0.5f) > 0.001f, () => { _volume = 0.5f; volume.Set(0.5f); }, "Default: 0.50");
        ui.HeaderButton("Max", () => { _volume = 1f; volume.Set(1f); }, "Turn it all the way up");

        ui.KeyBind("Shortcut", _shortcut, chord => _shortcut = chord);
        ui.Tooltip("Hold Ctrl, Shift or Alt and press a key.");

        var colours = new Color[12];
        for (int i = 0; i < colours.Length; i++)
            colours[i] = Color.HSVToRGB(i / 12f, 0.6f, 0.9f);
        ui.Swatches(null, colours, 0, i => { });

        ui.Header("A scroll area of its own");
        ui.Scroll(240f, s =>
        {
            for (int i = 0; i < 12; i++)
                s.Toggle("Option " + (i + 1), i % 3 == 0, on => { });
        });

        var area = ui.Area(80f);
        var image = new GameObject("Demo image", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(area, false);
        var rect = (RectTransform)image.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 60f);
        image.GetComponent<Image>().color = new Color(0.2f, 0.35f, 0.6f).linear;
    }
}

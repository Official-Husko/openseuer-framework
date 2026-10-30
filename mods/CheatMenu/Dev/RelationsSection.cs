// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Npcs;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// The player's relationships: pick one, see where it is and where it drifts, and set its disposition.
/// Setting it also sets the target it drifts to, so it stays put (<see cref="Relationships.SetDisposition"/>).
/// </summary>
internal static class RelationsSection
{
    private static int _picked;
    private static string _pickedId;
    private static float _value;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Relationships");
        if (!Relationships.IsAvailable)
        {
            ui.Label("There are no relationships in this scene.", muted: true);
            return;
        }
        var all = Relationships.All();
        if (all.Count == 0)
        {
            ui.Label("The player has no relationships yet.", muted: true);
            return;
        }
        if (_picked >= all.Count)
            _picked = 0;
        var names = new string[all.Count];
        for (int i = 0; i < all.Count; i++)
            names[i] = UiText.Plain(all[i].Name);

        var record = all[_picked];
        if (_pickedId != record.Id)
        {
            // The slider starts at the disposition whenever another relationship is picked.
            _pickedId = record.Id;
            _value = record.Disposition;
        }
        ui.Cycle("Relationship", names, _picked, i =>
        {
            _picked = i;
            ui.Window.Defer(ui.Window.Rebuild);
        });
        string id = record.Id;
        ui.Live(() =>
        {
            var now = Relationships.Get(id);
            return now == null ? "Gone." : UiText.Plain($"Now {now.Disposition:0.#}, drifting to {now.Target:0.#} by {now.ChangePerDay:0.##} a day" + (now.Active ? "." : "; paused."));
        }, muted: true);
        ui.Slider("Disposition", -100f, 100f, UnityEngine.Mathf.Clamp(_value, -100f, 100f), v => _value = v, "0");
        ui.Button("Set the disposition", () => DevActions.Act(ctx, ui, $"Set {record.Name} to {_value:0}",
            () => Relationships.SetDisposition(id, _value), "refused while the game saves or loads."));
    }
}

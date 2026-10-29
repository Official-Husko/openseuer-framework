// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Player;
using ObenUIKit;
using UnityEngine;

namespace GameTweaks.Camera;

/// <summary>
/// A key (F6 by default, rebindable on the Hotkeys page) switches between first and third person through the
/// framework's <see cref="CameraView"/>, and optionally the mouse wheel zooms in and out like the game's old
/// third-person camera. Third person is a mode the game no longer uses, so the body, its animations and
/// clipping through walls may look rough. While off there is no per-frame work.
/// </summary>
internal sealed class ThirdPersonTweak : TweakFeature
{
    public const string Key = "ThirdPerson";
    private const string StayFirst = "The camera has to stay in first person here.";

    private float _distance = CameraView.DefaultDistance;
    private bool _toldHeldBack;

    public override string Id => Key;
    public override string Name => "Third-person camera";
    public override string Description =>
        "F6 switches between first and third person (rebind it on the Hotkeys page). The game no longer uses third person, so the body can look rough.";

    public override void Apply(TweaksConfig config)
    {
        float distance = TweakMath.Factor(config.CameraDistance, 0.5f, 10f, CameraView.DefaultDistance);
        bool moved = distance != _distance;
        _distance = distance;
        CameraView.AllowScrollZoom(Owner, config.ScrollZoom);
        // The distance slider moves a camera that is already out.
        if (moved && CameraView.Wants(Owner) && !CameraView.IsFirstPerson)
            CameraView.SetThirdPerson(Owner, _distance);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Hotkeys.Register(this, "camera", "Switch between first and third person", KeyCode.F6, Toggle);
    }

    // Says it once when the game takes the camera in (a cutscene, a tight space, building).
    public override void OnUpdate()
    {
        bool held = CameraView.ThirdPersonHeldBack && CameraView.Wants(Owner);
        if (held && !_toldHeldBack)
            UiKit.Toast(StayFirst, "Camera");
        _toldHeldBack = held;
    }

    protected override void OnDisable()
    {
        CameraView.SetFirstPerson(Owner);
        CameraView.AllowScrollZoom(Owner, false);
        _toldHeldBack = false;
    }

    private void Toggle()
    {
        if (CameraView.Toggle(Owner, _distance) || !CameraView.IsAvailable)
            return;
        UiKit.Toast(CameraView.IsFirstPerson ? StayFirst : "Another mod keeps the camera in third person.", "Camera");
    }
}

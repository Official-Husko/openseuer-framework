// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Modules;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>
/// First and third person for the player camera. The game still has its third-person camera (the camera sits
/// <c>currentDistance</c> behind the head and the body shows beyond 0.5 m) but keeps it locked. Mods ask for
/// third person with <see cref="SetThirdPerson"/>: the locks are lifted while any mod asks for it or allows
/// scroll zoom, and the game's own values come back when the last one lets go. Where the game needs first
/// person (camera triggers in cutscenes and tight spaces, building mode) the camera goes in and the requests
/// wait; it goes back out when the game lets go. Requests end with a level load or when their mod unloads.
/// While a save is written the camera looks the way the game left it, so a save made in third person loads
/// fine without mods.
/// </summary>
[PatchArea("Camera")]
public static class CameraView
{
    /// <summary>How far behind the head third person goes when a mod doesn't say, in metres.</summary>
    public const float DefaultDistance = 2.5f;

    /// <summary>The camera switched view; the payload is true for first person.</summary>
    public static readonly GameEvent<bool> ModeChanged = new GameEvent<bool>(nameof(ModeChanged));

    private static readonly Dictionary<string, float> Requests = new Dictionary<string, float>();
    private static readonly HashSet<string> ZoomOwners = new HashSet<string>();
    private static readonly HashSet<string> BodyOwners = new HashSet<string>();
    private static OwnerWatch _owners;
    private static ModLogger _log;
    private static FrameworkModule _module;

    // The distance of the newest request.
    private static float _distance = DefaultDistance;

    /// <summary>There is a player camera (a gameplay level is loaded).</summary>
    public static bool IsAvailable => ThirdPersonCameraCollision.instance != null;

    /// <summary>The camera is in first person. False when there is no camera.</summary>
    public static bool IsFirstPerson
    {
        get
        {
            var view = ThirdPersonCameraCollision.instance;
            return view != null && view.firstperson;
        }
    }

    /// <summary>
    /// Third person is possible right now: there is a camera, the camera hooks are in place, no camera trigger
    /// forces first person and the player isn't building.
    /// </summary>
    public static bool ThirdPersonAllowed
    {
        get
        {
            var view = ThirdPersonCameraCollision.instance;
            return view != null && HooksOk && CameraLocks.Allowed(view);
        }
    }

    /// <summary>Some mod asks for third person.</summary>
    public static bool ThirdPersonWanted => Requests.Count > 0;

    /// <summary>A mod asks for third person, but the game needs first person here; the camera goes back out when it lets go.</summary>
    public static bool ThirdPersonHeldBack => Requests.Count > 0 && CameraLocks.HeldBack;

    /// <summary>Some mod lets the mouse wheel zoom out of first person.</summary>
    public static bool ScrollZoomAllowed => ZoomOwners.Count > 0;

    /// <summary>Some mod keeps the player's body drawn in first person.</summary>
    public static bool FirstPersonBodyShown => BodyOwners.Count > 0;

    /// <summary>True when <paramref name="owner"/> asks for third person.</summary>
    public static bool Wants(ObenMod owner) => owner != null && Requests.ContainsKey(owner.Info.Id);

    /// <summary>
    /// Moves the camera <paramref name="distance"/> metres behind the head (within what the game's camera
    /// allows) and keeps third person for <paramref name="owner"/> until it calls <see cref="SetFirstPerson"/>,
    /// the level changes or the mod unloads. Asking again changes the distance. False, and nothing asked, when
    /// third person isn't allowed here (see <see cref="ThirdPersonAllowed"/>).
    /// </summary>
    public static bool SetThirdPerson(ObenMod owner, float distance = DefaultDistance)
    {
        string id = _owners.Watch(owner);
        if (!ThirdPersonAllowed)
            return false;
        Requests[id] = distance;
        _distance = distance;
        CameraLocks.GoOut(ThirdPersonCameraCollision.instance, distance);
        return true;
    }

    /// <summary>
    /// Drops <paramref name="owner"/>'s third-person request. When no other mod asks for it, the camera goes
    /// back to the head and the game's locks come back (unless a mod allows scroll zoom); true then. False
    /// while another mod still keeps third person.
    /// </summary>
    public static bool SetFirstPerson(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        Requests.Remove(owner.Info.Id);
        if (Requests.Count > 0)
            return false;
        CameraLocks.ToFirst(keepUnlocked: ZoomOwners.Count > 0);
        return true;
    }

    /// <summary>
    /// First person to third, third to first, for <paramref name="owner"/> (a hotkey). False when nothing
    /// changed: no camera, third person not allowed here, or another mod keeps third person.
    /// </summary>
    public static bool Toggle(ObenMod owner, float distance = DefaultDistance)
    {
        if (!IsAvailable)
            return false;
        return IsFirstPerson ? SetThirdPerson(owner, distance) : SetFirstPerson(owner);
    }

    /// <summary>
    /// Lets the mouse wheel (and the pad's triggers) zoom the camera in and out of third person like the game's
    /// old camera, while any mod allows it. Kept over level loads; ends when the mod unloads.
    /// </summary>
    public static void AllowScrollZoom(ObenMod owner, bool allow)
    {
        string id = _owners.Watch(owner);
        if (allow)
            ZoomOwners.Add(id);
        else if (ZoomOwners.Remove(id))
            Settle();
    }

    /// <summary>
    /// Keeps the player's body drawn in first person too (for a mirror or a cutscene) until
    /// <see cref="ClearFirstPersonBody"/>, the level changes or the mod unloads. False when the hook that
    /// hides the body isn't in place.
    /// </summary>
    public static bool SetFirstPersonBody(ObenMod owner)
    {
        string id = _owners.Watch(owner);
        if (!HooksOk)
            return false;
        if (BodyOwners.Add(id))
            CameraLocks.ShowBody();
        return true;
    }

    /// <summary>Lets the game hide the body in first person again once no mod keeps it. False when <paramref name="owner"/> didn't keep it.</summary>
    public static bool ClearFirstPersonBody(ObenMod owner)
    {
        if (owner == null || !BodyOwners.Remove(owner.Info.Id))
            return false;
        if (BodyOwners.Count == 0)
            CameraLocks.HideBody();
        return true;
    }

    private static bool HooksOk
    {
        get
        {
            if (_module == null)
                _module = FrameworkModules.Find("camera");
            return _module != null && _module.PatchesOk;
        }
    }

    internal static void Init(ObenMod framework)
    {
        _log = framework.Log;
        _owners = new OwnerWatch(Release);
        ThirdPersonCameraCollision.CameraModeChange += Forward;
        GameEvents.SceneLoaded.Subscribe(framework, _ => LevelChanged());
        framework.Track(Disposable.Create(() =>
        {
            ThirdPersonCameraCollision.CameraModeChange -= Forward;
            _owners.Clear();
            bool body = BodyOwners.Count > 0;
            Requests.Clear();
            ZoomOwners.Clear();
            BodyOwners.Clear();
            Guard("Giving the camera back", () =>
            {
                CameraLocks.ToFirst(keepUnlocked: false);
                if (body)
                    CameraLocks.HideBody();
            });
            CameraLocks.Forget();
            _module = null;
        }));
    }

    /// <summary>Per frame: keeps the locks lifted while wanted and follows the game's first-person zones. Allocates nothing.</summary>
    internal static void Update()
    {
        if (Requests.Count == 0 && ZoomOwners.Count == 0)
            return;
        if (HooksOk)
            CameraLocks.Enforce(Requests.Count > 0, _distance);
    }

    /// <summary>A mod is gone: its requests go with it.</summary>
    private static void Release(string id)
    {
        bool third = Requests.Remove(id) && Requests.Count == 0;
        bool zoom = ZoomOwners.Remove(id);
        Guard("Giving the camera back", () =>
        {
            if (third)
                CameraLocks.ToFirst(keepUnlocked: ZoomOwners.Count > 0);
            else if (zoom)
                Settle();
            if (BodyOwners.Remove(id) && BodyOwners.Count == 0)
                CameraLocks.HideBody();
        });
    }

    // Gives the locks back once neither third person nor scroll zoom needs them lifted.
    private static void Settle()
    {
        if (Requests.Count == 0 && ZoomOwners.Count == 0)
            CameraLocks.Relock();
    }

    // A new level has a new camera; requests and kept bodies end with the old one.
    private static void LevelChanged()
    {
        Requests.Clear();
        BodyOwners.Clear();
        CameraLocks.Forget();
    }

    private static void Forward(bool firstPerson)
    {
        // The game shows or hides the body before it says so; make sure there is a body to show.
        try
        {
            if (!firstPerson)
                BodyHolders.Open(ThirdPersonCameraCollision.instance);
            else if (!FirstPersonBodyShown)
                BodyHolders.Close();
        }
        catch (Exception e)
        {
            _log?.Error("Showing the player's body for the camera failed", e);
        }
        ModeChanged.Raise(firstPerson, _log);
    }

    private static void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { _log?.Error($"{what} failed", e); }
    }
}

using ObenCoop.Net.Presence;
using ObenFramework.Game;
using ObenFramework.Player;
using UnityEngine;

namespace ObenCoop.Presence;

/// <summary>
/// Feeds the local player into <see cref="CoopPresence"/> every frame while a level is ready: the
/// scene name as the area, the body's position and heading, a velocity worked out from movement,
/// and what the player is doing. At the main menu, while loading or during a level change it
/// publishes nothing, so presence stops and the others drop this player until they are back.
/// Allocation free per frame: the scene name is only read when the scene changes.
/// </summary>
internal sealed class PresencePublisher
{
    // Velocity is measured over at least this long; single frames are too noisy.
    private const float VelocityWindow = 0.05f;
    private const float MovingSpeedSqr = 0.2f * 0.2f;

    private readonly CoopPresence _presence;
    private int _sceneHandle;
    private bool _hasScene;
    private string _area = "";
    private bool _hasLast;
    private Vector3 _lastPosition;
    private float _lastTime;
    private Vector3 _velocity;

    public PresencePublisher(CoopPresence presence)
    {
        _presence = presence;
    }

    /// <summary>The area name last published, for the status text.</summary>
    public string Area => _area;

    public void Update()
    {
        var locator = PlayerLocator.instance;
        if (locator == null || !WorldState.IsReady || PlayerState.IsTransitioning)
        {
            _hasLast = false;
            return;
        }

        var scene = locator.gameObject.scene;
        if (!_hasScene || scene.handle != _sceneHandle)
        {
            _hasScene = true;
            _sceneHandle = scene.handle;
            string name = scene.name ?? "";
            _area = name.Length <= PresenceCodec.MaxAreaLength ? name : name.Substring(0, PresenceCodec.MaxAreaLength);
            _hasLast = false;
        }

        var body = locator.transform;
        Vector3 position = body.position;
        TrackVelocity(position, Time.realtimeSinceStartup);

        var at = new PresenceVector(position.x, position.y, position.z);
        var velocity = new PresenceVector(_velocity.x, _velocity.y, _velocity.z);
        float yaw = body.eulerAngles.y;
        if (!PresenceCodec.IsValid(_area, at, yaw, velocity, out _))
            return;
        _presence.Publish(_area, at, yaw, velocity, Flags());
    }

    private void TrackVelocity(Vector3 position, float now)
    {
        if (!_hasLast)
        {
            _hasLast = true;
            _lastPosition = position;
            _lastTime = now;
            _velocity = Vector3.zero;
            return;
        }
        float elapsed = now - _lastTime;
        if (elapsed < VelocityWindow)
            return;
        var velocity = (position - _lastPosition) / elapsed;
        // A jump (door, teleport) is not a speed; presence flags it as a jump on its own.
        _velocity = velocity.magnitude > PresenceCodec.MaxSpeed / 10f ? Vector3.zero : velocity;
        _lastPosition = position;
        _lastTime = now;
    }

    private PresenceFlags Flags()
    {
        var flags = PresenceFlags.None;
        if (_velocity.sqrMagnitude > MovingSpeedSqr)
            flags |= PresenceFlags.Moving;
        var movement = OpenSewerCharacterController.instance;
        if (movement != null && movement.walk)
            flags |= PresenceFlags.Walking;
        if (PlayerState.IsCrouching)
            flags |= PresenceFlags.Crouching;
        if (PlayerState.IsSeated)
            flags |= PresenceFlags.Sitting;
        if (PlayerState.IsSwimming)
            flags |= PresenceFlags.Swimming;
        if (PlayerState.IsSleeping)
            flags |= PresenceFlags.Sleeping;
        return flags;
    }
}

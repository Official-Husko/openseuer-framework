using System;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Where the player is, without ever throwing: every member returns a neutral value (null, empty)
/// at the main menu or while a level is loading. The game rebuilds its player singletons on each
/// level load, so nothing here is cached.
/// </summary>
public static class PlayerRef
{
    /// <summary>A playable player exists in the loaded scene.</summary>
    public static bool Exists => PlayerLocator.instance != null;

    /// <summary>The player's locator component (its transform is the body that moves), or null.</summary>
    public static PlayerLocator Locator => PlayerLocator.instance != null ? PlayerLocator.instance : null;

    /// <summary>The body transform, or null when there is no player.</summary>
    public static Transform Body => PlayerLocator.instance != null ? PlayerLocator.instance.transform : null;

    /// <summary>The camera the player sees through, or null.</summary>
    public static Transform CameraTransform => PlayerCamera.instance != null ? PlayerCamera.instance.transform : null;

    /// <summary>The player's rigidbody, or null.</summary>
    public static Rigidbody Rigidbody => PlayerLocator.instance != null ? PlayerLocator.instance.GetComponent<Rigidbody>() : null;

    /// <summary>World position of the body (at the feet), or null without a player.</summary>
    public static Vector3? Position
    {
        get
        {
            var body = Body;
            return body != null ? body.position : (Vector3?)null;
        }
    }

    /// <summary>World yaw of the body in degrees, 0 to 360, or null.</summary>
    public static float? Yaw
    {
        get
        {
            var body = Body;
            return body != null ? body.eulerAngles.y : (float?)null;
        }
    }

    /// <summary>
    /// Camera pitch in degrees, -90 to 90, positive looking down (Unity's own convention), or null.
    /// </summary>
    public static float? Pitch
    {
        get
        {
            var camera = CameraTransform;
            return camera != null ? Mathf.DeltaAngle(0f, camera.eulerAngles.x) : (float?)null;
        }
    }

    /// <summary>World yaw the camera looks along, or null. It matches <see cref="Yaw"/> in first person.</summary>
    public static float? LookYaw
    {
        get
        {
            var camera = CameraTransform;
            return camera != null ? camera.eulerAngles.y : (float?)null;
        }
    }

    /// <summary>Name of the scene the player lives in, or an empty string.</summary>
    public static string SceneName => PlayerLocator.instance != null ? PlayerLocator.instance.gameObject.scene.name : string.Empty;

    /// <summary>Velocity of the player's rigidbody, or zero.</summary>
    public static Vector3 Velocity
    {
        get
        {
            var body = Rigidbody;
            return body != null ? body.linearVelocity : Vector3.zero;
        }
    }

    /// <summary>The locator, or a clear error for callers that cannot continue without a player.</summary>
    public static PlayerLocator Require() =>
        Locator != null ? Locator : throw new InvalidOperationException("There is no player right now (main menu or loading).");
}

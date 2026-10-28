// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>
/// The rig a look was built with on a body: the holder object everything sits under, the avatar it moves
/// with and, for retargeted looks, control over the pose driver that copies the body's pose onto it.
/// </summary>
public sealed class SceneLookRigView
{
    private readonly SceneLookPoseDriver _driver;

    internal SceneLookRigView(GameObject holder, Avatar avatar, string path, SceneLookPoseDriver driver)
    {
        Holder = holder;
        Avatar = avatar;
        IsHuman = avatar != null && avatar.isHuman;
        Path = path;
        _driver = driver;
    }

    /// <summary>The object the look's renderers (and skeleton, when retargeted) are under.</summary>
    public GameObject Holder { get; }

    /// <summary>The look's own avatar when retargeted, otherwise the body's.</summary>
    public Avatar Avatar { get; }

    public bool IsHuman { get; }

    /// <summary>"name", "retarget" or "skeleton": how the look was put on.</summary>
    public string Path { get; }

    public bool HasPoseDriver => _driver != null;

    /// <summary>Whether the driver copies the body's pose every frame. False when there is no driver.</summary>
    public bool AutoPose
    {
        get => _driver != null && _driver.Auto;
        set
        {
            if (_driver != null)
                _driver.Auto = value;
        }
    }

    /// <summary>Copies the body's pose onto the look once. False when there is no driver.</summary>
    public bool SyncPose()
    {
        if (_driver == null)
            return false;
        _driver.Sync();
        return true;
    }
}

using UnityEngine;

namespace ObenFramework.Npcs;

public sealed partial class NpcClaim
{
    private AnimationPlayback _animation;

    /// <summary>The animation this claim plays on the NPC, or null.</summary>
    public AnimationPlayback Animation => _animation != null && !_animation.Finished ? _animation : null;

    /// <summary>
    /// Plays a clip on the NPC's body over its own animation (see <see cref="NpcAnimation"/>). Pair it with
    /// <see cref="Pause"/> or <see cref="NpcHolds.Freeze"/> when the NPC should stand still for it. Returns
    /// null when the claim is no longer valid or the NPC has no Animator. The claim's end stops it.
    /// </summary>
    public AnimationPlayback PlayAnimation(AnimationClip clip, bool loop = true, float fadeSeconds = 0.2f)
    {
        if (!IsValid)
            return null;
        var animator = _npc.GetComponentInChildren<Animator>();
        if (animator == null)
            return null;
        var playback = NpcAnimation.Start(Owner, animator, clip, loop, fadeSeconds);
        if (playback != null)
            _animation = playback;
        return playback;
    }

    /// <summary>Fades out the animation started by <see cref="PlayAnimation"/> (0 stops at once).</summary>
    public void StopAnimation(float fadeSeconds = 0.2f)
    {
        var playback = _animation;
        _animation = null;
        if (playback != null)
            playback.Stop(fadeSeconds);
    }
}

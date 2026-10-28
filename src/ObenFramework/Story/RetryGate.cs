// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Story;

/// <summary>
/// Waits for a condition that should come true within a few frames, trying once per frame and giving
/// up after <see cref="MaxAttempts"/>. Once it passed it stays done until <see cref="Reset"/>, so arming
/// it again in the same session does nothing. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class RetryGate
{
    /// <summary>About ten seconds at 60 frames per second.</summary>
    public const int MaxAttempts = 600;

    /// <summary>Armed and not yet passed or given up.</summary>
    public bool Waiting { get; private set; }

    /// <summary>The condition was met since the last <see cref="Reset"/>.</summary>
    public bool Done { get; private set; }

    /// <summary>The last wait ran out of attempts.</summary>
    public bool GaveUp { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Starts waiting, unless the gate already passed.</summary>
    public void Arm()
    {
        if (Done)
            return;
        Waiting = true;
        GaveUp = false;
        Attempts = 0;
    }

    /// <summary>Stops waiting without passing; a later <see cref="Arm"/> starts over.</summary>
    public void Clear()
    {
        Waiting = false;
        Attempts = 0;
    }

    /// <summary>Forgets everything, including that the gate passed.</summary>
    public void Reset()
    {
        Waiting = false;
        Done = false;
        GaveUp = false;
        Attempts = 0;
    }

    /// <summary>
    /// One attempt. Returns true exactly once, on the attempt where <paramref name="ready"/> is true while
    /// waiting. After <see cref="MaxAttempts"/> failed attempts the gate stops waiting and sets <see cref="GaveUp"/>.
    /// </summary>
    public bool Try(bool ready)
    {
        if (!Waiting)
            return false;
        Attempts++;
        if (ready)
        {
            Waiting = false;
            Done = true;
            return true;
        }
        if (Attempts >= MaxAttempts)
        {
            Waiting = false;
            GaveUp = true;
        }
        return false;
    }
}

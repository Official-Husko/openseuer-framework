namespace ObenUIKit.Input;

/// <summary>
/// Turns a held direction into steps the way the game's menus do: one right away, the next after
/// 0.3 s, then one every 0.15 s.
/// </summary>
internal struct AxisRepeat
{
    private const float FirstDelay = 0.3f;
    private const float RepeatDelay = 0.15f;

    private int _direction;
    private float _next;

    /// <summary>True when a step is due. <paramref name="direction"/> is -1, 0 or 1.</summary>
    public bool Pulse(int direction, float now)
    {
        if (direction == 0)
        {
            _direction = 0;
            return false;
        }
        if (direction != _direction)
        {
            _direction = direction;
            _next = now + FirstDelay;
            return true;
        }
        if (now < _next)
            return false;
        _next = now + RepeatDelay;
        return true;
    }
}

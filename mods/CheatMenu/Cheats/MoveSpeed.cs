using HarmonyLib;

namespace CheatMenu.Cheats;

/// <summary>
/// Scales the player's walking, running, swimming and sneaking speed. The game never changes
/// those values after the controller is built, so setting them through the controller's own
/// properties is enough. The base values are read once per controller, because the game builds a
/// new one for every level.
/// </summary>
internal static class MoveSpeed
{
    public const float Min = 0.5f;
    public const float Max = 5f;

    private static readonly string[] FieldNames =
        { "_walkSpeed", "_runSpeed", "_swimmingSpeed", "_crouchSpeed", "_backwardSpeed", "_strafeSpeed" };

    private static readonly AccessTools.FieldRef<OpenSewerCharacterController, float>[] Fields = BuildFields();

    private static readonly float[] Base = new float[FieldNames.Length];
    private static int _controllerId;

    /// <summary>The multiplier now in effect, 1 is the game's own speed.</summary>
    public static float Factor { get; private set; } = 1f;

    public static bool Available => OpenSewerCharacterController.instance != null;

    public static void Set(float factor)
    {
        Factor = UnityEngine.Mathf.Clamp(factor, Min, Max);
        Apply();
    }

    /// <summary>Writes the current factor to the controller in this level, if there is one.</summary>
    public static void Apply()
    {
        var controller = OpenSewerCharacterController.instance;
        if (controller == null)
            return;

        int id = controller.GetInstanceID();
        if (id != _controllerId)
        {
            // A controller we haven't touched yet still holds the game's values.
            for (int i = 0; i < Fields.Length; i++)
                Base[i] = Fields[i](controller);
            _controllerId = id;
        }

        controller.walkSpeed = Base[0] * Factor;
        controller.runSpeed = Base[1] * Factor;
        controller.swimmingSpeed = Base[2] * Factor;
        controller.crouchSpeed = Base[3] * Factor;
        controller.backwardSpeed = Base[4] * Factor;
        controller.strafeSpeed = Base[5] * Factor;
    }

    /// <summary>Puts the game's own speeds back (used when the mod unloads).</summary>
    public static void Restore()
    {
        Factor = 1f;
        Apply();
    }

    private static AccessTools.FieldRef<OpenSewerCharacterController, float>[] BuildFields()
    {
        var refs = new AccessTools.FieldRef<OpenSewerCharacterController, float>[FieldNames.Length];
        for (int i = 0; i < refs.Length; i++)
            refs[i] = AccessTools.FieldRefAccess<OpenSewerCharacterController, float>(FieldNames[i]);
        return refs;
    }
}

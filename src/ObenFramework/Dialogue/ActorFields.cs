using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Fields on the Dialogue System's actors (the Lua <c>Actor</c> table), for example
/// <c>Player.Money</c> or an NPC's <c>KnowsTheName</c>. Actors are addressed by the name they have in the
/// dialogue database. Like <see cref="LuaVariables"/>, reads are always safe and writes during a save
/// load are held back until it is done.
/// </summary>
public static class ActorFields
{
    public static bool ActorExists(string actor) =>
        !string.IsNullOrEmpty(actor) && LuaState.IsReady && DialogueLua.DoesTableElementExist("Actor", actor);

    public static bool Exists(string actor, string field) => Get(actor, field).Exists;

    /// <summary>The field's value, or a value whose <see cref="DialogueValue.Exists"/> is false.</summary>
    public static DialogueValue Get(string actor, string field)
    {
        if (string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(field) || !LuaState.IsReady)
            return default;
        return new DialogueValue(DialogueLua.GetActorField(actor, field));
    }

    public static string GetString(string actor, string field, string fallback = null) => Get(actor, field).StringOr(fallback);

    public static int GetInt(string actor, string field, int fallback = 0) => Get(actor, field).IntOr(fallback);

    public static float GetFloat(string actor, string field, float fallback = 0f) => (float)Get(actor, field).NumberOr(fallback);

    public static bool GetBool(string actor, string field, bool fallback = false) => Get(actor, field).BoolOr(fallback);

    /// <summary>
    /// Sets a field, creating the actor's entry when it is missing. The value rules are the same as for
    /// <see cref="LuaVariables.Set"/>. Money has its own side effects (HUD, sounds): change it through
    /// <c>Wallet</c>, not by writing <c>Player.Money</c> here.
    /// </summary>
    public static LuaWriteResult Set(string actor, string field, object value)
    {
        LuaChecks.Name(actor, nameof(actor));
        LuaChecks.Name(field, nameof(field));
        return LuaWrites.ActorField(actor, field, LuaChecks.Value(value, nameof(value)), onlyIfMissing: false);
    }

    /// <summary>Creates the field with this value if it doesn't exist, and leaves an existing one alone.</summary>
    public static LuaWriteResult SetDefault(string actor, string field, object value)
    {
        LuaChecks.Name(actor, nameof(actor));
        LuaChecks.Name(field, nameof(field));
        return LuaWrites.ActorField(actor, field, LuaChecks.Value(value, nameof(value)), onlyIfMissing: true);
    }
}

using System;
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Writes to the Lua state, held back while a save is loading. The game applies the saved Lua text as
/// the last step of a load, so a variable set earlier in the load would silently revert.
/// </summary>
internal static class LuaWrites
{
    private struct Pending
    {
        public string Name;
        public string Field;        // null for a variable, the field name for an actor field
        public object Value;
        public bool OnlyIfMissing;

        public bool SameTarget(in Pending other) => Name == other.Name && Field == other.Field;
    }

    private static readonly List<Pending> Queue = new List<Pending>();
    private static bool _flushing;

    public static LuaWriteResult Variable(string name, object value, bool onlyIfMissing) =>
        Submit(new Pending { Name = name, Value = value, OnlyIfMissing = onlyIfMissing });

    public static LuaWriteResult ActorField(string actor, string field, object value, bool onlyIfMissing) =>
        Submit(new Pending { Name = actor, Field = field, Value = value, OnlyIfMissing = onlyIfMissing });

    /// <summary>Applies everything that was held back, if writes are possible now.</summary>
    public static void Flush()
    {
        if (_flushing || Queue.Count == 0 || !LuaState.CanWrite)
            return;

        _flushing = true;
        try
        {
            var items = Queue.ToArray();
            Queue.Clear();
            foreach (var item in items)
            {
                try { Apply(item); }
                catch (Exception e) { Events.GameEvents.Log.Error($"A queued Lua write to '{Describe(item)}' failed", e); }
            }
        }
        finally
        {
            _flushing = false;
        }
    }

    private static LuaWriteResult Submit(Pending write)
    {
        if (LuaState.CanWrite)
        {
            Flush();   // anything held back goes first, so writes keep their order
            if (Queue.Count == 0)
            {
                Apply(write);
                return LuaWriteResult.Applied;
            }
        }

        // A newer plain write makes older ones for the same target pointless.
        if (!write.OnlyIfMissing)
            Queue.RemoveAll(q => q.SameTarget(write));
        Queue.Add(write);
        return LuaWriteResult.Deferred;
    }

    private static void Apply(Pending write)
    {
        if (write.Field == null)
        {
            if (write.OnlyIfMissing && DialogueLua.DoesVariableExist(write.Name))
                return;
            DialogueLua.SetVariable(write.Name, write.Value);
            return;
        }

        if (write.OnlyIfMissing && new DialogueValue(DialogueLua.GetActorField(write.Name, write.Field)).Exists)
            return;
        DialogueLua.SetActorField(write.Name, write.Field, write.Value);
    }

    private static string Describe(in Pending write) => write.Field == null ? write.Name : write.Name + "." + write.Field;
}

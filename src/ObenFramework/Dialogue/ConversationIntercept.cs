// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Story;
using ObenLoader;
using ObenLoader.Runtime;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Dialogue;

/// <summary>A conversation about to start. Handlers of <see cref="Conversations.Intercept"/> may redirect or cancel it.</summary>
public sealed class ConversationStarting
{
    internal ConversationStarting(string title, Transform actor, Transform conversant)
    {
        Title = title;
        OriginalTitle = title;
        Actor = actor;
        Conversant = conversant;
    }

    /// <summary>The conversation that will start. Set it to another title of the database to play that one instead.</summary>
    public string Title { get; set; }

    /// <summary>The title the game asked for.</summary>
    public string OriginalTitle { get; }

    /// <summary>The first speaker (the player in the game's conversations); may be null.</summary>
    public Transform Actor { get; }

    /// <summary>The NPC, or null.</summary>
    public Transform Conversant { get; }

    /// <summary>
    /// Stops the conversation from starting. Once one handler cancels it stays cancelled. For an NPC the game
    /// gives the player control back by itself a second later.
    /// </summary>
    public bool Cancel { get; set; }
}

public static partial class Conversations
{
    private sealed class Interceptor
    {
        public ITracker Owner;
        public Action<ConversationStarting> Handler;
    }

    private static readonly List<Interceptor> Interceptors = new List<Interceptor>();
    private static Interceptor[] _snapshot = Array.Empty<Interceptor>();
    private static bool _intercepting;

    /// <summary>
    /// Sees every conversation before it starts (NPC talk, phone calls, narration, scripted scenes) and may
    /// redirect it to another conversation or cancel it. Handlers run in the order they were added, each in
    /// its own try/catch; a redirect to a title the database doesn't have is ignored. Removed when
    /// <paramref name="owner"/> unloads.
    /// </summary>
    public static void Intercept(ITracker owner, Action<ConversationStarting> handler)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var entry = new Interceptor { Owner = owner, Handler = handler };
        Interceptors.Add(entry);
        _snapshot = Interceptors.ToArray();
        owner.Track(Disposable.Create(() =>
        {
            Interceptors.Remove(entry);
            _snapshot = Interceptors.ToArray();
        }));
    }

    /// <summary>Runs the interceptors. Returns false to stop the start.</summary>
    internal static bool RunInterceptors(ref string title, Transform actor, Transform conversant)
    {
        var handlers = _snapshot;
        if (handlers.Length == 0 || _intercepting)
            return true;
        _intercepting = true;
        try
        {
            var args = new ConversationStarting(title, actor, conversant);
            bool cancelled = false;
            foreach (var interceptor in handlers)
            {
                try { interceptor.Handler(args); }
                catch (Exception e) { StoryLog.Failed("Conversation interceptor of " + StoryLog.NameOf(interceptor.Owner), e); }
                cancelled |= args.Cancel;
                args.Cancel = cancelled;
            }
            if (cancelled)
                return false;
            if (!string.IsNullOrEmpty(args.Title) && args.Title != title)
            {
                if (Exists(args.Title))
                    title = args.Title;
                else
                    StoryLog.Refused("Conversation redirect", $"'{args.Title}' is not in the dialogue database; '{title}' plays instead");
            }
            return true;
        }
        finally
        {
            _intercepting = false;
        }
    }
}

/// <summary>Every conversation start goes through this overload of the Dialogue System controller.</summary>
[HarmonyPatch]
internal static class ConversationInterceptPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DialogueSystemController), nameof(DialogueSystemController.StartConversation),
        typeof(string), typeof(Transform), typeof(Transform), typeof(int), typeof(IDialogueUI), typeof(List<ActorOverride>))]
    private static bool BeforeStart(ref string title, Transform actor, Transform conversant)
    {
        try
        {
            return Conversations.RunInterceptors(ref title, actor, conversant);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Conversation interception", e);
            return true;
        }
    }
}

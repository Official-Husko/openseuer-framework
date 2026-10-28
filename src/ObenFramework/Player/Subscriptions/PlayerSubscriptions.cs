// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>A subscription the player has, from <see cref="PlayerSubscriptions.Active"/>.</summary>
public sealed class SubscriptionInfo
{
    internal SubscriptionInfo(PlayerSubscription subscription)
    {
        Id = subscription.Id ?? "";
        Name = subscription.subscriptionName ?? "";
        Description = subscription.description ?? "";
        Coins = subscription.cost.OC;
        RealMoney = subscription.cost.RM;
        Interval = subscription.invoicingInterval;
        IsPhonePlan = subscription is PlayerPhonePlanSubscription;
    }

    /// <summary>The game's id (its own id, else its asset path, else its name).</summary>
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public int Coins { get; }
    public float RealMoney { get; }
    public PlayerSubscription.Interval Interval { get; }

    /// <summary>A phone plan. These go through the phone shop; <see cref="PlayerSubscriptions"/> won't change them.</summary>
    public bool IsPhonePlan { get; }
}

/// <summary>
/// The player's running subscriptions (electricity, mail, magazines, phone plans): list them, check one, and
/// subscribe or cancel the way the game's own offers do, invoice included. Phone plans are left to the
/// phone shop, which also swaps the phone's plan.
/// </summary>
public static class PlayerSubscriptions
{
    /// <summary>Every subscription that is running, empty without a player.</summary>
    public static List<SubscriptionInfo> Active()
    {
        var all = new List<SubscriptionInfo>();
        var controller = PlayerSubscriptionsController.instance;
        if (controller == null || controller.activeSubscriptions == null)
            return all;
        foreach (var info in controller.activeSubscriptions)
        {
            if (info != null && info.subscription != null)
                all.Add(new SubscriptionInfo(info.subscription));
        }
        return all;
    }

    public static bool IsActive(string id)
    {
        var controller = PlayerSubscriptionsController.instance;
        return !string.IsNullOrEmpty(id) && controller != null && controller.SubscriptionIdActive(id);
    }

    /// <summary>
    /// Starts a subscription by id or asset path, with the game's notification and first invoice. False when
    /// it is unknown, already running, a phone plan, or there is no player.
    /// </summary>
    public static bool Subscribe(string idOrPath)
    {
        if (PlayerSubscriptionsController.instance == null || SaveGate.IsBusy || IsActive(idOrPath))
            return false;
        var subscription = Resolve(idOrPath);
        if (subscription == null || subscription is PlayerPhonePlanSubscription)
            return false;
        subscription.Subscribe();
        return IsActive(subscription.Id);
    }

    /// <summary>Cancels a running subscription (not a phone plan). False when it isn't running.</summary>
    public static bool Unsubscribe(string id)
    {
        var controller = PlayerSubscriptionsController.instance;
        if (controller == null || SaveGate.IsBusy || !IsActive(id))
            return false;
        foreach (var info in controller.activeSubscriptions)
        {
            var subscription = info != null ? info.subscription : null;
            if (subscription == null || subscription.Id != id)
                continue;
            if (subscription is PlayerPhonePlanSubscription)
                return false;
            subscription.Unsubscribe();
            return !IsActive(id);
        }
        return false;
    }

    private static PlayerSubscription Resolve(string idOrPath)
    {
        if (string.IsNullOrEmpty(idOrPath))
            return null;
        foreach (var subscription in Resources.FindObjectsOfTypeAll<PlayerSubscription>())
        {
            if (subscription != null && (subscription.Id == idOrPath || subscription.addressableAssetPath == idOrPath))
                return subscription;
        }
        // Not loaded: asset paths can be loaded the way the game loads saved subscriptions.
        if (idOrPath.IndexOf('/') < 0)
            return null;
        try
        {
            return SavableScriptableObject.LoadFromPath(idOrPath) as PlayerSubscription;
        }
        catch (Exception e)
        {
            GameEvents.Log.Warn($"No subscription at '{idOrPath}': {e.Message}");
            return null;
        }
    }
}

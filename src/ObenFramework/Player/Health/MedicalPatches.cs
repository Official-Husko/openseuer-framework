// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using Treatment = DoctorPanelSelection.Treatment;

namespace ObenFramework.Player;

/// <summary>
/// Raises <see cref="Medical.Treated"/> after a successful purchase. Each doctor page overrides
/// BuyTreatment (calling the base for the payment first), so every override is patched.
/// </summary>
[HarmonyPatch]
internal static class MedicalPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TreatmentAddictions), nameof(TreatmentAddictions.BuyTreatment))]
    private static void AfterAddictions(DoctorPanelSelection __instance, Treatment treatment, bool __result) => Report(__instance, treatment, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TreatmentFungalDisease), nameof(TreatmentFungalDisease.BuyTreatment))]
    private static void AfterFungal(DoctorPanelSelection __instance, Treatment treatment, bool __result) => Report(__instance, treatment, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TreatmentMentalHealth), nameof(TreatmentMentalHealth.BuyTreatment))]
    private static void AfterMental(DoctorPanelSelection __instance, Treatment treatment, bool __result) => Report(__instance, treatment, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TreatmentPhysicalHealth), nameof(TreatmentPhysicalHealth.BuyTreatment))]
    private static void AfterPhysical(DoctorPanelSelection __instance, Treatment treatment, bool __result) => Report(__instance, treatment, __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TreatmentRadiation), nameof(TreatmentRadiation.BuyTreatment))]
    private static void AfterRadiation(DoctorPanelSelection __instance, Treatment treatment, bool __result) => Report(__instance, treatment, __result);

    private static void Report(DoctorPanelSelection page, Treatment treatment, bool bought)
    {
        if (!bought || treatment == null)
            return;
        try
        {
            Medical.Bought(page, treatment);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a treatment failed", e);
        }
    }
}

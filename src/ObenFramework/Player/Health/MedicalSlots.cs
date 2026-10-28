// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using UnityEngine;
using Treatment = DoctorPanelSelection.Treatment;

namespace ObenFramework.Player;

/// <summary>Every treatment a doctor can sell, by the page it is on.</summary>
public enum MedicalTreatment
{
    PrescriptionShroomSubstitute,
    Fungicide,
    PrescriptionFungicide,
    GrowthsRemovalBasic,
    DangerousExperimentalTreatment,
    Antidepressants,
    PrescriptionAntidepressants,
    RubberDuck,
    PhysicalPainkillers,
    PhysicalPrescriptionPainkillers,
    AdhesiveBandages,
    Bandages,
    HealWounds,
    RadiationPainkillers,
    RadiationPrescriptionPainkillers,
    IodinePills,
}

/// <summary>
/// Where each <see cref="MedicalTreatment"/> lives: the doctor page class (addictions, fungal disease,
/// mental, physical, radiation) and its private field holding the game's Treatment. Field reads go through
/// cached field refs.
/// </summary>
internal static class MedicalSlots
{
    private static readonly Slot[] All =
    {
        Make<TreatmentAddictions>(MedicalTreatment.PrescriptionShroomSubstitute, "PrescriptionShroomSubtitute"),
        Make<TreatmentFungalDisease>(MedicalTreatment.Fungicide, "Fungicide"),
        Make<TreatmentFungalDisease>(MedicalTreatment.PrescriptionFungicide, "PrescriptionFungicide"),
        Make<TreatmentFungalDisease>(MedicalTreatment.GrowthsRemovalBasic, "GrowthsRemovalBasic"),
        Make<TreatmentFungalDisease>(MedicalTreatment.DangerousExperimentalTreatment, "DangerousExperimentalTreatment"),
        Make<TreatmentMentalHealth>(MedicalTreatment.Antidepressants, "Antidepressants"),
        Make<TreatmentMentalHealth>(MedicalTreatment.PrescriptionAntidepressants, "PrescriptionAntidepressants"),
        Make<TreatmentMentalHealth>(MedicalTreatment.RubberDuck, "Rubberduck"),
        Make<TreatmentPhysicalHealth>(MedicalTreatment.PhysicalPainkillers, "Painkillers"),
        Make<TreatmentPhysicalHealth>(MedicalTreatment.PhysicalPrescriptionPainkillers, "PrescriptionPainkillers"),
        Make<TreatmentPhysicalHealth>(MedicalTreatment.AdhesiveBandages, "AdhesiveBandages"),
        Make<TreatmentPhysicalHealth>(MedicalTreatment.Bandages, "Bandages"),
        Make<TreatmentPhysicalHealth>(MedicalTreatment.HealWounds, "HealWounds"),
        Make<TreatmentRadiation>(MedicalTreatment.RadiationPainkillers, "Painkillers"),
        Make<TreatmentRadiation>(MedicalTreatment.RadiationPrescriptionPainkillers, "PrescriptionPainkillers"),
        Make<TreatmentRadiation>(MedicalTreatment.IodinePills, "IodinePills"),
    };

    /// <summary>The page and the game's Treatment for a kind at the given doctors, or false.</summary>
    public static bool Find(MedicalTreatment kind, IEnumerable<Doctor> doctors, out DoctorPanelSelection page, out Treatment treatment)
    {
        var slot = All[(int)kind];
        foreach (var doctor in doctors)
        {
            if (doctor == null || doctor.treatments == null)
                continue;
            foreach (var candidate in doctor.treatments)
            {
                if (candidate == null || candidate.GetType() != slot.Page)
                    continue;
                page = candidate;
                treatment = slot.Read(candidate);
                return treatment != null;
            }
        }
        page = null;
        treatment = null;
        return false;
    }

    /// <summary>Which kind a page's Treatment is, by reference.</summary>
    public static bool KindOf(DoctorPanelSelection page, Treatment treatment, out MedicalTreatment kind)
    {
        foreach (var slot in All)
        {
            if (slot.Page == page.GetType() && ReferenceEquals(slot.Read(page), treatment))
            {
                kind = slot.Kind;
                return true;
            }
        }
        kind = default;
        return false;
    }

    /// <summary>Every doctor in the loaded scenes. A scan, so only on request.</summary>
    public static Doctor[] Doctors() => UnityEngine.Object.FindObjectsByType<Doctor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    // A field a game update renamed leaves that one treatment unknown instead of breaking all of them.
    private static Slot Make<TPage>(MedicalTreatment kind, string field) where TPage : DoctorPanelSelection
    {
        try
        {
            var read = AccessTools.FieldRefAccess<TPage, Treatment>(field);
            return new Slot(kind, typeof(TPage), page => read((TPage)page));
        }
        catch (Exception e)
        {
            GameEvents.Log?.Warn($"Doctor treatment {typeof(TPage).Name}.{field} not found: {e.Message}");
            return new Slot(kind, typeof(TPage), _ => null);
        }
    }

    private sealed class Slot
    {
        public Slot(MedicalTreatment kind, Type page, Func<DoctorPanelSelection, Treatment> read)
        {
            Kind = kind;
            Page = page;
            Read = read;
        }

        public MedicalTreatment Kind { get; }
        public Type Page { get; }
        public Func<DoctorPanelSelection, Treatment> Read { get; }
    }
}

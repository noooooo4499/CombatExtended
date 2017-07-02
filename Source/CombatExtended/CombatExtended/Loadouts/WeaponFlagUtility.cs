using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public static class WeaponFlagUtility
    {
        public static WeaponSwitchContext GetCombatContextFor(LocalTargetInfo targ, Pawn pawn)
        {
            // Check if we're at melee range
            if (targ.HasThing && (targ.Thing.Position - pawn.Position).LengthManhattan <= 1)
            {
                return WeaponSwitchContext.MeleeRange;
            }
            // Check job for whether we're hunting or responding to hostiles
            if (pawn.CurJob != null)
            {
                if (pawn.CurJob?.def == JobDefOf.Hunt) return WeaponSwitchContext.Hunting;
                if (pawn.playerSettings != null
                    && pawn.playerSettings.UsesConfigurableHostilityResponse
                    && !pawn.Drafted
                    && !pawn.InMentalState
                    && !pawn.CurJob.playerForced
                    && pawn.HostileTo(pawn.CurJob.targetA.Thing))
                    return WeaponSwitchContext.HostileResponse;
            }

            // Check for short range
            if (targ.HasThing)
            {
                var closeRangeWeapon = pawn.GetLoadout()?.weaponFlags.closeRangeWeapon;
                if (closeRangeWeapon != null && (targ.Thing.Position - pawn.Position).LengthHorizontal <= closeRangeWeapon.Verbs.First(v => !v.MeleeRange).range)
                {
                    return WeaponSwitchContext.CloseRange;
                }
            }
            return WeaponSwitchContext.Undefined;
        }

        /// <summary>
        /// Checks if the pawn has a better weapon to switch to for the given context in his inventory and will perform the switch if necessary.
        /// </summary>
        /// <param name="pawn">Pawn performing the switch</param>
        /// <param name="context">Context in which the switch occurs</param>
        public static void TrySwitchWeaponForContext(this Pawn pawn, WeaponSwitchContext context)
        {
            Log.Message("CE :: Trying to switch with pawn=" + pawn.ToString() + ", context=" + context.ToString());
            var inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                return;
            }
            ThingWithComps newWeapon = null;
            if (context == WeaponSwitchContext.Sapping)
            {
                // Switch to base destroyer weapon if we don't already have one
                if (pawn.equipment.PrimaryEq?.PrimaryVerb.verbProps.ai_IsBuildingDestroyer ?? false) return;
                if (!inventory.RangedWeaponListForReading.NullOrEmpty())
                {
                    newWeapon = inventory.RangedWeaponListForReading.FirstOrDefault(t => t.GetComp<CompEquippable>().PrimaryVerb.verbProps.ai_IsBuildingDestroyer);
                }
            }
            else
            {
                var flags = pawn.WeaponFlags();
                var defToUse = GetWeaponForContext(flags, context);
                if (defToUse == null) defToUse = flags.primaryWeapon;   // Use primary as a fallback in case of undefined flags
                if (pawn.equipment.Primary?.def == defToUse) return;    // Check if we already have the right weapon equipped

                // Select new weapon from inventory
                var weaponList = defToUse.IsMeleeWeapon ? inventory.MeleeWeaponListForReading : inventory.RangedWeaponListForReading;
                newWeapon = weaponList.FirstOrDefault(t => t.def == defToUse);
            }
            inventory.TrySwitchToWeapon(newWeapon);
        }

        /// <summary>
        /// Returns the weapon flag data for this pawn. First checks if pawn has a non-null non-empty loadout to get loadout flags from, otherwise will return defaults.
        /// </summary>
        /// <param name="pawn"></param>
        /// <returns></returns>
        private static WeaponFlagData WeaponFlags(this Pawn pawn)
        {
            var loadout = pawn.GetLoadout();
            if (loadout != null && loadout.Slots.Count > 0)
            {
                return loadout.weaponFlags;
            }
            return GetDefaultDataFor(pawn);
        }

        /// <summary>
        /// Determines the default primary, close range and melee weapon using equipped weapon and weapons carried in inventory of the given Pawn.
        /// </summary>
        /// <param name="pawn">Pawn whose carried weapons will be used for assignment.</param>
        /// <returns></returns>
        private static WeaponFlagData GetDefaultDataFor(Pawn pawn)
        {
            var data = new WeaponFlagData();
            var inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                Log.Error("CE tried getting WeaponFlagData for " + pawn.ToString() + " but it doesn't have CompInventory");
                return data;
            }
            // If we have a primary, set it
            if (pawn.equipment.Primary != null)
            {
                data.primaryWeapon = pawn.equipment.Primary.def;
            }

            // Assign all ranged weapons
            var rangedWeapons = new List<ThingWithComps>(inventory.RangedWeaponListForReading);
            if (rangedWeapons.Count() > 0)
            {
                rangedWeapons.SortByDescending(t => t.GetComp<CompEquippable>().PrimaryVerb.verbProps.range);

                // Set longest-range weapon to primary if we don't have one already
                if (data.primaryWeapon == null)
                {
                    data.primaryWeapon = rangedWeapons.First().def;
                }
                data.closeRangeWeapon = rangedWeapons.FirstOrDefault(t => t.GetComp<CompEquippable>().PrimaryVerb.verbProps.minRange == 0)?.def;
            }
            // If our primary is not a melee weapon assign one
            if ((!data.primaryWeapon?.IsMeleeWeapon ?? false) && !inventory.MeleeWeaponListForReading.NullOrEmpty())
            {
                var meleeWeapons = new List<ThingWithComps>(inventory.MeleeWeaponListForReading);
                meleeWeapons.SortByDescending(t => t.GetStatValue(CE_StatDefOf.MeleeWeapon_Penetration) * t.GetStatValue(StatDefOf.MeleeWeapon_DamageAmount) / t.GetStatValue(StatDefOf.MeleeWeapon_Cooldown));
                Log.Message("CE :: meleeWeapons contains:");
                foreach (ThingWithComps cur in meleeWeapons)
                {
                    Log.Message(cur.ToString());
                }
                data.meleeWeapon = meleeWeapons.First().def;
                if (data.primaryWeapon == null)
                {
                    data.primaryWeapon = data.meleeWeapon;
                }
            }
            return data;
        }

        private static ThingDef GetWeaponForContext(WeaponFlagData flags, WeaponSwitchContext context)
        {
            switch (context)
            {
                case WeaponSwitchContext.CloseRange:
                    return flags.closeRangeWeapon;
                case WeaponSwitchContext.HostileResponse:
                    return flags.defenseWeapon;
                case WeaponSwitchContext.Hunting:
                    return flags.huntingWeapon;
                case WeaponSwitchContext.MeleeRange:
                    return flags.meleeWeapon;
                default:
                    return flags.primaryWeapon;
            }
        }
    }
}

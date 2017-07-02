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

        public static void TrySwitchWeaponForContext(this Pawn pawn, WeaponSwitchContext context)
        {
            var inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null)
            {
                Log.Error("CE tried switching weapon for pawn" + pawn.ToString() + " with context " + context.ToString() + " but pawn is missing CompInventory");
                return;
            }
            ThingWithComps newWeapon = null;
            if (context == WeaponSwitchContext.Sapping)
            {
                // Switch to base destroyer weapon
                if (!inventory.RangedWeaponListForReading.NullOrEmpty())
                {
                    newWeapon = inventory.RangedWeaponListForReading.FirstOrDefault(t => t.GetComp<CompEquippable>().PrimaryVerb.verbProps.ai_IsBuildingDestroyer);
                }
            }
            else
            {
                // Switch to context-appropriate weapon
                var defToUse = GetWeaponForContext(pawn.WeaponFlags(), context);
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

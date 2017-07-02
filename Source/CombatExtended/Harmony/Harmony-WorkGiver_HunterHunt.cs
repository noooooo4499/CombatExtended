using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using Harmony;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "HasHuntingWeapon")]
    public class Harmony_WorkGiver_HunterHunt_HasHuntingWeapon_Patch
    {
        public static bool Prefix(ref bool __result, Pawn p)
        {
            __result = p.equipment.Primary != null && IsValidHuntingWeapon(p.equipment.Primary);

            // Check for hunting or primary weapon we could switch to
            var loadout = p.GetLoadout();
            if (loadout != null)
            {
                var huntWeaponDef = loadout.weaponFlags.huntingWeapon == null ? loadout.weaponFlags.primaryWeapon : loadout.weaponFlags.huntingWeapon;
                if (huntWeaponDef != null)
                {
                    var inventory = p.TryGetComp<CompInventory>();
                    if (inventory != null)
                    {
                        __result = inventory.RangedWeaponListForReading.Concat(inventory.MeleeWeaponListForReading).Any(t => t.def == huntWeaponDef && IsValidHuntingWeapon(t));
                    }
                }
            }
            return false;
        }

        private static bool IsValidHuntingWeapon(ThingWithComps weapon)
        {
            if (weapon.def.IsRangedWeapon)
            {
                var ammoComp = weapon.TryGetComp<CompAmmoUser>();
                return ammoComp == null || ammoComp.CanBeFiredNow || ammoComp.HasAmmo;
            }
            return weapon.def.IsMeleeWeapon && Controller.settings.AllowMeleeHunting;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "JobOnThing")]
    public class Harmony_WorkGiver_HunterHunt_JobOnThing
    {
        public static void Prefix(Pawn pawn)
        {
            pawn.TrySwitchWeaponForContext(WeaponSwitchContext.Hunting);
        }
    }
}

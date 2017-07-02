using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using Harmony;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
    public static class Harmony_JobGiver_ReactToCloseMeleeThreat
    {
        public static void Postfix(Job __result, Pawn pawn)
        {
            if (__result != null)
            {
                pawn.TrySwitchWeaponForContext(WeaponSwitchContext.MeleeRange);
            }
        }
    }
}

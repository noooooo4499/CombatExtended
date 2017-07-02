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
    [HarmonyPatch(typeof(DigUtility), "PassBlockerJob")]
    public static class Harmony_DigUtility_PassBlockerJob
    {
        public static void Prefix(Pawn pawn)
        {
            pawn.TrySwitchWeaponForContext(WeaponSwitchContext.Sapping);
        }
    }
}

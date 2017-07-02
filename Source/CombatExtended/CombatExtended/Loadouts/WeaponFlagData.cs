using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public struct WeaponFlagData
    {
        public ThingDef primaryWeapon;
        public ThingDef closeRangeWeapon;
        public ThingDef meleeWeapon;
        public ThingDef huntingWeapon;
        public ThingDef defenseWeapon;
    }
}

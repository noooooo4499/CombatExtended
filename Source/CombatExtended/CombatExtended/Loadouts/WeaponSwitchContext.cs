using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public enum WeaponSwitchContext
    {
        Undefined,
        Hunting,
        HostileResponse,
        Sapping,
        CloseRange,
        MeleeRange
    }
}

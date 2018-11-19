using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace CombatExtended
{
    public class CompProperties_Bouncy : CompProperties
    {
        MaterialFailMode materialFailMode;
        
        public CompProperties_Bouncy()
        {
            compClass = typeof(CompBouncy);
        }

        /*public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
        }*/
    }
}

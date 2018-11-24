using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace CombatExtended
{
    public class CompProperties_Bouncy : CompProperties
    {
        public MaterialFailMode materialFailMode = MaterialFailMode.Unyielding;
        public float density = 8.05f;   // specific gravity
        public float hardness = 200f;    // strength [GPa]
        public float weight = 12f; // gram
        public float diameter = 1f; // mm

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

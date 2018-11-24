using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class BounceInfo
    {
        public MaterialFailMode materialFailMode;
        public float hardness;  //GPa
        public float density;   //kg/L
        public Vector3 normal = new Vector3();
    }
}

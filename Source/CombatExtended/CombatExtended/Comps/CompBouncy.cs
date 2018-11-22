using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    //  https://www.bevfitchett.us/ballistics/ricochet-analysis-introduction.html
    //  https://books.google.nl/books?id=Y4Nvl1kan58C&pg=PA154&lpg=PA154&dq=loss+of+velocity+with+ricochet&source=bl&ots=VpQmNeXP0N&sig=dMyriHYBSetok4ZSWhmb4x0-oYo&hl=en&sa=X&ved=2ahUKEwjE1Z_-1d7eAhVLKlAKHW57An0Q6AEwEHoECAAQAQ#v=onepage&q=loss%20of%20velocity%20with%20ricochet&f=false
    //  - After ricocheting from the surface, the missile will lose a considerable amount of its velocity (anything up to 35% in test firings) and, invariably, lose its stability.
    //  - The actual degree at which a bullet will ricochet from a surface is called the critical angle.
    //      - Predicting this critical angle for any bullet/surface configuration is, however, extremely difficult.
    //      - Factors such as bullet shape, construction, velocity and ricocheting surface all have a pronounced effect on the outcome
    //      - E.G HOLLOWPOINTS: The hollow- point nose (..) [collapses] on impact, effectively increasing the angle and allowing the bullet to ricochet from [less angled surfaces than expected].
    /*  There are, however, a few generalizations which can be applied.
     *      - HARD SURFACE: The angle of ricochet is often considerably less than the angle of incidence.
     *          - HOLLOWPOINT: 1100 ft/s: 1.3-1.9 degrees critical angle at 10-30 degrees impact (smooth concrete)
     *          - OTHER NON-FMJ: 650, 850, 1100 ft/s: 1.02-1.05, 1.3-1.7, 1.33-1.88 at 10-30 degrees impact
     *          - FMJ: 1300, 2700 ft/s: 2.0-[12-35], 3.5-[2-25] at 10-30 degrees impact. Some cause severe cratering. Some disintegrate the bullet.
     *          - Conclusion: High-velocity FMJ/hard jacketed bullets on stone/concrete (FRANGIBLE MATERIAL) possibly have LARGE ANGLE OF RICOCHET
     *          - Conclusion: Nearly all bullets on stone/concrete have SMALL ANGLE OF RICOCHET
     *          - FMJ (45 ACP) at different conditions:
     *              -               15 deg          25 deg  (incident angle)
     *              - Glass:        breaks          breaks
     *              - Concrete:     2 deg           3 deg   (ricochet angle)
     *              - Steel:        2.5 deg         4 deg   (ricochet angle)
     *              - Wood:         17 deg;         17 deg  (ricochet angle)
     *              - Sand:         penetrate       penetrate
     *          - BULLET CHANGES: Bullets on hard, unyielding surfaces deform to yield a roughly CONSTANT angle of ricochet of 1-2 deg. (M.G. Haag, L.C. Haag (2011), p. 152)
     *              - When incident angle > critical angle, angle of ricochet != constant ==> and ricochet may even reach IN = OUT (Mythbusters?).
     *      - HOLLOWPOINT: Critical angle LOWER than FMJ. In this instance, it would appear that the collapsing hollow-point bullet nose increases the incidence angle, thus increasing the propensity for ricochet.
     *      - VELOCITY DEPENDENCE:
     *          - NOT VELOCITY DEPENDENT: The CRITICAL ANGLE for a given bullet type/target medium is not velocity dependent. Specifically, 0.22, 0.38, 0.357 calibers (ROUND NOSE) all 8 degree critical angle off of water.
     *          - HIGH VELOCITY: Propensity to FRAGMENT on impact rather than ricochet. (Hueske (2015), p.260)
     *      - SIZE DEPENDENCE:
     *          - LARGE PROJECTILE: Will ricochet more often (Hueske (2015), p.260)
     *      - SURFACE DEPENDENCE:
     *          - ROUND SHOT: Ricochets readily off water.
     *      - SOUND: Ricocheting bullets causes destabilization, which causes SOUNDS
     *      - DAMAGING EFFECTS:
     *          - Wounds produced by bullets ricocheting from HARD SURFACES will generally be easy to identify due to the bullet ' s tumbling action.
     *          - If the bullet does happen to strike point first, the misshapen bullet will leave a distinctive entry hole generally with ragged edges.
     *              - Once it enters the body, the bullet will, due to its inherent unstable condition, tumble end over end, leaving a large irregular wound channel.
     *              - Jacketed bullets tend to break up on ricocheting, peppering the skin with jacket and lead core fragments.
     */

    /*
     * Reading on bullet ricochet:
     * 
     *  - Lucien Haag (1975). Bullet Ricochet: An Empirical Study and a Device for Measuring Ricochet Angle. AFTE Journal.
     *  - Michael G. Haag, Lucien C. Haag (2011). Shooting Incident Reconstruction.
     *  - Effects of shotgun pellets ricocheting from steel and concrete
     *         - Jauhari and Mohan, 1969
     *         - McConnell, Triplett and Rowe, 1981
     *         - Hartline, Abraham and Rowe, 1982
     *         - Rathman, 1987
     *  - Edward E. Hueske (2015). Practical Analysis and Reconstruction of Shooting Incidents
     */
    
    public class CompBouncy : ThingComp
    {
        public CompProperties_Bouncy Props
        {
            get
            {
                return (CompProperties_Bouncy)props;
            }
        }

        public MaterialFailMode SurfaceYields(Thing hitThing, ref float surfaceStrength)
        {
            //Exceptions
            /*
             * 1. Pawns
             *  1a. Mechanoids
             *  1b. Animals
             *  1c. Humans
             */

            //  Armadillo: http://meyersgroup.ucsd.edu/papers/journals/Meyers%20348.pdf

            //"Last resort" Find material
            if (hitThing.def.MadeFromStuff)
            {
                return SurfaceYields(hitThing.Stuff, ref surfaceStrength);
            }
            else if (hitThing.def.IsStuff)
            {
                return SurfaceYields(hitThing.def, ref surfaceStrength);
            }
            
            return MaterialFailMode.Unyielding;
        }

        //  https://people.eng.unimelb.edu.au/stsy/geomechanics_text/Ch8_Strength.pdf
        //  http://www-materials.eng.cam.ac.uk/mpsite/interactive_charts/strength-toughness/basic.html

        public MaterialFailMode SurfaceYields(ThingDef thingDef, ref float surfaceStrength)
        {
            if (thingDef.IsStuff)
            {
                var categories = thingDef.stuffProps.categories;

                if (categories.Contains(StuffCategoryDefOf.Metallic))
                {
                    surfaceStrength = 60 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                    return MaterialFailMode.Malleable;
                }
                else if (categories.Contains(StuffCategoryDefOf.Woody))
                {
                    surfaceStrength = 5 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                    return MaterialFailMode.Frangible;
                }
                else if (categories.Contains(StuffCategoryDefOf.Stony))
                {
                    surfaceStrength = 12 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.Mass
                          || x.stat == StatDefOf.MaxHitPoints
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                    return MaterialFailMode.Frangible;
                }
                else if (categories.Contains(StuffCategoryDefOf.Fabric))
                {
                    surfaceStrength = 5 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp).Select(x => x.value).Sum();
                    return MaterialFailMode.Frangible;
                }
                else if (categories.Contains(StuffCategoryDefOf.Leathery))
                {
                    surfaceStrength = thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp).Select(x => x.value).Sum();
                    return MaterialFailMode.Malleable;
                }
            }
            return MaterialFailMode.Unyielding;
        }

        public MaterialFailMode SurfaceYields(RoofDef roofDef, ref float surfaceStrength)
        {
            if (roofDef.isThickRoof)        // Sufficiently thick slabs of material are not penetrable/ricochettable
                                            // M. Jauhari (1969). Bullet Ricochet from Metal Plates. The Journal of Criminal Law, Criminology and Police Science, 60(3), pp.387-394.
            {
                SurfaceYields(ThingDefOf.BlocksGranite, ref surfaceStrength);
                return MaterialFailMode.Unyielding;
            }

            if (roofDef.isNatural)
            {
                SurfaceYields(ThingDefOf.BlocksGranite, ref surfaceStrength);
                return MaterialFailMode.Frangible;  //Assume frangible - rock-like chipping off and thus lower ricochet angle than impact angle
            }

            return MaterialFailMode.Malleable;      //Non-natural
        }

        /*
         * Ignored surfaces:
         * 
         * Underwall
         * 
         */
        public MaterialFailMode SurfaceYields(TerrainDef terrainDef, ref float surfaceStrength)
        {
            if (terrainDef.HasTag("Water")
             || terrainDef.takeSplashes
             || terrainDef.affordances.Contains(TerrainAffordanceDefOf.MovingFluid))
            {
                return MaterialFailMode.Liquid;
            }

            if (terrainDef.affordances.Contains(TerrainAffordanceDefOf.Diggable) // PackedDirt, Soil, MossyTerrain, MarshyTerrain, SoilRich, Gravel, Mud, Sand, SoftSand, Ice
             || terrainDef.generatedFilth == ThingDefOf.Filth_Dirt
             || terrainDef.takeFootprints)
            {
                //  Strengths for soils: https://www.jsg.utexas.edu/tyzhu/files/Some-Useful-Numbers.pdf
                if (terrainDef.driesTo != null)             // Wet terrain
                {
                    surfaceStrength = 0.1f;
                }
                else if (terrainDef.scatterType == "SoftGray")   // Ice
                {
                    surfaceStrength = 8.7f;     //  Young's modulus from http://people.ee.ethz.ch/~luethim/pdf/script/pdg/appendixB.pdf
                }
                else if (terrainDef.fertility > 1.2f)   // High-fertility soils contain more clay
                {
                    surfaceStrength = 0.01f;
                }
                else if (terrainDef.generatedFilth == DefDatabase<ThingDef>.GetNamed("Filth_Sand"))    // Sand, SoftSand
                {
                    //  Sand/water impacts:     https://www.sciencedirect.com/science/article/pii/S2214914715000860#f0010

                    surfaceStrength = 0.05f;
                }
                else
                {
                    surfaceStrength = 0.2f;
                }

                return MaterialFailMode.Frangible;
            }

                //Soil types already filtered out
            if (terrainDef.HasTag("CE_Concrete")
             || terrainDef.scatterType == "Rocky")    // Concrete, PavedTile, BrokenAsphalt
            {
                return Rand.Chance(0.8f) ? MaterialFailMode.Frangible : MaterialFailMode.Malleable;
                // Concrete fails according to random chance as either frangible or malleable
            }

            /*
             * Things with costlists:
             * 
             * Bridge           -   WoodLog
             * Flagstone_       -   Blocks_
             * WoodPlankFloor   -   WoodLog
             * MetalTile        -   Steel
             * SilverTile       -   Silver
             * GoldTile         -   Gold
             * SterileTile      -   Steel       !!!!    (and Silver!)
             * Carpet_          -   Cloth
             * 
             */
            //Find material
            if (!terrainDef.costList.NullOrEmpty())
            {
                var assumedStuff = terrainDef.costList.Select(x => x.thingDef).Where(x => x.IsStuff).FirstOrDefault();
                if (assumedStuff != null)
                {
                    return SurfaceYields(assumedStuff, ref surfaceStrength);
                }
            }

            if (terrainDef.driesTo != null)
            {
                return SurfaceYields(terrainDef.driesTo, ref surfaceStrength);
            }

            /*
             * BurnedWoodPlankFloor
             * BurnedCarpet
             */
            var attemptedBurnedDef = DefDatabase<TerrainDef>.AllDefs.FirstOrDefault(x => x.burnedDef == terrainDef);
            if (attemptedBurnedDef != null)
            {
                return SurfaceYields(attemptedBurnedDef, ref surfaceStrength);
            }

            return MaterialFailMode.Unyielding;
        }

        /// <summary>
        /// See whether a projectile at pos can bounce off of a hit thing
        /// </summary>
        /// <param name="hitThing"></param>
        /// <param name="pos">The position of impact</param>
        /// <param name="map"></param>
        /// <returns></returns>
        public virtual bool Bounce(Thing hitThing, Vector3 pos, Map map)
        {
            var posIV = pos.ToIntVec3();

            #region Early opt-outs

            if (map == null)
            {
                Log.Warning("CombatExtended :: CompBouncy.Bounce Tried to bounce in a null map.");
                return false;
            }
            if (!posIV.InBounds(map))
            {
                Log.Warning("CombatExtended :: CompBouncy.Bounce Tried to bounce out of bounds");
                return false;
            }
           
            #endregion

            ProjectileCE projCE = parent as ProjectileCE;

            float ricochetSpeed = projCE.shotSpeed;
            float ricochetAngleRadians = projCE.shotAngle;
            float ricochetRotation = projCE.shotRotation;

            float ricochetSurfaceAngle = -1;

            #region Obtaining surface properties

            var failMode = MaterialFailMode.Unyielding;
            float surfaceStrength = 0f;
            Vector3 surfaceNormal = Vector3.zero;

            /*Consider nulls when:
                ProjectileCE.TryCollideWithRoof(success),       =>  ExactPosition is EXACTLY the raycast intersect with the roof
                ProjectileCE.ImpactSomething(last resort)       =>  ExactPosition is the EXACT Destination with Height == 0f
              Ignored:
                ProjectileCE_Explosive.Explode(),               =>  turns into ProjectileCE.Impact(null)
                BulletCE.Impact(hitThing=null),                 =>  turns into ProjectileCE.Impact(null)
            */
            if (hitThing == null)
            {
                if (pos.y < 0.001)                  //Simplest case: projectile hits terrain
                {
                    surfaceNormal = Vector3.up;     //(0, 1f, 0)
                    failMode = SurfaceYields(posIV.GetTerrain(map), ref surfaceStrength);
                }
                else                                //Projectile hits the roof
                {
                    surfaceNormal = Vector3.down;   //(0,-1f, 0)
                    failMode = SurfaceYields(posIV.GetRoof(map), ref surfaceStrength);
                }
            }
            else
            {
                var height = new CollisionVertical(hitThing);
                var sphericalNormal = (pos - hitThing.DrawPos);

                if (hitThing is Building)
                {
                    if (pos.y >= height.Max - 0.001f)            //Impacted top of building
                    {
                        surfaceNormal = Vector3.up;     //(0, 1f, 0)
                    }
                    else                                         //Distinguish between left, top, right or bottom hit of the building
                    {
                        var rotatedNormal = sphericalNormal.RotatedBy(45);

                        if (rotatedNormal.x > 0)
                        {
                            if (rotatedNormal.z < 0)
                            {
                                surfaceNormal = Vector3.back;       //(  0, 0,-1f)
                            }
                            else
                            {
                                surfaceNormal = Vector3.right;      //( 1f, 0, 0)
                            }
                        }
                        else
                        {
                            if (rotatedNormal.z < 0)
                            {
                                surfaceNormal = Vector3.left;       //(-1f, 0, 0)
                            }
                            else
                            {
                                surfaceNormal = Vector3.forward;    //(  0, 0, 1f)
                            }
                        }
                    }

                    failMode = SurfaceYields(hitThing, ref surfaceStrength);

                    //Consider trees, in that case use the vector going from hitThing.DrawPos to projCE.ExactPosition
                }
            }
            #endregion

            //  TODO : Use equations of motion or smaller delta time interval to get more accurate direction
            Vector3 incidentDirection = projCE.ShotLine.direction;
            float incidentSurfaceAngle = 90 - Vector3.Angle(incidentDirection, surfaceNormal);

            //  Error logging
            if (incidentSurfaceAngle < 0)
                Log.Error("CombatExtended :: incidentSurfaceAngle is below 0 for CompBouncy impacting "+hitThing.ToString());

            if (failMode == MaterialFailMode.Malleable)         // metals and non-thick, non-natural roofs
            {
                float r = 0f;               //[inch]
                float h = 0f;               //[inch]
                float mg = 0f;              //[lb]

                float psiSurfaceStrength = surfaceStrength * 145038f;   // 145038 psi = 1 GPa

                //Werner Goldsmith (1999). Non-ideal projectile impact on targets . International Journal of Impact Engineering, 22, pp.362-365
                float v50n = 11.9f * Mathf.Pow(psiSurfaceStrength, 0.333f) * Mathf.Pow(r * h, 0.75f) / Mathf.Sqrt(mg);   //   m/s
                float sinAngle = Mathf.Sin(incidentSurfaceAngle * Mathf.Deg2Rad);
                ricochetSpeed = projCE.shotSpeed * (1 - (Mathf.Pow(sinAngle, 2) / 2 + (1 - Mathf.Pow(sinAngle, 2) / 2) * Mathf.Sqrt(sinAngle * 4 * projCE.shotSpeed / v50n)));
            }
            else if (failMode == MaterialFailMode.Frangible)    // nearly everything else
            {

            }
            else if (failMode == MaterialFailMode.Unyielding)   // thick roofs
            {

            }
            else if (failMode == MaterialFailMode.Liquid)       // water
            {
                //  Very simple formulae for critical angle:    http://www2.eng.cam.ac.uk/~hemh1/dambusters/ricochet_hutchings1.pdf
            }

            //  Impact angle ALPHA must be at or below critical angle ALPHA_crit (Hueske (2015), p.260) for ricochetting
            float RhoP = 0f;            // [kg/m3]
            float Yprojectile = 0f;
            float RhoT = 0f;
            float Rtarget = 0f;
            float v = 4 * projCE.shotSpeed;

            float u = (RhoP * v - Mathf.Sqrt(RhoP*RhoP * v*v - (RhoP - RhoT) * (RhoP * v*v + 2 * (Yprojectile - Rtarget))))/(RhoP - RhoT);
            float criticalAngle = Mathf.Atan(Mathf.Sqrt(RhoP * v*v / Rtarget * (v + u)/(v - u)));

            if (incidentSurfaceAngle <= criticalAngle * 0.9f)
            {
                //  Frangibility effect on out angle (Hueske (2015), p.266-267)
                //  Harder surface => smaller ricochet angle (Hueske (2015), p.260)     ==> Ricochet angle is "Lerped" (or acos(x)/1-exp(-b x)) from ~2 (hard) upwards based on delta hardness!
                if (failMode == MaterialFailMode.Unyielding)
                {
                    ricochetSurfaceAngle = 2;   //1 to 2 degrees (lit.)
                }
                else if (failMode == MaterialFailMode.Frangible)            // Expect OUT angle << IN angle (woods)     =>  |_,.-'"
                {
                    //Todo: more in-depth?
                    ricochetSurfaceAngle = Mathf.Min(90f, 0.5f * incidentSurfaceAngle);
                }
                else if (failMode == MaterialFailMode.Malleable)            // Expect OUT angle > IN angle (metals)      =>  "'-.,_-"
                {
                    //Todo: more in-depth?
                    ricochetSurfaceAngle = Mathf.Min(90f, 2f * incidentSurfaceAngle);
                }
            }
            else if (incidentSurfaceAngle <= criticalAngle * 1.1f)
            {
                //ADD EXCEPTIONS for other failmodes

                ricochetSurfaceAngle = Rand.Range(2, Mathf.Min(90f, incidentSurfaceAngle * 2f));
            }
            else
            {
                if (failMode == MaterialFailMode.Unyielding)        //Nonyielding surface: fragment (and penetrate?)
                {
                    //Spawn in fragments to replace the bullet
                    //projCE.Destroy();
                }
                return false;                                       //Yielding surface: penetrate.
            }

            Vector3 ricochetPlaneNormal = Vector3.Cross(incidentDirection, surfaceNormal);

            Vector3 reflectedDirection = Quaternion.AngleAxis(180 - (incidentSurfaceAngle + ricochetSurfaceAngle), ricochetPlaneNormal) * incidentDirection;

            ricochetRotation = -90 + Mathf.Rad2Deg * Mathf.Atan2(reflectedDirection.z, reflectedDirection.x);
            
            ricochetAngleRadians =  Vector3.Scale(reflectedDirection, new Vector3(1f, 0f, 1f)).MagnitudeHorizontal();

            //Perfectly elastic collision Vector3 reflectedDirection = Vector3.Reflect(incidentDirection, surfaceNormal);
            
            projCE.Launch(
                new Vector2(pos.x, pos.z),
                ricochetAngleRadians,
                ricochetRotation,
                pos.y,
                ricochetSpeed);

            return true;
        }
    }
}

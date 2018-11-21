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

        public MaterialFailMode SurfaceYields(Thing hitThing)
        {
            //Exceptions
            /*
             * 1. Pawns
             *  1a. Mechanoids
             *  1b. Animals
             *  1c. Humans
             */

            //"Last resort" Find material
            if (hitThing.def.MadeFromStuff)
            {
                return SurfaceYields(hitThing.Stuff);
            }
            else if (hitThing.def.IsStuff)
            {
                return SurfaceYields(hitThing.def);
            }
            
            return MaterialFailMode.Unyielding;
        }

        public MaterialFailMode SurfaceYields(ThingDef thingDef)
        {
            if (thingDef.IsStuff)
            {
                var categories = thingDef.stuffProps.categories;
                float factor;

                if (categories.Contains(StuffCategoryDefOf.Metallic))
                {
                    factor = 60 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                }
                else if (categories.Contains(StuffCategoryDefOf.Woody))
                {
                    factor = 5 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                }
                else if (categories.Contains(StuffCategoryDefOf.Stony))
                {
                    factor = 12 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.Mass
                          || x.stat == StatDefOf.MaxHitPoints
                          || x.stat == StatDefOf.BluntDamageMultiplier
                          || x.stat == StatDefOf.SharpDamageMultiplier).Select(x => x.value).Sum();
                }
                else if (categories.Contains(StuffCategoryDefOf.Fabric))
                {
                    factor = 5 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp).Select(x => x.value).Sum();
                }
                else if (categories.Contains(StuffCategoryDefOf.Leathery))
                {
                    factor = 2 * thingDef.statBases.Where(
                        x => x.stat == StatDefOf.StuffPower_Armor_Blunt
                          || x.stat == StatDefOf.StuffPower_Armor_Sharp).Select(x => x.value).Sum();
                }
            }
            return MaterialFailMode.Unyielding;
        }

        public MaterialFailMode SurfaceYields(RoofDef roofDef)
        {
            if (roofDef.isThickRoof)        // Sufficiently thick slabs of material are not penetrable/ricochettable
                                            // M. Jauhari (1969). Bullet Ricochet from Metal Plates. The Journal of Criminal Law, Criminology and Police Science, 60(3), pp.387-394.
            {
                return MaterialFailMode.Unyielding;
            }

            if (roofDef.isNatural)
            {
                return MaterialFailMode.Frangible;  //Assume frangible - rock-like chipping off and thus lower ricochet angle than impact angle
            }
            return MaterialFailMode.Malleable;      //Non-natural
        }

        /*
         * Ignored surfaces:
         * 
         * Underwall
         * BurnedWoodPlankFloor
         * BurnedCarpet
         * 
         */
        public MaterialFailMode SurfaceYields(TerrainDef terrainDef)
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
                if (terrainDef.scatterType == "SoftGray")   // Ice
                {

                }

                //if (hitTerrainDef.generatedFilth == ThingDefOf.Filth_Sand)    // Sand, SoftSand
               // {
//
               // }

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
                    return SurfaceYields(assumedStuff);
                }
            }

            if (terrainDef.driesTo != null)
            {
                return SurfaceYields(terrainDef.driesTo);
            }

            var attemptedBurnedDef = DefDatabase<TerrainDef>.AllDefs.FirstOrDefault(x => x.burnedDef == terrainDef);
            if (attemptedBurnedDef != null)
            {
                return SurfaceYields(attemptedBurnedDef);
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

            var projCE = parent as ProjectileCE;

            var ricochetSpeed = projCE.shotSpeed;
            var ricochetAngleRadians = projCE.shotAngle;
            var ricochetRotation = projCE.shotRotation;

            var failMode = MaterialFailMode.Unyielding;

            float ricochetSurfaceAngle = -1;
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
                //Simplest case: projectile hits terrain
                if (pos.y < 0.001)
                {
                    surfaceNormal = Vector3.up;     //(0, 1f, 0)
                    failMode = SurfaceYields(posIV.GetTerrain(map));
                }
                //Projectile hits the roof
                else
                {
                    surfaceNormal = Vector3.down;   //(0,-1f, 0)
                    failMode = SurfaceYields(posIV.GetRoof(map));
                }
            }
            else if (hitThing is Building)
            {
                var height = new CollisionVertical(hitThing);

                if (pos.y >= height.Max - 0.001f)            //Impacted top of building
                {
                    surfaceNormal = Vector3.up;     //(0, 1f, 0)
                }
                else                                         //Distinguish between left, top, right or bottom hit of the building
                {
                    var sphericalNormal = (pos - hitThing.DrawPos);
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

                failMode = SurfaceYields(hitThing);

                //Consider trees, in that case use the vector going from hitThing.DrawPos to projCE.ExactPosition
            }
            
            // TODO : Use equations of motion or smaller delta time interval to get more accurate direction
            Vector3 incidentDirection = projCE.ShotLine.direction;

            float incidentSurfaceAngle = 90 - Vector3.Angle(incidentDirection, surfaceNormal);

            if (incidentSurfaceAngle < 0)
            {
                Log.Error("CombatExtended :: incidentSurfaceAngle is below 0 for CompBouncy impacting "+hitThing.ToString());
            }

            //  Impact angle ALPHA must be at or below critical angle ALPHA_crit (Hueske (2015), p.260) for ricochetting
            float criticalAngle = 90f;

            if (incidentSurfaceAngle <= criticalAngle)
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
            else if (incidentSurfaceAngle + 10 <= criticalAngle)
            {
                //ADD EXCEPTIONS for other failmodes

                ricochetSurfaceAngle = Rand.Range(2, Mathf.Min(90f, incidentSurfaceAngle * 2f));
            }
            else
            {
                if (failMode == MaterialFailMode.Unyielding)        //Nonyielding surface: fragment (and penetrate?)
                {
                    //Spawn in fragments to replace the bullet
                    projCE.Destroy();
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

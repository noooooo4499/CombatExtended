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
     *              - When incident angle > critical angle, angle of ricochet != constant ==> AND RICOCHET IS AS BY IN = OUT.
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
            
            var ricochetSpeed = projCE.shotSpeed;
            var ricochetAngle = projCE.shotAngle;
            var ricochetRotation = projCE.shotRotation;

            /*Consider nulls when:
                ProjectileCE.TryCollideWithRoof(success),       =>  ExactPosition is EXACTLY the raycast intersect with the roof
                ProjectileCE.ImpactSomething(last resort)       =>  
              Ignored:
                ProjectileCE_Explosive.Explode(),               =>  turns into ProjectileCE.Impact(null)
                BulletCE.Impact(hitThing=null),                 =>  turns into ProjectileCE.Impact(null)
            */
            if (hitThing == null)
            {

            }

            var projCE = parent as ProjectileCE;
            
            float ricochetAngle;

            //  Impact angle ALPHA must be at or below critical angle ALPHA_crit (Hueske (2015), p.260) for ricochetting
            float criticalAngle;
            
            //
            if (impactAngle <= criticalAngle)
            {
                bool frangible;

                //  Frangibility effect on out angle (Hueske (2015), p.266-267)
                if (frangible)
                {
                    // Expect OUT angle << IN angle (woods)     =>  |_,.-'"     
                }
                else
                {
                    // Expect OUT angle > IN angle (metals)      =>  "'-.,_-"
                }

                //  Harder surface => smaller ricochet angle (Hueske (2015), p.260)
                ricochetAngle = ;
            }
            else        //Nonyielding surface: fragment. Yielding surface: penetrate.
            {

            }

            Vector3 incidentDirection = new Vector3();

            Vector3 surfaceNormal = new Vector3();

            //Perfectly elastic collision
            Vector3 reflectedDirection = Vector3.Reflect(incidentDirection, surfaceNormal);
            
            projCE.Launch(
                new Vector2(pos.x, pos.z),
                ricochetAngle,
                ricochetRotation,
                pos.y,
                ricochetSpeed);

            return true;
        }
    }
}

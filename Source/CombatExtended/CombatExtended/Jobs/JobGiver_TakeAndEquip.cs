using RimWorld;
using System;
using System.Linq;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;

namespace CombatExtended
{
	/// <summary>
	/// Purpose: Provide a self-improving "loadout";<br></br>
	/// 1) - - Weapons - -<br></br>
	/// 1.1) Find weapons (when holding none)<br></br>
	/// 1.2) Find better weapons taking into account pawn capacities<br></br>
	/// 1.3) Equip the best weapon (when holding many)<br></br>
	/// 2) - - Ammo - -<br></br>
	/// 2.1) Find ammo for held weapon<br></br>
	/// 2.2) Find ammo for inventory weapons<br></br>
	/// 2.3) Find better ammo taking into account pawn capacities/damage<br></br>
	/// 3) - - Unload - -<br></br>
	/// 3.1) Drop weapons (when encumbered)<br></br>
	/// 3.1.1) Drop empty weapons with no chance to resupply (when encumbered)<br></br>
	/// 3.1.2) Drop empty weapons with chance to resupply (when severely encumbered)<br></br>
	/// 3.1.3) Drop inferior (loaded) weapons with no chance to resupply (when severely encumbered)<br></br>
	/// 3.1.4) Drop inferior (loaded) weapons with chance to resupply (when severely encumbered)<br></br>
	/// 3.1.5) Drop loaded weapons with chance to resupply (when severely encumbered)<br></br>
	/// 3.2) Drop ammo (when encumbered)<br></br>
	/// 3.2.1) Drop ammo that doesn't fit any inventory weapons (when encumbered)<br></br>
	/// 3.2.2) Drop ammo that doesn't fit in the primary weapon (when encumbered)<br></br>
	/// 3.2.2) Drop ammo that fits inventory weapons (when encumbered)<br></br>
	/// </summary>
    public class JobGiver_TakeAndEquip : ThinkNode_JobGiver
    {
    	private const float ammoFractionOfNonAmmoInventory = 0.666f;
    	private const float maxSearchDistance = 25f;
    	private const float safetyMultiplier = 0.25f;
    	private const float minSearchDistance = 2f;
    	private const float safetyDistance = 30f;
    	
    	private const float bulkMargin = 4f;
    	private const float weightMargin = 3f;
    	
        private enum WorkPriority
        {
            None,
            Unloading,
            LowAmmo,
            Weapon,
            Ammo
        }

        private enum WorkUrgency
        {
    		Fleeing,
    			None,
        	Opportunistic,
        	ThreatExists,
        	ThreatNearby,
    		Aggro,
        	Combat
        }
        
        private IntRange MagazineSize(Pawn pawn)
        {
        	var primary = pawn.equipment.Primary;
        	var primaryAmmoUser = primary.TryGetComp<CompAmmoUser>();
        	
        	if (primaryAmmoUser != null)
        	{
	            bool hasWeaponTags = pawn.kindDef.weaponTags?.Any() ?? false;
	            
            	LoadoutPropertiesExtension loadoutPropertiesExtension = pawn.kindDef.modExtensions?
            		.FirstOrDefault(x => x is LoadoutPropertiesExtension) as LoadoutPropertiesExtension;
	            
            	float a = 1;
            	float b = 2;
            	
	        	if (hasWeaponTags
	              && primary.def.weaponTags.Any(pawn.kindDef.weaponTags.Contains)
	        	  && loadoutPropertiesExtension?.primaryMagazineCount != FloatRange.Zero)
	        	{
            		a = Mathf.Max(loadoutPropertiesExtension.primaryMagazineCount.min, 1f);	// So lowAmmo is always triggered below a full clip's worth
	            	b = loadoutPropertiesExtension.primaryMagazineCount.max;
	        	}
	            
            	a *= (float)primaryAmmoUser.Props.magazineSize;
	            b *= (float)primaryAmmoUser.Props.magazineSize;
	            
	            return new IntRange((int)a, (int)b);
        	}
        	
        	return IntRange.zero;
        }
        
        private WorkUrgency GetWorkUrgency(Pawn pawn)
        {
        	return WorkUrgency.None;
        }
        
        private WorkPriority GetPriorityWork(Pawn pawn)
        {
            #region Traders have no work priority, but you'd want them to choose the best weapon they're wearing
            if (pawn.kindDef.trader)
            {
                return WorkPriority.None;
            }
            #endregion
            
            #region Colonists with a loadout have no work priority
            if (pawn.Faction.IsPlayer)
            {
                Loadout loadout = pawn.GetLoadout();
                
                if (loadout != null && !loadout.KeepExcess)	// In this case, dropping is performed by UpdateLoadout
                {
                    return WorkPriority.None;
                }
            }
            #endregion
           	
			bool hasPrimary = (pawn.equipment != null && pawn.equipment.Primary != null);
			
            // Pawns without weapon..
            if (!hasPrimary)
            {
            	// With inventory && non-colonist && not stealing && little space left
                if (Unload(pawn))
                {
                    return WorkPriority.Unloading;
                }
                // Without inventory || colonist || stealing || lots of space left
                return WorkPriority.Weapon;
            }
			
            CompAmmoUser primaryAmmoUser = hasPrimary ? pawn.equipment.Primary.TryGetComp<CompAmmoUser>() : null;
            
            // Pawn with ammo-using weapon..
            if (primaryAmmoUser != null && primaryAmmoUser.UseAmmo)	// Inherently checks !Controller.settings.EnableAmmoSystem
            {
            	CompInventory compInventory = pawn.TryGetComp<CompInventory>();
            	
            	// Number of things in inventory that could be put in the weapon
                int viableAmmoCarried = primaryAmmoUser.CurMagCount;	// Include currently loaded amount of ammo
                float viableAmmoBulk = 0;
                foreach (AmmoLink link in primaryAmmoUser.Props.ammoSet.ammoTypes)
                {
                	var count = compInventory.AmmoCountOfDef(link.ammo);
                	viableAmmoCarried += count;
                	viableAmmoBulk += count * link.ammo.GetStatValueAbstract(CE_StatDefOf.Bulk);
                }
                
                // ~2/3rds of the inventory bulk minus non-usable and non-ammo bulk could be filled with ammo
                float potentialAmmoBulk = ammoFractionOfNonAmmoInventory * (compInventory.capacityBulk - compInventory.currentBulk + viableAmmoBulk);
                
                // There's less ammo [bulk] than fits the potential ammo bulk [bulk]
                if (viableAmmoBulk < potentialAmmoBulk)
                {
                	IntRange magazineSize = MagazineSize(pawn);
                	
	                // There's less ammo [nr] than the minimum allowed for the pawnKindDef when it spawns [nr]
	                if (viableAmmoCarried < magazineSize.min)
	                {
	                	return Unload(pawn) ? WorkPriority.Unloading : WorkPriority.LowAmmo;
	                }
	                
	                // There's less ammo [nr] than the maximum allowed for the pawnKindDef when it spawns [nr] && no enemies are close
	                if (viableAmmoCarried < magazineSize.max
	                 && !PawnUtility.EnemiesAreNearby(pawn, 30, true))
	                {
	                	return Unload(pawn) ? WorkPriority.Unloading : WorkPriority.Ammo;
	                }
                }
            }
			
            return WorkPriority.None;
        }
		
        public override float GetPriority(Pawn pawn)
        {
            //if ((!Controller.settings.AutoTakeAmmo && pawn.IsColonist) || !Controller.settings.EnableAmmoSystem) return 0f;
            
            TimeAssignmentDef assignment = (pawn.timetable != null) ? pawn.timetable.CurrentAssignment : TimeAssignmentDefOf.Anything;
            if (assignment == TimeAssignmentDefOf.Sleep) return 0f;
			
            var workPriority = GetPriorityWork(pawn);

            if (workPriority == WorkPriority.Unloading) return 9.2f;
            else if (workPriority == WorkPriority.LowAmmo) return 9f;	// TODO : Base these off GetWorkUrgency
            else if (workPriority == WorkPriority.Weapon) return 6f;
            else if (workPriority == WorkPriority.Ammo) return 6f;
            else return 0f;
        }
        
        /// <summary>
        /// Parsed for every pawn about once per second.
        /// </summary>
        protected override Job TryGiveJob(Pawn pawn)
        {
        	// Additionally, ThinkNode_ConditionalCanDoConstantThinkTreeJobNow.Satisfied(pawn) means:
        	//		!pawn.Downed && !pawn.IsBurning() && !pawn.InMentalState && !pawn.Drafted && pawn.Awake();
        	
        	#region Non-human || Violent disabled
            if (!pawn.RaceProps.Humanlike
        	  || (pawn.story != null && pawn.story.WorkTagIsDisabled(WorkTags.Violent)))
            {
                return null;
            }
            #endregion
			
            if (!Rand.MTBEventOccurs(60, 1, 30))
            {
                return null;
            }
            
            /*
             * CHECKS:
             * 	1) weapon (preferably close)
             * 	1.1) check inventory
             * 1.1.1) if ammo enabled:
             * 	1.1.1.1) .. and its loaded ammo
             * 	1.1.1.2) .. and inventory ammo
             * 	1.1.1.3) .. and nearby ammo
             *  1.2) consider brawler
             * 	1.3) check nearby
             * 1.3.1) if ammo enabled:
             * 	1.3.1.1) .. and its loaded ammo
             * 	1.3.1.2) .. and inventory ammo
             * 	1.3.1.3) .. and nearby ammo
             *  1.4) consider melee nearby
             * 	2) ammo (preferably close)
             * 	2.1) very far ammo (same as loadouts)
             * 	3) overencumbrance
             * 	3.1) drop weapons
             * 	3.1.1) brawler, drop ranged
             * 	3.1.2) non-brawler, drop melee
             * 	3.1.3) brawler, drop extra melee
             * 	3.1.4) non-brawler, drop extra ranged
             * 	3.2) drop ammo
             * 	3.2.1) .. not usable for any weapon
             * 	3.2.2) .. not usable for primary
             *  3.2.3) .. usable for primary but extra
             * 3.3) drop other stuff?
             * 3.3.1) .. if not contained in PawnKindDef
             * 	4) correct weapon (preferably useful)
             * 	4.1) brawler, ranged weapon, poor shooter, check inventory
             * 	4.2) non-brawler, melee weapon, check inventory
             * 5) enough ammo
             * 6) best weapon (preferably strong)
             * 7) best ammo
             */
            
            /*
             * ISSUES:
             * 1) LowAmmo pawns lag the game a lot
             * 2) Started 10 jobs in one tick
             * 3) Wood logs and beer are considered weapons
             * 4) Some things need to be restricted to colonists/raiders only
             * 
             * --> Implement system of "Urgency" for getting weapons etc.
             */
            
            CompInventory inventory = pawn.TryGetComp<CompInventory>();
            if (inventory == null || !inventory.AllowTakeAndEquip)
            {
            	return null;
            }
            
            #region Pawns with non-idle jobs have "busy" work priority
            bool hasCurJob = pawn.CurJob != null;
            JobDef jobDef = hasCurJob ? pawn.CurJob.def : null;
            
            if (hasCurJob && !jobDef.isIdle)
            {
            	inventory.ExtendTakeAndEquip(pawn.CurJob.expiryInterval);
                return null;
            }
            #endregion
            
            WorkPriority workPriority = GetPriorityWork(pawn);
            
            if (workPriority == WorkPriority.None)
            {
            	inventory.ExtendTakeAndEquip();
            }
            else
            {
            	MoteMaker.ThrowText(pawn.Position.ToVector3Shifted(), pawn.Map, "TAE:"+workPriority.ToString());
            }
            
            bool brawler = pawn.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false;
            
        	int ranged = inventory.rangedWeaponList.Count;
        	int melee = inventory.meleeWeaponList.Count;
            
            #region Check 1) weapon (preferably close)
            if (workPriority == WorkPriority.Weapon)	// Means !hasPrimary & no need to unload
            {
            	bool noWeaponsInInventory = (ranged + melee == 0);
            	
            	// Check 1.1) check inventory
            	// Check 1.1.1.1) .. and its loaded ammo
            	// Check 1.1.1.2) .. and inventory ammo
            	if (!noWeaponsInInventory && inventory.SwitchToNextViableWeapon(brawler))
            	{
                	inventory.ExtendTakeAndEquip(-10000);	// Succesful at finding weapon, see if there's more
            		return null;
            	}
            	
                int x;	//used for outs
                
        		/*if (!pawn.Faction.IsPlayer)	TODO : Implement this */
        		
        		float searchDistance = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) * maxSearchDistance;
        		float searchSafeDistance = Mathf.Max(safetyMultiplier * searchDistance, minSearchDistance);
        		
                	//Ammo validator
        		Func<Thing,bool> validatorA = 
        			(Thing t) => !t.IsForbidden(pawn)
        			    && pawn.CanReserve(t, 1)
                        && pawn.Position.InHorDistOf(t.Position, searchDistance)
                		&& (!DangerInPosRadius(pawn, t.Position, pawn.Map, safetyDistance).Any() || pawn.Position.InHorDistOf(t.Position, searchSafeDistance))
                        && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly, true)
                        && (pawn.Faction.HostileTo(Faction.OfPlayer) || pawn.Faction == Faction.OfPlayer || !pawn.Map.areaManager.Home[t.Position]);
                
        		IEnumerable<Thing> allAmmo = Controller.settings.EnableAmmoSystem	// If nothing uses ammo, it's not instantiated
        			? pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Shell).Where(validatorA)
        			: null;
        		
        		// Check 1.1.1) if ammo enabled:
        		if (ranged > 0 && Controller.settings.EnableAmmoSystem)
        		{
	        		foreach (ThingWithComps inventoryRanged in inventory.rangedWeaponList)
	        		{
	                	var ammoUser = inventoryRanged.TryGetComp<CompAmmoUser>();
	                	
	                	if (ammoUser.UseAmmo)
	                	{
			        		var ammoDefs = ammoUser.Props.ammoSet.ammoTypes.Select(y => y.ammo as ThingDef);
			        		
			            	foreach (ThingDef ammoDef in ammoDefs)
			            	{
			            			// TODO : Base this on urgency of getting weapons
        						// Check 1.1.1.3) .. and nearby ammo
			            		if (allAmmo.Any<Thing>(
			            			y => y.def == ammoDef
			            			  //&& y.stackCount >= ammoUser.Props.magazineSize
			            			 )
			            		  && inventory.CanFitInInventory(inventoryRanged, out x))
			            		{
			            			inventory.ExtendTakeAndEquip(-10000);	// Succesful at finding weapon, see if there's more
			            			inventory.TrySwitchToWeapon(inventoryRanged);
			            			return null;
			            		}
			            	}
	                	}
	        		}
        		}
        		
        			//Weapon validator
                Predicate<Thing> validatorWS =
                	(Thing w) => !w.IsForbidden(pawn)
                	    && pawn.CanReserve(w, 1)
                        && pawn.Position.InHorDistOf(w.Position, searchDistance)
                		&& (!DangerInPosRadius(pawn, w.Position, pawn.Map, safetyDistance).Any() || pawn.Position.InHorDistOf(w.Position, searchSafeDistance))
                        && pawn.CanReach(w, PathEndMode.Touch, Danger.Deadly, true)
                        && (pawn.Faction.HostileTo(Faction.OfPlayer) || pawn.Faction == Faction.OfPlayer || !pawn.Map.areaManager.Home[w.Position]);
                
                	// TODO : Base ordering on urgency
        		var allWeapons = (
                    from w in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                    where validatorWS(w)
                    orderby w.MarketValue - w.Position.DistanceToSquared(pawn.Position) * 2f descending
                    select w
                    );
                
                // Check 1.2) consider brawler
	            if (brawler)
	            {
	            	var nearbyMelee = allWeapons.FirstOrDefault(w => w.def.IsMeleeWeapon);
	            	
	            	if (nearbyMelee != null)
	            	{
                        return new Job(JobDefOf.Equip, nearbyMelee)
                        {
                            checkOverrideOnExpire = true,
                            expiryInterval = 1000	// TODO : Function of inverse moving capacity and direct distanceToSquared
                        };
	            	}
	            }
	            
        		// Check 1.3) check nearby
                foreach (Thing nearbyRanged in allWeapons.Where(w => w.def.IsRangedWeapon))
                {
                	var ammoUser = nearbyRanged.TryGetComp<CompAmmoUser>();
                	
                	// Check 1.3.1) if ammo enabled:
                	// Check 1.3.1.1) .. and its loaded ammo
                	bool pickup = ammoUser == null || ammoUser.HasAndUsesAmmoOrMagazine;	// Found a loaded gun / gun not needing ammo
                	
                	if (!pickup && ammoUser.UseAmmo)	//ammoUser is inherently checked against null inside pickup
                	{
                    	foreach (ThingDef ammoDef in ammoUser.Props.ammoSet.ammoTypes.Select(y => y.ammo as ThingDef))
                    	{
            				// Check 1.3.1.2) .. and inventory ammo
            				// Check 1.3.1.3) .. and nearby ammo
                    		if (inventory.ammoList.Concat(allAmmo).Any<Thing>(	// Check inventory and nearby ammo at the same time
                    			y => y.def == ammoDef
                    			  //&& y.stackCount >= ammoUser.Props.magazineSize
                    			 ))
                    		{
            					pickup = true;
            					break;
                    		}
                    	}
                	}
                	
                	if (pickup && inventory.CanFitInInventory(nearbyRanged, out x))
                	{
	    				inventory.ExtendTakeAndEquip(-10000);	// Succesful at finding weapon, see if there's more
	                    return new Job(JobDefOf.Equip, nearbyRanged)
	                    {
	                        checkOverrideOnExpire = true,
	                        expiryInterval = 1000	// TODO : This is generally way too low to reach wpn
	                    };
                	}
                }
                
                // Check 1.4) consider melee nearby
            	var someMeleeWeapon = allWeapons.FirstOrDefault(w => w.def.IsMeleeWeapon);	// TODO : Prioritize certain melee || TODO : Allow wood and such if "desperate"
            	
            	if (someMeleeWeapon != null)
            	{
                    return new Job(JobDefOf.Equip, someMeleeWeapon)
                    {
                        checkOverrideOnExpire = true,
                        expiryInterval = 1000	// TODO : Function of inverse moving capacity and direct distanceToSquared
                    };
            	}
            	
            	//If we arrive here, no weapons have been found. Extend next check by 2000 ticks.
            	//Since we don't have a weapon, don't check for weapon switches.
            	inventory.ExtendTakeAndEquip();
            	return null;
            }
            #endregion
            
        	ThingWithComps primary = pawn.equipment?.Primary;
        	
            CompAmmoUser primaryAmmoUser = primary?.TryGetComp<CompAmmoUser>();
            
            // TODO : For this part we assume that the primary ammo user should be kept
            // Inherently only obtained if ammo is enabled (from GetWorkPriority)
            #region Check 2) ammo (preferably close)
            if (workPriority == WorkPriority.Ammo
              || workPriority == WorkPriority.LowAmmo)
            {
        		float searchDistance = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) * maxSearchDistance;
        		float searchSafeDistance = Mathf.Max(safetyMultiplier * searchDistance, minSearchDistance);
        		
                	//Ammo validator
        		Predicate<Thing> validatorA = 
        			(Thing t) => !t.IsForbidden(pawn)
            			&& pawn.CanReserve(t, 1)
                        && pawn.Position.InHorDistOf(t.Position, searchDistance)
                		&& (!DangerInPosRadius(pawn, t.Position, pawn.Map, safetyDistance).Any() || pawn.Position.InHorDistOf(t.Position, searchSafeDistance))
                        && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly, true)
                        && (pawn.Faction.HostileTo(Faction.OfPlayer) || pawn.Faction == Faction.OfPlayer || !pawn.Map.areaManager.Home[t.Position]);
        		
        		var ammoDefs = primaryAmmoUser.Props.ammoSet.ammoTypes.Select(y => y.ammo as ThingDef);
        		
        			// TODO : Change ordering based on urgency of getting ammo
        		IEnumerable<Thing> allUsableAmmo = ammoDefs
        			.SelectMany(x => pawn.Map.listerThings.ThingsOfDef(x))
        			.Where(y => validatorA(y))
        			.OrderByDescending(z => 100f * (float)(z.stackCount) / Mathf.Max(primaryAmmoUser.Props.magazineSize, 1f) - z.Position.DistanceToSquared(pawn.Position));
        		
            	foreach (Thing nearbyAmmo in allUsableAmmo)
            	{
                    int numToCarry = 0;
                    if (inventory.CanFitInInventory(nearbyAmmo, out numToCarry))
                    {
                    	numToCarry = Math.Min(numToCarry, MagazineSize(pawn).max);	// Prevent taking too much		TODO Add a factor here so a slight extra could be taken		TODO Consider max ammo bulk
                    	
                    	inventory.ExtendTakeAndEquip(-10000);	// Succesful at finding ammo once, see if there's more
                        return new Job(JobDefOf.TakeInventory, nearbyAmmo)
                        {
                        	count = numToCarry,	// TODO : Make sure the pawn doesn't over-load on numToCarry
                            expiryInterval = 1500,
                            checkOverrideOnExpire = true
                        };
                    }
                    // TODO : Add Check 2.1 - very far ammo (same as loadouts)
            	}
            	
            	//If we arrive here, no ammo has been found. Extend next check by 2000 ticks.
            	inventory.ExtendTakeAndEquip();
            }
            #endregion
            
            bool hasPrimary = primary != null;
            bool rangedPrimary = hasPrimary && primary.def.IsRangedWeapon;
            
            bool goodShooter = pawn.skills.GetSkill(SkillDefOf.Shooting).Level >= Math.Min(pawn.skills.GetSkill(SkillDefOf.Melee).Level, 6);
            
            // Check 4) correct weapon
            // Check 4.1) brawler, ranged weapon, poor shooter, check inventory
            // Check 4.2) non-brawler, melee weapon, check inventory
            if (hasPrimary
            	&& ((!brawler && !rangedPrimary && ranged > 0)
                    || (brawler && rangedPrimary && !goodShooter && melee > 0)))
            {
            	if (inventory.SwitchToNextViableWeapon(brawler))
            		return null;
            }
            
            // Check 3) overencumbrance
            if (workPriority == WorkPriority.Unloading)
            {
            	Thing droppedThing = null;
            	
            	int ammo = inventory.ammoList.Count;
            	
            	if (hasPrimary)
            	{
	            	// Check 3.1) drop weapons
	            	// Check 3.1.2) non-brawler, drop melee in inventory
	            	// Check 3.1.3) brawler, drop extra melee
	            	if ((!brawler || !rangedPrimary) && melee > 0)
	            	{
	            		droppedThing = inventory.meleeWeaponList
	            			.OrderBy(x => x.MarketValue)
	            			.FirstOrDefault<ThingWithComps>();	//drop lowest market value
	            	}
	            	
	            	// Check 3.1.1) brawler, drop ranged in inventory
	            	// Check 3.1.4) non-brawler, drop extra ranged
	            	else if ((brawler || rangedPrimary)
	            	    && ranged > 0)
	            	{
	            		var droppables = new List<ThingWithComps>();
	            		
	            		foreach (ThingWithComps gun in inventory.rangedWeaponList)
	            		{
	            			var ammoUser = gun.TryGetComp<CompAmmoUser>();
	            			if (ammoUser == null || ammoUser.HasAndUsesAmmoOrMagazine)
	            			{
	            				continue;
	            			}
	            			droppables.Add(gun);
	            		}
	            		
	            		droppedThing = (droppables.Any() ? droppables : inventory.rangedWeaponList)
	            			.OrderBy(x => x.MarketValue)
	            			.FirstOrDefault<ThingWithComps>();	//drop lowest market value
	            	}
            	}
            	
            	// Check 3.2) drop ammo
            	if (droppedThing == null && ammo > 0)
            	{
            		IEnumerable<CompAmmoUser> ammoUsers = inventory.rangedWeaponList
            			.Select<ThingWithComps, CompAmmoUser>(x => x.TryGetComp<CompAmmoUser>())
            			.Concat<CompAmmoUser>(new[] { primaryAmmoUser });
            		
            		List<Thing> droppables = new List<Thing>();
            		
            		// Check 3.2.1) .. not usable for any weapon
            		foreach (Thing currentAmmo in inventory.ammoList)
            		{
            			if (ammoUsers.Any(x => x.Props.ammoSet.ammoTypes.Any(y => y.ammo == currentAmmo.def)))
            				continue;
            			droppables.Add(currentAmmo);
            		}
            		
            		// Check 3.2.2) .. not usable for primary
            		if (!droppables.Any() && hasPrimary)
            		{
	            		foreach (Thing currentAmmo in inventory.ammoList)
	            		{
	            			if (primaryAmmoUser.Props.ammoSet.ammoTypes.Any(x => x.ammo == currentAmmo.def))
	            				continue;
	            			droppables.Add(currentAmmo);
	            		}
            		}
            		
            		// Check 3.2.3) .. usable for primary but extra
            		if (!droppables.Any() && hasPrimary)
            		{
            			
            		}
            		
            		droppedThing = droppables
            			.OrderBy(x => x.MarketValue)
            			.FirstOrDefault<Thing>();	//drop lowest market value
            	}
            	
            	// Check 3.3) drop other stuff?
            	
            		// TODO : Implement UpdateLoadout dropping behaviour here
            	
            	else
            	{
            		return null;
            	}
            	
        		if (droppedThing != null)
        		{
	            	Thing outThing;
	                if (inventory.container.TryDrop(droppedThing, pawn.Position, pawn.Map, ThingPlaceMode.Near, droppedThing.stackCount, out outThing))
	                {
	                    pawn.jobs.EndCurrentJob(JobCondition.None, true);
	                    pawn.jobs.TryTakeOrderedJob(new Job(JobDefOf.DropEquipment, 30, true));
	                }
        		}
            }
            return null;
        }
        
        /*
        private static Job GotoForce(Pawn pawn, LocalTargetInfo target, PathEndMode pathEndMode)
        {
            using (PawnPath pawnPath = pawn.Map.pathFinder.FindPath(pawn.Position, target, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings, false), pathEndMode))
            {
                IntVec3 cellBeforeBlocker;
                Thing thing = pawnPath.FirstBlockingBuilding(out cellBeforeBlocker, pawn);
                if (thing != null)
                {
                    Job job = DigUtility.PassBlockerJob(pawn, thing, cellBeforeBlocker, true);
                    if (job != null)
                    {
                        return job;
                    }
                }
                if (thing == null)
                {
                    return new Job(JobDefOf.Goto, target, 100, true);
                }
                if (pawn.equipment.Primary != null)
                {
                    Verb primaryVerb = pawn.equipment.PrimaryEq.PrimaryVerb;
                    if (primaryVerb.verbProps.ai_IsBuildingDestroyer && (!primaryVerb.verbProps.ai_IsIncendiary || thing.FlammableNow))
                    {
                        return new Job(JobDefOf.UseVerbOnThing)
                        {
                            targetA = thing,
                            verbToUse = primaryVerb,
                            expiryInterval = 100
                        };
                    }
                }
                return MeleeOrWaitJob(pawn, thing, cellBeforeBlocker);
            }
        }
        */

        private static bool Unload(Pawn pawn)
        {
            var inv = pawn.TryGetComp<CompInventory>();
            if (inv != null
            && !pawn.Faction.IsPlayer
            && (pawn.CurJob != null && pawn.CurJob.def != JobDefOf.Steal)
            && ((inv.capacityWeight - inv.currentWeight < weightMargin)
            	|| (inv.capacityBulk - inv.currentBulk < bulkMargin)))
            {
                return true;
            }
            
            return false;
        }

        private static IEnumerable<Pawn> DangerInPosRadius(Pawn pawn, IntVec3 position, Map map, float distance)
        {
			return map.mapPawns.AllPawns.Where((p => p.Position.InHorDistOf(position, distance) && !p.RaceProps.Animal && !p.Downed && !p.Dead && FactionUtility.HostileTo(p.Faction, pawn.Faction)));
        }

        private static Job MeleeOrWaitJob(Pawn pawn, Thing blocker, IntVec3 cellBeforeBlocker)
        {
            if (!pawn.CanReserve(blocker, 1))
            {
                return new Job(JobDefOf.Goto, CellFinder.RandomClosewalkCellNear(cellBeforeBlocker, pawn.Map, 10), 100, true);
            }
            return new Job(JobDefOf.AttackMelee, blocker)
            {
                ignoreDesignations = true,
                expiryInterval = 100,
                checkOverrideOnExpire = true
            };
        }

        /*
        private Apparel FindGarmentCoveringPart(Pawn pawn, BodyPartGroupDef bodyPartGroupDef)
        {
            Room room = pawn.GetRoom();
            Predicate<Thing> validator = (Thing t) => pawn.CanReserve(t, 1) 
            && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly, true) 
            && (t.Position.DistanceToSquared(pawn.Position) < 12f || room == RegionAndRoomQuery.RoomAtFast(t.Position, t.Map));
            List<Thing> aList = (
                from t in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                orderby t.MarketValue - t.Position.DistanceToSquared(pawn.Position) * 2f descending
                where validator(t)
                select t
                ).ToList();
            foreach (Thing current in aList)
            {
                Apparel ap = current as Apparel;
                if (ap != null && ap.def.apparel.bodyPartGroups.Contains(bodyPartGroupDef) && pawn.CanReserve(ap, 1) && ApparelUtility.HasPartsToWear(pawn, ap.def))
                {
                    return ap;
                }
            }
            return null;
        }
        */
    }
}

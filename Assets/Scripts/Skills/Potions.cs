using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Frontiers;
using Frontiers.World;
using Frontiers.World.Gameplay;

namespace Frontiers.World.Gameplay
{
	public class Potions : Skill
	{
		public override bool DoesContextAllowForUse (IItemOfInterest targetObject)
		{
			if (base.DoesContextAllowForUse (targetObject)) {
				CraftingItem craftingItem = targetObject.gameObject.GetComponent <CraftingItem> ();
				if (craftingItem.SkillToUse == name) {
					return true;
				}
			}
			return false;
		}

		public override bool Use (IItemOfInterest targetObject, int flavorIndex)
		{
			//assume we're looking at a crafting object by this point
			targetObject.gameObject.SendMessage ("OpenCraftingInterface");
			return true;
		}

		//TODO override requirements met with potion-specific rules
	}
}
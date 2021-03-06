using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Frontiers;
using Frontiers.World;
using System;

namespace Frontiers.GUI {
	public class InventorySquareCraftingResult : InventorySquareDisplay
	{
		public GenericWorldItem CraftedItemTemplate
		{
			get{
				return mCraftedItemTemplate;
			}
			set {
				mCraftedItemTemplate = value;
				UpdateDisplay ();
			}
		}
		public bool RequirementsMet
		{
			get {
				return mRequirementsMet;
			}
			set {
				mRequirementsMet = value;
				RefreshRequest ();
			}
		}
		public int NumItemsPossible
		{
			get{
				return mNumItemsPossible;
			}
			set{
				mNumItemsPossible = value;
				RefreshRequest ();
			}
		}
		public int NumItemsCrafted
		{
			get{
				return mNumItemsCrafted;
			}
			set{
				mNumItemsCrafted = value;
				RefreshRequest ();
			}
		}

		public Action RefreshAction;

		public bool HasItemTemplate {
			get {
				return CraftedItemTemplate != null && !CraftedItemTemplate.IsEmpty;
			}
		}

		public bool ReadyForRetrieval {
			get {
				return CraftedItemTemplate != null && !CraftedItemTemplate.IsEmpty && NumItemsCrafted > 0;
			}
		}

		public void OnClickSquare ()
		{
			Debug.Log ("Clicking result square");
			WIStackError error = WIStackError.None;
			if (ReadyForRetrieval) {
				while (NumItemsCrafted > 0) {
					StackItem craftedItem = CraftedItemTemplate.ToStackItem ();
					craftedItem.Group = WIGroups.Get.Player;
					if (Player.Local.Inventory.AddItems (craftedItem, ref error)) {
						NumItemsCrafted--;
					}
					else {
						Debug.Log ("We have to carry the item");
						if (Player.Local.ItemPlacement.IsCarryingSomething) {
							Player.Local.ItemPlacement.PlaceOrDropCarriedItem ();
						}
						//turn it into a worlditem and have the player carry it
						WorldItem craftedWorldItem = null;
						if (WorldItems.CloneFromStackItem (craftedItem, WIGroups.Get.World, out craftedWorldItem)) {
							craftedWorldItem.Initialize ();
							craftedWorldItem.ActiveState = WIActiveState.Active;
							craftedWorldItem.Props.Local.FreezeOnStartup = false;
							craftedWorldItem.tr.rotation = Quaternion.identity;
							craftedWorldItem.SetMode (WIMode.World);
							craftedWorldItem.tr.position = Player.Local.ItemPlacement.GrabberIdealPosition;
							craftedWorldItem.LastActiveDistanceToPlayer = 0f;
							//if we have an interface open, close it now
							GUIInventoryInterface.Get.Minimize ();
							//then force the player to carry the item
							if (Player.Local.ItemPlacement.ItemCarry (craftedWorldItem, true)) {
								NumItemsCrafted--;
							} else {
								GUIManager.PostWarning ("You have to drop what you're carrying first");
							}
						}
						break;
					}
				}
			}

			RefreshRequest ();
		}

		public override void UpdateDisplay ()
		{
			InventoryItemName.text = string.Empty;
			DisplayMode = SquareDisplayMode.Disabled;
			string stackNumberLabelText = string.Empty;
			ShowDoppleganger = false;
			MouseoverHover = false;
			DopplegangerProps.CopyFrom (mCraftedItemTemplate);

			if (HasItemTemplate) {
				DisplayMode = SquareDisplayMode.Enabled;
				ShowDoppleganger = true;

				if (NumItemsCrafted > 0) {
					DisplayMode = SquareDisplayMode.Success;
					MouseoverHover = true;
					DopplegangerMode = WIMode.Stacked;
					stackNumberLabelText = NumItemsCrafted.ToString ();
				} else {
					DopplegangerMode = WIMode.Stacked;
					if (NumItemsPossible > 0) {
						stackNumberLabelText = NumItemsPossible.ToString ();
					}
				}
			}

			StackNumberLabel.text = stackNumberLabelText;
		
			base.UpdateDisplay ();
		}

		protected override void OnRefresh ()
		{
			base.OnRefresh ();
			RefreshAction.SafeInvoke ();
		}

		protected bool mRequirementsMet = false;
		protected GenericWorldItem mCraftedItemTemplate = null;
		protected int mNumItemsPossible = 1;
		protected int mNumItemsCrafted = 0;
	}
}
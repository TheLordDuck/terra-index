using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.Content.Storage
{
	// Shift + click in the inventory while the deposit slots are open moves the item there, like with a chest.
	public class TerminalPlayer : ModPlayer
	{
		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		private bool CanQuickDeposit(Item[] inventory, int context, int slot) =>
			Terminal.DepositOpen && Terminal.DepositItems != null && context == ItemSlot.Context.InventoryItem
			&& inventory == Player.inventory && !inventory[slot].IsAir && !inventory[slot].favorited;

		public override bool HoverSlot(Item[] inventory, int context, int slot)
		{
			if (!ItemSlot.ShiftInUse || !CanQuickDeposit(inventory, context, slot))
				return false;
			Main.cursorOverride = CursorOverrideID.InventoryToChest;
			return true;
		}

		public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
		{
			if (!CanQuickDeposit(inventory, context, slot))
				return false;

			Item item = inventory[slot];
			Item[] deposit = Terminal.DepositItems;

			// First onto matching stacks, then into the first empty slot.
			foreach (Item target in deposit) {
				if (item.IsAir)
					break;
				if (!target.IsAir && target.type == item.type && target.prefix == item.prefix && target.stack < target.maxStack) {
					int amount = Math.Min(item.stack, target.maxStack - target.stack);
					target.stack += amount;
					item.stack -= amount;
				}
			}
			if (item.stack <= 0) {
				item.TurnToAir();
			}
			else {
				int empty = Array.FindIndex(deposit, target => target.IsAir);
				if (empty >= 0) {
					deposit[empty] = item.Clone();
					item.TurnToAir();
				}
			}

			SoundEngine.PlaySound(SoundID.Grab);
			return true;
		}
	}
}

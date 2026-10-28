using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.UI;
using Terraria;
using Terraria.ModLoader;

namespace TerraIndex.Content.Storage
{
	// Every item's tooltip says how many of it are in the chests of the terminal in reach (the one the recipe index
	// crafts from), anywhere in the game: the inventory, chests, shops...
	public class ChestCountTooltip : GlobalItem
	{
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			var terminal = ModContent.GetInstance<TerminalSystem>();
			if (item.IsAir || !ClientConfig.Instance.ChestCountTooltip || !terminal.CanCraft)
				return;
			// Keeps the counts fresh while items are hovered, like the index does while it is open.
			terminal.KeepFreshForCrafting();
			int stored = terminal.StoredCount(item.type);
			if (stored > 0)
				tooltips.Add(new TooltipLine(Mod, "InChests", UIHelpers.T("InChests", stored)) { OverrideColor = new Color(120, 190, 255) });
		}
	}
}

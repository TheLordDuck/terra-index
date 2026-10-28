using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Tiles;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// Links two wireless teleporters: hold it and right click one, then the other (see WirelessTeleporterTile.RightClick).
	// The first one picked is only remembered until then, in this world.
	public class TeleporterLinkerItem : ModItem
	{
		private Point picked = new(-1, -1);
		private int pickedWorld;

		private bool HasPicked => picked.X >= 0 && pickedWorld == Main.worldID;

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.sellPrice(silver: 50);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wrench)
				.AddRecipeGroup(RecipeGroupID.IronBar, 3)
				.AddTile(TileID.Anvils)
				.Register();
		}

		// First click picks a teleporter; the second links it to the first. Clicking the picked one again cancels.
		public void Pick(Point teleporter)
		{
			if (!HasPicked) {
				picked = teleporter;
				pickedWorld = Main.worldID;
				Main.NewText(UIHelpers.T("TeleporterPicked"), Color.LightBlue);
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (picked == teleporter) {
				picked = new Point(-1, -1);
				Main.NewText(UIHelpers.T("TeleporterCancelled"), Color.LightGray);
				SoundEngine.PlaySound(SoundID.MenuClose);
			}
			else {
				ModContent.GetInstance<TeleporterSystem>().RequestLink(picked, teleporter);
				int blocks = (int)(Vector2.Distance(WirelessTeleporterTile.Center(picked), WirelessTeleporterTile.Center(teleporter)) / 16);
				Main.NewText(UIHelpers.T("TeleporterLinked", blocks), Color.LightGreen);
				SoundEngine.PlaySound(SoundID.Unlock);
				picked = new Point(-1, -1);
			}
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			if (HasPicked)
				tooltips.Add(new TooltipLine(Mod, "Picked", UIHelpers.T("TeleporterPickedTooltip")) { OverrideColor = Color.LightBlue });
		}
	}
}

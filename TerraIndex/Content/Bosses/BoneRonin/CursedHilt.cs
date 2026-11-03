using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Summons the Bone Ronin. Only after the Moon Lord, and only if he isn't already here.
	public class CursedHilt : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 3;
			ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = Item.CommonMaxStack;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.buyPrice(gold: 5);
			Item.useAnimation = 45;
			Item.useTime = 45;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.consumable = true;
		}

		public override bool CanUseItem(Player player) => NPC.downedMoonlord && !NPC.AnyNPCs(ModContent.NPCType<BoneRonin>());

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer) {
				SoundEngine.PlaySound(SoundID.Roar, player.position);
				int type = ModContent.NPCType<BoneRonin>();
				if (Main.netMode != NetmodeID.MultiplayerClient)
					NPC.SpawnOnPlayer(player.whoAmI, type);
				else
					NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: type);
			}
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.LunarBar, 8)
				.AddIngredient(ItemID.Bone, 30)
				.AddIngredient(ItemID.Ectoplasm, 10)
				.AddTile(TileID.LunarCraftingStation)
				.Register();
		}
	}
}

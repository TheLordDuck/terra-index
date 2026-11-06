using TerraIndex.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Summons the Bone Ronin. Only after Supreme Calamitas, and only if he isn't already here.
	// It is not used up: once crafted it can call him again and again.
	public class CursedHilt : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 1;
			ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = 1;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.buyPrice(gold: 5);
			Item.useAnimation = 45;
			Item.useTime = 45;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.consumable = false;
		}

		// The last boss: only once Supreme Calamitas has fallen.
		public override bool CanUseItem(Player player) => CalamityGuide.Downed("supremecalamitas") && !NPC.AnyNPCs(ModContent.NPCType<BoneRonin>());

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
			// Supreme Calamitas' ashes and the Exo Mechs' prisms, at Draedon's Forge (if a Calamity update renamed
			// something, that ingredient or station is left out rather than breaking the recipe).
			Recipe recipe = CreateRecipe()
				.AddIngredient(ItemID.Bone, 50)
				.AddIngredient(ItemID.Ectoplasm, 20);
			if (ModContent.TryFind("CalamityMod", "AshesofAnnihilation", out ModItem ashes))
				recipe.AddIngredient(ashes.Type, 5);
			if (ModContent.TryFind("CalamityMod", "ExoPrism", out ModItem prism))
				recipe.AddIngredient(prism.Type, 5);
			recipe.AddTile(ModContent.TryFind("CalamityMod", "DraedonsForge", out ModTile forge) ? forge.Type : TileID.LunarCraftingStation);
			recipe.Register();
		}
	}
}

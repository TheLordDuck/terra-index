using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Building
{
	// Right click: the blueprint catalog. Left click: builds the chosen house where its preview is (see BuildSystem).
	public class BlueprintItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(silver: 10);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			BuildSystem build = ModContent.GetInstance<BuildSystem>();
			if (player.altFunctionUse == 2)
				build.OpenCatalog();
			else
				build.TryBuild();
			return true;
		}
	}
}

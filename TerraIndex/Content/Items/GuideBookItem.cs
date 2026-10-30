using TerraIndex.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// Opens the Calamity guide book (GuideBookState): the guide's build for each class and stage, and the bosses
	// beaten, missing and next. Only exists with Calamity enabled and the Calamity features on, like the guide.
	public class GuideBookItem : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("CalamityMod") && ServerConfig.Instance.CalamityFeatures;

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 30;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.rare = ItemRarityID.Orange;
			Item.value = Terraria.Item.sellPrice(silver: 10);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				ModContent.GetInstance<GuideBookSystem>().Toggle();
			return true;
		}
	}
}

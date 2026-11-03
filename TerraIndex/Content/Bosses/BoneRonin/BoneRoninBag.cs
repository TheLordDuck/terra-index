using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Expert / Master mode treasure bag: the katana, the expert accessory and coins.
	public class BoneRoninBag : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetStaticDefaults()
		{
			ItemID.Sets.BossBag[Type] = true;
			Item.ResearchUnlockCount = 3;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = Item.CommonMaxStack;
			Item.consumable = true;
			Item.rare = ItemRarityID.Purple;
			Item.expert = true;
		}

		public override bool CanRightClick() => true;

		public override void ModifyItemLoot(ItemLoot itemLoot)
		{
			itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<RoninKatana>()));
			itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<RoninSpirit>()));
			itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<BoneRonin>()));
		}
	}
}

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Expert mode accessory from the Bone Ronin's treasure bag: the swordsman's spirit, for melee players.
	public class RoninSpirit : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 32;
			Item.accessory = true;
			Item.expert = true;
			Item.rare = ItemRarityID.Expert;
			Item.value = Item.sellPrice(gold: 10);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			// Endgame numbers: he is fought after Supreme Calamitas.
			player.GetDamage(DamageClass.Melee) += 0.15f;
			player.GetCritChance(DamageClass.Melee) += 10;
			player.GetAttackSpeed(DamageClass.Melee) += 0.15f;
		}
	}
}

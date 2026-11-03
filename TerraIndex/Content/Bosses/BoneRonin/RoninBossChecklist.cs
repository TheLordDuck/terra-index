using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's Boss Checklist entry, if that mod is enabled (after the Moon Lord, whose weight is 18).
	public class RoninBossChecklist : ModSystem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
				return;
			bossChecklist.Call("LogBoss", Mod, nameof(BoneRonin), 19f, () => RoninDownedSystem.downedBoneRonin, ModContent.NPCType<BoneRonin>(),
				new Dictionary<string, object> {
					["spawnItems"] = ModContent.ItemType<CursedHilt>(),
					["spawnInfo"] = Language.GetText("Mods.TerraIndex.BossChecklist.BoneRoninSpawn"),
					["collectibles"] = new List<int> { ModContent.ItemType<RoninKatana>(), ModContent.ItemType<RoninSpirit>() },
				});
		}
	}
}

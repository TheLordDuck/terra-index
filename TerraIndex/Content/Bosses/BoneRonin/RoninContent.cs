using TerraIndex.Common;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin (a post-Moon Lord boss, also published on its own as the Terra Bosses mod) is part of Terra Index
	// only with Calamity: every NPC, item, projectile, player and system of his returns this from IsLoadingEnabled, so
	// without Calamity (or with the "Calamity features" option off) none of it exists. If Terra Bosses is enabled too,
	// its copy wins and this one stays out, so there aren't two Bone Ronins. Configs load before the content, so the
	// option can be read here; changing it needs a reload (it is ReloadRequired).
	public static class RoninContent
	{
		public static bool Enabled => ModLoader.HasMod("CalamityMod") && ServerConfig.Instance.CalamityFeatures && !ModLoader.HasMod("TerraBosses");
	}
}

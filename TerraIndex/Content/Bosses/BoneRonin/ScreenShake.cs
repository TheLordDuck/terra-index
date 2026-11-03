using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Screen shake for heavy hits. Local to each client: bosses call Shake from their AI, which runs everywhere.
	public class ScreenShake : ModPlayer
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private static float intensity;

		// Shakes the screen if the local player is near the spot; a stronger shake replaces a weaker one.
		public static void Shake(Vector2 at, float strength)
		{
			if (Main.dedServ || Main.gameMenu)
				return;
			if (Main.LocalPlayer.Distance(at) < 2400f && strength > intensity)
				intensity = strength;
		}

		public override void ModifyScreenPosition()
		{
			if (intensity < 0.2f) {
				intensity = 0f;
				return;
			}
			Main.screenPosition += Main.rand.NextVector2Circular(intensity, intensity);
			intensity *= 0.88f;
		}
	}
}

using Microsoft.Xna.Framework;
using TerraIndex.Common;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The colours of the Bone Ronin's effects for the chosen look (ClientConfig.BoneRoninLook): cold blue for the ghost in the white
	// kimono, fire orange for the zombie samurai in lacquered armour.
	public static class RoninLook
	{
		public static bool Ember => ClientConfig.Instance?.BoneRoninLook == RoninStyle.Ember;

		// Main glow, a deeper one (auras) and a brighter one (arcs, flashes).
		public static Color Main => Ember ? new Color(255, 140, 50) : new Color(120, 200, 255);
		public static Color Soft => Ember ? new Color(255, 90, 30) : new Color(90, 170, 255);
		public static Color Bright => Ember ? new Color(255, 200, 120) : new Color(160, 225, 255);
		// Tint for the phantom katanas.
		public static Color Tint => Ember ? new Color(255, 210, 170, 120) : new Color(200, 230, 255, 120);
		public static Vector3 LightVector => Ember ? new Vector3(0.8f, 0.4f, 0.12f) : new Vector3(0.25f, 0.45f, 0.8f);
		public static int Dust => Ember ? DustID.Torch : DustID.IceTorch;

		// The colour drawn as light (added to what's behind).
		public static Color Additive(Color color, float strength = 1f) => new Color(color.R, color.G, color.B, 0) * strength;
	}
}

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Our panels sit over the vanilla UI (the inventory, the hotbar...), which handles its clicks and hovers while it is
	// drawn, before ours. So a click on a recipe also clicked the inventory slot under it. While the mouse is over one of
	// our panels (UIHelpers.BlockMouse, from their updates), it is moved off screen for the layers drawn before ours, and
	// put back for ours (UIHelpers.Layer) and the mouse text. Each layer works out the mouse from PlayerInput's
	// unscaled position (its SetZoom_ methods), so that is the one moved.
	public class MouseBlocker : ModSystem
	{
		private const int OffScreen = -100000;

		private static readonly FieldInfo OriginalX = typeof(PlayerInput).GetField("_originalMouseX", BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly FieldInfo OriginalY = typeof(PlayerInput).GetField("_originalMouseY", BindingFlags.NonPublic | BindingFlags.Static);

		// Asked for during this frame, and during the last one (a panel that asks while it is drawn asks too late for
		// this frame's vanilla layers).
		private static bool requested;
		private static bool requestedBefore;

		private static bool hidden;
		private static int savedOriginalX, savedOriginalY, savedMouseX, savedMouseY, savedPlayerInputX, savedPlayerInputY;

		public static void Request() => requested = true;

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			layers.Insert(0, new LegacyGameInterfaceLayer("TerraIndex: Hide Mouse", () => {
				Hide();
				return true;
			}, InterfaceScaleType.None));
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index != -1) {
				layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Show Mouse", () => {
					Restore();
					return true;
				}, InterfaceScaleType.None));
			}
		}

		private static void Hide()
		{
			if (hidden || !(requested || requestedBefore) || OriginalX == null || OriginalY == null)
				return;
			hidden = true;
			savedOriginalX = (int)OriginalX.GetValue(null);
			savedOriginalY = (int)OriginalY.GetValue(null);
			(savedMouseX, savedMouseY, savedPlayerInputX, savedPlayerInputY) = (Main.mouseX, Main.mouseY, PlayerInput.MouseX, PlayerInput.MouseY);
			OriginalX.SetValue(null, OffScreen);
			OriginalY.SetValue(null, OffScreen);
			Main.mouseX = Main.mouseY = PlayerInput.MouseX = PlayerInput.MouseY = OffScreen;
		}

		// Puts the mouse back. Returns true if it was hidden, so the caller can work it out again for its scale.
		public static bool Restore()
		{
			if (!hidden)
				return false;
			hidden = false;
			OriginalX.SetValue(null, savedOriginalX);
			OriginalY.SetValue(null, savedOriginalY);
			(Main.mouseX, Main.mouseY, PlayerInput.MouseX, PlayerInput.MouseY) = (savedMouseX, savedMouseY, savedPlayerInputX, savedPlayerInputY);
			return true;
		}

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			Restore();
			requestedBefore = requested;
			requested = false;
		}
	}
}

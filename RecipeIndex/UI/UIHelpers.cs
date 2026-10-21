using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.UI;

namespace RecipeIndex.UI
{
	internal static class UIHelpers
	{
		public static string T(string key, params object[] args) => Language.GetTextValue("Mods.RecipeIndex.UI." + key, args);

		// Initializes a panel. Only call this once the mod is loaded (not in Load), or T() returns the raw keys.
		public static TState Build<TState>(TState state) where TState : UIState
		{
			state.Activate();
			return state;
		}

		public static Vector2 Measure(string text, float scale) => FontAssets.MouseText.Value.MeasureString(text) * scale;

		// Truncates the text with "..." so it fits in maxWidth.
		public static string Fit(string text, float maxWidth, float scale)
		{
			if (Measure(text, scale).X <= maxWidth)
				return text;
			while (text.Length > 1 && Measure(text + "...", scale).X > maxWidth)
				text = text[..^1];
			return text + "...";
		}

		// Draws an item slot and shows the item's tooltip on hover. Returns true if the mouse is over it.
		public static bool DrawItem(SpriteBatch spriteBatch, Item item, Vector2 position, float scale)
		{
			float oldScale = Main.inventoryScale;
			Main.inventoryScale = scale;
			ItemSlot.Draw(spriteBatch, ref item, ItemSlot.Context.InventoryItem, position);
			Main.inventoryScale = oldScale;

			int size = (int)(52 * scale);
			bool hovered = new Rectangle((int)position.X, (int)position.Y, size, size).Contains(Main.MouseScreen.ToPoint());
			if (hovered) {
				Main.LocalPlayer.mouseInterface = true;
				ItemSlot.MouseHover(ref item, ItemSlot.Context.InventoryItem);
			}
			return hovered;
		}

		// For recipe groups, cycles the icon through the group's members every second (like NEI).
		public static int Cycle(RecipeGroup group)
		{
			int count = group.ValidItems.Count;
			return count == 0 ? group.IconicItemId : group.ValidItems.ElementAt((int)(Main.GameUpdateCount / 60 % count));
		}

		public static int CountOwned(Func<int, bool> matches)
		{
			int total = 0;
			foreach (Item item in Main.LocalPlayer.inventory) {
				if (!item.IsAir && matches(item.type))
					total += item.stack;
			}
			return total;
		}
	}
}

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

		public static Vector2 Measure(string text, float scale) => FontAssets.MouseText.Value.MeasureString(text) * scale;

		// Recorta el texto con "..." para que quepa en maxWidth.
		public static string Fit(string text, float maxWidth, float scale)
		{
			if (Measure(text, scale).X <= maxWidth)
				return text;
			while (text.Length > 1 && Measure(text + "...", scale).X > maxWidth)
				text = text[..^1];
			return text + "...";
		}

		// Dibuja una ranura con el objeto y muestra su tooltip al pasar el ratón. Devuelve true si está bajo el ratón.
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

		// Para grupos de receta, va rotando el icono entre sus miembros cada segundo (como NEI).
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

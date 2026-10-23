using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Terminal panel: the crafting stations on the terminal's wire, as small item slots (hover one for its name).
	// They count for crafting from the chests, as if they were next to the player.
	public class StationsRow : UIElement
	{
		private const float SlotScale = 0.5f;

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle inner = GetInnerDimensions();
			string label = UIHelpers.T("WireStations");
			Utils.DrawBorderString(spriteBatch, label, new Vector2(inner.X, inner.Y + 5), Color.LightGray, 0.75f);
			float x = inner.X + UIHelpers.Measure(label, 0.75f).X + 8;

			// Stations whose item isn't known can't be drawn; they still count for crafting.
			List<int> items = ModContent.GetInstance<TerminalSystem>().WireStations
				.Select(RecipeDatabase.ItemForTile).Where(item => item > 0).ToList();
			if (items.Count == 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("NoWireStations"), new Vector2(x, inner.Y + 5), Color.Gray, 0.75f);
				return;
			}

			int slot = (int)(52 * SlotScale) + 2;
			float right = inner.X + inner.Width;
			for (int i = 0; i < items.Count; i++) {
				// The last slot's room is kept for "+N" when they don't all fit.
				if (i < items.Count - 1 && x + slot * 2 > right) {
					Utils.DrawBorderString(spriteBatch, $"+{items.Count - i}", new Vector2(x + 2, inner.Y + 5), Color.LightGray, 0.75f);
					return;
				}
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[items[i]], new Vector2(x, inner.Y), SlotScale);
				x += slot;
			}
		}
	}
}

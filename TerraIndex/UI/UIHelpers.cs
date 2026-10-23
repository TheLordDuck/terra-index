using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;

namespace TerraIndex.UI
{
	internal static class UIHelpers
	{
		public static string T(string key, params object[] args) => Language.GetTextValue("Mods.TerraIndex.UI." + key, args);

		// Initializes a panel. Only call this once the mod is loaded (not in Load), or T() returns the raw keys.
		public static TState Build<TState>(TState state) where TState : UIState
		{
			state.Activate();
			return state;
		}

		// The recipe index docked next to the terminal panel: the index on the left, both centered together. On narrow
		// screens the index gets narrower (its recipe side shrinks) rather than covering the terminal.
		public const float IndexWidth = 860;
		public const float TerminalWidth = 520;
		private const float MinDockedIndexWidth = 680;
		private const float DockGap = 12;

		public static (float IndexLeft, float IndexWidth, float TerminalLeft) DockLayout(float screenWidth)
		{
			float indexWidth = Math.Clamp(screenWidth - TerminalWidth - DockGap - 32, MinDockedIndexWidth, IndexWidth);
			float left = Math.Max(0, (screenWidth - indexWidth - DockGap - TerminalWidth) / 2);
			return (left, indexWidth, left + indexWidth + DockGap);
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

		// Splits the text into lines that fit in maxWidth.
		public static List<string> Wrap(string text, float maxWidth, float scale)
		{
			string[] lines = Utils.WordwrapString(text, FontAssets.MouseText.Value, (int)(maxWidth / scale), 20, out _);
			return lines.Where(line => !string.IsNullOrEmpty(line)).Select(line => line.Trim()).ToList();
		}

		// A copper amount as "1 platinum 20 gold", like vanilla prices.
		public static string Coins(long copper)
		{
			var parts = new List<string>();
			(long amount, string key)[] coins = {
				(copper / 1000000, "Platinum"), (copper / 10000 % 100, "Gold"), (copper / 100 % 100, "Silver"), (copper % 100, "Copper"),
			};
			foreach (var (amount, key) in coins) {
				if (amount > 0)
					parts.Add(T(key, amount));
			}
			return string.Join(" ", parts);
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

		// Draws an NPC's first animation frame on a slot background, shrunk to fit a size x size box.
		public static void DrawNpc(SpriteBatch spriteBatch, int type, Vector2 position, int size)
		{
			Utils.DrawInvBG(spriteBatch, new Rectangle((int)position.X, (int)position.Y, size, size), new Color(40, 45, 100) * 0.8f);

			Main.instance.LoadNPC(type);
			Texture2D texture = TextureAssets.Npc[type].Value;
			var frame = new Rectangle(0, 0, texture.Width, texture.Height / Math.Max(1, Main.npcFrameCount[type]));
			float scale = Math.Min(1f, (size - 4) / (float)Math.Max(frame.Width, frame.Height));
			// Slimes and a few others use a grey sprite tinted by the NPC's color.
			Color color = ContentSamples.NpcsByNetId.TryGetValue(type, out NPC npc) && npc.color != default ? new Color(npc.color.R, npc.color.G, npc.color.B) : Color.White;
			spriteBatch.Draw(texture, position + new Vector2(size / 2f), frame, color, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using RecipeIndex.Content.Tiles;
using RecipeIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.Content.Storage
{
	public class TerminalSystem : ModSystem
	{
		private const int HighlightTicks = 60 * 10;
		private const float MaxDistance = 16 * 10;
		private const float ArrowMargin = 40;

		private UserInterface ui;
		private TerminalState state;
		private GameTime lastGameTime;
		private Asset<Texture2D> arrowTexture;

		// Highlighted chests: chest top-left corner -> ticks remaining.
		private readonly Dictionary<Point, int> highlights = new();

		// Latest scan (received from the server in multiplayer, computed locally in single player).
		private StorageSnapshot snapshot = new();

		public Point Terminal { get; private set; }
		public int ChestCount => snapshot.ChestCount;
		public List<Item> StoredItems { get; } = new();

		// Bumped whenever new data arrives, so the panel knows when to rebuild its list.
		public int Version { get; private set; }

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			if (!Main.dedServ) {
				ui = new UserInterface();
				state = new TerminalState();
				state.Activate();
				arrowTexture = ModContent.Request<Texture2D>("RecipeIndex/Content/Storage/ChestArrow");
			}
		}

		public override void OnWorldUnload()
		{
			ui?.SetState(null);
			highlights.Clear();
		}

		public void Open(Point terminal)
		{
			Terminal = terminal;
			Apply(new StorageSnapshot());
			ui.SetState(state);
			SoundEngine.PlaySound(SoundID.MenuOpen);
			RequestRefresh();
		}

		public void Close()
		{
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void RequestRefresh()
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				Apply(StorageNetwork.Scan(Terminal));
			else
				TerminalNet.SendRequest(Terminal);
		}

		// Server reply; ignored if the panel was closed or another terminal opened in the meantime.
		public void ApplyRemote(Point terminal, StorageSnapshot remote)
		{
			if (IsOpen && terminal == Terminal)
				Apply(remote);
		}

		private void Apply(StorageSnapshot newSnapshot)
		{
			snapshot = newSnapshot;
			StoredItems.Clear();
			foreach (var (type, stack) in snapshot.Totals.OrderBy(pair => Lang.GetItemNameValue(pair.Key)))
				StoredItems.Add(new Item(type, stack));
			Version++;
		}

		// Highlights the connected chests that hold the item. Returns how many there are.
		public int Highlight(int type)
		{
			highlights.Clear();
			if (snapshot.Locations.TryGetValue(type, out List<Point> chests)) {
				foreach (Point chest in chests)
					highlights[chest] = HighlightTicks;
			}
			return highlights.Count;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (!IsOpen)
				return;

			// Closes like a chest: when walking away, when the terminal is broken, or on Esc (unless typing).
			Tile tile = Main.tile[Terminal.X, Terminal.Y];
			var center = new Vector2(Terminal.X * 16 + 16, Terminal.Y * 16 + 16);
			bool escape = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape) && !SearchBox.AnyFocused;
			if (!tile.HasTile || tile.TileType != ModContent.TileType<TerminalTile>() || Main.LocalPlayer.Distance(center) > MaxDistance || escape) {
				Close();
				return;
			}

			ui.Update(gameTime);
		}

		public override void PostUpdateEverything()
		{
			foreach (Point chest in highlights.Keys.ToList()) {
				if (--highlights[chest] <= 0) {
					highlights.Remove(chest);
					continue;
				}
				Vector2 center = new Vector2(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);
				Lighting.AddLight(center, 1f, 0.85f, 0.3f);
				if (Main.rand.NextBool(8))
					Dust.NewDust(new Vector2(chest.X * 16, chest.Y * 16), ChestWidth(chest) * 16, 32, DustID.GoldFlame);
			}
		}

		public override void PostDrawTiles()
		{
			if (highlights.Count == 0)
				return;

			SpriteBatch spriteBatch = Main.spriteBatch;
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
			foreach (Point chest in highlights.Keys) {
				var area = new Rectangle(chest.X * 16 - (int)Main.screenPosition.X - 2, chest.Y * 16 - (int)Main.screenPosition.Y - 2, ChestWidth(chest) * 16 + 4, 36);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, Color.Gold * (0.15f + 0.15f * pulse));
				DrawBorder(spriteBatch, area, Color.Gold * (0.6f + 0.4f * pulse), 2);
			}

			spriteBatch.End();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer("RecipeIndex: Terminal", () => {
				if (IsOpen && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}, InterfaceScaleType.UI));

			layers.Insert(index, new LegacyGameInterfaceLayer("RecipeIndex: Chest Arrows", () => {
				DrawOffscreenArrows(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		// For each highlighted chest off screen, an arrow on the screen edge pointing at it, with the distance in blocks.
		private void DrawOffscreenArrows(SpriteBatch spriteBatch)
		{
			if (highlights.Count == 0)
				return;

			// The layer uses UI scale, so work in screen pixels and then divide by Main.UIScale.
			var screen = new Vector2(Main.screenWidth, Main.screenHeight);
			Vector2 center = screen / 2f;
			Texture2D texture = arrowTexture.Value;
			float alpha = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);

			foreach (Point chest in highlights.Keys) {
				var world = new Vector2(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);
				Vector2 onScreen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
				if (onScreen.X >= 0 && onScreen.Y >= 0 && onScreen.X <= screen.X && onScreen.Y <= screen.Y)
					continue;

				Vector2 direction = onScreen - center;
				float toEdge = MathF.Min((center.X - ArrowMargin) / MathF.Abs(direction.X), (center.Y - ArrowMargin) / MathF.Abs(direction.Y));
				Vector2 position = (center + direction * toEdge) / Main.UIScale;

				spriteBatch.Draw(texture, position, null, Color.White * alpha, direction.ToRotation(), texture.Size() / 2f, 1.5f, SpriteEffects.None, 0f);

				int blocks = (int)(Vector2.Distance(Main.LocalPlayer.Center, world) / 16);
				string text = UIHelpers.T("Blocks", blocks);
				Vector2 textSize = UIHelpers.Measure(text, 0.8f);
				Vector2 textPosition = position - direction.SafeNormalize(Vector2.Zero) * 26 - textSize / 2f;
				Utils.DrawBorderString(spriteBatch, text, textPosition, Color.Gold, 0.8f);
			}
		}

		// Chests are 2 tiles wide; dressers are 3.
		private static int ChestWidth(Point chest) => TileID.Sets.BasicDresser[Main.tile[chest.X, chest.Y].TileType] ? 3 : 2;

		private static void DrawBorder(SpriteBatch spriteBatch, Rectangle area, Color color, int thickness)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Bottom - thickness, area.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, thickness, area.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(area.Right - thickness, area.Y, thickness, area.Height), color);
		}
	}
}

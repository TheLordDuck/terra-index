using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.Content.Building
{
	// Blueprints: right click with the blueprint item opens the catalog (BlueprintState), where a house and its
	// material are chosen. Then, while the item is held, a see-through preview follows the mouse (red where something
	// is in the way) and left click builds it there with materials from the inventory (the catalog can take what is
	// missing out of a terminal's chests). The client checks the place and takes the materials; the server (or single
	// player) checks again and places the tiles, giving the materials back if it can't.
	public class BuildSystem : ModSystem
	{
		// The server lets players build this far from the house's center.
		private const float MaxDistance = 16 * 60;
		private const int MinTicksBetweenBuilds = 30;

		private UserInterface ui;
		// Built on first use: in Load the translations aren't loaded yet, so the texts would show their keys.
		private BlueprintState state;
		private BlueprintState State => state ??= UIHelpers.Build(new BlueprintState());
		private GameTime lastGameTime;
		private readonly uint[] lastBuild = new uint[Main.maxPlayers];

		// Chosen in the catalog. Kept for the session.
		public int Selected { get; set; }
		public int MaterialIndex { get; set; }

		public Blueprint Blueprint => Blueprints.All[Math.Clamp(Selected, 0, Blueprints.All.Length - 1)];
		public BuildMaterial Material => MaterialIndex >= 0 && MaterialIndex < Blueprints.Materials.Count ? Blueprints.Materials[MaterialIndex] : null;
		public bool CatalogOpen => ui?.CurrentState != null;

		private static bool HoldingBlueprint => Main.LocalPlayer.HeldItem.ModItem is BlueprintItem;

		public override void Load()
		{
			if (!Main.dedServ) {
				ui = new UserInterface();
				LanguageManager.Instance.OnLanguageChanged += RebuildPanel;
			}
		}

		public override void Unload()
		{
			if (!Main.dedServ)
				LanguageManager.Instance.OnLanguageChanged -= RebuildPanel;
		}

		private void RebuildPanel(LanguageManager language)
		{
			if (ui?.CurrentState == null)
				state = null;
		}

		public override void OnWorldUnload() => ui?.SetState(null);

		public void OpenCatalog()
		{
			ui.SetState(State);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void CloseCatalog()
		{
			if (ui.CurrentState == null)
				return;
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (ui?.CurrentState == null)
				return;
			if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape)) {
				CloseCatalog();
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Blueprints", () => {
				if (ui?.CurrentState != null && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}, InterfaceScaleType.UI));
		}

		private Point MouseOrigin => Blueprints.Origin(Blueprint, Main.MouseWorld.ToTileCoordinates());

		// Left click with the blueprint item.
		public void TryBuild()
		{
			BuildMaterial material = Material;
			if (material == null || CatalogOpen) {
				OpenCatalog();
				return;
			}

			Blueprint blueprint = Blueprint;
			Point origin = MouseOrigin;
			if (Blueprints.Blocked(blueprint, material, origin).Count > 0) {
				Main.NewText(UIHelpers.T("BuildBlocked"), Color.Orange);
				return;
			}

			// Building is free: the blueprint itself (10 wood) is all it costs.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				TerminalNet.SendBuild(Selected, material.Block, origin);
				return;
			}
			Blueprints.Place(blueprint, material, origin);
			ShowBuildResult(true, Selected, origin);
		}

		// Server: builds it if the place is still clear and the player close enough.
		public void ReceiveBuild(int player, int blueprintIndex, int block, Point origin)
		{
			Player builder = Main.player[player];
			BuildMaterial material = Blueprints.Find(block);
			bool ok = blueprintIndex >= 0 && blueprintIndex < Blueprints.All.Length && material != null
				&& Main.GameUpdateCount - lastBuild[player] >= MinTicksBetweenBuilds;
			if (ok) {
				Blueprint blueprint = Blueprints.All[blueprintIndex];
				var center = new Vector2((origin.X + blueprint.Width / 2f) * 16, (origin.Y + blueprint.Height / 2f) * 16);
				ok = builder.Distance(center) <= MaxDistance && Blueprints.Blocked(blueprint, material, origin).Count == 0;
				if (ok) {
					lastBuild[player] = Main.GameUpdateCount;
					Blueprints.Place(blueprint, material, origin);
				}
			}

			TerminalNet.SendBuildResult(player, ok, blueprintIndex, origin);
		}

		// Client: a puff of dust over the new house, or why it wasn't built.
		public void ShowBuildResult(bool ok, int blueprintIndex, Point origin)
		{
			if (!ok) {
				Main.NewText(UIHelpers.T("BuildFailed"), Color.Orange);
				return;
			}
			Blueprint blueprint = Blueprints.All[Math.Clamp(blueprintIndex, 0, Blueprints.All.Length - 1)];
			var area = new Rectangle(origin.X * 16, origin.Y * 16, blueprint.Width * 16, blueprint.Height * 16);
			for (int i = 0; i < 40; i++)
				Dust.NewDust(new Vector2(area.X, area.Y), area.Width, area.Height, DustID.Smoke, 0f, -1f, 100, default, 1.4f);
			SoundEngine.PlaySound(SoundID.Dig, area.Center.ToVector2());
			Main.NewText(UIHelpers.T("BuildDone", UIHelpers.T("Blueprint" + blueprint.Key)), Color.LightGreen);
		}

		// While the blueprint item is held: the house drawn see-through where it would be built.
		public override void PostDrawTiles()
		{
			if (Main.gameMenu || !HoldingBlueprint || CatalogOpen || Material == null || Main.LocalPlayer.mouseInterface)
				return;

			Blueprint blueprint = Blueprint;
			Point origin = MouseOrigin;
			var blocked = new HashSet<Point>(Blueprints.Blocked(blueprint, Material, origin));

			SpriteBatch spriteBatch = Main.spriteBatch;
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			Vector2 topLeft = new Vector2(origin.X * 16, origin.Y * 16) - Main.screenPosition;
			DrawBlueprint(spriteBatch, blueprint, Material, topLeft, 16, 0.55f, blocked.Count == 0 ? null : cell => blocked.Contains(new Point(origin.X + cell.X, origin.Y + cell.Y)));

			var area = new Rectangle((int)topLeft.X, (int)topLeft.Y, blueprint.Width * 16, blueprint.Height * 16);
			DrawBorder(spriteBatch, area, (blocked.Count == 0 ? Color.LightGreen : Color.Red) * 0.8f);
			string hint = UIHelpers.T(blocked.Count == 0 ? "BlueprintPreviewHint" : "BuildBlocked");
			Utils.DrawBorderString(spriteBatch, hint, new Vector2(area.X, area.Y - 24), blocked.Count == 0 ? Color.White : Color.Orange, 0.8f);
			spriteBatch.End();
		}

		// Draws the plan with the item icons of what goes in each cell, cell pixels per tile: background walls dimmed,
		// then the tiles, furniture over its whole size. Blocked cells (if given) are tinted red. Also used by the
		// catalog for its small preview.
		public static void DrawBlueprint(SpriteBatch spriteBatch, Blueprint blueprint, BuildMaterial material, Vector2 topLeft, float cell, float alpha, Func<Point, bool> blocked = null)
		{
			for (int y = 0; y < blueprint.Height; y++) {
				for (int x = 0; x < blueprint.Width; x++) {
					if (Blueprints.WallBrushes.TryGetValue(blueprint.WallAt(x, y), out Brush brush) && brush.Item(material) is int wall and > 0)
						DrawIcon(spriteBatch, wall, topLeft + new Vector2(x, y) * cell, new Vector2(cell), alpha * 0.5f);
				}
			}
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			for (int y = 0; y < blueprint.Height; y++) {
				for (int x = 0; x < blueprint.Width; x++) {
					var position = topLeft + new Vector2(x, y) * cell;
					if (Blueprints.TileBrushes.TryGetValue(blueprint.TileAt(x, y), out Brush brush) && brush.Item(material) is int item and > 0) {
						Point size = Blueprints.Size(item);
						DrawIcon(spriteBatch, item, position, new Vector2(size.X, size.Y) * cell, alpha);
					}
					if (blocked != null && blocked(new Point(x, y)))
						spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, (int)Math.Ceiling(cell), (int)Math.Ceiling(cell)), Color.Red * 0.45f);
				}
			}
		}

		// The item's icon fitted inside the area, centered.
		private static void DrawIcon(SpriteBatch spriteBatch, int type, Vector2 position, Vector2 area, float alpha)
		{
			Main.instance.LoadItem(type);
			Main.GetItemDrawFrame(type, out Texture2D texture, out Rectangle frame);
			float scale = Math.Min(area.X / frame.Width, area.Y / frame.Height);
			spriteBatch.Draw(texture, position + area / 2f, frame, Color.White * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		private static void DrawBorder(SpriteBatch spriteBatch, Rectangle area, Color color)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Bottom - 2, area.Width, 2), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, 2, area.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(area.Right - 2, area.Y, 2, area.Height), color);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Common;
using TerraIndex.Content.Building;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Blueprint catalog (see BuildSystem), in two tabs. Buildings: which house, which material and a small preview;
	// building is free. Town NPCs: picking one shows the biomes and neighbours it loves, likes, dislikes and hates, as
	// icons, to choose where its house goes.
	public class BlueprintState : UIState
	{
		private UIPanel panel;

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(640, 0);
			panel.Height.Set(660, 0);
			panel.MaxHeight.Set(0, 0.96f);
			panel.HAlign = panel.VAlign = 0.5f;
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("BlueprintTitle"), 1.1f));

			var close = new UITextPanel<string>("X");
			close.Width.Set(34, 0);
			close.Height.Set(30, 0);
			close.HAlign = 1f;
			close.Top.Set(-4, 0);
			close.OnLeftClick += (_, _) => ModContent.GetInstance<BuildSystem>().CloseCatalog();
			panel.Append(close);

			var view = new BlueprintView();
			view.Top.Set(36, 0);
			view.Width.Set(0, 1f);
			view.Height.Set(-36, 1f);
			panel.Append(view);
		}

		public override void Update(GameTime gameTime)
		{
			UIHelpers.Place(panel, 640, 660);
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				UIHelpers.BlockMouse();
		}
	}

	// Drawn by hand, like the recipe view: clickable areas are registered while drawing.
	public class BlueprintView : UIElement
	{
		private const int Slot = 34;
		private const float PreviewHeight = 160;
		private const int Head = 34;
		private const int Icon = 32;

		private readonly List<(Rectangle Area, Action Click)> hotspots = new();

		private static BuildSystem Build => ModContent.GetInstance<BuildSystem>();
		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			// Keeps the chests' counts fresh while a terminal is in reach.
			Terminal.KeepFreshForCrafting();
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			foreach (var (area, click) in hotspots) {
				if (area.Contains(evt.MousePosition.ToPoint())) {
					click();
					SoundEngine.PlaySound(SoundID.MenuTick);
					return;
				}
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			hotspots.Clear();
			CalculatedStyle inner = GetInnerDimensions();
			float left = inner.X;
			float right = inner.X + inner.Width;
			float y = inner.Y;

			// The tabs.
			float x = left;
			foreach (bool npcTab in new[] { false, true }) {
				string label = UIHelpers.T(npcTab ? "BlueprintTabNpcs" : "BlueprintTabBuildings");
				var area = new Rectangle((int)x, (int)y, (int)UIHelpers.Measure(label, 0.9f).X + 24, 30);
				Button(spriteBatch, area, label, true, Build.NpcTab == npcTab);
				hotspots.Add((area, () => Build.NpcTab = npcTab));
				x += area.Width + 6;
			}
			y += 42;
			if (Build.NpcTab) {
				DrawHappiness(spriteBatch, left, right, y);
				return;
			}

			// Which house.
			x = left;
			for (int i = 0; i < Blueprints.All.Length; i++) {
				Blueprint blueprint = Blueprints.All[i];
				string name = $"{UIHelpers.T("Blueprint" + blueprint.Key)} ({blueprint.Width}x{blueprint.Height})";
				int width = (int)UIHelpers.Measure(name, 0.85f).X + 16;
				if (x + width > right) {
					x = left;
					y += 34;
				}
				var area = new Rectangle((int)x, (int)y, width, 28);
				int index = i;
				Button(spriteBatch, area, name, true, i == Build.Selected);
				hotspots.Add((area, () => Build.Selected = index));
				x += width + 6;
			}
			y += 40;

			// Which material.
			Utils.DrawBorderString(spriteBatch, UIHelpers.T("BlueprintMaterial"), new Vector2(left, y + 8), Color.White, 0.85f);
			float materialsLeft = left + UIHelpers.Measure(UIHelpers.T("BlueprintMaterial"), 0.85f).X + 10;
			x = materialsLeft;
			List<BuildMaterial> materials = Blueprints.Materials;
			for (int i = 0; i < materials.Count; i++) {
				if (x + Slot > right) {
					x = materialsLeft;
					y += Slot + 4;
				}
				var area = new Rectangle((int)x, (int)y, Slot, Slot);
				if (i == Build.MaterialIndex)
					Utils.DrawInvBG(spriteBatch, new Rectangle(area.X - 2, area.Y - 2, Slot + 4, Slot + 4), new Color(255, 210, 90) * 0.9f);
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[materials[i].Block], new Vector2(x, y), Slot / 52f);
				int index = i;
				hotspots.Add((area, () => Build.MaterialIndex = index));
				x += Slot + 4;
			}
			y += Slot + 12;

			BuildMaterial material = Build.Material;
			if (material == null) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T(materials.Count == 0 ? "BlueprintNoMaterials" : "BlueprintPickMaterial"), new Vector2(left, y), Color.Orange, 0.85f);
				return;
			}

			// Small preview.
			Blueprint chosen = Build.Blueprint;
			float cell = Math.Min(10f, Math.Min(inner.Width / chosen.Width, PreviewHeight / chosen.Height));
			var topLeft = new Vector2(left + (inner.Width - chosen.Width * cell) / 2f, y + (PreviewHeight - chosen.Height * cell) / 2f);
			BuildSystem.DrawBlueprint(spriteBatch, chosen, material, topLeft, cell, 1f);
			y += PreviewHeight + 8;

			Utils.DrawBorderString(spriteBatch, UIHelpers.T("BlueprintFree"), new Vector2(left, y), new Color(140, 255, 140), 0.85f);
			y += 30;

			// Buttons.
			x = left;
			string place = UIHelpers.T("BlueprintPlace");
			var placeArea = new Rectangle((int)x, (int)y, (int)UIHelpers.Measure(place, 0.85f).X + 20, 30);
			Button(spriteBatch, placeArea, place, true, false);
			hotspots.Add((placeArea, () => Build.CloseCatalog()));
			y += 40;

			foreach (string hintLine in UIHelpers.Wrap(UIHelpers.T("BlueprintHint"), inner.Width, 0.75f)) {
				Utils.DrawBorderString(spriteBatch, hintLine, new Vector2(left, y), Color.LightGray, 0.75f);
				y += 20;
			}
		}

		private static readonly Color[] LevelColors = { new(120, 255, 120), new(190, 240, 170), new(255, 190, 110), new(255, 110, 100) };

		// The town NPCs' heads; under them, the picked NPC's name and one row of icons per level: biomes, then neighbours.
		// Hovering an icon names it; clicking a neighbour shows that NPC instead.
		private void DrawHappiness(SpriteBatch spriteBatch, float left, float right, float y)
		{
			List<int> npcs = Happiness.Npcs;
			if (npcs.Count == 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessNone"), new Vector2(left, y), Color.LightGray, 0.85f);
				return;
			}
			foreach (string line in UIHelpers.Wrap(UIHelpers.T("HappinessTitle"), right - left, 0.85f)) {
				Utils.DrawBorderString(spriteBatch, line, new Vector2(left, y), Color.White, 0.85f);
				y += 22;
			}
			y += 6;

			float x = left;
			foreach (int type in npcs) {
				if (x + Head > right) {
					x = left;
					y += Head + 4;
				}
				var area = new Rectangle((int)x, (int)y, Head, Head);
				int npc = type;
				DrawNpcHead(spriteBatch, area, type, type == Build.HappinessNpc ? new Color(100, 120, 210) : new Color(55, 65, 130));
				hotspots.Add((area, () => Build.HappinessNpc = Build.HappinessNpc == npc ? 0 : npc));
				x += Head + 4;
			}
			y += Head + 16;

			int picked = Build.HappinessNpc;
			if (picked <= 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessPick"), new Vector2(left, y), Color.LightGray, 0.85f);
				return;
			}
			Utils.DrawBorderString(spriteBatch, Lang.GetNPCNameValue(picked), new Vector2(left, y), new Color(255, 230, 120), 1f);
			y += 30;

			// The level names in a column, the icons after the widest one.
			float iconsLeft = left + Happiness.Levels.Max(level => UIHelpers.Measure(UIHelpers.T("Happiness" + level), 0.85f).X) + 12;
			bool any = false;
			for (int i = 0; i < Happiness.Levels.Length; i++) {
				AffectionLevel level = Happiness.Levels[i];
				List<IShoppingBiome> biomes = Happiness.Biomes(picked, level);
				List<int> neighbours = Happiness.Neighbours(picked, level);
				if (biomes.Count == 0 && neighbours.Count == 0)
					continue;
				any = true;
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("Happiness" + level), new Vector2(left, y + 8), LevelColors[i], 0.85f);
				Color background = Color.Lerp(new Color(55, 65, 130), LevelColors[i], 0.25f);
				x = iconsLeft;
				foreach (IShoppingBiome biome in biomes) {
					string name = Happiness.BiomeName(biome);
					bool icon = Happiness.BiomeIcon(biome, out Texture2D texture, out Rectangle frame);
					int width = icon ? Icon : (int)UIHelpers.Measure(name, 0.75f).X + 12;
					if (x + width > right) {
						x = iconsLeft;
						y += Icon + 4;
					}
					var area = new Rectangle((int)x, (int)y, width, Icon);
					Utils.DrawInvBG(spriteBatch, area, background * 0.95f);
					if (icon)
						DrawFitted(spriteBatch, texture, frame, area);
					else
						Utils.DrawBorderString(spriteBatch, name, new Vector2(area.X + 6, area.Y + 8), Color.White, 0.75f);
					if (area.Contains(Main.MouseScreen.ToPoint()))
						Main.hoverItemName = name;
					x += width + 4;
				}
				foreach (int neighbour in neighbours) {
					if (x + Icon > right) {
						x = iconsLeft;
						y += Icon + 4;
					}
					var area = new Rectangle((int)x, (int)y, Icon, Icon);
					DrawNpcHead(spriteBatch, area, neighbour, background);
					if (npcs.Contains(neighbour)) {
						int npc = neighbour;
						hotspots.Add((area, () => Build.HappinessNpc = npc));
					}
					x += Icon + 4;
				}
				y += Icon + 8;
			}
			if (!any) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessNone"), new Vector2(left, y), Color.LightGray, 0.85f);
				y += 24;
			}
			y += 4;
			foreach (string line in UIHelpers.Wrap(UIHelpers.T("HappinessCrowd"), right - left, 0.75f)) {
				Utils.DrawBorderString(spriteBatch, line, new Vector2(left, y), Color.LightGray, 0.75f);
				y += 20;
			}
		}

		// A town NPC's head on a slot (its name when hovered), or its name's first letter if it has no head.
		private static void DrawNpcHead(SpriteBatch spriteBatch, Rectangle area, int type, Color background)
		{
			bool hovered = area.Contains(Main.MouseScreen.ToPoint());
			Utils.DrawInvBG(spriteBatch, area, (hovered ? Color.Lerp(background, Color.White, 0.2f) : background) * 0.95f);
			string name = Lang.GetNPCNameValue(type);
			Texture2D texture = Happiness.Head(type);
			if (texture != null)
				DrawFitted(spriteBatch, texture, texture.Bounds, area);
			else if (name.Length > 0)
				Utils.DrawBorderString(spriteBatch, name[..1], area.Center.ToVector2() - UIHelpers.Measure(name[..1], 0.9f) / 2f + new Vector2(0, 2), Color.White, 0.9f);
			if (hovered)
				Main.hoverItemName = name;
		}

		// Draws a texture frame centered in the area, shrunk to leave a small margin.
		private static void DrawFitted(SpriteBatch spriteBatch, Texture2D texture, Rectangle frame, Rectangle area)
		{
			float scale = Math.Min(1f, (area.Height - 6) / (float)Math.Max(frame.Width, frame.Height));
			spriteBatch.Draw(texture, area.Center.ToVector2(), frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		private static void Button(SpriteBatch spriteBatch, Rectangle area, string text, bool enabled, bool selected)
		{
			bool hovered = enabled && area.Contains(Main.MouseScreen.ToPoint());
			Color background = selected ? new Color(100, 120, 210) : hovered ? new Color(80, 95, 170) : new Color(55, 65, 130);
			Utils.DrawInvBG(spriteBatch, area, background * 0.95f);
			Vector2 size = UIHelpers.Measure(text, 0.85f);
			Utils.DrawBorderString(spriteBatch, text, new Vector2(area.Center.X - size.X / 2, area.Y + (area.Height - 20) / 2 + 2), enabled ? Color.White : Color.Gray, 0.85f);
		}
	}
}

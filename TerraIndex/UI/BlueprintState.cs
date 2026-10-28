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
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Blueprint catalog (see BuildSystem): which house, which material and a small preview. Building is free.
	// Below, the town NPCs: picking one shows the biomes and neighbours it loves, likes, dislikes and hates, to choose
	// where its house goes.
	public class BlueprintState : UIState
	{
		private UIPanel panel;

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(640, 0);
			panel.Height.Set(760, 0);
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
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}

	// Drawn by hand, like the recipe view: clickable areas are registered while drawing.
	public class BlueprintView : UIElement
	{
		private const int Slot = 34;
		private const float PreviewHeight = 160;
		private const int Head = 28;

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

			// Which house.
			float x = left;
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
			y += 10;

			DrawHappiness(spriteBatch, left, right, y);
		}

		private static readonly Color[] LevelColors = { new(120, 255, 120), new(190, 240, 170), new(255, 190, 110), new(255, 110, 100) };

		// The town NPCs' heads; under them, the picked NPC's preferences, one line per level.
		private void DrawHappiness(SpriteBatch spriteBatch, float left, float right, float y)
		{
			List<int> npcs = Happiness.Npcs;
			if (npcs.Count == 0)
				return;
			Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessTitle"), new Vector2(left, y), Color.White, 0.85f);
			y += 26;

			float x = left;
			foreach (int type in npcs) {
				if (x + Head > right) {
					x = left;
					y += Head + 4;
				}
				var area = new Rectangle((int)x, (int)y, Head, Head);
				bool picked = type == Build.HappinessNpc;
				bool hovered = area.Contains(Main.MouseScreen.ToPoint());
				Utils.DrawInvBG(spriteBatch, area, (picked ? new Color(100, 120, 210) : hovered ? new Color(80, 95, 170) : new Color(55, 65, 130)) * 0.95f);
				Texture2D texture = TextureAssets.NpcHead[NPC.TypeToDefaultHeadIndex(type)].Value;
				float scale = Math.Min(1f, (Head - 6) / (float)Math.Max(texture.Width, texture.Height));
				spriteBatch.Draw(texture, area.Center.ToVector2(), null, Color.White, 0f, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
				if (hovered)
					Main.hoverItemName = Lang.GetNPCNameValue(type);
				int npc = type;
				hotspots.Add((area, () => Build.HappinessNpc = Build.HappinessNpc == npc ? 0 : npc));
				x += Head + 4;
			}
			y += Head + 10;

			if (Build.HappinessNpc <= 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessPick"), new Vector2(left, y), Color.LightGray, 0.75f);
				return;
			}
			bool any = false;
			for (int i = 0; i < Happiness.Levels.Length; i++) {
				List<string> names = Happiness.Names(Build.HappinessNpc, Happiness.Levels[i]);
				if (names.Count == 0)
					continue;
				any = true;
				string text = UIHelpers.T("Happiness" + Happiness.Levels[i], string.Join(", ", names));
				foreach (string line in UIHelpers.Wrap(text, right - left, 0.75f)) {
					Utils.DrawBorderString(spriteBatch, line, new Vector2(left, y), LevelColors[i], 0.75f);
					y += 20;
				}
			}
			if (!any) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("HappinessNone"), new Vector2(left, y), Color.LightGray, 0.75f);
				y += 20;
			}
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(UIHelpers.T("HappinessCrowd"), right - left, 0.75f), new Vector2(left, y), Color.LightGray, 0.75f);
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

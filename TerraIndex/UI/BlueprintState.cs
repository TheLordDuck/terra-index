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
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Blueprint catalog (see BuildSystem): which house, which material, a small preview and what it costs.
	public class BlueprintState : UIState
	{
		private UIPanel panel;

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(640, 0);
			panel.Height.Set(620, 0);
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
	// Cost colors as in the recipe index: green = in the inventory, blue = the rest is in a terminal's chests, red = missing.
	public class BlueprintView : UIElement
	{
		private const int Slot = 34;
		private const float PreviewHeight = 160;
		private const int LineHeight = 24;

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

			// What it costs, and what the chests can add.
			Utils.DrawBorderString(spriteBatch, UIHelpers.T("BlueprintCost"), new Vector2(left, y), new Color(255, 220, 120), 0.85f);
			y += 26;
			Dictionary<int, int> inventory = CraftingPlan.Inventory(Main.LocalPlayer);
			List<CostLine> lines = Blueprints.Cost(chosen.Items(material), inventory);
			int[] missing = Blueprints.Allocate(lines, inventory);
			var stored = new Dictionary<int, int>();
			if (Terminal.CanCraft) {
				foreach (Item item in Terminal.StoredItems)
					stored[item.type] = item.stack;
			}
			var take = new List<(int Type, int Amount)>();
			int[] stillMissing = Blueprints.Allocate(lines.Select((line, i) => line with { Amount = missing[i] }).ToList(), stored, take);

			// Two columns: the big designs take many different materials.
			float columnWidth = inner.Width / 2f;
			for (int i = 0; i < lines.Count; i++) {
				CostLine line = lines[i];
				int shown = line.Group != null ? UIHelpers.Cycle(line.Group) : line.Type;
				float lineX = left + i % 2 * columnWidth;
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[shown], new Vector2(lineX, y), 0.42f);
				string text = UIHelpers.T("BlueprintHave", line.Name, line.Amount - missing[i], line.Amount);
				int fromChests = missing[i] - stillMissing[i];
				if (fromChests > 0)
					text += "  " + UIHelpers.T("BlueprintInChests", fromChests);
				Color color = missing[i] == 0 ? new Color(140, 255, 140) : stillMissing[i] == 0 ? new Color(120, 180, 255) : new Color(255, 120, 120);
				Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(text, columnWidth - 34, 0.8f), new Vector2(lineX + 28, y + 3), color, 0.8f);
				if (i % 2 == 1 || i == lines.Count - 1)
					y += LineHeight;
			}
			y += 8;

			// Buttons.
			x = left;
			if (take.Count > 0) {
				string label = UIHelpers.T("TakeMissing");
				var area = new Rectangle((int)x, (int)y, (int)UIHelpers.Measure(label, 0.85f).X + 20, 30);
				Button(spriteBatch, area, label, true, false);
				hotspots.Add((area, () => Terminal.RequestTakeItems(take)));
				x += area.Width + 8;
			}
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

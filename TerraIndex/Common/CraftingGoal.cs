using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace TerraIndex.Common
{
	// Crafting goal ("shopping list"), set from the recipe index's tree view. Saved with the character.
	public class GoalPlayer : ModPlayer
	{
		public int ItemType { get; private set; }
		// Which of the item's recipes (page in the index), since some items have several.
		public int RecipeIndex { get; private set; }

		public Recipe Recipe {
			get {
				if (ItemType <= 0)
					return null;
				List<Recipe> recipes = RecipeDatabase.RecipesFor(ItemType);
				return recipes.Count == 0 ? null : recipes[Math.Clamp(RecipeIndex, 0, recipes.Count - 1)];
			}
		}

		public bool IsGoal(int type, int recipeIndex) => ItemType == type && RecipeIndex == recipeIndex;

		public void Set(int type, int recipeIndex)
		{
			ItemType = type;
			RecipeIndex = recipeIndex;
		}

		public void Clear() => ItemType = 0;

		// Goal panel: where the player dragged it and whether it is folded down to its title. Saved with the character.
		public static readonly Point DefaultPanelPosition = new(20, 300);
		public Point PanelPosition = DefaultPanelPosition;
		public bool PanelCollapsed;

		public override void SaveData(TagCompound tag)
		{
			if (PanelPosition != DefaultPanelPosition) {
				tag["panelX"] = PanelPosition.X;
				tag["panelY"] = PanelPosition.Y;
			}
			if (PanelCollapsed)
				tag["panelCollapsed"] = true;
			if (ItemType <= 0)
				return;
			// Saved as an item so modded goals survive the mod's item IDs changing between sessions.
			tag["goal"] = ItemIO.Save(new Item(ItemType));
			tag["recipe"] = RecipeIndex;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("panelX"))
				PanelPosition = new Point(tag.GetInt("panelX"), tag.GetInt("panelY"));
			PanelCollapsed = tag.GetBool("panelCollapsed");
			if (!tag.ContainsKey("goal"))
				return;
			Item item = ItemIO.Load(tag.GetCompound("goal"));
			// The item's mod was removed: forget the goal.
			if (item.IsAir || item.ModItem is UnloadedItem)
				return;
			Set(item.type, tag.GetInt("recipe"));
		}
	}

	// Recomputes the goal's plan twice a second and draws what is still missing (hidden while the inventory is open,
	// since it would sit under it). The panel is dragged by its title and folded with the button on its right.
	public class GoalSystem : ModSystem
	{
		private const int RefreshInterval = 30;
		private const int Width = 230;
		private const int TitleHeight = 38;
		private const int RowHeight = 24;
		private const int ToggleSize = 18;

		private CraftingPlan plan;
		private int timer;
		private bool wasReady;

		// Layout from the last draw, used for the mouse.
		private Rectangle panelArea;
		private Rectangle toggleArea;
		private bool dragging;
		private Vector2 dragOffset;

		private static GoalPlayer Goal => Main.LocalPlayer.GetModPlayer<GoalPlayer>();

		private bool Visible => plan != null && ClientConfig.Instance.ShowGoalPanel && !Main.playerInventory && !Main.LocalPlayer.dead;

		public override void OnWorldUnload()
		{
			plan = null;
			dragging = false;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.gameMenu)
				return;
			HandleMouse();
			if (--timer > 0)
				return;
			timer = RefreshInterval;

			GoalPlayer goal = Goal;
			Recipe recipe = goal.Recipe;
			if (recipe == null) {
				plan = null;
				return;
			}

			plan = new CraftingPlan(goal.ItemType, recipe, CraftingPlan.Inventory(Main.LocalPlayer));
			if (plan.Ready && !wasReady)
				Main.NewText(UIHelpers.T("GoalReady", Lang.GetItemNameValue(goal.ItemType)), Color.LightGreen);
			wasReady = plan.Ready;
		}

		// Called when the goal changes, so the panel updates straight away.
		public void Refresh()
		{
			timer = 0;
			wasReady = false;
		}

		private void HandleMouse()
		{
			if (!Visible) {
				dragging = false;
				return;
			}

			if (dragging) {
				if (Main.mouseLeft)
					Goal.PanelPosition = (Main.MouseScreen - dragOffset).ToPoint();
				else
					dragging = false;
				Main.LocalPlayer.mouseInterface = true;
				return;
			}

			Point mouse = Main.MouseScreen.ToPoint();
			if (!panelArea.Contains(mouse))
				return;
			// Over the panel the mouse is ours: no swinging the held item.
			Main.LocalPlayer.mouseInterface = true;
			if (!Main.mouseLeft || !Main.mouseLeftRelease)
				return;
			Main.mouseLeftRelease = false;

			if (toggleArea.Contains(mouse)) {
				Goal.PanelCollapsed = !Goal.PanelCollapsed;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}
			else if (mouse.Y < panelArea.Y + TitleHeight) {
				dragging = true;
				dragOffset = Main.MouseScreen - panelArea.Location.ToVector2();
			}
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Goal", () => {
				if (Visible)
					Draw(Main.spriteBatch);
				else
					panelArea = Rectangle.Empty;
				return true;
			}, InterfaceScaleType.UI));
		}

		private void Draw(SpriteBatch spriteBatch)
		{
			GoalPlayer goal = Goal;
			int maxRows = ClientConfig.Instance.GoalPanelRows;
			List<PlanMaterial> missing = plan.Materials.FindAll(material => material.Missing > 0);
			int rows = goal.PanelCollapsed ? 0 : plan.Ready ? 1 : Math.Min(missing.Count, maxRows) + (missing.Count > maxRows ? 1 : 0);
			int height = TitleHeight + (rows > 0 ? 6 + rows * RowHeight : 0);

			// Kept on screen, also after a resolution or UI scale change.
			int screenWidth = (int)(Main.screenWidth / Main.UIScale);
			int screenHeight = (int)(Main.screenHeight / Main.UIScale);
			goal.PanelPosition = new Point(
				Math.Clamp(goal.PanelPosition.X, 0, Math.Max(0, screenWidth - Width)),
				Math.Clamp(goal.PanelPosition.Y, 0, Math.Max(0, screenHeight - TitleHeight)));
			panelArea = new Rectangle(goal.PanelPosition.X, goal.PanelPosition.Y, Width, height);
			toggleArea = new Rectangle(panelArea.Right - ToggleSize - 8, panelArea.Y + (TitleHeight - ToggleSize) / 2, ToggleSize, ToggleSize);
			Utils.DrawInvBG(spriteBatch, panelArea, new Color(40, 45, 100) * (dragging ? 0.9f : 0.75f));

			float x = panelArea.X + 8;
			float y = panelArea.Y + 4;
			UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[plan.Root.Type], new Vector2(x, y), 0.6f);
			string title = UIHelpers.Fit(UIHelpers.T("GoalTitle", Lang.GetItemNameValue(plan.Root.Type)), toggleArea.X - x - 44, 0.85f);
			// Folded, the title turns green once everything is ready, since the list isn't visible.
			Color titleColor = goal.PanelCollapsed && plan.Ready ? new Color(140, 255, 140) : new Color(255, 220, 120);
			Utils.DrawBorderString(spriteBatch, title, new Vector2(x + 38, y + 10), titleColor, 0.85f);

			bool toggleHovered = toggleArea.Contains(Main.MouseScreen.ToPoint());
			Utils.DrawInvBG(spriteBatch, toggleArea, (toggleHovered ? new Color(80, 95, 170) : new Color(55, 65, 130)) * 0.95f);
			string toggle = goal.PanelCollapsed ? "+" : "-";
			Vector2 toggleSize = UIHelpers.Measure(toggle, 0.8f);
			Utils.DrawBorderString(spriteBatch, toggle, toggleArea.Center.ToVector2() - toggleSize / 2 + new Vector2(0, 2), Color.White, 0.8f);
			if (toggleHovered)
				Main.hoverItemName = UIHelpers.T(goal.PanelCollapsed ? "GoalExpand" : "GoalCollapse");

			if (rows == 0)
				return;
			y = panelArea.Y + TitleHeight + 2;
			if (plan.Ready) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("GoalReadyShort"), new Vector2(x, y + 2), new Color(140, 255, 140), 0.8f);
				return;
			}

			for (int i = 0; i < Math.Min(missing.Count, maxRows); i++) {
				PlanMaterial material = missing[i];
				int shown = material.Group != null ? UIHelpers.Cycle(material.Group) : material.Type;
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[shown], new Vector2(x, y), 0.45f);
				string text = UIHelpers.Fit($"{material.Name}: {material.Owned}/{material.Needed}", Width - 46, 0.75f);
				Utils.DrawBorderString(spriteBatch, text, new Vector2(x + 30, y + 4), Color.White, 0.75f);
				y += RowHeight;
			}
			if (missing.Count > maxRows)
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("GoalMore", missing.Count - maxRows), new Vector2(x, y + 2), Color.LightGray, 0.75f);
		}
	}
}

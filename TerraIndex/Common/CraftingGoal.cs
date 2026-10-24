using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Content.Storage;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
	// Above the list, a button crafts the next step towards the goal (see FindStep), so it is made one step at a time:
	// first the bars, then the sword.
	public class GoalSystem : ModSystem
	{
		private const int RefreshInterval = 30;
		private const int Width = 230;
		private const int TitleHeight = 38;
		private const int RowHeight = 24;
		private const int ToggleSize = 18;
		// After a click the button waits a moment, so a double click doesn't craft twice before the plan catches up
		// (items crafted from the chests take a while to come out of the terminal).
		private const int StepCooldownTicks = 45;

		private CraftingPlan plan;
		private int timer;
		private bool wasReady;
		// What the current plan was computed from: it is only recomputed when the goal or the inventory changes.
		private (int Item, int Recipe, int Inventory) planKey;

		// Layout from the last draw, used for the mouse.
		private Rectangle panelArea;
		private Rectangle toggleArea;
		private Rectangle stepArea;
		private bool dragging;
		private Vector2 dragOffset;

		// Next step towards the goal, worked out on every refresh. When none can be made but one only lacks its crafting
		// station, that station is shown instead (-1 = none).
		private PlanNode step;
		private bool stepFromChests;
		private int missingStation = -1;
		private int stepCooldown;

		private static GoalPlayer Goal => Main.LocalPlayer.GetModPlayer<GoalPlayer>();
		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		private bool Visible => plan != null && ClientConfig.Instance.ShowGoalPanel && !Main.playerInventory && !Main.LocalPlayer.dead;

		public override void OnWorldUnload()
		{
			plan = null;
			step = null;
			dragging = false;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.gameMenu)
				return;
			HandleMouse();
			if (stepCooldown > 0)
				stepCooldown--;
			if (--timer > 0)
				return;
			timer = RefreshInterval;

			GoalPlayer goal = Goal;
			Recipe recipe = goal.Recipe;
			if (recipe == null) {
				plan = null;
				step = null;
				return;
			}

			var key = (goal.ItemType, goal.RecipeIndex, InventoryHash(Main.LocalPlayer));
			if (plan == null || key != planKey) {
				planKey = key;
				plan = new CraftingPlan(goal.ItemType, recipe, CraftingPlan.Inventory(Main.LocalPlayer));
				if (plan.Ready && !wasReady)
					Main.NewText(UIHelpers.T("GoalReady", Lang.GetItemNameValue(goal.ItemType)), Color.LightGreen);
				wasReady = plan.Ready;
			}

			// The stations nearby and the chests can change without the inventory changing. Vanilla only updates the
			// nearby stations while the inventory is open, and the terminal's contents only while one of its panels is.
			Main.LocalPlayer.AdjTiles();
			if (Terminal.CanCraft && !Terminal.IsOpen)
				Terminal.RequestRefresh();
			FindStep();
		}

		// The deepest step of the tree that can be made now (children before the item they make): from the inventory,
		// with its crafting station nearby (or on the wire of a terminal in reach), or else from that terminal's chests.
		private void FindStep()
		{
			step = null;
			missingStation = -1;
			FindStep(plan.Root);
		}

		private bool FindStep(PlanNode node)
		{
			if (node.Recipe == null)
				return false;
			foreach (PlanNode child in node.Children) {
				if (FindStep(child))
					return true;
			}

			Recipe recipe = node.Recipe;
			bool inInventory = node.Children.All(child => child.Recipe == null && child.Owned >= child.Needed);
			if (inInventory) {
				int station = recipe.requiredTile.FirstOrDefault(tile => !HasStation(tile), -1);
				if (station < 0 && recipe.Conditions.All(condition => condition.IsMet())) {
					step = node;
					stepFromChests = false;
					return true;
				}
				if (station >= 0 && missingStation < 0)
					missingStation = station;
			}
			if (Terminal.CanCraft && Terminal.MaxCrafts(recipe) >= node.Crafts) {
				step = node;
				stepFromChests = true;
				return true;
			}
			return false;
		}

		// The terminal's wire only counts while the terminal is in reach.
		private static bool HasStation(int tile) => Terminal.CanCraft ? Terminal.HasStation(tile) : Main.LocalPlayer.adjTile[tile];

		private void CraftStep()
		{
			if (step == null || stepCooldown > 0)
				return;
			stepCooldown = StepCooldownTicks;
			Recipe recipe = step.Recipe;
			int times = step.Crafts;
			if (stepFromChests) {
				Terminal.RequestCraft(recipe, times);
				return;
			}

			// FindStep saw the ingredients, but the inventory may have changed since.
			Player player = Main.LocalPlayer;
			if (!TakeFromInventory(player, recipe, times))
				return;
			// Like vanilla crafting: a copy of the result, with a random prefix for weapons and accessories.
			for (int total = recipe.createItem.stack * times; total > 0;) {
				Item item = recipe.createItem.Clone();
				item.stack = Math.Min(total, item.maxStack);
				item.Prefix(-1);
				total -= item.stack;
				player.QuickSpawnItem(new EntitySource_Misc("TerraIndex/Goal"), item, item.stack);
			}
			SoundEngine.PlaySound(SoundID.Grab);
			Main.NewText(UIHelpers.T("GoalCrafted", recipe.createItem.stack * times, Lang.GetItemNameValue(recipe.createItem.type)), Color.LightGreen);
			timer = 0;
		}

		// Takes the ingredients of `times` crafts out of the inventory, any member counting for groups: all or nothing.
		private static bool TakeFromInventory(Player player, Recipe recipe, int times)
		{
			Dictionary<int, int> owned = CraftingPlan.Inventory(player);
			var taking = new List<(int Type, int Amount)>();
			foreach (Item ingredient in recipe.requiredItem) {
				int needed = ingredient.stack * times;
				foreach (int type in RecipeDatabase.AcceptedItems(recipe, ingredient.type)) {
					int take = Math.Min(owned.GetValueOrDefault(type), needed);
					if (take <= 0)
						continue;
					owned[type] -= take;
					needed -= take;
					taking.Add((type, take));
					if (needed == 0)
						break;
				}
				if (needed > 0)
					return false;
			}

			TerminalSystem.TakeFromInventory(player, taking);
			return true;
		}

		// Called when the goal changes, so the panel updates straight away.
		public void Refresh()
		{
			timer = 0;
			wasReady = false;
			plan = null;
		}

		private static int InventoryHash(Player player)
		{
			var hash = new HashCode();
			foreach (Item item in player.inventory)
				hash.Add(item.IsAir ? 0 : (item.type, item.stack).GetHashCode());
			return hash.ToHashCode();
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

			if (stepArea.Contains(mouse)) {
				CraftStep();
			}
			else if (toggleArea.Contains(mouse)) {
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
			List<PlanMaterial> missing = plan.MissingMaterials;
			int rows = goal.PanelCollapsed ? 0 : plan.Ready ? 1 : Math.Min(missing.Count, maxRows) + (missing.Count > maxRows ? 1 : 0);
			bool stepRow = !goal.PanelCollapsed && (step != null || missingStation >= 0);
			if (stepRow)
				rows++;
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

			stepArea = Rectangle.Empty;
			if (rows == 0)
				return;
			y = panelArea.Y + TitleHeight + 2;
			if (stepRow) {
				DrawStep(spriteBatch, x, y);
				y += RowHeight;
			}
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

		// "Craft 3 x Iron Bar" button, or what station the next step needs.
		private void DrawStep(SpriteBatch spriteBatch, float x, float y)
		{
			float maxWidth = Width - 16;
			if (step == null) {
				string needs = UIHelpers.Fit(UIHelpers.T("GoalNeedsStation", RecipeDatabase.TileName(missingStation)), maxWidth, 0.75f);
				Utils.DrawBorderString(spriteBatch, needs, new Vector2(x, y + 4), Color.Orange, 0.75f);
				return;
			}

			stepArea = new Rectangle((int)x, (int)y, (int)maxWidth, RowHeight - 2);
			bool hovered = stepCooldown == 0 && stepArea.Contains(Main.MouseScreen.ToPoint());
			Color background = stepCooldown > 0 ? new Color(45, 50, 90) : hovered ? new Color(70, 140, 90) : new Color(50, 110, 70);
			Utils.DrawInvBG(spriteBatch, stepArea, background * 0.95f);

			Recipe recipe = step.Recipe;
			UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[recipe.createItem.type], new Vector2(x + 2, y), 0.42f);
			string key = stepFromChests ? "GoalStepChests" : "GoalStep";
			string text = UIHelpers.T(key, recipe.createItem.stack * step.Crafts, Lang.GetItemNameValue(recipe.createItem.type));
			Color color = stepCooldown > 0 ? Color.Gray : Color.White;
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(text, maxWidth - 34, 0.75f), new Vector2(x + 30, y + 4), color, 0.75f);
		}
	}
}

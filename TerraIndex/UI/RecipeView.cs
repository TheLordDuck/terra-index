using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Right-hand panel: shows the recipes (or uses) of an item, one per page, or the full crafting tree of the
	// recipe on the current page (Tree tab), from where it can be set as the crafting goal (see GoalSystem).
	// Clickable areas are registered while drawing, because the layout depends on the recipe.
	public class RecipeView : UIPanel
	{
		private const float SlotScale = 0.6f;
		private const int RowHeight = 34;
		private const int ButtonHeight = 28;

		private const int TreeRowHeight = 28;
		private const int TreeIndent = 14;
		private const int MaxTreeIndent = 10;
		private const int TreeRefreshTicks = 30;

		private readonly record struct Hotspot(Rectangle Area, Action Left, Action Right);

		// One line of the tree view: a section label, or an item (Type > 0, clickable) or plain text (Type 0).
		private readonly record struct TreeRow(bool IsLabel, int Depth, int Type, RecipeGroup Group, string Text, Color Color);

		private readonly List<Hotspot> hotspots = new();
		private readonly Stack<(int Item, bool Uses, int Page)> history = new();
		private int item;
		private bool uses;
		private int page;

		// First row shown in the "how to obtain" list (items that can't be crafted), scrolled with the mouse wheel.
		private int sourceScroll;
		private bool showingSources;

		// Tree view: the full crafting tree of the recipe on the current page, recomputed twice a second
		// so it follows the inventory. Scrolled with the mouse wheel.
		private bool tree;
		private int treeScroll;
		private CraftingPlan treePlan;
		private List<TreeRow> treeRows;
		private (int Item, int Page) treeKey;
		private uint treeTime;

		// Compare view: up to MaxCompared items side by side (stats table + tooltip text), opened from the header's
		// Compare button. Stays filled while browsing, so items can be added one by one.
		private const int MaxCompared = 3;
		private const int CompareLabelWidth = 140;
		private enum CompareKind { Label, Items, Stat, Text }
		private readonly record struct CompareRow(CompareKind Kind, string Text, Color Color, string[] Values = null, Color[] ValueColors = null);

		// Better: 1 = higher is better, -1 = lower is better, 0 = not highlighted. A value of 0 means "doesn't apply" ("-").
		private static readonly (string Key, Func<Item, float> Get, int Better, Func<float, string> Format)[] Stats = {
			("CompareDamage", i => i.damage, 1, v => $"{v}"),
			("CompareCrit", i => i.damage > 0 ? i.crit + 4 : 0, 1, v => $"{v}%"),
			("CompareUseTime", i => i.damage > 0 || i.pick > 0 || i.axe > 0 || i.hammer > 0 ? i.useAnimation : 0, -1, v => $"{v}"),
			("CompareKnockback", i => i.knockBack, 1, v => v.ToString("0.#")),
			("CompareMana", i => i.mana, -1, v => $"{v}"),
			("CompareVelocity", i => i.shootSpeed, 1, v => v.ToString("0.#")),
			("ComparePickaxe", i => i.pick, 1, v => $"{v}%"),
			("CompareAxe", i => i.axe * 5, 1, v => $"{v}%"),
			("CompareHammer", i => i.hammer, 1, v => $"{v}%"),
			("CompareDefense", i => i.defense, 1, v => $"{v}"),
			("CompareValue", i => i.value / 5, 0, v => UIHelpers.Coins((long)v)),
		};

		private readonly List<int> compared = new();
		private bool compare;
		// Reading and word-wrapping tooltips every frame is costly: the rows are kept until something changes.
		private List<CompareRow> compareRows;
		private (string Items, float Width, GameCulture Culture) compareKey;
		private int compareScroll;

		private List<Recipe> Recipes => uses ? RecipeDatabase.UsesOf(item) : RecipeDatabase.RecipesFor(item);

		// The last storage terminal used: when it is in reach, recipes can be crafted from its chests.
		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();
		private static readonly Color InChests = new(120, 210, 255);

		public void Show(int type, bool showUses)
		{
			if (type <= 0)
				return;
			if (type == item && showUses == uses) {
				// Clicking the current item from the tree or the comparison goes back to its recipe.
				if (tree || compare) {
					tree = compare = false;
					SoundEngine.PlaySound(SoundID.MenuTick);
				}
				return;
			}
			if (item > 0)
				history.Push((item, uses, page));
			item = type;
			uses = showUses;
			page = 0;
			sourceScroll = 0;
			tree = compare = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void Back()
		{
			if (history.Count == 0)
				return;
			(item, uses, page) = history.Pop();
			sourceScroll = 0;
			tree = compare = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void SetMode(bool showUses)
		{
			if (uses == showUses && !tree && !compare)
				return;
			if (uses != showUses)
				page = 0;
			uses = showUses;
			tree = compare = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		// Adds the current item to the comparison (dropping the oldest when full) and shows it.
		private void OpenCompare()
		{
			if (!compared.Contains(item)) {
				if (compared.Count >= MaxCompared)
					compared.RemoveAt(0);
				compared.Add(item);
			}
			compare = true;
			tree = false;
			compareScroll = 0;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void RemoveCompared(int type)
		{
			compared.Remove(type);
			if (compared.Count == 0)
				compare = false;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		// Keeps the recipe page that was showing, so the tree is for the recipe the player was looking at.
		private void ShowTree()
		{
			if (tree)
				return;
			if (uses) {
				uses = false;
				page = 0;
			}
			compare = false;
			tree = true;
			treeScroll = 0;
			treePlan = null;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void ToggleGoal(GoalPlayer goal, bool isGoal)
		{
			if (isGoal)
				goal.Clear();
			else
				goal.Set(item, page);
			ModContent.GetInstance<GoalSystem>().Refresh();
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void TurnPage(int delta)
		{
			int count = Recipes.Count;
			if (count > 1)
				page = (page + delta + count) % count;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				PlayerInput.LockVanillaMouseScroll("TerraIndex/RecipeView");
			Terminal.KeepFreshForCrafting();
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (compare)
				compareScroll = Math.Max(0, compareScroll - Math.Sign(evt.ScrollWheelValue));
			else if (tree)
				treeScroll = Math.Max(0, treeScroll - Math.Sign(evt.ScrollWheelValue));
			else if (showingSources)
				sourceScroll = Math.Max(0, sourceScroll - Math.Sign(evt.ScrollWheelValue));
			else
				TurnPage(-Math.Sign(evt.ScrollWheelValue));
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			FindHotspot(evt)?.Left?.Invoke();
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			FindHotspot(evt)?.Right?.Invoke();
		}

		private Hotspot? FindHotspot(UIMouseEvent evt)
		{
			Point mouse = evt.MousePosition.ToPoint();
			foreach (Hotspot hotspot in hotspots) {
				if (hotspot.Area.Contains(mouse))
					return hotspot;
			}
			return null;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			hotspots.Clear();

			CalculatedStyle inner = GetInnerDimensions();
			showingSources = false;
			if (item <= 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("Hint"), new Vector2(inner.X, inner.Y), Color.LightGray, 0.85f);
				return;
			}

			// Header: [Back] icon + name
			float x = inner.X;
			if (history.Count > 0)
				x += Button(spriteBatch, UIHelpers.T("Back"), x, inner.Y + 6, Back) + 8;
			Item sample = ContentSamples.ItemsByType[item];
			UIHelpers.DrawItem(spriteBatch, sample, new Vector2(x, inner.Y), 0.8f);
			string compareText = compared.Contains(item) ? UIHelpers.T("CompareOpen", compared.Count) : UIHelpers.T("CompareAdd");
			float compareX = inner.X + inner.Width - ButtonWidth(compareText);
			Button(spriteBatch, compareText, compareX, inner.Y + 6, OpenCompare, compare);
			string name = UIHelpers.Fit(sample.Name, compareX - x - 58, 1f);
			Utils.DrawBorderString(spriteBatch, name, new Vector2(x + 50, inner.Y + 10), ItemRarity.GetColor(sample.rare));

			// Recipes / Uses tabs, with the pager on the right
			float y = inner.Y + 50;
			x = inner.X;
			x += Button(spriteBatch, UIHelpers.T("RecipesTab", RecipeDatabase.RecipesFor(item).Count), x, y, () => SetMode(false), !uses && !tree && !compare) + 6;
			x += Button(spriteBatch, UIHelpers.T("UsesTab", RecipeDatabase.UsesOf(item).Count), x, y, () => SetMode(true), uses) + 6;
			if (RecipeDatabase.RecipesFor(item).Count > 0)
				Button(spriteBatch, UIHelpers.T("TreeTab"), x, y, ShowTree, tree);
			else
				tree = false;

			if (compare) {
				var key = (string.Join(",", compared), inner.Width, Language.ActiveCulture);
				if (compareRows == null || key != compareKey) {
					compareRows = CompareRows(inner.Width);
					compareKey = key;
				}
				DrawCompareRows(spriteBatch, compareRows, inner.X, y + ButtonHeight + 10, inner.Width, inner.Y + inner.Height);
				return;
			}
			if (tree) {
				DrawTree(spriteBatch, inner, y);
				return;
			}

			List<Recipe> list = Recipes;
			if (list.Count == 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T(uses ? "NoUses" : "NoRecipes"), new Vector2(inner.X, y + 40), Color.Gray, 0.9f);
				if (!uses)
					DrawSources(spriteBatch, inner.X, y + 70, inner.Width, inner.Y + inner.Height);
				return;
			}
			page = Math.Clamp(page, 0, list.Count - 1);

			if (list.Count > 1) {
				float right = inner.X + inner.Width;
				right -= ButtonWidth(">");
				Button(spriteBatch, ">", right, y, () => TurnPage(1));
				string label = $"{page + 1} / {list.Count}";
				right -= 8 + UIHelpers.Measure(label, 0.85f).X;
				Utils.DrawBorderString(spriteBatch, label, new Vector2(right, y + 5), Color.White, 0.85f);
				right -= 8 + ButtonWidth("<");
				Button(spriteBatch, "<", right, y, () => TurnPage(-1));
			}

			DrawRecipe(spriteBatch, list[page], inner.X, y + ButtonHeight + 10, inner.Width);
			DrawCraftRow(spriteBatch, list[page], inner);
		}

		// Bottom row, only near a storage terminal (or with its linked portable terminal): what the chests allow,
		// and Craft / x10 / Max buttons.
		// What the craft and "Take missing" rows show, worked out a few times a second (and when the recipe or the
		// chests change) instead of every frame: "Take missing" copies the inventory and the whole storage list.
		private const int CraftRefreshTicks = 10;
		private Recipe craftRecipe;
		private uint craftTime;
		private int craftVersion;
		private int craftMax;
		private string craftStatus;
		private int craftMissing;

		private void UpdateCraftInfo(TerminalSystem terminal, Recipe recipe)
		{
			if (recipe == craftRecipe && terminal.Version == craftVersion && Main.GameUpdateCount - craftTime < CraftRefreshTicks)
				return;
			craftRecipe = recipe;
			craftVersion = terminal.Version;
			craftTime = Main.GameUpdateCount;

			craftMax = terminal.MaxCrafts(recipe);
			if (!recipe.requiredTile.All(terminal.HasStation))
				craftStatus = UIHelpers.T("CraftNoStation");
			else if (!recipe.Conditions.All(condition => condition.IsMet()))
				craftStatus = UIHelpers.T("CraftNoConditions");
			else
				craftStatus = craftMax > 0 ? UIHelpers.T("CraftCanMake", craftMax * recipe.createItem.stack) : UIHelpers.T("CraftNoIngredients");
			craftMissing = terminal.MissingIngredients(recipe, 1).Sum(entry => entry.Amount);
		}

		// Bottom row, only near a storage terminal (or with its linked portable terminal): what the chests allow,
		// and Craft / x10 / Max buttons.
		private void DrawCraftRow(SpriteBatch spriteBatch, Recipe recipe, CalculatedStyle inner)
		{
			TerminalSystem terminal = Terminal;
			if (!terminal.CanCraft)
				return;
			UpdateCraftInfo(terminal, recipe);

			float y = inner.Y + inner.Height - ButtonHeight;
			float right = inner.X + inner.Width;
			if (craftMax > 0) {
				var buttons = new List<(string Text, int Times)> { (UIHelpers.T("Max"), craftMax) };
				if (craftMax >= 10)
					buttons.Add(("x10", 10));
				buttons.Add((UIHelpers.T("CraftButton"), 1));
				foreach (var (text, times) in buttons) {
					right -= ButtonWidth(text);
					Button(spriteBatch, text, right, y, () => terminal.RequestCraft(recipe, times));
					right -= 6;
				}
			}
			Color color = craftMax > 0 ? InChests : new Color(255, 170, 120);
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(craftStatus, right - inner.X - 6, 0.8f), new Vector2(inner.X, y + 6), color, 0.8f);

			DrawTakeRow(spriteBatch, recipe, inner, y - ButtonHeight - 6);
		}

		// Row above the craft row: "Take missing" takes out of the chests what the inventory lacks for one craft
		// (Shift: for 10), to craft it the usual way. Only shown when the chests hold some of what is missing.
		private void DrawTakeRow(SpriteBatch spriteBatch, Recipe recipe, CalculatedStyle inner, float y)
		{
			if (craftMissing == 0)
				return;
			TerminalSystem terminal = Terminal;

			string text = UIHelpers.T("TakeMissing");
			float x = inner.X + inner.Width - ButtonWidth(text);
			// The exact list is worked out on click, since the inventory may have changed since the last update.
			Button(spriteBatch, text, x, y, () => terminal.RequestTakeItems(terminal.MissingIngredients(recipe, Main.keyState.PressingShift() ? 10 : 1)));
			if (new Rectangle((int)x, (int)y, (int)ButtonWidth(text), ButtonHeight).Contains(Main.MouseScreen.ToPoint()))
				Main.hoverItemName = UIHelpers.T("TakeMissingHint");

			string status = UIHelpers.T("TakeMissingStatus", craftMissing);
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(status, x - inner.X - 6, 0.8f), new Vector2(inner.X, y + 6), InChests, 0.8f);
		}

		private void DrawRecipe(SpriteBatch spriteBatch, Recipe recipe, float left, float y, float width)
		{
			Label(spriteBatch, UIHelpers.T("Result"), left, ref y);
			DrawEntry(spriteBatch, recipe.createItem.type, recipe.createItem.stack, null, left, y, width, false);
			y += RowHeight + 4;

			Label(spriteBatch, UIHelpers.T("Ingredients"), left, ref y);
			int count = recipe.requiredItem.Count;
			bool twoColumns = count > 4;
			float columnWidth = twoColumns ? width / 2 : width;
			int perColumn = twoColumns ? (count + 1) / 2 : count;
			for (int i = 0; i < count; i++) {
				Item ingredient = recipe.requiredItem[i];
				float entryX = left + i / perColumn * columnWidth;
				float entryY = y + i % perColumn * RowHeight;
				DrawEntry(spriteBatch, ingredient.type, ingredient.stack, RecipeDatabase.GroupFor(recipe, ingredient.type), entryX, entryY, columnWidth - 4, true);
			}
			y += perColumn * RowHeight + 4;

			Label(spriteBatch, UIHelpers.T("Stations"), left, ref y);
			if (recipe.requiredTile.Count == 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("ByHand"), new Vector2(left, y), Color.White, 0.85f);
				y += 26;
			}
			foreach (int tile in recipe.requiredTile) {
				DrawStation(spriteBatch, tile, left, y, width);
				y += RowHeight;
			}

			if (recipe.Conditions.Count > 0) {
				Label(spriteBatch, UIHelpers.T("Conditions"), left, ref y);
				foreach (Condition condition in recipe.Conditions) {
					string text = UIHelpers.Fit("- " + condition.Description.Value, width, 0.8f);
					Utils.DrawBorderString(spriteBatch, text, new Vector2(left, y), Color.LightGoldenrodYellow, 0.8f);
					y += 22;
				}
			}
		}

		// Tree view: the crafting tree (indented), the base materials still missing and the crafting stations needed.
		// y is the tabs row; the "Set as goal" button goes on its right.
		private void DrawTree(SpriteBatch spriteBatch, CalculatedStyle inner, float y)
		{
			List<Recipe> recipes = RecipeDatabase.RecipesFor(item);
			page = Math.Clamp(page, 0, recipes.Count - 1);

			GoalPlayer goal = Main.LocalPlayer.GetModPlayer<GoalPlayer>();
			bool isGoal = goal.IsGoal(item, page);
			string goalText = UIHelpers.T(isGoal ? "RemoveGoal" : "SetGoal");
			Button(spriteBatch, goalText, inner.X + inner.Width - ButtonWidth(goalText), y, () => ToggleGoal(goal, isGoal), isGoal);

			if (treePlan == null || treeKey != (item, page) || Main.GameUpdateCount - treeTime >= TreeRefreshTicks) {
				treePlan = new CraftingPlan(item, recipes[page], CraftingPlan.Inventory(Main.LocalPlayer));
				treeRows = null;
				treeKey = (item, page);
				treeTime = Main.GameUpdateCount;
			}

			string title = UIHelpers.T("TreeTitle");
			if (recipes.Count > 1)
				title += " " + UIHelpers.T("TreeRecipe", page + 1, recipes.Count);
			// The rows' texts are built once per plan, not every frame.
			treeRows ??= TreeRows(treePlan, title);
			DrawTreeRows(spriteBatch, treeRows, inner.X, y + ButtonHeight + 10, inner.Width, inner.Y + inner.Height);
		}

		private static List<TreeRow> TreeRows(CraftingPlan plan, string title)
		{
			var rows = new List<TreeRow> { new(true, 0, 0, null, title, Color.White) };
			AddNode(rows, plan.Root, 0);

			rows.Add(new TreeRow(true, 0, 0, null, UIHelpers.T("TreeMissing"), Color.White));
			List<PlanMaterial> missing = plan.MissingMaterials;
			if (missing.Count == 0)
				rows.Add(new TreeRow(false, 0, 0, null, UIHelpers.T("TreeComplete"), new Color(140, 255, 140)));
			foreach (PlanMaterial material in missing)
				rows.Add(new TreeRow(false, 0, material.Type, material.Group, $"{material.Name}: {material.Owned}/{material.Needed}", new Color(255, 150, 150)));

			if (plan.Stations.Count > 0) {
				rows.Add(new TreeRow(true, 0, 0, null, UIHelpers.T("TreeStations"), Color.White));
				foreach (int tile in plan.Stations)
					rows.Add(new TreeRow(false, 0, RecipeDatabase.ItemForTile(tile), null, RecipeDatabase.TileName(tile), Color.White));
			}
			return rows;
		}

		// Green: the player has enough. White: missing, but crafted from the rows below it. Red: missing base material.
		private static void AddNode(List<TreeRow> rows, PlanNode node, int depth)
		{
			string text = $"{node.Needed} x {node.Name}";
			if (node.Owned > 0)
				text += " " + UIHelpers.T("TreeHave", node.Owned);
			Color color = node.Owned >= node.Needed ? new Color(140, 255, 140) : node.Recipe != null ? Color.White : new Color(255, 150, 150);
			rows.Add(new TreeRow(false, depth, node.Type, node.Group, text, color));
			foreach (PlanNode child in node.Children)
				AddNode(rows, child, depth + 1);
		}

		private void DrawTreeRows(SpriteBatch spriteBatch, List<TreeRow> rows, float left, float y, float width, float bottom)
		{
			treeScroll = Math.Clamp(treeScroll, 0, rows.Count - 1);
			if (treeScroll > 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreAbove", treeScroll), new Vector2(left, y), Color.Gray, 0.75f);
				y += 18;
			}

			for (int i = treeScroll; i < rows.Count; i++) {
				TreeRow row = rows[i];
				if (y + (row.IsLabel ? 22 : TreeRowHeight) > bottom - 18) {
					Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreBelow", rows.Count - i), new Vector2(left, y), Color.Gray, 0.75f);
					break;
				}
				if (row.IsLabel) {
					Label(spriteBatch, row.Text, left, ref y);
					continue;
				}

				float x = left + Math.Min(row.Depth, MaxTreeIndent) * TreeIndent;
				float textX = x;
				if (row.Type > 0) {
					int shown = row.Group != null ? UIHelpers.Cycle(row.Group) : row.Type;
					UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[shown], new Vector2(x, y), 0.5f);
					hotspots.Add(new Hotspot(new Rectangle((int)x, (int)y, (int)(left + width - x), TreeRowHeight - 2), () => Show(shown, false), () => Show(shown, true)));
					textX = x + 30;
				}
				Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(row.Text, left + width - textX, 0.8f), new Vector2(textX, y + 6), row.Color, 0.8f);
				y += TreeRowHeight;
			}
		}

		// Stats that apply to at least one of the items (best value in green), then each item's tooltip text,
		// since accessory effects are code and only described there.
		private List<CompareRow> CompareRows(float width)
		{
			Item[] items = compared.Select(type => ContentSamples.ItemsByType[type]).ToArray();
			var rows = new List<CompareRow> {
				new(CompareKind.Label, UIHelpers.T("CompareTitle"), Color.White),
				new(CompareKind.Items, UIHelpers.T("CompareRemoveHint"), Color.Gray),
			};

			foreach (var (key, get, better, format) in Stats) {
				float[] values = items.Select(get).ToArray();
				if (values.All(value => value == 0))
					continue;
				float[] applicable = values.Where(value => value != 0).ToArray();
				float best = better > 0 ? applicable.Max() : applicable.Min();
				bool highlight = better != 0 && applicable.Length > 1;
				rows.Add(new CompareRow(CompareKind.Stat, UIHelpers.T(key), Color.White,
					values.Select(value => value == 0 ? "-" : format(value)).ToArray(),
					values.Select(value => highlight && value == best ? new Color(140, 255, 140) : Color.White).ToArray()));
			}

			var effects = new List<CompareRow>();
			foreach (Item sample in items) {
				var tooltip = Lang.GetTooltip(sample.type);
				if (tooltip.Lines == 0)
					continue;
				effects.Add(new CompareRow(CompareKind.Text, sample.Name, ItemRarity.GetColor(sample.rare)));
				for (int i = 0; i < tooltip.Lines; i++) {
					foreach (string line in UIHelpers.Wrap(tooltip.GetLine(i), width - 12, 0.75f))
						effects.Add(new CompareRow(CompareKind.Text, "  " + line, Color.LightGray));
				}
			}
			if (effects.Count > 0) {
				rows.Add(new CompareRow(CompareKind.Label, UIHelpers.T("CompareEffects"), Color.White));
				rows.AddRange(effects);
			}
			return rows;
		}

		private void DrawCompareRows(SpriteBatch spriteBatch, List<CompareRow> rows, float left, float y, float width, float bottom)
		{
			float columnWidth = (width - CompareLabelWidth) / Math.Max(1, compared.Count);
			compareScroll = Math.Clamp(compareScroll, 0, rows.Count - 1);
			if (compareScroll > 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreAbove", compareScroll), new Vector2(left, y), Color.Gray, 0.75f);
				y += 18;
			}

			for (int i = compareScroll; i < rows.Count; i++) {
				CompareRow row = rows[i];
				int height = row.Kind switch { CompareKind.Items => 38, CompareKind.Text => 20, _ => 22 };
				if (y + height > bottom - 18) {
					Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreBelow", rows.Count - i), new Vector2(left, y), Color.Gray, 0.75f);
					break;
				}

				switch (row.Kind) {
					case CompareKind.Label:
						Label(spriteBatch, row.Text, left, ref y);
						continue;
					case CompareKind.Items:
						Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(row.Text, CompareLabelWidth - 6, 0.7f), new Vector2(left, y + 10), row.Color, 0.7f);
						for (int c = 0; c < compared.Count; c++) {
							int type = compared[c];
							var position = new Vector2(left + CompareLabelWidth + c * columnWidth + columnWidth / 2 - 17, y);
							UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[type], position, 0.65f);
							hotspots.Add(new Hotspot(new Rectangle((int)position.X, (int)position.Y, 34, 34), () => Show(type, false), () => RemoveCompared(type)));
						}
						break;
					case CompareKind.Stat:
						Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(row.Text, CompareLabelWidth - 6, 0.8f), new Vector2(left, y), row.Color, 0.8f);
						for (int c = 0; c < row.Values.Length; c++) {
							string value = UIHelpers.Fit(row.Values[c], columnWidth - 6, 0.8f);
							float valueX = left + CompareLabelWidth + c * columnWidth + (columnWidth - UIHelpers.Measure(value, 0.8f).X) / 2;
							Utils.DrawBorderString(spriteBatch, value, new Vector2(valueX, y), row.ValueColors[c], 0.8f);
						}
						break;
					case CompareKind.Text:
						Utils.DrawBorderString(spriteBatch, row.Text, new Vector2(left, y), row.Color, 0.75f);
						break;
				}
				y += height;
			}
		}

		// "How to obtain": shops, enemy drops and bags/crates, for items that can't be crafted.
		private void DrawSources(SpriteBatch spriteBatch, float left, float y, float width, float bottom)
		{
			List<ItemSource> sources = SourceDatabase.SourcesOf(item);
			Label(spriteBatch, UIHelpers.T("HowToObtain"), left, ref y);
			if (sources.Count == 0) {
				foreach (string line in UIHelpers.T("NoSources").Split('\n')) {
					Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(line, width, 0.8f), new Vector2(left, y), Color.Gray, 0.8f);
					y += 20;
				}
				return;
			}

			showingSources = true;
			sourceScroll = Math.Clamp(sourceScroll, 0, sources.Count - 1);
			if (sourceScroll > 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreAbove", sourceScroll), new Vector2(left, y), Color.Gray, 0.75f);
				y += 18;
			}

			for (int i = sourceScroll; i < sources.Count; i++) {
				ItemSource source = sources[i];
				float height = RowHeight + 4 + (Location(source).Length > 0 ? 18 : 0) + (source.Conditions.Length > 0 ? 18 : 0);
				if (y + height > bottom - 18) {
					Utils.DrawBorderString(spriteBatch, UIHelpers.T("MoreBelow", sources.Count - i), new Vector2(left, y), Color.Gray, 0.75f);
					break;
				}
				DrawSource(spriteBatch, source, left, y, width);
				y += height;
			}
		}

		private void DrawSource(SpriteBatch spriteBatch, ItemSource source, float x, float y, float width)
		{
			string name = SourceDatabase.SourceName(source);
			string text = source.Kind switch {
				SourceKind.Shop => source.Detail.Length > 0 ? UIHelpers.T("SoldBy", name, source.Detail) : UIHelpers.T("SoldByFree", name),
				SourceKind.Drop => UIHelpers.T("DroppedBy", name, source.Detail),
				_ => UIHelpers.T("FoundInside", name, source.Detail),
			};
			Color color = source.Kind switch {
				SourceKind.Shop => new Color(140, 220, 255),
				SourceKind.Drop => new Color(255, 170, 140),
				_ => new Color(200, 170, 255),
			};

			// Bags and crates are items: show their icon and make them clickable, like an ingredient.
			// Shops and drops show the NPC's sprite.
			float textX = x + 38;
			if (source.Kind == SourceKind.Container) {
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[source.Id], new Vector2(x, y), SlotScale);
				hotspots.Add(new Hotspot(new Rectangle((int)x, (int)y, (int)width, RowHeight - 2), () => Show(source.Id, false), () => Show(source.Id, true)));
			}
			else if (source.Id > 0) {
				UIHelpers.DrawNpc(spriteBatch, source.Id, new Vector2(x, y), 32);
			}
			else {
				textX = x + 8;
			}
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(text, x + width - textX, 0.85f), new Vector2(textX, y + 8), color, 0.85f);

			float lineY = y + RowHeight - 6;
			string location = Location(source);
			if (location.Length > 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(UIHelpers.T("SpawnsIn", location), x + width - textX, 0.72f), new Vector2(textX, lineY), new Color(150, 230, 150), 0.72f);
				lineY += 18;
			}
			if (source.Conditions.Length > 0)
				Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(source.Conditions, x + width - textX, 0.72f), new Vector2(textX, lineY), Color.LightGoldenrodYellow, 0.72f);
		}

		// Only for enemies: where to find them. Town NPCs selling it are easy to find.
		private static string Location(ItemSource source) => source.Kind == SourceKind.Drop ? SourceDatabase.LocationOf(source.Id) : "";

		// One "icon + 3 x Name" row. Left click: that item's recipes; right click: its uses.
		private void DrawEntry(SpriteBatch spriteBatch, int type, int stack, RecipeGroup group, float x, float y, float width, bool colorByOwned)
		{
			int shown = group != null ? UIHelpers.Cycle(group) : type;
			UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[shown], new Vector2(x, y), SlotScale);

			string name = group != null ? group.GetText() : Lang.GetItemNameValue(shown);
			string text = stack > 1 ? $"{stack} x {name}" : name;
			Color color = Color.White;
			// Green: enough in the inventory. Blue: enough in the terminal's chests (in reach), or in both together,
			// which the terminal can craft with. Red: not enough.
			if (colorByOwned) {
				int owned = group != null ? UIHelpers.CountOwned(group.ValidItems.Contains) : UIHelpers.CountOwned(t => t == type);
				TerminalSystem terminal = Terminal;
				int stored = !terminal.CanCraft ? 0 : group != null ? group.ValidItems.Sum(t => terminal.StoredCount(t)) : terminal.StoredCount(type);
				color = owned >= stack ? new Color(140, 255, 140) : owned + stored >= stack ? InChests : new Color(255, 150, 150);
			}
			Utils.DrawBorderString(spriteBatch, UIHelpers.Fit(text, width - 40, 0.85f), new Vector2(x + 38, y + 8), color, 0.85f);

			hotspots.Add(new Hotspot(new Rectangle((int)x, (int)y, (int)width, RowHeight - 2), () => Show(shown, false), () => Show(shown, true)));
		}

		private void DrawStation(SpriteBatch spriteBatch, int tile, float x, float y, float width)
		{
			int stationItem = RecipeDatabase.ItemForTile(tile);
			if (stationItem > 0) {
				UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[stationItem], new Vector2(x, y), SlotScale);
				hotspots.Add(new Hotspot(new Rectangle((int)x, (int)y, (int)width, RowHeight - 2), () => Show(stationItem, false), () => Show(stationItem, true)));
			}
			string name = UIHelpers.Fit(RecipeDatabase.TileName(tile), width - 40, 0.85f);
			// Near a terminal: green if the station is nearby or on its wire, red if not.
			TerminalSystem terminal = Terminal;
			Color color = !terminal.CanCraft ? Color.White : terminal.HasStation(tile) ? new Color(140, 255, 140) : new Color(255, 150, 150);
			Utils.DrawBorderString(spriteBatch, name, new Vector2(x + 38, y + 8), color, 0.85f);
		}

		private static void Label(SpriteBatch spriteBatch, string text, float x, ref float y)
		{
			Utils.DrawBorderString(spriteBatch, text, new Vector2(x, y), new Color(255, 220, 120), 0.85f);
			y += 22;
		}

		private static float ButtonWidth(string text) => UIHelpers.Measure(text, 0.85f).X + 16;

		private float Button(SpriteBatch spriteBatch, string text, float x, float y, Action onClick, bool active = false)
		{
			var area = new Rectangle((int)x, (int)y, (int)ButtonWidth(text), ButtonHeight);
			bool hovered = area.Contains(Main.MouseScreen.ToPoint());
			Color background = active ? new Color(100, 120, 210) : hovered ? new Color(80, 95, 170) : new Color(55, 65, 130);
			Utils.DrawInvBG(spriteBatch, area, background * 0.95f);
			Utils.DrawBorderString(spriteBatch, text, new Vector2(x + 8, y + 5), Color.White, 0.85f);
			hotspots.Add(new Hotspot(area, onClick, null));
			return area.Width;
		}
	}
}

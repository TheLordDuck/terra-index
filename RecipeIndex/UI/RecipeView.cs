using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RecipeIndex.Common;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace RecipeIndex.UI
{
	// Right-hand panel: shows the recipes (or uses) of an item, one per page.
	// Clickable areas are registered while drawing, because the layout depends on the recipe.
	public class RecipeView : UIPanel
	{
		private const float SlotScale = 0.6f;
		private const int RowHeight = 34;
		private const int ButtonHeight = 28;

		private readonly record struct Hotspot(Rectangle Area, Action Left, Action Right);

		private readonly List<Hotspot> hotspots = new();
		private readonly Stack<(int Item, bool Uses, int Page)> history = new();
		private int item;
		private bool uses;
		private int page;

		private List<Recipe> Recipes => uses ? RecipeDatabase.UsesOf(item) : RecipeDatabase.RecipesFor(item);

		public void Show(int type, bool showUses)
		{
			if (type <= 0 || (type == item && showUses == uses))
				return;
			if (item > 0)
				history.Push((item, uses, page));
			item = type;
			uses = showUses;
			page = 0;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void Back()
		{
			if (history.Count == 0)
				return;
			(item, uses, page) = history.Pop();
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void SetMode(bool showUses)
		{
			if (uses == showUses)
				return;
			uses = showUses;
			page = 0;
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
				PlayerInput.LockVanillaMouseScroll("RecipeIndex/RecipeView");
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
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
			string name = UIHelpers.Fit(sample.Name, inner.X + inner.Width - x - 50, 1f);
			Utils.DrawBorderString(spriteBatch, name, new Vector2(x + 50, inner.Y + 10), ItemRarity.GetColor(sample.rare));

			// Recipes / Uses tabs, with the pager on the right
			float y = inner.Y + 50;
			x = inner.X;
			x += Button(spriteBatch, UIHelpers.T("RecipesTab", RecipeDatabase.RecipesFor(item).Count), x, y, () => SetMode(false), !uses) + 6;
			Button(spriteBatch, UIHelpers.T("UsesTab", RecipeDatabase.UsesOf(item).Count), x, y, () => SetMode(true), uses);

			List<Recipe> list = Recipes;
			if (list.Count == 0) {
				Utils.DrawBorderString(spriteBatch, UIHelpers.T(uses ? "NoUses" : "NoRecipes"), new Vector2(inner.X, y + 40), Color.Gray, 0.9f);
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

		// One "icon + 3 x Name" row. Left click: that item's recipes; right click: its uses.
		private void DrawEntry(SpriteBatch spriteBatch, int type, int stack, RecipeGroup group, float x, float y, float width, bool colorByOwned)
		{
			int shown = group != null ? UIHelpers.Cycle(group) : type;
			UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[shown], new Vector2(x, y), SlotScale);

			string name = group != null ? group.GetText() : Lang.GetItemNameValue(shown);
			string text = stack > 1 ? $"{stack} x {name}" : name;
			Color color = Color.White;
			if (colorByOwned) {
				int owned = group != null ? UIHelpers.CountOwned(group.ValidItems.Contains) : UIHelpers.CountOwned(t => t == type);
				color = owned >= stack ? new Color(140, 255, 140) : new Color(255, 150, 150);
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
			Utils.DrawBorderString(spriteBatch, name, new Vector2(x + 38, y + 8), Color.White, 0.85f);
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

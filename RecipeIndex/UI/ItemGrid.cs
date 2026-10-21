using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.UI
{
	// Item grid (every item in the index, chest contents in the terminal), shown one page at a time.
	// Only the current page is drawn, and item textures are loaded a few per frame, so big modpacks don't freeze the game.
	// Footer: |< < [page bar] > >|. The mouse wheel turns pages (Shift: 10 at a time); dragging the bar jumps to a page on release.
	public class ItemGrid : UIElement
	{
		private const float SlotScale = 0.75f;
		private const int Cell = 42;
		private const int FooterHeight = 28;
		private const int ButtonWidth = 30;
		private const int LoadsPerFrame = 16;

		private static readonly Item Empty = new();
		// Lowercase item names, so filtering thousands of items on every keystroke stays cheap.
		private static readonly Dictionary<int, string> lowerNames = new();
		private static GameCulture namesCulture;

		private readonly Func<IEnumerable<Item>> source;
		private List<Item> items = new();
		private string filter = "";
		private int page;
		private int pageCount = 1;
		private int columns = 1;
		private int rows = 1;
		private Vector2 origin;

		// Footer layout from the last draw.
		private Rectangle first, previous, next, last, bar;
		private bool dragging;
		private int dragPage;
		private bool suppressClick;

		public event Action<int, bool> OnItemClicked;

		public int Count => items.Count;
		public string EmptyText { get; set; } = UIHelpers.T("NoResults");
		private int PageSize => columns * rows;

		public ItemGrid(Func<IEnumerable<Item>> source)
		{
			this.source = source;
		}

		public static void ClearCache() => lowerNames.Clear();

		public void SetFilter(string text)
		{
			filter = text.Trim().ToLowerInvariant();
			page = 0;
			Refresh();
		}

		public void Refresh() => items = source().Where(item => Matches(item.type)).ToList();

		// "@name" filters by mod (e.g. "@calamity", "@terraria"); any other text filters by item name.
		private bool Matches(int type)
		{
			if (filter.Length == 0)
				return true;
			if (filter[0] == '@') {
				string mod = ItemLoader.GetItem(type)?.Mod.Name ?? "Terraria";
				return mod.ToLowerInvariant().Contains(filter[1..]);
			}
			return LowerName(type).Contains(filter);
		}

		private static string LowerName(int type)
		{
			if (namesCulture != Language.ActiveCulture) {
				lowerNames.Clear();
				namesCulture = Language.ActiveCulture;
			}
			if (!lowerNames.TryGetValue(type, out string name))
				lowerNames[type] = name = Lang.GetItemNameValue(type).ToLowerInvariant();
			return name;
		}

		private void SetPage(int value)
		{
			value = Math.Clamp(value, 0, pageCount - 1);
			if (value == page)
				return;
			page = value;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private int PageAt(float mouseX)
		{
			if (pageCount <= 1 || bar.Width <= 0)
				return 0;
			return Math.Clamp((int)((mouseX - bar.X) * pageCount / bar.Width), 0, pageCount - 1);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				PlayerInput.LockVanillaMouseScroll("RecipeIndex/ItemGrid");

			// Checked here rather than in LeftMouseUp so releasing outside the grid also ends the drag.
			if (dragging) {
				if (Main.mouseLeft) {
					dragPage = PageAt(Main.MouseScreen.X);
				}
				else {
					dragging = false;
					SetPage(dragPage);
				}
			}
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			suppressClick = false;
			if (pageCount <= 1 || !bar.Contains(evt.MousePosition.ToPoint()))
				return;
			dragging = true;
			suppressClick = true;
			dragPage = PageAt(evt.MousePosition.X);
		}

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			int step = Main.keyState.PressingShift() ? 10 : 1;
			SetPage(page - Math.Sign(evt.ScrollWheelValue) * step);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (suppressClick)
				return;

			Point mouse = evt.MousePosition.ToPoint();
			if (first.Contains(mouse))
				SetPage(0);
			else if (previous.Contains(mouse))
				SetPage(page - 1);
			else if (next.Contains(mouse))
				SetPage(page + 1);
			else if (last.Contains(mouse))
				SetPage(pageCount - 1);
			else if (IndexAt(evt.MousePosition) is int index and >= 0)
				OnItemClicked?.Invoke(items[index].type, false);
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			int index = IndexAt(evt.MousePosition);
			if (index >= 0)
				OnItemClicked?.Invoke(items[index].type, true);
		}

		private int IndexAt(Vector2 mouse)
		{
			Vector2 local = mouse - origin;
			if (local.X < 0 || local.Y < 0)
				return -1;
			int col = (int)(local.X / Cell);
			int row = (int)(local.Y / Cell);
			if (col >= columns || row >= rows)
				return -1;
			int index = page * PageSize + row * columns + col;
			return index < items.Count ? index : -1;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle inner = GetInnerDimensions();
			origin = new Vector2(inner.X, inner.Y);
			columns = Math.Max(1, (int)(inner.Width / Cell));
			rows = Math.Max(1, (int)((inner.Height - FooterHeight - 4) / Cell));
			pageCount = Math.Max(1, (items.Count + PageSize - 1) / PageSize);
			page = Math.Clamp(page, 0, pageCount - 1);

			DrawFooter(spriteBatch, inner);

			if (items.Count == 0) {
				Utils.DrawBorderString(spriteBatch, EmptyText, origin, Color.Gray, 0.9f);
				return;
			}

			// Textures not loaded yet are spread over several frames: those slots stay empty for a moment.
			int loads = LoadsPerFrame;
			int start = page * PageSize;
			int end = Math.Min(items.Count, start + PageSize);
			for (int index = start; index < end; index++) {
				Item item = items[index];
				bool ready = TextureAssets.Item[item.type].State != AssetState.NotLoaded || loads-- > 0;
				int slot = index - start;
				UIHelpers.DrawItem(spriteBatch, ready ? item : Empty, origin + new Vector2(slot % columns * Cell, slot / columns * Cell), SlotScale);
			}
		}

		private void DrawFooter(SpriteBatch spriteBatch, CalculatedStyle inner)
		{
			int y = (int)(inner.Y + inner.Height - FooterHeight);
			int left = (int)inner.X;
			int right = (int)(inner.X + inner.Width);
			first = new Rectangle(left, y, ButtonWidth, FooterHeight);
			previous = new Rectangle(first.Right + 4, y, ButtonWidth, FooterHeight);
			last = new Rectangle(right - ButtonWidth, y, ButtonWidth, FooterHeight);
			next = new Rectangle(last.X - 4 - ButtonWidth, y, ButtonWidth, FooterHeight);
			bar = new Rectangle(previous.Right + 6, y, next.X - 6 - (previous.Right + 6), FooterHeight);

			Button(spriteBatch, first, "|<", page > 0);
			Button(spriteBatch, previous, "<", page > 0);
			Button(spriteBatch, next, ">", page < pageCount - 1);
			Button(spriteBatch, last, ">|", page < pageCount - 1);

			Point mouse = Main.MouseScreen.ToPoint();
			int shown = dragging ? dragPage : page;
			Utils.DrawInvBG(spriteBatch, bar, new Color(30, 35, 80) * 0.9f);
			if (pageCount > 1) {
				int thumbWidth = Math.Max(16, bar.Width / pageCount);
				int thumbX = bar.X + (bar.Width - thumbWidth) * shown / (pageCount - 1);
				bool hot = dragging || bar.Contains(mouse);
				Color thumb = hot ? new Color(160, 180, 255) : new Color(120, 140, 220);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(thumbX, bar.Y + 4, thumbWidth, bar.Height - 8), thumb * 0.6f);
			}

			string label = UIHelpers.Fit(UIHelpers.T("Page", shown + 1, pageCount, items.Count), bar.Width - 8, 0.8f);
			Vector2 size = UIHelpers.Measure(label, 0.8f);
			Utils.DrawBorderString(spriteBatch, label, new Vector2(bar.Center.X - size.X / 2, bar.Y + 6), Color.White, 0.8f);
		}

		private static void Button(SpriteBatch spriteBatch, Rectangle area, string text, bool enabled)
		{
			bool hovered = enabled && area.Contains(Main.MouseScreen.ToPoint());
			Color background = !enabled ? new Color(45, 50, 90) : hovered ? new Color(80, 95, 170) : new Color(55, 65, 130);
			Utils.DrawInvBG(spriteBatch, area, background * 0.95f);
			Vector2 size = UIHelpers.Measure(text, 0.85f);
			Utils.DrawBorderString(spriteBatch, text, new Vector2(area.Center.X - size.X / 2, area.Y + 5), enabled ? Color.White : Color.Gray, 0.85f);
		}
	}
}

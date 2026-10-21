using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.UI
{
	// Item grid (every item in the index, chest contents in the terminal). Only visible rows are drawn, so it handles thousands of items without one UIElement per item.
	public class ItemGrid : UIElement
	{
		private const float SlotScale = 0.75f;
		private const int Cell = 42;
		private const int ScrollbarWidth = 8;

		private readonly Func<IEnumerable<Item>> source;
		private List<Item> items = new();
		private string filter = "";
		private int scroll;
		private int columns = 1;
		private Vector2 origin;

		// Scrollbar layout from the last draw, used to drag the thumb.
		private Rectangle track;
		private int thumbHeight;
		private int maxScroll;
		private bool dragging;
		private float grabOffset;
		private bool suppressClick;

		public event Action<int, bool> OnItemClicked;

		public int Count => items.Count;
		public string EmptyText { get; set; } = UIHelpers.T("NoResults");

		public ItemGrid(Func<IEnumerable<Item>> source)
		{
			this.source = source;
		}

		public void SetFilter(string text)
		{
			filter = text.Trim().ToLowerInvariant();
			scroll = 0;
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
			return Lang.GetItemNameValue(type).ToLowerInvariant().Contains(filter);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (IsMouseHovering)
				PlayerInput.LockVanillaMouseScroll("RecipeIndex/ItemGrid");

			// Checked here rather than in LeftMouseUp so releasing outside the grid also ends the drag.
			if (dragging) {
				if (Main.mouseLeft)
					DragTo(Main.MouseScreen.Y);
				else
					dragging = false;
			}
		}

		public override void LeftMouseDown(UIMouseEvent evt)
		{
			base.LeftMouseDown(evt);
			suppressClick = false;
			if (maxScroll <= 0 || !track.Contains(evt.MousePosition.ToPoint()))
				return;

			int thumbY = ThumbY();
			// Clicking the track outside the thumb centers the thumb on the mouse, then drags from there.
			grabOffset = evt.MousePosition.Y >= thumbY && evt.MousePosition.Y < thumbY + thumbHeight
				? evt.MousePosition.Y - thumbY
				: thumbHeight / 2f;
			dragging = true;
			suppressClick = true;
			DragTo(evt.MousePosition.Y);
		}

		private void DragTo(float mouseY)
		{
			int range = track.Height - thumbHeight;
			if (range <= 0)
				return;
			float thumbTop = mouseY - grabOffset - track.Y;
			scroll = Math.Clamp((int)Math.Round(thumbTop * maxScroll / range), 0, maxScroll);
		}

		private int ThumbY() => track.Y + (track.Height - thumbHeight) * scroll / maxScroll;

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			scroll -= Math.Sign(evt.ScrollWheelValue) * 2;
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (suppressClick)
				return;
			int index = IndexAt(evt.MousePosition);
			if (index >= 0)
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
			if (col >= columns)
				return -1;
			int index = (scroll + row) * columns + col;
			return index < items.Count ? index : -1;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			CalculatedStyle inner = GetInnerDimensions();
			origin = new Vector2(inner.X, inner.Y);
			columns = Math.Max(1, (int)((inner.Width - ScrollbarWidth) / Cell));
			int rows = Math.Max(1, (int)(inner.Height / Cell));
			int totalRows = (items.Count + columns - 1) / columns;
			maxScroll = Math.Max(0, totalRows - rows);
			scroll = Math.Clamp(scroll, 0, maxScroll);

			if (items.Count == 0) {
				Utils.DrawBorderString(spriteBatch, EmptyText, origin, Color.Gray, 0.9f);
				return;
			}

			for (int row = 0; row < rows; row++) {
				for (int col = 0; col < columns; col++) {
					int index = (scroll + row) * columns + col;
					if (index >= items.Count)
						break;
					UIHelpers.DrawItem(spriteBatch, items[index], origin + new Vector2(col * Cell, row * Cell), SlotScale);
				}
			}

			if (maxScroll > 0) {
				track = new Rectangle((int)(inner.X + inner.Width - ScrollbarWidth), (int)inner.Y, ScrollbarWidth, (int)inner.Height);
				thumbHeight = Math.Max(20, track.Height * rows / totalRows);
				bool hot = dragging || track.Contains(Main.MouseScreen.ToPoint());
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, track, Color.Black * 0.4f);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(track.X, ThumbY(), ScrollbarWidth, thumbHeight), hot ? new Color(160, 180, 255) : new Color(120, 140, 220));
			}
		}
	}
}

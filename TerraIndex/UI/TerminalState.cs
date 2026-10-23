using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace TerraIndex.UI
{
	// Terminal panel: search box (focused on open) + items from the connected chests, in the order picked with the sort button.
	// Above the items, the crafting stations on the wire (StationsRow).
	// Header buttons: "Deposit all" (TerminalSystem.QuickDeposit) and "Deposit items" (the deposit slots).
	// Left click: takes a full stack out through the terminal's door. Right click (hold to speed up, like picking up
	// from a vanilla slot): picks some up on the cursor, and the next left click on an item takes that many out instead.
	// Right clicking empty space drops the selection. Shift + left click: highlights the chests holding it.
	// Ctrl + left click: shows its recipes in the index, which opens next to the panel (also with the "Recipes" button).
	public class TerminalState : UIState
	{
		private const int RefreshInterval = 30;
		// Reopening the same terminal within this time keeps the search, the category tab and the page.
		private const int RememberTicks = 60 * 30;
		private const float HintWidth = 496;
		private const float InfoScale = 0.8f;

		private UIPanel panel;
		private UIText info;
		private UIText hint;
		private SearchBox search;
		private ItemGrid grid;
		private UITextPanel<string> sort;
		private UITextPanel<string> recipes;
		private int refreshTimer;
		private int seenVersion = -1;
		// Layout applied last: next to the recipe index or centered, and for which screen width.
		private bool docked;
		private float layoutWidth;
		private Point closedTerminal;
		private uint closedAt;

		// Picked up with right click, shown on the cursor.
		private int selectedType;
		private int selectedAmount;
		private bool wasRightDown;
		private int holdTimer;
		private int holdRepeats;
		private readonly Item cursorItem = new();

		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();
		private static RecipeIndexSystem Index => ModContent.GetInstance<RecipeIndexSystem>();

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(UIHelpers.TerminalWidth, 0);
			panel.Height.Set(510, 0);
			panel.HAlign = panel.VAlign = 0.5f;
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("TerminalTitle"), 1.1f));

			var close = new UITextPanel<string>("X");
			close.Width.Set(34, 0);
			close.Height.Set(30, 0);
			close.HAlign = 1f;
			close.Top.Set(-4, 0);
			close.OnLeftClick += (_, _) => Terminal.Close();
			panel.Append(close);

			var deposit = new UITextPanel<string>(UIHelpers.T("DepositButton"), 0.8f);
			deposit.HAlign = 1f;
			deposit.Left.Set(-40, 0);
			deposit.Top.Set(-4, 0);
			deposit.Height.Set(30, 0);
			deposit.OnLeftClick += (_, _) => Terminal.OpenDeposit();
			panel.Append(deposit);

			// Placed to the left of "Deposit items"; UITextPanel sets MinWidth from its text.
			var depositAll = new UITextPanel<string>(UIHelpers.T("QuickDepositButton"), 0.8f);
			depositAll.HAlign = 1f;
			depositAll.Left.Set(-40 - deposit.MinWidth.Pixels - 6, 0);
			depositAll.Top.Set(-4, 0);
			depositAll.Height.Set(30, 0);
			depositAll.OnLeftClick += (_, _) => Terminal.QuickDeposit();
			panel.Append(depositAll);

			var help = new HelpButton("HelpTerminal");
			help.HAlign = 1f;
			help.Left.Set(depositAll.Left.Pixels - depositAll.MinWidth.Pixels - 6, 0);
			help.Top.Set(-4, 0);
			panel.Append(help);

			sort = new UITextPanel<string>(SortLabel(), 0.8f);
			sort.HAlign = 1f;
			sort.Top.Set(66, 0);
			sort.Height.Set(30, 0);
			sort.OnLeftClick += (_, _) => {
				Terminal.CycleSort();
				sort.SetText(SortLabel());
				PlaceRecipesButton();
				panel.Recalculate();
				// The info line fits next to it, so it's measured again.
				seenVersion = -1;
				SoundEngine.PlaySound(SoundID.MenuTick);
			};
			panel.Append(sort);

			// To the left of the sort button, which changes width with its text (see PlaceRecipesButton).
			recipes = new UITextPanel<string>(UIHelpers.T("RecipesButton"), 0.8f);
			recipes.HAlign = 1f;
			recipes.Top.Set(66, 0);
			recipes.Height.Set(30, 0);
			recipes.OnLeftClick += (_, _) => Index.Toggle();
			panel.Append(recipes);
			PlaceRecipesButton();

			var stations = new StationsRow();
			stations.Top.Set(122, 0);
			stations.Width.Set(0, 1f);
			stations.Height.Set(28, 0);
			panel.Append(stations);

			grid = new ItemGrid(() => Terminal.StoredItems);
			grid.Top.Set(154, 0);
			grid.Width.Set(0, 1f);
			grid.Height.Set(-154, 1f);
			grid.OnItemClicked += OnItemClicked;
			panel.Append(grid);

			search = new SearchBox(UIHelpers.T("SearchHint"));
			search.Top.Set(34, 0);
			search.Width.Set(0, 1f);
			search.Height.Set(34, 0);
			search.OnTextChanged += grid.SetFilter;
			panel.Append(search);

			info = new UIText("", InfoScale);
			info.Top.Set(76, 0);
			panel.Append(info);

			hint = new UIText("", 0.75f);
			hint.Top.Set(100, 0);
			hint.TextColor = Color.LightGray;
			panel.Append(hint);
		}

		public override void OnActivate()
		{
			if (Terminal.Terminal != closedTerminal || Main.GameUpdateCount - closedAt > RememberTicks)
				search.SetText("");
			// Optional (ClientConfig): while typing, the mod's keys (N, [, ]) don't work.
			if (ClientConfig.Instance.FocusTerminalSearch)
				search.Focus();
			// The sort resets to the config's default on world load.
			sort.SetText(SortLabel());
			PlaceRecipesButton();
			Layout();
			refreshTimer = RefreshInterval;
			seenVersion = -1;
			wasRightDown = Main.mouseRight;
			ClearSelection();
		}

		public override void OnDeactivate()
		{
			closedTerminal = Terminal.Terminal;
			closedAt = Main.GameUpdateCount;
			search.Unfocus();
			ClearSelection();
		}

		private void PlaceRecipesButton() => recipes.Left.Set(-sort.MinWidth.Pixels - 6, 0);

		// Next to the recipe index while it is open (see RecipeIndexSystem), so both can be used at once; centered otherwise.
		private void Layout()
		{
			bool dock = Index.IsOpen;
			float screenWidth = GetDimensions().Width;
			if (dock == docked && screenWidth == layoutWidth)
				return;
			docked = dock;
			layoutWidth = screenWidth;

			panel.HAlign = dock ? 0f : 0.5f;
			panel.Left.Set(dock ? UIHelpers.DockLayout(screenWidth).TerminalLeft : 0, 0);
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			Layout();
			base.Update(gameTime);

			// Chests can change while the panel is open (including by other players),
			// so the contents are requested again every half second.
			if (--refreshTimer <= 0) {
				refreshTimer = RefreshInterval;
				Terminal.RequestRefresh();
			}

			if (seenVersion != Terminal.Version) {
				seenVersion = Terminal.Version;
				grid.Refresh();
				// Chests, free slots and how full they are, the fuller the redder.
				int slots = Terminal.Slots;
				int full = slots > 0 ? (int)Math.Round(100.0 * Terminal.UsedSlots / slots) : 0;
				// Only as wide as the room left of the recipes and sort buttons on the same row.
				float room = panel.GetInnerDimensions().Width - sort.MinWidth.Pixels - recipes.MinWidth.Pixels - 18;
				info.SetText(UIHelpers.Fit(UIHelpers.T("ConnectedChests", Terminal.ChestCount, slots - Terminal.UsedSlots, slots, full), room, InfoScale));
				info.TextColor = full >= 90 ? new Color(255, 120, 110) : full >= 70 ? new Color(255, 215, 110) : Color.White;
				grid.EmptyText = UIHelpers.T(Terminal.StoredItems.Count == 0 ? "TerminalEmpty" : "NoResults");
				// Someone else may have taken some out meanwhile.
				if (selectedType > 0)
					SetSelection(selectedType, selectedAmount);
			}

			UpdateSelection();

			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			base.Draw(spriteBatch);
			if (selectedType <= 0)
				return;

			// Drawn like vanilla's item on the mouse: icon just below and right of the cursor, with the amount.
			if (cursorItem.type != selectedType)
				cursorItem.SetDefaults(selectedType);
			Vector2 mouse = Main.MouseScreen;
			ItemSlot.DrawItemIcon(cursorItem, ItemSlot.Context.MouseItem, spriteBatch, mouse + new Vector2(24, 24), 1f, 32f, Color.White);
			if (selectedAmount > 1) {
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, selectedAmount.ToString(),
					mouse + new Vector2(10, 28), Color.White, 0f, Vector2.Zero, new Vector2(0.8f));
			}
		}

		private static string SortLabel() => UIHelpers.T("SortBy", UIHelpers.T("Sort" + Terminal.Sort));

		private static int Stored(int type) => Terminal.StoredItems.Find(item => item.type == type)?.stack ?? 0;

		// Right click picks up 1, then holding it adds more and more each tick, like taking from a vanilla slot.
		private void UpdateSelection()
		{
			bool down = Main.mouseRight;
			bool pressed = down && !wasRightDown;
			wasRightDown = down;
			if (!down)
				return;

			int type = grid.HoveredType;
			if (pressed) {
				if (type <= 0) {
					if (panel.ContainsPoint(Main.MouseScreen))
						ClearSelection();
					return;
				}
				if (Add(type, 1)) {
					holdTimer = 20;
					holdRepeats = 0;
				}
				return;
			}

			// Moving off the item pauses it.
			if (type != selectedType || --holdTimer > 0)
				return;
			holdRepeats++;
			holdTimer = Math.Max(1, 8 - holdRepeats);
			Add(type, holdRepeats < 30 ? 1 : 1 + (holdRepeats - 30) / 10);
		}

		// Returns false if nothing more could be picked up.
		private bool Add(int type, int amount)
		{
			int before = type == selectedType ? selectedAmount : 0;
			SetSelection(type, before + amount);
			if (selectedAmount == before)
				return false;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return true;
		}

		// At most what is stored and one stack, since a withdrawal is one stack.
		private void SetSelection(int type, int amount)
		{
			int max = Math.Min(Stored(type), ContentSamples.ItemsByType[type].maxStack);
			amount = Math.Min(amount, max);
			if (amount <= 0) {
				ClearSelection();
				return;
			}
			selectedType = type;
			selectedAmount = amount;
			hint.SetText(UIHelpers.Fit(UIHelpers.T("TerminalHoldingHint", amount), HintWidth, 0.75f));
		}

		private void ClearSelection()
		{
			selectedType = selectedAmount = 0;
			hint?.SetText(UIHelpers.Fit(UIHelpers.T("TerminalHint"), HintWidth, 0.75f));
		}

		private void OnItemClicked(int type, bool rightClick)
		{
			// Handled in UpdateSelection, which also sees the button being held.
			if (rightClick)
				return;

			if (Main.keyState.PressingControl()) {
				Index.ShowItem(type, false);
				return;
			}

			if (Main.keyState.PressingShift()) {
				Terminal.Close();
				int found = Terminal.Highlight(type);
				Main.NewText(UIHelpers.T("FoundIn", Lang.GetItemNameValue(type), found), Color.Gold);
				return;
			}

			// With something on the cursor, clicking any item takes that out; otherwise a full stack of the clicked one.
			if (selectedType > 0)
				Withdraw(selectedType, selectedAmount);
			else
				Withdraw(type, 0);
		}

		// The panel (and the index next to it) stays open, so several things can be taken out in a row: the item drops
		// out of the door, or at the player's feet with a portable terminal far away (see TerminalSystem.Deliver).
		private void Withdraw(int type, int amount)
		{
			Terminal.RequestWithdraw(type, amount);
			ClearSelection();
			// Shows the new amounts as soon as possible, instead of at the next half-second refresh.
			refreshTimer = 2;
		}
	}
}

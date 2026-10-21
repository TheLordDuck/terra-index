using Microsoft.Xna.Framework;
using RecipeIndex.Content.Storage;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.UI
{
	// Terminal panel: search box (focused on open) + items from the connected chests.
	// Left click: takes a stack out through the terminal's door. Shift + left click: highlights the chests holding it.
	// Right click: opens its recipes in the index.
	public class TerminalState : UIState
	{
		private const int RefreshInterval = 30;

		private UIPanel panel;
		private UIText info;
		private SearchBox search;
		private ItemGrid grid;
		private int refreshTimer;
		private int seenVersion = -1;

		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(470, 0);
			panel.Height.Set(440, 0);
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

			grid = new ItemGrid(() => Terminal.StoredItems);
			grid.Top.Set(122, 0);
			grid.Width.Set(0, 1f);
			grid.Height.Set(-122, 1f);
			grid.OnItemClicked += OnItemClicked;
			panel.Append(grid);

			search = new SearchBox(UIHelpers.T("SearchHint"));
			search.Top.Set(34, 0);
			search.Width.Set(0, 1f);
			search.Height.Set(34, 0);
			search.OnTextChanged += grid.SetFilter;
			panel.Append(search);

			info = new UIText("", 0.85f);
			info.Top.Set(76, 0);
			panel.Append(info);

			var hint = new UIText(UIHelpers.T("TerminalHint"), 0.8f);
			hint.Top.Set(98, 0);
			hint.TextColor = Color.LightGray;
			panel.Append(hint);
		}

		public override void OnActivate()
		{
			search.SetText("");
			search.Focus();
			refreshTimer = RefreshInterval;
			seenVersion = -1;
		}

		public override void OnDeactivate() => search.Unfocus();

		public override void Update(GameTime gameTime)
		{
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
				info.SetText(UIHelpers.T("ConnectedChests", Terminal.ChestCount));
				grid.EmptyText = UIHelpers.T(Terminal.StoredItems.Count == 0 ? "TerminalEmpty" : "NoResults");
			}

			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}

		private void OnItemClicked(int type, bool rightClick)
		{
			Terminal.Close();

			if (rightClick) {
				ModContent.GetInstance<RecipeIndexSystem>().ShowItem(type, false);
				return;
			}

			if (Main.keyState.PressingShift()) {
				int found = Terminal.Highlight(type);
				Main.NewText(UIHelpers.T("FoundIn", Lang.GetItemNameValue(type), found), Color.Gold);
				return;
			}

			// The panel closes so the door can be seen opening.
			Terminal.RequestWithdraw(type);
		}
	}
}

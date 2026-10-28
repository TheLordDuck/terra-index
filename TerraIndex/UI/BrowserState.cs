using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	public class BrowserState : UIState
	{
		private UIPanel panel;
		private SearchBox search;
		private ItemGrid grid;
		private RecipeView view;
		// Layout applied last: docked next to the terminal panel or centered, and for which screen width.
		private bool docked;
		private float layoutWidth;

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(UIHelpers.IndexWidth, 0);
			panel.Height.Set(600, 0);
			panel.HAlign = panel.VAlign = 0.5f;
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("Title"), 1.1f));

			var close = new UITextPanel<string>("X");
			close.Width.Set(34, 0);
			close.Height.Set(30, 0);
			close.HAlign = 1f;
			close.Top.Set(-4, 0);
			close.OnLeftClick += (_, _) => ModContent.GetInstance<RecipeIndexSystem>().Close();
			panel.Append(close);

			var help = new HelpButton("HelpIndex");
			help.HAlign = 1f;
			help.Left.Set(-40, 0);
			help.Top.Set(-4, 0);
			panel.Append(help);

			grid = new ItemGrid(() => RecipeDatabase.AllItems.Select(type => ContentSamples.ItemsByType[type]), upgradesTab: true);
			grid.Top.Set(76, 0);
			grid.Width.Set(400, 0);
			grid.Height.Set(-76, 1f);
			panel.Append(grid);

			search = new SearchBox(UIHelpers.T("SearchHint"));
			search.Top.Set(34, 0);
			search.Width.Set(400, 0);
			search.Height.Set(34, 0);
			search.OnTextChanged += grid.SetFilter;
			panel.Append(search);

			view = new RecipeView();
			view.Left.Set(412, 0);
			view.Top.Set(34, 0);
			view.Width.Set(-412, 1f);
			view.Height.Set(-34, 1f);
			panel.Append(view);

			grid.OnItemClicked += view.Show;
		}

		public override void OnActivate()
		{
			Layout();
			// The item list only exists once recipes are loaded, not when the UI is initialized; and the Next upgrades tab
			// follows the player's gear and the bosses beaten.
			grid.Refresh();
		}

		public override void OnDeactivate() => search.Unfocus();

		public void Show(int type, bool uses) => view.Show(type, uses);

		// Next to the terminal panel while it is open (see RecipeIndexSystem), so both can be used at once; centered otherwise.
		private void Layout()
		{
			bool dock = ModContent.GetInstance<TerminalSystem>().PanelOpen;
			float screenWidth = GetDimensions().Width;
			if (dock == docked && screenWidth == layoutWidth)
				return;
			docked = dock;
			layoutWidth = screenWidth;

			if (dock) {
				var (left, width, _) = UIHelpers.DockLayout(screenWidth);
				panel.HAlign = 0f;
				panel.Left.Set(left, 0);
				panel.Width.Set(width, 0);
			}
			else {
				panel.HAlign = 0.5f;
				panel.Left.Set(0, 0);
				panel.Width.Set(UIHelpers.IndexWidth, 0);
			}
			Recalculate();
		}

		public override void Update(GameTime gameTime)
		{
			Layout();
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}
}

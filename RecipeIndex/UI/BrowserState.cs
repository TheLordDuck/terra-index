using System.Linq;
using Microsoft.Xna.Framework;
using RecipeIndex.Common;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex.UI
{
	public class BrowserState : UIState
	{
		private UIPanel panel;
		private SearchBox search;
		private ItemGrid grid;
		private RecipeView view;

		public override void OnInitialize()
		{
			panel = new UIPanel();
			panel.Width.Set(860, 0);
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

			grid = new ItemGrid(() => RecipeDatabase.AllItems.Select(type => ContentSamples.ItemsByType[type]));
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
			// La lista de objetos solo existe cuando las recetas ya están cargadas, no al inicializar la UI.
			if (grid.Count == 0)
				grid.Refresh();
		}

		public override void OnDeactivate() => search.Unfocus();

		public void Show(int type, bool uses) => view.Show(type, uses);

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}
}

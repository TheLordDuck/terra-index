using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
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
		// The Calamity guide's button (only with Calamity) and what its label and the class button's show.
		private UITextPanel<string> guideButton;
		private UITextPanel<string> classButton;
		private string guideShown;
		private string classShown;
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

			// The Guide tab is easy to miss among the icon tabs, so it also has a labelled button, which shows the class
			// and the stage.
			if (CalamityGuide.Enabled) {
				guideButton = new UITextPanel<string>("", 0.85f);
				guideButton.Left.Set(UIHelpers.Measure(UIHelpers.T("Title"), 1.1f).X + 16, 0);
				guideButton.Top.Set(-6, 0);
				guideButton.Height.Set(30, 0);
				guideButton.SetPadding(7);
				guideButton.OnLeftClick += (_, _) => grid.SetCategory(grid.Category == ItemCategory.Guide ? ItemCategory.All : ItemCategory.Guide);
				panel.Append(guideButton);
			}

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

			grid = new ItemGrid(() => RecipeDatabase.AllItems.Select(type => ContentSamples.ItemsByType[type]), guideTab: true);
			grid.Top.Set(76, 0);
			grid.Width.Set(400, 0);
			grid.Height.Set(-76, 1f);
			panel.Append(grid);

			search = new SearchBox(UIHelpers.T("SearchHint"));
			search.Top.Set(34, 0);
			search.Width.Set(230, 0);
			search.Height.Set(34, 0);
			search.OnTextChanged += grid.SetFilter;
			panel.Append(search);

			// Combat class filter: left click the next class, right click the previous one. In the Guide, the class
			// the guide is for (saved with the character).
			classButton = new UITextPanel<string>("", 0.8f);
			classButton.Left.Set(236, 0);
			classButton.Top.Set(34, 0);
			classButton.Width.Set(164, 0);
			classButton.Height.Set(34, 0);
			classButton.OnLeftClick += (_, _) => CycleClass(1);
			classButton.OnRightClick += (_, _) => CycleClass(-1);
			panel.Append(classButton);

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

		private static CalamityGuidePlayer GuidePlayer => Main.LocalPlayer.GetModPlayer<CalamityGuidePlayer>();

		// All classes, then each one in ItemClasses.All, round and round. In the Guide, the guide's five classes.
		private void CycleClass(int step)
		{
			if (grid.Category == ItemCategory.Guide) {
				GuidePlayer.CycleClass(step);
				grid.Refresh();
				SoundEngine.PlaySound(SoundID.MenuTick);
				return;
			}
			var classes = new List<DamageClass> { null };
			classes.AddRange(ItemClasses.All);
			int index = classes.IndexOf(grid.ClassFilter);
			grid.SetClass(classes[((index + step) % classes.Count + classes.Count) % classes.Count]);
		}

		private string ClassLabel()
		{
			string text = grid.Category == ItemCategory.Guide ? CalamityGuide.ClassName(GuidePlayer.GuideClass)
				: grid.ClassFilter == null ? UIHelpers.T("ClassAll") : ItemClasses.Name(grid.ClassFilter);
			return UIHelpers.Fit(text, 148, 0.8f);
		}

		private string GuideLabel() => UIHelpers.Fit(UIHelpers.T("GuideButton", CalamityGuide.ClassName(GuidePlayer.GuideClass),
			CalamityGuide.StageName(CalamityGuide.Stage())), 420, 0.85f);

		// Labels follow the tab, the class and the stage; checked twice a second, since the stage asks Calamity.
		private void UpdateLabels()
		{
			// Opening the Guide without a class picks the held weapon's, if the guide has it.
			if (grid.Category == ItemCategory.Guide && GuidePlayer.GuideClass == null) {
				Item held = Main.LocalPlayer.HeldItem;
				string guess = held.IsAir ? null : ItemClasses.Of(held.type) is DamageClass damageClass ? CalamityGuide.ClassOf(damageClass) : null;
				if (guess != null) {
					GuidePlayer.GuideClass = guess;
					grid.Refresh();
				}
			}
			string label = ClassLabel();
			if (label != classShown)
				classButton.SetText(classShown = label);
			if (guideButton != null && (guideShown == null || Main.GameUpdateCount % 30 == 0)) {
				label = GuideLabel();
				if (label != guideShown)
					guideButton.SetText(guideShown = label);
			}
		}

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
			UpdateLabels();
			if (guideButton != null) {
				bool on = grid.Category == ItemCategory.Guide;
				guideButton.BackgroundColor = (on ? new Color(220, 170, 60) : guideButton.IsMouseHovering ? new Color(185, 140, 50) : new Color(150, 110, 40)) * 0.9f;
			}
			classButton.BackgroundColor = (grid.ClassFilter != null || grid.Category == ItemCategory.Guide ? new Color(100, 120, 210) : classButton.IsMouseHovering ? new Color(80, 95, 170) : new Color(63, 82, 151)) * 0.8f;
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}
}

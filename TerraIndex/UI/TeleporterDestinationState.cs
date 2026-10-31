using System;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Where to go from a wireless teleporter with two or more others on its channel: one row per teleporter, with its
	// name and how far it is. Opened by right clicking the teleporter; built each time it opens.
	public class TeleporterDestinationState : UIState
	{
		private const float Width = 400;
		private const float RowHeight = 34;
		private const float MaxListHeight = 8 * RowHeight;

		private static readonly Color Normal = new Color(63, 82, 151) * 0.8f;
		private static readonly Color Hover = new Color(85, 110, 200);

		private UIPanel panel;

		private static TeleporterSystem Links => ModContent.GetInstance<TeleporterSystem>();

		public override void OnInitialize()
		{
			Point here = Links.MenuTeleporter;
			var destinations = Links.Destinations(here);

			panel = new UIPanel();
			panel.Width.Set(Width, 0);
			panel.HAlign = panel.VAlign = 0.5f;
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("TeleporterPickTitle"), 1f));
			var from = new UIText(UIHelpers.Fit(UIHelpers.T("TeleporterFrom", Links.Label(here)), Width - 70, 0.8f), 0.8f);
			from.Top.Set(26, 0);
			from.TextColor = Color.LightGray;
			panel.Append(from);

			var close = new UITextPanel<string>("X");
			close.Width.Set(34, 0);
			close.Height.Set(30, 0);
			close.HAlign = 1f;
			close.Top.Set(-4, 0);
			close.OnLeftClick += (_, _) => Links.CloseMenu();
			panel.Append(close);

			float top = 52;
			var list = new UIList();
			list.Top.Set(top, 0);
			list.Width.Set(-24, 1f);
			list.Height.Set(Math.Min(destinations.Count * (RowHeight + 4), MaxListHeight), 0);
			list.ListPadding = 4;
			list.ManualSortMethod = _ => { };
			foreach (Point destination in destinations) {
				string text = $"{Links.Label(destination)} — {TeleporterSystem.Offset(here, destination)}";
				list.Add(Row(UIHelpers.Fit(text, Width - 70, 0.85f), () => Go(destination)));
			}
			panel.Append(list);

			var scrollbar = new UIScrollbar();
			scrollbar.Top.Set(top, 0);
			scrollbar.Height.Set(list.Height.Pixels, 0);
			scrollbar.HAlign = 1f;
			list.SetScrollbar(scrollbar);
			panel.Append(scrollbar);
			top += list.Height.Pixels + 6;

			var hint = new UIText(UIHelpers.T("TeleporterPickHint"), 0.7f);
			hint.Top.Set(top, 0);
			hint.TextColor = Color.Gray;
			panel.Append(hint);
			top += 20;

			panel.Height.Set(top + 18, 0);
		}

		private static UITextPanel<string> Row(string text, Action click)
		{
			var row = new UITextPanel<string>(text, 0.85f);
			row.Width.Set(0, 1f);
			row.Height.Set(RowHeight, 0);
			row.TextHAlign = 0f;
			row.BackgroundColor = Normal;
			row.OnMouseOver += (_, _) => {
				row.BackgroundColor = Hover;
				SoundEngine.PlaySound(SoundID.MenuTick);
			};
			row.OnMouseOut += (_, _) => row.BackgroundColor = Normal;
			row.OnLeftClick += (_, _) => click();
			return row;
		}

		private static void Go(Point destination)
		{
			Links.CloseMenu();
			if (!WirelessTeleporterTile.CanReach(destination)) {
				Main.NewText(UIHelpers.T("TeleporterGone"), Color.Orange);
				return;
			}
			WirelessTeleporterTile.Teleport(Main.LocalPlayer, destination);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				UIHelpers.BlockMouse();
		}
	}
}

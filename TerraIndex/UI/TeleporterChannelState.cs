using System;
using System.Collections.Generic;
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
	// A wireless teleporter's channel menu, opened by right clicking it with the Teleporter Linker: the channels in
	// use (with how many teleporters each has), a new one, or none. Built each time it opens, for the channels then.
	public class TeleporterChannelState : UIState
	{
		private const float Width = 300;
		private const float RowHeight = 34;
		private const float MaxListHeight = 8 * RowHeight;

		private static readonly Color Normal = new Color(63, 82, 151) * 0.8f;
		private static readonly Color Hover = new Color(85, 110, 200);
		private static readonly Color Current = new Color(60, 140, 90);

		private UIPanel panel;

		private static TeleporterSystem Links => ModContent.GetInstance<TeleporterSystem>();

		public override void OnInitialize()
		{
			Point teleporter = Links.MenuTeleporter;
			int current = Links.ChannelOf(teleporter);
			var channels = Links.Channels();

			panel = new UIPanel();
			panel.Width.Set(Width, 0);
			panel.HAlign = panel.VAlign = 0.5f;
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("TeleporterMenuTitle"), 1f));

			var close = new UITextPanel<string>("X");
			close.Width.Set(34, 0);
			close.Height.Set(30, 0);
			close.HAlign = 1f;
			close.Top.Set(-4, 0);
			close.OnLeftClick += (_, _) => Links.CloseMenu();
			panel.Append(close);

			float top = 36;
			panel.Append(Button(UIHelpers.T("TeleporterNewChannel"), top, false, () => Pick(teleporter, TeleporterSystem.NewChannel, channels)));
			top += RowHeight + 6;

			if (channels.Count > 0) {
				var list = new UIList();
				list.Top.Set(top, 0);
				list.Width.Set(-24, 1f);
				list.Height.Set(Math.Min(channels.Count * (RowHeight + 4), MaxListHeight), 0);
				list.ListPadding = 4;
				foreach (var (channel, count) in channels) {
					var row = Button(UIHelpers.T("TeleporterChannelCount", channel, count), 0, channel == current, () => Pick(teleporter, channel, channels));
					row.Width.Set(0, 1f);
					list.Add(row);
				}
				panel.Append(list);

				var scrollbar = new UIScrollbar();
				scrollbar.Top.Set(top, 0);
				scrollbar.Height.Set(list.Height.Pixels, 0);
				scrollbar.HAlign = 1f;
				list.SetScrollbar(scrollbar);
				panel.Append(scrollbar);
				top += list.Height.Pixels + 6;
			}
			else {
				var none = new UIText(UIHelpers.T("TeleporterNoChannels"), 0.8f);
				none.Top.Set(top, 0);
				none.TextColor = Color.LightGray;
				panel.Append(none);
				top += 26;
			}

			if (current != TeleporterSystem.NoChannel) {
				panel.Append(Button(UIHelpers.T("TeleporterClearChannel"), top, false, () => Pick(teleporter, TeleporterSystem.NoChannel, channels)));
				top += RowHeight + 6;
			}

			panel.Height.Set(top + 18, 0);
		}

		private static UITextPanel<string> Button(string text, float top, bool current, Action click)
		{
			var button = new UITextPanel<string>(text, 0.85f);
			button.Top.Set(top, 0);
			button.Width.Set(0, 1f);
			button.Height.Set(RowHeight, 0);
			button.BackgroundColor = current ? Current : Normal;
			button.OnMouseOver += (_, _) => {
				button.BackgroundColor = Hover;
				SoundEngine.PlaySound(SoundID.MenuTick);
			};
			button.OnMouseOut += (_, _) => button.BackgroundColor = current ? Current : Normal;
			button.OnLeftClick += (_, _) => click();
			return button;
		}

		private static void Pick(Point teleporter, int channel, List<(int Channel, int Count)> channels)
		{
			// In multiplayer the server picks the new channel's number; it is the same one unless someone else just made one.
			int shown = channel == TeleporterSystem.NewChannel ? (channels.Count == 0 ? 1 : channels[^1].Channel + 1) : channel;
			Links.RequestChannel(teleporter, channel);
			Links.CloseMenu();
			if (shown == TeleporterSystem.NoChannel)
				Main.NewText(UIHelpers.T("TeleporterChannelCleared"), Color.LightGray);
			else
				Main.NewText(UIHelpers.T("TeleporterChannelSet", shown), Color.LightGreen);
			SoundEngine.PlaySound(SoundID.Unlock);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				Main.LocalPlayer.mouseInterface = true;
		}
	}
}

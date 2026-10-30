using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Common;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// The Calamity guide book, drawn as an open book. On the left page, the guide's build (CalamityGuide) for a class at
	// any of its stages, by section, starting at the world's stage; clicking an item shows how to craft it in the recipe
	// index. On the right page, the bosses of the playthrough with its events and minibosses (CalamityBosses): how many
	// are beaten, the next ones, and the list of the missing ones (or all of them).
	public class GuideBookState : UIState
	{
		private const float PanelWidth = 920;
		private const float PanelHeight = 620;
		// Room for the cover around the pages.
		private const float Padding = 22;
		private const float InnerWidth = PanelWidth - 2 * Padding;
		private const float GearWidth = 570;
		private const float BossLeft = GearWidth + 20;
		private const float BossWidth = InnerWidth - BossLeft;
		private const float ButtonHeight = 32;
		private const float GearTop = 118;
		// How many of the bosses still to beat are marked as next.
		private const int NextCount = 3;

		// Leather buttons on the parchment.
		private static readonly Color Normal = new Color(110, 70, 40) * 0.95f;
		private static readonly Color Hover = new Color(150, 95, 55);
		private static readonly Color Selected = new Color(165, 35, 45);
		private static readonly Color Gold = new Color(255, 200, 80);

		private static Texture2D ownedDot;

		private BookPanel panel;
		private readonly Dictionary<string, UITextPanel<string>> classButtons = new();
		private readonly List<UITextPanel<string>> buttons = new();
		private UIText stageLabel;
		private UITextPanel<string> currentButton;
		private UITextPanel<string> downedButton;
		private UIList gear;
		private UIList bossList;
		private TextBlock summary;

		// The stage shown and the world's one.
		private int stage;
		private int currentStage;
		private string shownClass;
		private int shownStage = -1;

		// Each row beaten or not, and the next bosses (indexes in CalamityBosses.All). Asked twice a second rather than
		// every frame, since Calamity's are a mod call.
		private bool[] downed;
		private HashSet<int> next = new();
		private bool hideDowned = true;

		private static CalamityGuidePlayer GuidePlayer => Main.LocalPlayer.GetModPlayer<CalamityGuidePlayer>();

		public static void Unload()
		{
			Texture2D texture = ownedDot;
			ownedDot = null;
			if (texture != null)
				Main.QueueMainThreadAction(texture.Dispose);
		}

		public override void OnInitialize()
		{
			panel = new BookPanel { SpineX = GearWidth + 10 };
			panel.Width.Set(PanelWidth, 0);
			panel.Height.Set(PanelHeight, 0);
			panel.SetPadding(Padding);
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("BookTitle"), 1.1f));

			var close = Button("X", 0, -4, 34, () => ModContent.GetInstance<GuideBookSystem>().Close());
			close.HAlign = 1f;

			// The guide's classes, the picked one highlighted (saved with the character, shared with the recipe index).
			float left = 0;
			foreach (string guideClass in CalamityGuide.Classes) {
				var button = Button(CalamityGuide.ClassName(guideClass), left, 36, 110, () => {
					GuidePlayer.GuideClass = guideClass;
					SoundEngine.PlaySound(SoundID.MenuTick);
				});
				classButtons[guideClass] = button;
				left += 114;
			}

			// The stage: back, forward, its name, and back to the world's one.
			Button("<", 0, 76, 40, () => SetStage(stage - 1));
			Button(">", 46, 76, 40, () => SetStage(stage + 1));
			stageLabel = new UIText("", 0.85f);
			stageLabel.Left.Set(96, 0);
			stageLabel.Top.Set(84, 0);
			panel.Append(stageLabel);
			currentButton = Button(UIHelpers.T("BookCurrentButton"), GearWidth - 120, 76, 120, () => SetStage(currentStage));

			gear = NewList(0, GearTop, GearWidth - 26);

			// Short of the close button.
			summary = new TextBlock(BossWidth - 40, 0.85f);
			summary.Left.Set(BossLeft, 0);
			summary.Top.Set(0, 0);
			panel.Append(summary);

			downedButton = Button("", BossLeft, 0, BossWidth, () => {
				hideDowned = !hideDowned;
				RebuildBosses();
			});
			downedButton.VAlign = 1f;

			bossList = NewList(BossLeft, 120, BossWidth - 26, -ButtonHeight - 6);

			currentStage = stage = CalamityGuide.Stage();
			// Without a class yet, the held weapon's, if the guide has it.
			Item held = Main.LocalPlayer.HeldItem;
			if (GuidePlayer.GuideClass == null && !held.IsAir && ItemClasses.Of(held.type) is DamageClass damageClass)
				GuidePlayer.GuideClass = CalamityGuide.ClassOf(damageClass);
			RefreshBosses();
			RebuildGear();
		}

		private UITextPanel<string> Button(string text, float left, float top, float width, Action click)
		{
			var button = new UITextPanel<string>(UIHelpers.Fit(text, width - 14, 0.8f), 0.8f);
			button.Left.Set(left, 0);
			button.Top.Set(top, 0);
			button.Width.Set(width, 0);
			button.Height.Set(ButtonHeight, 0);
			button.SetPadding(7);
			button.BorderColor = new Color(60, 35, 20);
			button.OnMouseOver += (_, _) => SoundEngine.PlaySound(SoundID.MenuTick);
			button.OnLeftClick += (_, _) => click();
			panel.Append(button);
			buttons.Add(button);
			return button;
		}

		// A scrolling list and its scrollbar, from top to the panel's bottom (less bottomSpace).
		private UIList NewList(float left, float top, float width, float bottomSpace = 0)
		{
			var list = new UIList();
			list.Left.Set(left, 0);
			list.Top.Set(top, 0);
			list.Width.Set(width, 0);
			list.Height.Set(bottomSpace - top, 1f);
			list.ListPadding = 4;
			// Keeps the order they are added in.
			list.ManualSortMethod = _ => { };
			panel.Append(list);

			var scrollbar = new UIScrollbar();
			scrollbar.Left.Set(left + width + 6, 0);
			scrollbar.Top.Set(top + 6, 0);
			scrollbar.Height.Set(bottomSpace - top - 12, 1f);
			list.SetScrollbar(scrollbar);
			panel.Append(scrollbar);
			return list;
		}

		private void SetStage(int value)
		{
			stage = Math.Clamp(value, 0, CalamityGuide.StageCount - 1);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private void RebuildGear()
		{
			shownClass = GuidePlayer.GuideClass;
			shownStage = stage;
			string label = UIHelpers.T("BookStage", stage + 1, CalamityGuide.StageCount, CalamityGuide.StageName(stage));
			if (stage == currentStage)
				label += " " + UIHelpers.T("BookCurrentStage");
			stageLabel.SetText(UIHelpers.Fit(label, GearWidth - 96 - 130, 0.85f));

			gear.Clear();
			float width = gear.Width.Pixels;
			if (shownClass == null) {
				gear.Add(new TextBlock(width, 0.9f, UIHelpers.T("BookPickClass")));
				return;
			}
			var items = CalamityGuide.Items(shownClass, stage);
			if (items.Count == 0) {
				gear.Add(new TextBlock(width, 0.9f, UIHelpers.T("GuideEmpty")));
				return;
			}
			gear.Add(new TextBlock(width, 0.75f, UIHelpers.T("BookHint")) { Color = Color.LightGray });
			foreach (var section in items.GroupBy(entry => entry.Section))
				gear.Add(new GearSection(UIHelpers.T("BookSection" + section.Key), section.Select(entry => entry.Type).ToList(), width, gear));
		}

		private void RefreshBosses()
		{
			var all = CalamityBosses.All;
			bool[] now = all.Select(boss => boss.IsDowned()).ToArray();
			int stageNow = CalamityGuide.Stage();
			// A boss beaten while the book is open moves it on, if it was showing the world's stage.
			if (stageNow != currentStage && stage == currentStage)
				stage = stageNow;
			currentStage = stageNow;
			if (downed != null && now.SequenceEqual(downed))
				return;
			downed = now;
			var rows = Enumerable.Range(0, all.Count);
			next = rows.Where(i => all[i].Required && !downed[i]).Take(NextCount).ToHashSet();

			var others = rows.Where(i => all[i].Kind != GuideBossKind.Boss).ToList();
			string nextNames = string.Join(", ", next.Select(i => all[i].Name));
			summary.SetText(UIHelpers.T("BookBossCount", rows.Count(i => all[i].Required && downed[i]), all.Count(boss => boss.Required)) + "\n"
				+ UIHelpers.T("BookEventCount", others.Count(i => downed[i]), others.Count) + "\n"
				+ (next.Count > 0 ? UIHelpers.T("BookNextBosses", nextNames) : UIHelpers.T("BookAllBeaten")));
			RebuildBosses();
		}

		private void RebuildBosses()
		{
			downedButton.SetText(UIHelpers.T(hideDowned ? "BookShowAll" : "BookHideDowned"));
			bossList.Clear();
			var all = CalamityBosses.All;
			float width = bossList.Width.Pixels;
			int tier = -1;
			for (int i = 0; i < all.Count; i++) {
				if (hideDowned && downed[i])
					continue;
				if (all[i].Tier != tier) {
					tier = all[i].Tier;
					bossList.Add(new TextBlock(width, 0.85f, UIHelpers.T("BookTier" + tier)) { Color = Gold });
				}
				bossList.Add(new BossRow(all[i], downed[i], next.Contains(i), width));
			}
			if (bossList.Count == 0)
				bossList.Add(new TextBlock(width, 0.85f, UIHelpers.T("BookAllBeaten")));
		}

		public override void Update(GameTime gameTime)
		{
			UIHelpers.Place(panel, PanelWidth, PanelHeight);
			base.Update(gameTime);
			if (Main.GameUpdateCount % 30 == 0)
				RefreshBosses();
			if (shownClass != GuidePlayer.GuideClass || shownStage != stage)
				RebuildGear();

			foreach (var button in buttons)
				button.BackgroundColor = button.IsMouseHovering ? Hover : Normal;
			foreach (var (guideClass, button) in classButtons) {
				if (guideClass == GuidePlayer.GuideClass)
					button.BackgroundColor = Selected;
			}
			if (stage == currentStage)
				currentButton.BackgroundColor = Selected;
			if (panel.ContainsPoint(Main.MouseScreen))
				UIHelpers.BlockMouse();
		}

		// The book opens from an item use, after this frame's UpdateUI, so it is drawn once before its first Update:
		// placed here too, or that frame shows it in the top left corner and it jumps to the middle on the next one.
		public override void Draw(SpriteBatch spriteBatch)
		{
			UIHelpers.Place(panel, PanelWidth, PanelHeight);
			base.Draw(spriteBatch);
		}

		// Shows an item in the recipe index, in place of the book.
		private static void ShowInIndex(int type, bool uses)
		{
			ModContent.GetInstance<GuideBookSystem>().Close();
			ModContent.GetInstance<RecipeIndexSystem>().ShowItem(type, uses);
		}

		private static void Fill(SpriteBatch spriteBatch, Rectangle area, Color color) =>
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, color);

		// The mark on the items the player has: a green dot with a light grey rim, smoothed at the edge. Made once.
		private static Texture2D OwnedDot()
		{
			if (ownedDot != null)
				return ownedDot;
			const int size = 24;
			float radius = size / 2f;
			var fill = new Color(70, 205, 85);
			var rim = new Color(225, 225, 220);
			var data = new Color[size * size];
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius));
					Color color = Color.Lerp(fill, rim, Math.Clamp(distance - (radius - 5f), 0f, 1f));
					// Premultiplied, like the game's sprites.
					data[y * size + x] = color * Math.Clamp(radius - distance, 0f, 1f);
				}
			}
			ownedDot = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			ownedDot.SetData(data);
			return ownedDot;
		}

		// The panel drawn as an open book: a red leather cover with a gold trim, two parchment pages shaded towards the
		// spine, and a ribbon bookmark hanging below.
		private class BookPanel : UIPanel
		{
			private static readonly Color CoverEdge = new Color(55, 12, 16);
			private static readonly Color Cover = new Color(125, 28, 34);
			private static readonly Color Trim = new Color(205, 165, 75);
			private static readonly Color Parchment = new Color(222, 203, 160);
			private static readonly Color Ribbon = new Color(175, 35, 45);

			// Where the pages meet, from the inner left edge.
			public float SpineX { get; init; }

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				Rectangle outer = GetDimensions().ToRectangle();
				int spine = (int)(GetInnerDimensions().X + SpineX);

				// The ribbon first, so the cover hides its top.
				// 14 wide, its end cut in a V.
				const int half = 7;
				int ribbonX = spine + 40;
				int notchTop = outer.Bottom + 21;
				Fill(spriteBatch, new Rectangle(ribbonX, outer.Bottom - 20, 2 * half, notchTop - outer.Bottom + 20), Ribbon);
				for (int row = 0; row < half; row++) {
					Fill(spriteBatch, new Rectangle(ribbonX, notchTop + row, half - row, 1), Ribbon);
					Fill(spriteBatch, new Rectangle(ribbonX + half + row, notchTop + row, half - row, 1), Ribbon);
				}
				Fill(spriteBatch, new Rectangle(ribbonX + half, outer.Bottom, 1, notchTop - outer.Bottom), Color.Black * 0.25f);

				Fill(spriteBatch, outer, CoverEdge);
				Fill(spriteBatch, Inset(outer, 3), Cover);
				Frame(spriteBatch, Inset(outer, 6), Trim);

				int margin = 11;
				var leftPage = new Rectangle(outer.X + margin, outer.Y + margin, spine - outer.X - margin, outer.Height - 2 * margin);
				var rightPage = new Rectangle(spine, outer.Y + margin, outer.Right - margin - spine, outer.Height - 2 * margin);
				// The stacked pages' edges under each page.
				Fill(spriteBatch, new Rectangle(leftPage.X - 2, leftPage.Y + 2, leftPage.Width, leftPage.Height), new Color(180, 160, 120));
				Fill(spriteBatch, new Rectangle(rightPage.X + 2, rightPage.Y + 2, rightPage.Width, rightPage.Height), new Color(180, 160, 120));
				Page(spriteBatch, leftPage, false);
				Page(spriteBatch, rightPage, true);
				Fill(spriteBatch, new Rectangle(spine - 1, outer.Y + 4, 2, outer.Height - 8), CoverEdge * 0.6f);
			}

			// A page, darker towards the spine (on its left for the right page) and a little at its outer edge.
			private static void Page(SpriteBatch spriteBatch, Rectangle page, bool spineOnLeft)
			{
				Fill(spriteBatch, page, Parchment);
				const int shade = 18;
				for (int i = 0; i < shade; i++) {
					float strength = 0.22f * (1f - i / (float)shade);
					int x = spineOnLeft ? page.X + i : page.Right - 1 - i;
					Fill(spriteBatch, new Rectangle(x, page.Y, 1, page.Height), Color.Black * strength);
				}
				int edge = spineOnLeft ? page.Right - 3 : page.X;
				Fill(spriteBatch, new Rectangle(edge, page.Y, 3, page.Height), Color.Black * 0.06f);
			}

			private static Rectangle Inset(Rectangle area, int amount) =>
				new(area.X + amount, area.Y + amount, area.Width - 2 * amount, area.Height - 2 * amount);

			private static void Frame(SpriteBatch spriteBatch, Rectangle area, Color color)
			{
				Fill(spriteBatch, new Rectangle(area.X, area.Y, area.Width, 1), color);
				Fill(spriteBatch, new Rectangle(area.X, area.Bottom - 1, area.Width, 1), color);
				Fill(spriteBatch, new Rectangle(area.X, area.Y, 1, area.Height), color);
				Fill(spriteBatch, new Rectangle(area.Right - 1, area.Y, 1, area.Height), color);
			}
		}

		// Wrapped text, as tall as its lines.
		private class TextBlock : UIElement
		{
			private const float LineHeight = 22;

			private readonly float maxWidth;
			private readonly float scale;
			private List<string> lines = new();

			public Color Color { get; init; } = Color.White;

			public TextBlock(float width, float scale, string text = "")
			{
				maxWidth = width;
				this.scale = scale;
				Width.Set(width, 0);
				SetText(text);
			}

			public void SetText(string text)
			{
				lines = text.Split('\n').SelectMany(line => UIHelpers.Wrap(line, maxWidth, scale)).ToList();
				Height.Set(lines.Count * LineHeight * scale + 4, 0);
				Recalculate();
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				Vector2 position = GetDimensions().Position();
				for (int i = 0; i < lines.Count; i++)
					Utils.DrawBorderString(spriteBatch, lines[i], position + new Vector2(2, 2 + i * LineHeight * scale), Color, scale);
			}
		}

		// One section of the build (armor, weapons...): its name and its items. A green dot marks the ones the player has.
		private class GearSection : UIElement
		{
			private const float Cell = 46;
			private const float Scale = 0.85f;
			private const float HeaderHeight = 26;
			private const int DotSize = 12;

			private readonly string title;
			private readonly List<int> items;
			private readonly int columns;
			// The list it scrolls in: items scrolled out of its view don't take the mouse.
			private readonly UIElement view;

			public GearSection(string title, List<int> items, float width, UIElement view)
			{
				this.title = title;
				this.items = items;
				this.view = view;
				columns = Math.Max(1, (int)(width / Cell));
				Width.Set(width, 0);
				Height.Set(HeaderHeight + (items.Count + columns - 1) / columns * Cell + 4, 0);
			}

			private int IndexAt(Vector2 mouse)
			{
				if (!view.ContainsPoint(mouse))
					return -1;
				Vector2 offset = mouse - GetDimensions().Position() - new Vector2(0, HeaderHeight);
				if (offset.X < 0 || offset.Y < 0)
					return -1;
				int column = (int)(offset.X / Cell), row = (int)(offset.Y / Cell);
				int index = row * columns + column;
				return column < columns && index < items.Count ? index : -1;
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				Vector2 position = GetDimensions().Position();
				Utils.DrawBorderString(spriteBatch, $"{title} ({items.Count})", position + new Vector2(2, 2), Gold, 0.9f);
				bool hoverable = view.ContainsPoint(Main.MouseScreen);
				HashSet<int> owned = Owned();
				for (int i = 0; i < items.Count; i++) {
					Vector2 slot = position + new Vector2(i % columns * Cell, HeaderHeight + i / columns * Cell);
					UIHelpers.DrawItem(spriteBatch, ContentSamples.ItemsByType[items[i]], slot, Scale, hoverable);
					if (owned.Contains(items[i]))
						spriteBatch.Draw(OwnedDot(), new Rectangle((int)slot.X + 30, (int)slot.Y + 2, DotSize, DotSize), Color.White);
				}
			}

			// In the inventory, or worn.
			private static HashSet<int> Owned()
			{
				Player player = Main.LocalPlayer;
				return player.inventory.Concat(player.armor).Where(item => !item.IsAir).Select(item => item.type).ToHashSet();
			}

			public override void LeftClick(UIMouseEvent evt)
			{
				base.LeftClick(evt);
				int index = IndexAt(evt.MousePosition);
				if (index >= 0)
					ShowInIndex(items[index], false);
			}

			public override void RightClick(UIMouseEvent evt)
			{
				base.RightClick(evt);
				int index = IndexAt(evt.MousePosition);
				if (index >= 0)
					ShowInIndex(items[index], true);
			}
		}

		// A boss, event or miniboss: its icon, its name and whether it is beaten, next, optional, an event or a miniboss.
		private class BossRow : UIElement
		{
			private const float RowHeight = 32;

			private readonly GuideBoss boss;
			private readonly bool downed;
			private readonly bool next;

			public BossRow(GuideBoss boss, bool downed, bool next, float width)
			{
				this.boss = boss;
				this.downed = downed;
				this.next = next;
				Width.Set(width, 0);
				Height.Set(RowHeight, 0);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				CalculatedStyle dimensions = GetDimensions();
				Color background = downed ? new Color(40, 100, 55) : next ? new Color(150, 110, 40) : boss.Kind switch {
					GuideBossKind.Event => new Color(85, 55, 120),
					GuideBossKind.Miniboss => new Color(40, 95, 105),
					_ => boss.Optional ? new Color(60, 60, 75) : new Color(40, 45, 100),
				};
				Utils.DrawInvBG(spriteBatch, dimensions.ToRectangle(), background * 0.85f);

				DrawIcon(spriteBatch, new Vector2(dimensions.X + 18, dimensions.Y + RowHeight / 2), 24);

				string status = Status();
				float statusWidth = status.Length > 0 ? UIHelpers.Measure(status, 0.7f).X + 8 : 0;
				string name = UIHelpers.Fit(boss.Name, dimensions.Width - 44 - statusWidth, 0.8f);
				Utils.DrawBorderString(spriteBatch, name, new Vector2(dimensions.X + 36, dimensions.Y + 8), downed ? Color.LightGray : Color.White, 0.8f);
				if (status.Length > 0)
					Utils.DrawBorderString(spriteBatch, status, new Vector2(dimensions.X + dimensions.Width - statusWidth, dimensions.Y + 10), downed ? Color.LightGreen : Gold, 0.7f);
			}

			private string Status()
			{
				if (downed)
					return UIHelpers.T(boss.Kind == GuideBossKind.Boss ? "BookDowned" : "BookCompleted");
				if (next)
					return UIHelpers.T("BookNext");
				return boss.Kind switch {
					GuideBossKind.Event => UIHelpers.T("BookEvent"),
					GuideBossKind.Miniboss => UIHelpers.T("BookMiniboss"),
					_ => boss.Optional ? UIHelpers.T("BookOptional") : "",
				};
			}

			// Its item, or its first NPC's map icon, or else that NPC's sprite, shrunk to fit a size x size box.
			private void DrawIcon(SpriteBatch spriteBatch, Vector2 center, float size)
			{
				Texture2D texture = null;
				Rectangle frame = default;
				if (boss.IconItem > 0) {
					Main.instance.LoadItem(boss.IconItem);
					Main.GetItemDrawFrame(boss.IconItem, out texture, out frame);
				}
				else if (boss.Npcs.Length > 0) {
					int npc = boss.Npcs[0];
					int head = ContentSamples.NpcsByNetId.TryGetValue(npc, out NPC sample) ? sample.GetBossHeadTextureIndex() : -1;
					if (head >= 0 && head < TextureAssets.NpcHeadBoss.Length && TextureAssets.NpcHeadBoss[head] is { IsLoaded: true } asset) {
						texture = asset.Value;
						frame = texture.Bounds;
					}
					else {
						Main.instance.LoadNPC(npc);
						texture = TextureAssets.Npc[npc].Value;
						frame = new Rectangle(0, 0, texture.Width, texture.Height / Math.Max(1, Main.npcFrameCount[npc]));
					}
				}
				if (texture == null)
					return;
				float scale = Math.Min(1f, size / Math.Max(frame.Width, frame.Height));
				spriteBatch.Draw(texture, center, frame, Color.White, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
			}
		}
	}
}

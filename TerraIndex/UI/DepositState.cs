using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Deposit slots of a terminal, shown below the inventory like a chest.
	// When closed, the items go into the connected chests through the terminal's door (see TerminalSystem.FinishDeposit).
	public class DepositState : UIState
	{
		private const int Columns = 10;
		private const float SlotScale = 0.755f; // Same scale vanilla uses for chest slots
		private const int SlotSpacing = 42;

		private UIPanel panel;

		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		public override void OnInitialize()
		{
			int rows = (TerminalEntity.Slots + Columns - 1) / Columns;

			panel = new UIPanel();
			panel.Left.Set(66, 0);
			panel.Width.Set(Columns * SlotSpacing + 24, 0);
			panel.Height.Set(rows * SlotSpacing + 70, 0);
			Append(panel);

			panel.Append(new UIText(UIHelpers.T("DepositTitle"), 0.9f));

			var done = new UITextPanel<string>(UIHelpers.T("Done"), 0.8f);
			done.HAlign = 1f;
			done.Top.Set(-6, 0);
			done.OnLeftClick += (_, _) => Terminal.Close();
			panel.Append(done);

			for (int i = 0; i < TerminalEntity.Slots; i++) {
				var slot = new DepositSlot(i);
				slot.Left.Set(i % Columns * SlotSpacing, 0);
				slot.Top.Set(28 + i / Columns * SlotSpacing, 0);
				slot.Width.Set(SlotSpacing - 2, 0);
				slot.Height.Set(SlotSpacing - 2, 0);
				panel.Append(slot);
			}

			var hint = new UIText(UIHelpers.T("DepositHint"), 0.75f);
			hint.Top.Set(30 + rows * SlotSpacing, 0);
			hint.TextColor = Color.LightGray;
			panel.Append(hint);
		}

		public override void Update(GameTime gameTime)
		{
			// Just below the inventory, where vanilla draws an open chest.
			float top = Main.instance.invBottom + 10;
			if (panel.Top.Pixels != top) {
				panel.Top.Set(top, 0);
				panel.Recalculate();
			}

			base.Update(gameTime);
			if (panel.ContainsPoint(Main.MouseScreen))
				UIHelpers.BlockMouse();
		}

		// A vanilla item slot over one entry of TerminalSystem.DepositItems: same clicks, dragging and shift-click as a chest.
		private class DepositSlot : UIElement
		{
			private readonly int index;

			public DepositSlot(int index) => this.index = index;

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				Item[] items = Terminal.DepositItems;
				if (items == null || index >= items.Length)
					return;

				float oldScale = Main.inventoryScale;
				Main.inventoryScale = SlotScale;
				if (ContainsPoint(Main.MouseScreen) && !PlayerInput.IgnoreMouseInterface) {
					UIHelpers.BlockMouse();
					ItemSlot.Handle(items, ItemSlot.Context.BankItem, index);
				}
				ItemSlot.Draw(spriteBatch, items, ItemSlot.Context.BankItem, index, GetDimensions().Position());
				Main.inventoryScale = oldScale;
			}
		}
	}
}

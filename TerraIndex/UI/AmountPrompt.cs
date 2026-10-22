using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace TerraIndex.UI
{
	// Pop-up asking how many of an item to take out of the terminal (at most one stack).
	// The box starts empty, which means the maximum. Enter or Take confirms; Esc or Cancel closes it.
	// -/+ change the amount by 1 (Shift: 10).
	public class AmountPrompt : UIPanel
	{
		private readonly SearchBox box;
		private int type;
		private int max;
		private int stored;

		public event Action<int, int> OnConfirm;
		public event Action OnClose;

		public AmountPrompt()
		{
			Width.Set(300, 0);
			Height.Set(184, 0);
			HAlign = VAlign = 0.5f;
			BackgroundColor = new Color(45, 55, 120) * 0.97f;

			box = new SearchBox("") { ShowHintWhileTyping = true };
			box.Width.Set(90, 0);
			box.Height.Set(30, 0);
			box.HAlign = 0.5f;
			box.Top.Set(52, 0);
			box.OnTextChanged += KeepDigits;
			box.OnSubmit += Confirm;
			box.OnCancel += Close;
			Append(box);

			AddButton("-", -66, 52, 34, () => SetAmount(Current - Step));
			AddButton("+", 66, 52, 34, () => SetAmount(Current + Step));
			AddButton("1", -84, 90, 70, () => SetAmount(1));
			AddButton("10", 0, 90, 70, () => SetAmount(10));
			AddButton(UIHelpers.T("Max"), 84, 90, 70, () => SetAmount(max));
			AddButton(UIHelpers.T("Take"), -62, 128, 116, Confirm);
			AddButton(UIHelpers.T("Cancel"), 62, 128, 116, Close);
		}

		private void AddButton(string text, float offset, float top, float width, Action onClick)
		{
			var button = new UITextPanel<string>(text, 0.85f);
			button.Width.Set(width, 0);
			button.Height.Set(30, 0);
			button.HAlign = 0.5f;
			button.Left.Set(offset, 0);
			button.Top.Set(top, 0);
			button.OnLeftClick += (_, _) => {
				onClick();
				// Clicking a button takes the focus from the box; give it back so Enter still works.
				if (Parent != null)
					box.Focus();
			};
			button.OnMouseOver += (_, _) => button.BackgroundColor = new Color(80, 95, 170);
			button.OnMouseOut += (_, _) => button.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			Append(button);
		}

		public void Open(int itemType, int available)
		{
			type = itemType;
			stored = available;
			max = Math.Min(available, ContentSamples.ItemsByType[itemType].maxStack);
			box.Hint = max.ToString();
			box.SetText("");
			box.Focus();
		}

		public void Unfocus() => box.Unfocus();

		private static int Step => Main.keyState.PressingShift() ? 10 : 1;

		// 0 = empty box, which means the maximum.
		private int Amount => int.TryParse(box.Text, out int value) ? value : 0;
		private int Current => Amount > 0 ? Amount : max;

		private void SetAmount(int value) => box.SetText(Math.Clamp(value, 1, Math.Max(1, max)).ToString());

		private void KeepDigits(string text)
		{
			string digits = new string(text.Where(char.IsDigit).ToArray()).TrimStart('0');
			if (digits.Length > 0 && int.Parse(digits.Length > 6 ? digits[..6] : digits) > max)
				digits = max.ToString();
			if (digits != text)
				box.SetText(digits);
		}

		private void Confirm()
		{
			int amount = Math.Min(Current, max);
			box.Unfocus();
			OnConfirm?.Invoke(type, amount);
		}

		private void Close()
		{
			box.Unfocus();
			OnClose?.Invoke();
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (type <= 0)
				return;

			CalculatedStyle inner = GetInnerDimensions();
			Item sample = ContentSamples.ItemsByType[type];
			UIHelpers.DrawItem(spriteBatch, sample, new Vector2(inner.X, inner.Y), 0.8f);
			string name = UIHelpers.Fit(sample.Name, inner.Width - 48, 0.95f);
			Utils.DrawBorderString(spriteBatch, name, new Vector2(inner.X + 48, inner.Y + 2), ItemRarity.GetColor(sample.rare), 0.95f);
			string available = UIHelpers.Fit(UIHelpers.T("Available", stored, max), inner.Width - 48, 0.75f);
			Utils.DrawBorderString(spriteBatch, available, new Vector2(inner.X + 48, inner.Y + 22), Color.LightGray, 0.75f);
		}
	}
}

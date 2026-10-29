using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.Content.Combat
{
	// Measures the damage each Target Dummy takes, like the Practice Tool dummy in League of Legends: a fight starts
	// with the first hit and ends after a few seconds without damage, and the meter shows its total damage, its DPS
	// (over the time from the first hit to the last, so the wait at the end doesn't lower it) and the last combo's
	// damage (hits less than a second apart). The results stay on screen for a while after the fight, and the next hit
	// starts a new one. Counts your hits (items, projectiles and minions) and damage over time from debuffs; other
	// players' hits aren't counted, each player sees their own damage.
	public class DummyDamageMeter : GlobalNPC
	{
		public const int ComboGap = 60;
		public const int FightEnd = 4 * 60;
		public const int ShowFor = 15 * 60;

		uint fightStart;
		uint lastHit;

		public float Total { get; private set; }
		public float Combo { get; private set; }

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == NPCID.TargetDummy;

		public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) => Add(hit.Damage);

		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) => Add(hit.Damage);

		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			// Vanilla takes 1 life for every 120 points of negative lifeRegen, once per tick.
			if (!Main.dedServ && npc.lifeRegen < 0)
				Add(-npc.lifeRegen / 120f);
		}

		uint SinceLastHit => Main.GameUpdateCount - lastHit;

		public bool InFight => Total > 0f && SinceLastHit <= FightEnd;

		public bool Visible => Total > 0f && SinceLastHit <= FightEnd + ShowFor;

		// From the first hit to the last, at least one second.
		public float Seconds => Math.Max(1f, (lastHit - fightStart) / 60f);

		public float Dps => Total / Seconds;

		void Add(float amount)
		{
			if (Main.dedServ || amount <= 0f)
				return;
			if (!InFight) {
				Total = 0f;
				Combo = 0f;
				fightStart = Main.GameUpdateCount;
			}
			else if (SinceLastHit > ComboGap)
				Combo = 0f;
			Total += amount;
			Combo += amount;
			lastHit = Main.GameUpdateCount;
		}
	}

	// Draws the fight's total damage, DPS and last combo over each Target Dummy, greyed out once the fight is over. It is
	// an interface layer instead of the NPC's PostDraw because the dummy NPC is invisible (the dummy you see is the tile).
	public class DummyDamageMeterLayer : ModSystem
	{
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index < 0)
				return;
			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Dummy Damage Meter", () => {
				if (ClientConfig.Instance.DummyDamageMeter)
					Draw();
				return true;
			}, InterfaceScaleType.Game));
		}

		static void Draw()
		{
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.type != NPCID.TargetDummy || !npc.TryGetGlobalNPC(out DummyDamageMeter meter) || !meter.Visible)
					continue;
				bool fighting = meter.InFight;
				// Bottom line first.
				var lines = new (string Text, Color Color)[] {
					(UIHelpers.T("DummyCombo", (int)Math.Round(meter.Combo)), Color.White),
					(UIHelpers.T("DummyDps", Math.Round(meter.Dps, 1), Math.Round(meter.Seconds, 1)), new Color(255, 220, 120)),
					(UIHelpers.T("DummyTotal", (int)Math.Round(meter.Total)), new Color(255, 140, 90)),
				};
				var pos = npc.Top - Main.screenPosition - new Vector2(0f, 10f);
				foreach (var (text, color) in lines) {
					Utils.DrawBorderString(Main.spriteBatch, text, pos, fighting ? color : Color.Lerp(color, Color.Gray, 0.6f), 0.8f, 0.5f, 1f);
					pos.Y -= 20f;
				}
			}
		}
	}
}

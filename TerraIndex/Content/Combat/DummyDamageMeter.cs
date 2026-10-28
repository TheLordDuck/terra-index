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
	// Counts the damage each Target Dummy took in the last 10 seconds: your hits (items, projectiles and minions) and
	// damage over time from debuffs. Other players' hits aren't counted, each player sees their own damage.
	public class DummyDamageMeter : GlobalNPC
	{
		public const int Ticks = 10 * 60;

		// Damage taken on each of the last 10 seconds' ticks, indexed by GameUpdateCount % Ticks.
		readonly float[] damage = new float[Ticks];
		uint lastTick;

		// Damage since the dummy started taking it; starts over after 10 seconds without damage.
		public float Total { get; private set; }

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

		void Add(float amount)
		{
			if (Main.dedServ)
				return;
			if (Sum(Ticks) <= 0f)
				Total = 0f;
			damage[lastTick % Ticks] += amount;
			Total += amount;
		}

		public float Sum(int ticks)
		{
			Advance();
			float total = 0f;
			for (int i = 0; i < ticks; i++)
				total += damage[(lastTick - (uint)i) % Ticks];
			return total;
		}

		// Clears the ticks that went by since the last call, so they don't keep damage from 10 seconds ago.
		void Advance()
		{
			uint now = Main.GameUpdateCount;
			if (now == lastTick)
				return;
			if (now - lastTick >= Ticks)
				Array.Clear(damage);
			else
				for (uint t = lastTick + 1; t != now + 1; t++)
					damage[t % Ticks] = 0f;
			lastTick = now;
		}
	}

	// Draws the damage of the last 5 and 10 seconds with their DPS, and the total, over each Target Dummy. It is an interface layer
	// instead of the NPC's PostDraw because the dummy NPC is invisible (the dummy you see is the tile).
	public class DummyDamageMeterLayer : ModSystem
	{
		static readonly int[] WindowSeconds = { 5, 10 };

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
				if (npc.type != NPCID.TargetDummy || !npc.TryGetGlobalNPC(out DummyDamageMeter meter) || meter.Sum(DummyDamageMeter.Ticks) <= 0f)
					continue;
				var pos = npc.Top - Main.screenPosition - new Vector2(0f, 10f);
				// Bottom line first, so the longest window ends up on top.
				for (int i = 0; i < WindowSeconds.Length; i++) {
					int seconds = WindowSeconds[i];
					float total = meter.Sum(seconds * 60);
					string text = UIHelpers.T("DummyDamage", seconds, (int)Math.Round(total), Math.Round(total / seconds, 1));
					Utils.DrawBorderString(Main.spriteBatch, text, pos, i == 0 ? Color.White : new Color(255, 220, 120), 0.8f, 0.5f, 1f);
					pos.Y -= 20f;
				}
				Utils.DrawBorderString(Main.spriteBatch, UIHelpers.T("DummyTotal", (int)Math.Round(meter.Total)), pos, new Color(255, 140, 90), 0.8f, 0.5f, 1f);
			}
		}
	}
}

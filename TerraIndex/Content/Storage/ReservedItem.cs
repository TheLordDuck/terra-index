using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace TerraIndex.Content.Storage
{
	// Items taken out with the terminal, while they are on the ground: only the player who asked for them can pick them
	// up for a while, so another player standing by the door doesn't get them.
	public class ReservedItem : GlobalItem
	{
		public const int ReserveTicks = 60 * 10;

		public override bool InstancePerEntity => true;

		public int Owner = -1;
		public int Ticks;

		// Single player or server, right after Item.NewItem and before the item is synced.
		public static void Reserve(Item item, int player)
		{
			ReservedItem reserved = item.GetGlobalItem<ReservedItem>();
			reserved.Owner = player;
			reserved.Ticks = ReserveTicks;
			// Nothing else to set: Item.FindOwner (the server picking who may grab it) skips players CanPickup refuses.
			// Setting keepTime here would stop FindOwner from running at all, and nobody could take the item until it ran out.
		}

		public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
		{
			if (Ticks > 0 && --Ticks == 0)
				Owner = -1;
		}

		public override bool CanPickup(Item item, Player player) => Owner < 0 || Ticks <= 0 || player.whoAmI == Owner;

		// Once picked up it is a normal item again.
		public override bool OnPickup(Item item, Player player)
		{
			Owner = -1;
			Ticks = 0;
			return true;
		}

		// Also sent with every inventory and chest sync, so only a short when the item isn't reserved.
		public override void NetSend(Item item, BinaryWriter writer)
		{
			writer.Write((short)(Owner < 0 ? 0 : Ticks));
			if (Owner >= 0 && Ticks > 0)
				writer.Write((byte)Owner);
		}

		public override void NetReceive(Item item, BinaryReader reader)
		{
			Ticks = reader.ReadInt16();
			Owner = Ticks > 0 ? reader.ReadByte() : -1;
		}
	}
}

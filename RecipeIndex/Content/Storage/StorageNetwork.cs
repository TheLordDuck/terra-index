using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace RecipeIndex.Content.Storage
{
	// Resultado de escanear una red: lo que hay y dónde. Es lo que el servidor envía a los clientes.
	public class StorageSnapshot
	{
		public int ChestCount;
		public readonly Dictionary<int, int> Totals = new();
		public readonly Dictionary<int, List<Point>> Locations = new();
	}

	public static class StorageNetwork
	{
		// Límite de casillas de cable a recorrer, para que una red enorme no congele el juego.
		private const int MaxWireTiles = 50000;

		private static readonly Point[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

		// Solo debe llamarse donde se conocen todos los cofres: en un jugador o en el servidor.
		public static StorageSnapshot Scan(Point terminal)
		{
			var snapshot = new StorageSnapshot();
			List<int> chests = FindConnectedChests(terminal);
			snapshot.ChestCount = chests.Count;

			foreach (int index in chests) {
				Chest chest = Main.chest[index];
				if (chest?.item == null)
					continue;

				var position = new Point(chest.x, chest.y);
				foreach (Item item in chest.item) {
					if (item == null || item.IsAir)
						continue;

					snapshot.Totals[item.type] = snapshot.Totals.GetValueOrDefault(item.type) + item.stack;
					if (!snapshot.Locations.TryGetValue(item.type, out List<Point> list))
						snapshot.Locations[item.type] = list = new List<Point>();
					// Los cofres se recorren de uno en uno, así que basta mirar el último para no repetir.
					if (list.Count == 0 || list[^1] != position)
						list.Add(position);
				}
			}

			return snapshot;
		}

		// Recorre el cable rojo desde las casillas de la terminal (2x2) y devuelve los cofres por los que pasa.
		// Si el cable no toca la terminal, no hay red: la terminal tiene que estar en el mismo cable que los cofres.
		public static List<int> FindConnectedChests(Point terminal)
		{
			var chests = new HashSet<int>();
			var visited = new HashSet<Point>();
			var queue = new Queue<Point>();

			for (int x = 0; x < 2; x++) {
				for (int y = 0; y < 2; y++) {
					var start = new Point(terminal.X + x, terminal.Y + y);
					if (HasRedWire(start) && visited.Add(start))
						queue.Enqueue(start);
				}
			}

			while (queue.Count > 0 && visited.Count < MaxWireTiles) {
				Point p = queue.Dequeue();

				int chest = Chest.FindChestByGuessing(p.X, p.Y);
				if (chest >= 0)
					chests.Add(chest);

				foreach (Point offset in Neighbors) {
					var next = new Point(p.X + offset.X, p.Y + offset.Y);
					if (HasRedWire(next) && visited.Add(next))
						queue.Enqueue(next);
				}
			}

			return new List<int>(chests);
		}

		private static bool HasRedWire(Point p) => WorldGen.InWorld(p.X, p.Y, 1) && Main.tile[p.X, p.Y].RedWire;
	}
}

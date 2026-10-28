using System;
using System.IO;
using System.Linq;
using System.Reflection;

// Usage: xnb <in.xnb> <out.rgba>   writes int width, int height, then RGBA bytes (alpha un-premultiplied).
var fna = typeof(Microsoft.Xna.Framework.Color).Assembly;
byte[] all = File.ReadAllBytes(args[0]);
byte flags = all[5];
int fileSize = BitConverter.ToInt32(all, 6);
byte[] data;
if ((flags & 0x80) != 0) {
	int decompressed = BitConverter.ToInt32(all, 10);
	var type = fna.GetType("Microsoft.Xna.Framework.Content.LzxDecoder");
	var dec = Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 16 }, null);
	var decompress = type.GetMethod("Decompress", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
	var input = new MemoryStream(all);
	var output = new MemoryStream(decompressed);
	long pos = 14, end = fileSize;
	while (pos < end) {
		input.Seek(pos, SeekOrigin.Begin);
		int hi = input.ReadByte(), lo = input.ReadByte();
		int blockSize = (hi << 8) | lo, frameSize = 0x8000;
		if (hi == 0xFF) { hi = lo; lo = input.ReadByte(); frameSize = (hi << 8) | lo; hi = input.ReadByte(); lo = input.ReadByte(); blockSize = (hi << 8) | lo; pos += 5; }
		else pos += 2;
		if (blockSize == 0 || frameSize == 0) break;
		decompress.Invoke(dec, new object[] { input, blockSize, output, frameSize });
		pos += blockSize;
	}
	data = output.ToArray();
} else data = all[10..];
var r = new BinaryReader(new MemoryStream(data));
int readers = r.Read7BitEncodedInt();
for (int i = 0; i < readers; i++) { r.ReadString(); r.ReadInt32(); }
r.Read7BitEncodedInt(); r.Read7BitEncodedInt();
int format = r.ReadInt32(), w = r.ReadInt32(), h = r.ReadInt32(); r.ReadInt32();
int size = r.ReadInt32();
byte[] px = r.ReadBytes(size);
for (int i = 0; i < px.Length; i += 4) { int a = px[i + 3]; if (a > 0 && a < 255) for (int c = 0; c < 3; c++) px[i + c] = (byte)Math.Min(255, px[i + c] * 255 / a); }
using var o = new BinaryWriter(File.Create(args[1]));
o.Write(w); o.Write(h); o.Write(px);
Console.WriteLine($"{args[0]}: {w}x{h} format {format}");

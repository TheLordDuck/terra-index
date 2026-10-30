"""Draws the mod icon (icon.png 80x80, icon_small.png 30x30) from the mod's own sprites.

Run from the repo root: python tools/sprites/icon.py
The big icon is the storage terminal, with the portable terminal in front of its top right corner, a wireless
teleporter at the bottom left and the gold Calamity guide book at the bottom right.
"""
from PIL import Image, ImageDraw

MOD = "TerraIndex/"

OUTLINE = (30, 30, 38, 255)
CASE = (106, 106, 122, 255)
CASE_LIGHT = (140, 140, 158, 255)
SCREEN = (14, 36, 48, 255)
CYAN = (80, 220, 255, 255)
GREEN = (90, 240, 120, 255)
GOLD_DARK = (122, 78, 18, 255)
GOLD = (222, 168, 46, 255)
GOLD_LIGHT = (255, 222, 110, 255)
PAGE = (246, 236, 206, 255)
RED_DARK = (140, 24, 20, 255)
RED = (226, 60, 30, 255)
ORANGE = (255, 150, 40, 255)
YELLOW = (255, 230, 120, 255)


def background(size, radius):
	"""Blue vertical gradient with transparent rounded corners and a 1 px light rim."""
	img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
	mask = Image.new("L", (size, size), 0)
	ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius, fill=255)
	grad = Image.new("RGBA", (size, size))
	for y in range(size):
		t = y / (size - 1)
		c = (int(26 - 14 * t), int(44 - 26 * t), int(78 - 44 * t), 255)
		ImageDraw.Draw(grad).line((0, y, size - 1, y), fill=c)
	img.paste(grad, (0, 0), mask)
	ImageDraw.Draw(img).rounded_rectangle((0, 0, size - 1, size - 1), radius, outline=(60, 120, 190, 255))
	return img


def terminal(w, h, lines, magnifier):
	"""The storage terminal: gray casing, dark screen with cyan lines, green power light."""
	img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
	d = ImageDraw.Draw(img)
	d.rectangle((0, 0, w - 1, h - 1), fill=OUTLINE)
	d.rectangle((1, 1, w - 2, h - 2), fill=CASE)
	d.line((1, 1, w - 2, 1), fill=CASE_LIGHT)
	d.line((1, 1, 1, h - 2), fill=CASE_LIGHT)
	sx0, sy0, sx1, sy1 = 4, 4, w - 5, h - 8
	d.rectangle((sx0 - 1, sy0 - 1, sx1 + 1, sy1 + 1), fill=OUTLINE)
	d.rectangle((sx0, sy0, sx1, sy1), fill=SCREEN)
	lengths = [0.85, 0.65, 0.75, 0.45, 0.6]
	for i in range(lines):
		y = sy0 + 2 + i * 3
		if y + 1 > sy1 - 1:
			break
		d.rectangle((sx0 + 2, y, sx0 + 2 + int((sx1 - sx0 - 4) * lengths[i % 5]), y + 1 if h > 20 else y), fill=CYAN)
	d.rectangle((w - 7, h - 5, w - 5, h - 4), fill=GREEN)
	if magnifier:
		# Lens over the bottom right of the screen: the recipe search.
		cx, cy, r = sx1 - 6, sy1 - 5, 5
		d.ellipse((cx - r - 1, cy - r - 1, cx + r + 1, cy + r + 1), fill=OUTLINE)
		d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(210, 210, 225, 255))
		d.ellipse((cx - r + 2, cy - r + 2, cx + r - 2, cy + r - 2), fill=(120, 200, 240, 255))
		d.point((cx - 2, cy - 2), fill=(240, 250, 255, 255))
		d.point((cx - 1, cy - 2), fill=(240, 250, 255, 255))
		d.point((cx - 2, cy - 1), fill=(240, 250, 255, 255))
		for k in range(5):
			x, y = cx + r + k, cy + r + k
			d.rectangle((x - 1, y - 1, x + 1, y + 1), fill=OUTLINE)
		for k in range(5):
			x, y = cx + r + k, cy + r + k
			d.point((x, y), fill=GOLD)
	return img


SMALL_FLAME = [
	"..r..r....",
	"..rr.rr...",
	".rroorr...",
	".royyor...",
	".roywor...",
	"..rrrr....",
]


def calamity_book(w, h):
	"""The Calamity guide: a gold-bound book with a red flame rising off it."""
	img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
	d = ImageDraw.Draw(img)
	top = h // 3
	# Book (closed, front cover facing us).
	bx0, bx1 = 2, w - 3
	d.rectangle((bx0 - 1, top - 1, bx1 + 1, h - 1), fill=OUTLINE)
	d.rectangle((bx0, top, bx1, h - 2), fill=GOLD)
	d.line((bx0, top, bx1, top), fill=GOLD_LIGHT)
	d.line((bx0, top, bx0, h - 2), fill=GOLD_LIGHT)
	d.rectangle((bx0 + 3, top, bx0 + 4, h - 2), fill=GOLD_DARK)  # spine
	d.rectangle((bx0 + 5, h - 4, bx1, h - 2), fill=PAGE)  # page edges
	d.line((bx0 + 5, h - 3, bx1, h - 3), fill=(200, 186, 150, 255))
	# Emblem on the cover: a red diamond with a gold dot.
	ex, ey = (bx0 + 5 + bx1) // 2, (top + h - 4) // 2
	for k in range(5):
		d.line((ex - (4 - k), ey - k, ex + (4 - k), ey - k), fill=RED_DARK)
		d.line((ex - (4 - k), ey + k, ex + (4 - k), ey + k), fill=RED_DARK)
	for k in range(3):
		d.line((ex - (2 - k), ey - k, ex + (2 - k), ey - k), fill=RED)
		d.line((ex - (2 - k), ey + k, ex + (2 - k), ey + k), fill=RED)
	d.point((ex, ey), fill=GOLD_LIGHT)
	# Flame on top of the book.
	fx = (bx0 + 5 + bx1) // 2 + (1 if w < 20 else 0)
	flame = SMALL_FLAME if w < 20 else [
		"....r.....",
		"...rr..r..",
		"...ror.rr.",
		"..rroorrr.",
		".rrooorror",
		".rooyyoorr",
		"rroyyyyorr",
		"rooyyyyoor",
		"rooyywyoor",
		".rooyyyor.",
		"..rrooorr.",
	]
	colors = {"r": RED, "o": ORANGE, "y": YELLOW, "w": (255, 255, 230, 255)}
	fh = len(flame)
	for j, row in enumerate(flame):
		for i, ch in enumerate(row):
			if ch in colors:
				d.point((fx - 5 + i, top - fh + j + 1), fill=colors[ch])
	return img


def glow(img, color, alpha):
	"""Soft 1 px halo around the opaque pixels of img."""
	w, h = img.size
	out = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
	src = img.getchannel("A")
	halo = Image.new("L", out.size, 0)
	for dx in (0, 1, 2):
		for dy in (0, 1, 2):
			halo.paste(255, (dx, dy), src.point(lambda a: 255 if a > 0 else 0))
	out.paste(Image.new("RGBA", out.size, color[:3] + (alpha,)), (0, 0), halo)
	out.alpha_composite(img, (1, 1))
	return out


def sprite(path):
	return Image.open(MOD + path).convert("RGBA")


def big_terminal():
	"""The terminal as the old icon drew it: 64x64, screen on top with five cyan lines."""
	img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
	d = ImageDraw.Draw(img)
	d.rectangle((0, 0, 63, 63), fill=OUTLINE)
	d.rectangle((2, 2, 61, 61), fill=CASE)
	d.rectangle((2, 2, 61, 3), fill=CASE_LIGHT)
	d.rectangle((2, 2, 3, 61), fill=CASE_LIGHT)
	d.rectangle((8, 8, 55, 43), fill=OUTLINE)
	d.rectangle((10, 10, 53, 41), fill=SCREEN)
	for i, length in enumerate([40, 34, 28, 22, 36]):
		d.rectangle((12, 12 + i * 6, 11 + length, 13 + i * 6), fill=CYAN)
	d.rectangle((50, 54, 53, 55), fill=GREEN)
	return img


def big_icon():
	img = background(80, 8)
	img.alpha_composite(big_terminal(), (6, 8))
	# The portable terminal in front of the big one's top right corner.
	img.alpha_composite(glow(sprite("Content/Items/RemoteTerminalItem.png"), (12, 18, 34, 255), 255), (55, 3))
	# The teleporter item is 48 px wide; drop 6 columns from each side of the middle to fit.
	tp = sprite("Content/Items/WirelessTeleporterItem.png")
	short = Image.new("RGBA", (36, 16), (0, 0, 0, 0))
	short.paste(tp.crop((0, 0, 12, 16)), (0, 0))
	short.paste(tp.crop((18, 0, 30, 16)), (12, 0))
	short.paste(tp.crop((36, 0, 48, 16)), (24, 0))
	img.alpha_composite(glow(short, (12, 18, 34, 255), 255), (4, 59))
	img.alpha_composite(glow(calamity_book(28, 34), GOLD, 140), (47, 41))
	return img


def small_icon():
	img = background(30, 4)
	img.alpha_composite(terminal(20, 17, 3, False), (2, 2))
	img.alpha_composite(calamity_book(13, 17), (16, 11))
	return img


if __name__ == "__main__":
	big_icon().save(MOD + "icon.png")
	small_icon().save(MOD + "icon_small.png")

# Repaints the Apothecary's sheet (built by apothecary.js on the Guide's frames) as "The Terrarian", the user's
# character: brown hair with red bangs, a gold band with blue, green and red plumes, a navy coat and wide white
# trousers. Works on the 2 x 2 art pixel grid. Needs Pillow; run from the repo root:
#   python tools/sprites/terrarian.py
# It rewrites TerraIndex/Content/NPCs/Apothecary.png and Apothecary_Head.png from the hooded version, so it only
# runs once on apothecary.js's output (it stops if there is no hood left).
from PIL import Image

SHEET = "TerraIndex/Content/NPCs/Apothecary.png"
HEAD = "TerraIndex/Content/NPCs/Apothecary_Head.png"
W, FH = 40, 56
AW, AH = W // 2, FH // 2

# The hood's colours (apothecary.js) -> hair.
HOOD = {(44, 20, 56): "O", (84, 40, 110): "D", (120, 62, 152): "M", (164, 104, 196): "L"}
HAIR = {"O": (45, 25, 12), "D": (95, 52, 22), "M": (135, 78, 35), "L": (175, 110, 55)}
BAND = {"O": (45, 25, 12), "D": (190, 140, 40), "M": (250, 210, 80), "L": (255, 240, 150)}
BANGS = (200, 40, 50)

# The robe's colours -> navy coat above the belt, white trousers below it.
ROBE = [(22, 46, 30), (38, 86, 52), (56, 120, 70), (82, 156, 94), (124, 192, 120)]
COAT = [(18, 18, 34), (35, 38, 65), (45, 52, 85), (70, 80, 120), (110, 120, 165)]
TROUSERS = [(60, 50, 90), (150, 148, 190), (180, 180, 210), (225, 228, 240), (250, 250, 255)]
BELT = (84, 52, 30)

# Plumes over frame 0's head (art rows 0-3), moved with the head on bobbing frames. Drawn only on empty or hood pixels.
PLUME_COLORS = {"o": (30, 18, 18), "R": (220, 40, 60), "r": (250, 120, 120), "G": (40, 170, 80), "g": (130, 225, 140),
                "B": (50, 80, 200), "b": (120, 160, 245)}
PLUMES = [
    "..........ooo.......",
    "......oo.oRRro......",
    "....ooGgooRRro......",
    "...oBbGgooRRo.......",
]


def repaint(art):
    hood = [(x, y) for y in range(AH) for x in range(AW) if art[y][x] in HOOD]
    if not hood:
        raise SystemExit("No hood found: the sheet was already repainted.")
    top = min(y for _, y in hood)
    dy = top - 1  # frame 0's hood starts on art row 1
    belts = [y for y in range(AH) for x in range(AW) if art[y][x] == BELT]
    belt = min(belts) if belts else 19 + dy

    out = [row[:] for row in art]
    for y in range(AH):
        for x in range(AW):
            c = art[y][x]
            rel = y - dy
            if c in HOOD:
                shade = HOOD[c]
                if rel <= 3:
                    out[y][x] = None  # the hood's tip; the plumes go there
                elif rel == 5:
                    out[y][x] = BAND[shade]
                elif rel == 9 and shade == "O" and 5 <= x <= 11:
                    out[y][x] = BANGS  # the hood's brim over the face
                else:
                    out[y][x] = HAIR[shade]
            elif c in ROBE:
                out[y][x] = (COAT if y < belt else TROUSERS)[ROBE.index(c)]
    for py, row in enumerate(PLUMES):
        y = py + dy
        if not 0 <= y < AH:
            continue
        for x, ch in enumerate(row):
            if ch != "." and (art[y][x] is None or art[y][x] in HOOD):
                out[y][x] = PLUME_COLORS[ch]
    return out


def load(im, frame):
    return [[(lambda p: p[:3] if p[3] else None)(im.getpixel((x * 2, frame * FH + y * 2))) for x in range(AW)] for y in range(AH)]


def paint(img, art, top):
    for y, row in enumerate(art):
        for x, c in enumerate(row):
            if c:
                for dx in range(2):
                    for dy in range(2):
                        img.putpixel((x * 2 + dx, (top + y) * 2 + dy), c + (255,))


im = Image.open(SHEET).convert("RGBA")
frames = [repaint(load(im, f)) for f in range(im.height // FH)]
sheet = Image.new("RGBA", im.size, (0, 0, 0, 0))
for f, art in enumerate(frames):
    paint(sheet, art, f * AH)
sheet.save(SHEET)

# Head icon: frame 0's head with its plumes (art rows 0-13, columns 2-16), without the coat's shoulders.
head = [[c if c and c not in COAT else None for c in row[2:17]] for row in frames[0][0:14]]
icon = Image.new("RGBA", (len(head[0]) * 2, len(head) * 2), (0, 0, 0, 0))
paint(icon, head, 0)
icon.save(HEAD)
print(len(frames), "frames")

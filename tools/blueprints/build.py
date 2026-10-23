"""Checks the blueprint designs, draws them and writes them into the mod.

    python tools/blueprints/build.py

- Checks every design: known characters, furniture footprints, what each piece stands on or hangs from, and that
  every room listed in the design is valid NPC housing (the same flood fill and limits as the game's room check).
- Draws docs/blueprints/<n> - <name>.png, plus overview.png with all of them.
- Writes TerraIndex/Content/Building/BlueprintDesigns.cs.

The pictures are approximate: wood parts are drawn as plain wood, whatever material is picked in the game.
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from designs import DESIGNS, FURNITURE  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DOCS = os.path.join(ROOT, 'docs', 'blueprints')
CS = os.path.join(ROOT, 'TerraIndex', 'Content', 'Building', 'BlueprintDesigns.cs')

# What each character places. Keep in sync with Blueprints.TileBrushes / WallBrushes in the mod.
SOLID = set('#SRdYU') | set('/\\()[]')
PLATFORM = '-'
TILE_CHARS = SOLID | {' ', '.', '+', '-', '|'} | set(FURNITURE)
WALL_CHARS = set(' wsgkq')
STANDING = set('CTWBKOF')
HANGING = set('Llyn')
ON_TABLE = set('cb')
LIGHTS = set('Llyci')
CHAIRS = set('C')
TABLES = set('TW')

NAMES = {
    'SmallHouse': 'Small house', 'TwoFloors': 'Two-floor house', 'Tower': 'Tower', 'Arena': 'Boss arena',
    'Medieval': 'Medieval house', 'Castle': 'Castle', 'Pagoda': 'Pagoda', 'WizardTower': 'Wizard tower',
}


def owners(d):
    """(x, y) -> (letter, left, top) for every cell covered by furniture."""
    result = {}
    for y, row in enumerate(d.tiles):
        for x, c in enumerate(row):
            if c in FURNITURE:
                w, h = FURNITURE[c]
                for dy in range(h):
                    for dx in range(w):
                        result[(x + dx, y + dy)] = (c, x, y)
    return result


def check(d):
    errors = []
    W, H = d.width, d.height
    t, wl = d.tiles, d.walls
    own = owners(d)
    for y in range(H):
        for x in range(W):
            if t[y][x] not in TILE_CHARS:
                errors.append(f'unknown tile {t[y][x]!r} at {x},{y}')
            if wl[y][x] not in WALL_CHARS:
                errors.append(f'unknown wall {wl[y][x]!r} at {x},{y}')
            if t[y][x] == '+' and (x, y) not in own:
                errors.append(f'stray + at {x},{y}')

    def solid(x, y):
        return 0 <= x < W and 0 <= y < H and t[y][x] in SOLID

    seen = {}
    for y in range(H):
        for x in range(W):
            c = t[y][x]
            if c not in FURNITURE:
                continue
            w, h = FURNITURE[c]
            for dy in range(h):
                for dx in range(w):
                    cx, cy = x + dx, y + dy
                    if not (0 <= cx < W and 0 <= cy < H):
                        errors.append(f'{c} at {x},{y} goes out of the design')
                        continue
                    if (dx, dy) != (0, 0) and t[cy][cx] != '+':
                        errors.append(f'{c} at {x},{y} covers {t[cy][cx]!r} at {cx},{cy}')
                    if (cx, cy) in seen and seen[(cx, cy)] != (x, y):
                        errors.append(f'{c} at {x},{y} overlaps another piece at {cx},{cy}')
                    seen[(cx, cy)] = (x, y)
            if c == 'D' and not (solid(x, y - 1) and solid(x, y + 3)):
                errors.append(f'door at {x},{y} needs blocks above and below')
            if c in STANDING and not all(solid(x + dx, y + h) for dx in range(w)):
                errors.append(f'{c} at {x},{y} needs blocks under it')
            if c in HANGING and not all(solid(x + dx, y - 1) for dx in range(w)):
                errors.append(f'{c} at {x},{y} needs blocks above it')
            if c in ON_TABLE:
                below = own.get((x, y + 1))
                if not below or below[0] not in TABLES or below[2] != y + 1:
                    errors.append(f'{c} at {x},{y} must stand on a table or work bench')
            if c == 'i' and wl[y][x] == ' ' and not (solid(x - 1, y) or solid(x + 1, y) or solid(x, y + 1)):
                errors.append(f'torch at {x},{y} needs a wall behind it')

    for seed in d.rooms:
        errors += [f'room at {seed}: {e}' for e in check_room(d, own, seed)]
    return errors


def check_room(d, own, seed):
    """The game's room check: flood fill through non-solid cells (closed doors and platforms are solid), counting
    the solid cells it touches too. 60 to 750 cells, walls behind every open cell, a light, a chair, a table and a
    door or platform."""
    W, H = d.width, d.height
    t, wl = d.tiles, d.walls

    def boundary(x, y):
        c = t[y][x]
        if c in SOLID or c == PLATFORM:
            return True
        o = own.get((x, y))
        return o is not None and o[0] == 'D'

    errors = []
    visited, stack = set(), [seed]
    found = set()
    while stack:
        x, y = stack.pop()
        if (x, y) in visited:
            continue
        if not (0 <= x < W and 0 <= y < H) or t[y][x] == ' ':
            return ['not closed: it reaches outside the design']
        visited.add((x, y))
        if len(visited) > 750:
            return ['bigger than 750 tiles']
        o = own.get((x, y))
        if boundary(x, y):
            found.add('D' if t[y][x] == PLATFORM or o else 'frame')
            continue
        if wl[y][x] == ' ':
            errors.append(f'no wall at {x},{y}')
        if o:
            found.add(o[0])
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    if len(visited) < 60:
        errors.append(f'too small ({len(visited)} tiles)')
    if not found & LIGHTS:
        errors.append('no light')
    if not found & CHAIRS:
        errors.append('no chair')
    if not found & TABLES:
        errors.append('no table')
    if 'D' not in found:
        errors.append('no door or platform')
    return errors[:5]


# ---------- Drawing ----------

C = 12
SKY = (150, 196, 236)
COLORS = {
    '#': ((168, 120, 72), (118, 80, 45)),     # wood
    'd': ((150, 60, 45), (100, 35, 25)),      # dynasty wood
    'S': ((128, 128, 136), (92, 92, 100)),    # gray brick
    'R': ((170, 62, 48), (120, 40, 32)),      # red brick
    'Y': ((190, 50, 40), (130, 30, 25)),      # red dynasty shingles
    'U': ((60, 80, 170), (40, 52, 120)),      # blue roof
}
SLOPES = {'/': ('R', 'dr'), '\\': ('R', 'dl'), '(': ('Y', 'dr'), ')': ('Y', 'dl'), '[': ('U', 'dr'), ']': ('U', 'dl')}
WALLS = {'w': (110, 78, 50), 's': (82, 82, 92), 'g': (170, 210, 235), 'k': (225, 215, 195), 'q': (70, 90, 150)}
WOOD = (150, 100, 55)
WOOD_DARK = (88, 56, 30)


def draw(d, scale=1):
    pad = 2
    W, H = d.width, d.height
    img = Image.new('RGB', ((W + pad * 2) * C, (H + pad * 2 + 1) * C), SKY)
    g = ImageDraw.Draw(img)
    ground = (H + pad) * C
    g.rectangle([0, ground, img.width, img.height], fill=(105, 72, 45))
    g.rectangle([0, ground, img.width, ground + 3], fill=(76, 160, 64))

    def box(x, y, w=1, h=1):
        px, py = (x + pad) * C, (y + pad) * C
        return px, py, px + w * C - 1, py + h * C - 1

    for y in range(H):
        for x in range(W):
            c = d.walls[y][x]
            if c != ' ':
                x0, y0, x1, y1 = box(x, y)
                g.rectangle([x0, y0, x1, y1], fill=WALLS[c])
                if c == 'g':
                    g.line([x0, y0, x1, y1], fill=(200, 230, 245))
    for y in range(H):
        for x in range(W):
            c = d.tiles[y][x]
            x0, y0, x1, y1 = box(x, y)
            if c in COLORS:
                light, dark = COLORS[c]
                g.rectangle([x0, y0, x1, y1], fill=light, outline=dark)
                g.line([x0 + 2, y0 + C // 2, x1 - 2, y0 + C // 2], fill=dark)
            elif c in SLOPES:
                material, kind = SLOPES[c]
                light, dark = COLORS[material]
                points = [(x0, y1), (x1, y1), (x1, y0)] if kind == 'dr' else [(x0, y1), (x1, y1), (x0, y0)]
                g.polygon(points, fill=light, outline=dark)
            elif c == '-':
                g.rectangle([x0, y0, x1, y0 + 3], fill=(190, 140, 85), outline=WOOD_DARK)
            elif c == '|':
                g.rectangle([x0 + 3, y0, x1 - 3, y1], fill=(120, 80, 45))
    for y in range(H):
        for x in range(W):
            c = d.tiles[y][x]
            if c in FURNITURE:
                draw_piece(g, c, *box(x, y, *FURNITURE[c]))
    if scale != 1:
        img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    return img


def draw_piece(g, c, x0, y0, x1, y1):
    w, h = x1 - x0 + 1, y1 - y0 + 1
    if c == 'D':
        g.rectangle([x0 + 2, y0, x1 - 2, y1], fill=WOOD, outline=WOOD_DARK)
        g.point([(x1 - 4, y0 + h * 2 // 3)], fill=(240, 210, 90))
    elif c == 'C':
        g.rectangle([x0 + 2, y0 + 1, x0 + 4, y1], fill=WOOD_DARK)
        g.rectangle([x0 + 2, y0 + h // 2, x1 - 2, y0 + h // 2 + 2], fill=WOOD)
        g.rectangle([x1 - 4, y0 + h // 2, x1 - 2, y1], fill=WOOD_DARK)
    elif c in 'TW':
        g.rectangle([x0, y0 + (h // 2 if c == 'T' else 1), x1, y0 + (h // 2 if c == 'T' else 1) + 3], fill=WOOD, outline=WOOD_DARK)
        g.rectangle([x0 + 2, y0 + h // 2 + 3, x0 + 4, y1], fill=WOOD_DARK)
        g.rectangle([x1 - 4, y0 + h // 2 + 3, x1 - 2, y1], fill=WOOD_DARK)
    elif c == 'B':
        g.rectangle([x0, y0 + h // 2, x1, y1 - 2], fill=WOOD, outline=WOOD_DARK)
        g.rectangle([x0 + 4, y0 + h // 2 - 3, x1 - 4, y0 + h // 2 + 2], fill=(200, 60, 60))
        g.rectangle([x0, y0 + 2, x0 + 3, y1], fill=WOOD_DARK)
    elif c == 'K':
        g.rectangle([x0, y0, x1, y1], fill=WOOD, outline=WOOD_DARK)
        for i in range(1, 4):
            yy = y0 + i * h // 4
            g.line([x0, yy, x1, yy], fill=WOOD_DARK)
            for k, color in enumerate([(180, 50, 50), (60, 90, 170), (60, 140, 70), (200, 170, 60)]):
                bx = x0 + 3 + k * (w - 6) // 4
                g.rectangle([bx, yy - h // 4 + 3, bx + 3, yy - 1], fill=color)
    elif c == 'L':
        g.line([x0 + w // 2, y0, x0 + w // 2, y0 + h // 3], fill=(60, 60, 60))
        g.rectangle([x0 + 2, y0 + h // 3, x1 - 2, y0 + h // 3 + 3], fill=(200, 160, 60))
        for k in range(3):
            fx = x0 + 3 + k * (w - 6) // 2
            g.ellipse([fx - 2, y0 + h // 3 - 5, fx + 2, y0 + h // 3], fill=(255, 220, 90))
    elif c in 'ly':
        g.line([x0 + w // 2, y0, x0 + w // 2, y0 + 4], fill=(60, 60, 60))
        color = (220, 60, 50) if c == 'y' else (255, 210, 90)
        g.ellipse([x0 + 1, y0 + 4, x1 - 1, y1 - 1], fill=color, outline=(120, 60, 20))
    elif c == 'n':
        g.rectangle([x0 + 1, y0, x1 - 1, y1 - 3], fill=(180, 30, 40))
        g.polygon([(x0 + 1, y1 - 3), (x1 - 1, y1 - 3), (x0 + w // 2, y1)], fill=(180, 30, 40))
    elif c == 'O':
        g.rectangle([x0 + 1, y0, x1 - 1, y1], fill=WOOD, outline=WOOD_DARK)
        g.ellipse([x0 + 3, y0 + 3, x1 - 3, y0 + w - 3], fill=(240, 235, 210))
    elif c == 'F':
        g.rectangle([x0, y0, x1, y1], fill=(120, 120, 128), outline=(80, 80, 88))
        g.rectangle([x0 + 6, y0 + h // 2 - 2, x1 - 6, y1], fill=(40, 30, 25))
        g.ellipse([x0 + 9, y0 + h // 2, x1 - 9, y1 - 1], fill=(255, 140, 40))
    elif c == 'c':
        g.rectangle([x0 + 4, y0 + 4, x1 - 4, y1], fill=(240, 235, 210))
        g.ellipse([x0 + 4, y0, x1 - 4, y0 + 4], fill=(255, 200, 60))
    elif c == 'b':
        g.rectangle([x0 + 3, y0 + 4, x1 - 3, y1], fill=(150, 210, 230), outline=(80, 130, 160))
    elif c == 'i':
        g.rectangle([x0 + 5, y0 + 4, x1 - 5, y1 - 1], fill=(120, 80, 40))
        g.ellipse([x0 + 3, y0, x1 - 3, y0 + 5], fill=(255, 200, 60))


# ---------- C# ----------

def cs_string(row):
    return '"' + row.replace('\\', '\\\\').replace('"', '\\"') + '"'


def write_cs(designs):
    lines = [
        '// Generated by tools/blueprints/build.py from tools/blueprints/designs.py: change the designs there and run it.',
        'namespace TerraIndex.Content.Building',
        '{',
        '\tpublic static class BlueprintDesigns',
        '\t{',
        '\t\tpublic static readonly Blueprint[] All = {',
    ]
    for d in designs:
        lines.append(f'\t\t\tnew("{d.key}",')
        lines.append('\t\t\t\tnew[] {')
        lines += [f'\t\t\t\t\t{cs_string(r)},' for r in d.rows('tiles')]
        lines.append('\t\t\t\t},')
        lines.append('\t\t\t\tnew[] {')
        lines += [f'\t\t\t\t\t{cs_string(r)},' for r in d.rows('walls')]
        lines.append('\t\t\t\t}),')
    lines += ['\t\t};', '\t}', '}', '']
    with open(CS, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines))


def main():
    designs = [make() for make in DESIGNS]
    failed = False
    for d in designs:
        errors = check(d)
        status = 'ok' if not errors else f'{len(errors)} problem(s)'
        print(f'{d.key:12} {d.width}x{d.height}  rooms: {len(d.rooms)}  {status}')
        for e in errors[:20]:
            print('   ', e)
        failed |= bool(errors)
    if failed:
        sys.exit(1)

    os.makedirs(DOCS, exist_ok=True)
    pictures = []
    for i, d in enumerate(designs, 1):
        img = draw(d, 2)
        g = ImageDraw.Draw(img)
        g.text((8, 6), f'{NAMES.get(d.key, d.key)} ({d.width}x{d.height}, {len(d.rooms)} NPC rooms)', fill=(20, 30, 60))
        img.save(os.path.join(DOCS, f'{i} - {NAMES.get(d.key, d.key)}.png'))
        pictures.append(img)
    width = sum(p.width for p in pictures) + 10 * (len(pictures) + 1)
    height = max(p.height for p in pictures) + 20
    overview = Image.new('RGB', (width, height), (235, 238, 245))
    x = 10
    for p in pictures:
        overview.paste(p, (x, height - 10 - p.height))
        x += p.width + 10
    overview.save(os.path.join(DOCS, 'overview.png'))
    write_cs(designs)
    print('pictures in', DOCS)
    print('wrote', CS)


if __name__ == '__main__':
    main()

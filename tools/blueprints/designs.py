"""Terra Index blueprints: the house designs, built in code.

Run `python tools/blueprints/build.py` after changing anything here: it checks every design, draws
docs/blueprints/*.png and writes TerraIndex/Content/Building/BlueprintDesigns.cs.

Each design has two layers of the same size, as rows of characters (what each one places is in
Blueprints.TileBrushes / WallBrushes in the mod; build.py knows which ones are solid, furniture, lights...). Tile layer: ' ' leaves the cell as it is, '.' must be empty, '+' is covered by a piece of furniture whose
top-left cell has its letter. Wall layer: ' ' leaves the background wall as it is.
"""

# Furniture letters and their size (width, height). Must match the game's tiles.
FURNITURE = {
    'D': (1, 3),  # door
    'C': (1, 2),  # chair
    'T': (3, 2),  # table
    'W': (2, 1),  # work bench
    'B': (4, 2),  # bed
    'K': (3, 4),  # bookcase
    'L': (3, 3),  # chandelier (hangs)
    'l': (1, 2),  # lantern (hangs)
    'y': (1, 2),  # dynasty lantern (hangs)
    'n': (1, 3),  # banner (hangs)
    'O': (2, 5),  # grandfather clock
    'F': (3, 2),  # fireplace
    'c': (1, 1),  # candle (on a table)
    'b': (1, 1),  # bottle (on a table)
    'i': (1, 1),  # torch (on a wall)
}


class Design:
    def __init__(self, key, width, height):
        self.key = key
        self.width = width
        self.height = height
        self.tiles = [[' '] * width for _ in range(height)]
        self.walls = [[' '] * width for _ in range(height)]
        # One cell inside each room meant as NPC housing.
        self.rooms = []

    def tile(self, x, y, c):
        self.tiles[y][x] = c

    def fill(self, x0, y0, x1, y1, c, layer='tiles'):
        grid = self.tiles if layer == 'tiles' else self.walls
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                grid[y][x] = c

    def wall(self, x0, y0, x1, y1, c):
        self.fill(x0, y0, x1, y1, c, 'walls')

    def put(self, c, x, y):
        """Furniture with its top-left cell at x, y."""
        w, h = FURNITURE[c]
        for dy in range(h):
            for dx in range(w):
                self.tiles[y + dy][x + dx] = '+'
        self.tiles[y][x] = c

    def rows(self, layer):
        grid = self.tiles if layer == 'tiles' else self.walls
        return [''.join(row) for row in grid]


def simple(key, rows, rooms):
    """The first blueprints, written as one layer: every cell but ' ', '#' and '_' gets the material's wall."""
    d = Design(key, len(rows[0]), len(rows))
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            d.tiles[y][x] = '-' if c == '_' else c
            if c not in ' #_':
                d.walls[y][x] = 'w'
    d.rooms = rooms
    return d


def small_house():
    return simple('SmallHouse', [
        "##########",
        "#........#",
        "D..i.....#",
        "+........#",
        "+..CT++..#",
        "#..++++..#",
        "##########",
    ], [(4, 3)])


def two_floors():
    return simple('TwoFloors', [
        "############",
        "#..........#",
        "#..i.......#",
        "#..........#",
        "#..CT++....#",
        "#..++++....#",
        "########---#",
        "#..........#",
        "#......i...#",
        "D..........#",
        "+......CT++#",
        "+......++++#",
        "############",
    ], [(5, 2), (5, 8)])


def tower():
    return simple('Tower', [
        "##########",
        "#........#",
        "#..i.....#",
        "#........#",
        "#..CT++..#",
        "#..++++..#",
        "#######--#",
        "#........#",
        "#.....i..#",
        "#........#",
        "#.CT++...#",
        "#.++++...#",
        "######---#",
        "#........#",
        "#......i.#",
        "D........#",
        "+...CT++.#",
        "+...++++.#",
        "##########",
    ], [(4, 2), (4, 8), (4, 14)])


def arena():
    rows = ['_' * 40] + [' ' * 40] * 5 + ['_' * 40] + [' ' * 40] * 5 + ['_' * 40]
    return simple('Arena', rows, [])


def medieval():
    """Stone ground floor, wooden upper floor and attic under a steep red brick roof with porch posts."""
    d = Design('Medieval', 24, 23)
    # Roof: 45 degree slopes two tiles thick, hollow inside (the attic), eaves over the porch posts.
    for k in range(11):
        a, b = 10 - k, 13 + k
        d.tile(a, k, '/')
        d.tile(b, k, '\\')
        d.tile(a + 1, k, 'R')
        d.tile(b - 1, k, 'R')
        if k < 10:
            d.fill(a + 2, k, b - 2, k, '.')
            if a + 2 <= b - 2:
                d.wall(a + 2, k, b - 2, k, 'w')
    # Attic floor, with platforms up from the left room.
    d.fill(2, 10, 21, 10, '#')
    d.fill(7, 10, 9, 10, '-')
    # Upper floor: wooden walls, two rooms.
    for x in (2, 11, 21):
        d.fill(x, 11, x, 15, '#')
    d.fill(3, 11, 10, 15, '.')
    d.fill(12, 11, 20, 15, '.')
    d.wall(2, 11, 21, 15, 'w')
    d.fill(2, 16, 21, 16, '#')
    d.fill(3, 16, 5, 16, '-')
    d.fill(16, 16, 18, 16, '-')
    # Ground floor: gray brick, two rooms, a door on each side.
    for x in (2, 11, 21):
        d.fill(x, 17, x, 21, 'S')
    d.fill(3, 17, 10, 21, '.')
    d.fill(12, 17, 20, 21, '.')
    d.wall(2, 17, 21, 21, 's')
    d.fill(1, 22, 22, 22, 'S')
    d.put('D', 2, 19)
    d.put('D', 21, 19)
    # Porch posts under the eaves.
    d.fill(1, 11, 1, 21, '|')
    d.fill(22, 11, 22, 21, '|')
    # Windows.
    d.wall(4, 17, 5, 18, 'g')
    d.wall(19, 19, 20, 20, 'g')
    d.wall(5, 12, 6, 13, 'g')
    d.wall(13, 11, 14, 12, 'g')
    d.wall(11, 3, 12, 4, 'g')
    # Ground left: fireplace, dinner table, chandelier.
    d.put('F', 3, 20)
    d.put('C', 7, 20)
    d.put('T', 8, 20)
    d.put('c', 10, 19)
    d.put('L', 7, 17)
    # Ground right: bookcase, table, work bench, lantern.
    d.put('K', 12, 18)
    d.put('C', 15, 20)
    d.put('T', 16, 20)
    d.put('c', 17, 19)
    d.put('W', 19, 21)
    d.put('l', 19, 17)
    # Upper left.
    d.put('C', 7, 14)
    d.put('T', 8, 14)
    d.put('c', 9, 13)
    d.put('l', 4, 11)
    # Upper right: clock and chandelier.
    d.put('T', 12, 14)
    d.put('C', 15, 14)
    d.put('c', 13, 13)
    d.put('O', 19, 11)
    d.put('L', 16, 11)
    # Attic bedroom.
    d.put('T', 4, 8)
    d.put('c', 5, 7)
    d.put('C', 10, 8)
    d.put('B', 12, 8)
    d.put('i', 17, 6)
    d.rooms = [(5, 18), (13, 17), (5, 14), (13, 12), (11, 6)]
    return d


def castle():
    """Two four-floor towers and a keep with a great hall and two rooms above it, with battlements."""
    d = Design('Castle', 42, 30)
    d.fill(0, 29, 41, 29, 'S')

    def tower_(left):
        x0, x1 = (0, 7) if left else (34, 41)
        d.fill(x0, 1, x0, 28, 'S')
        d.fill(x1, 1, x1, 28, 'S')
        d.fill(x0, 1, x1, 1, 'S')
        for x in range(x0, x1 + 1):
            if (x - x0) % 3 != 2:
                d.tile(x, 0, 'S')
        d.fill(x0 + 1, 2, x1 - 1, 28, '.')
        d.wall(x0, 2, x1, 28, 's')
        shaft = (x0 + 1, x0 + 2) if left else (x1 - 2, x1 - 1)
        for floor in (22, 15, 8):
            d.fill(x0 + 1, floor, x1 - 1, floor, 'S')
            d.fill(shaft[0], floor, shaft[1], floor, '-')
        # One room per floor: table, chair, candle, torch, a slit window.
        for bottom in (28, 21, 14, 7):
            if left:
                d.put('C', 3, bottom - 1)
                d.put('T', 4, bottom - 1)
                d.put('c', 5, bottom - 2)
                d.put('i', 1, bottom - 4)
                d.wall(5, bottom - 5, 5, bottom - 4, 'g')
            else:
                d.put('C', 38, bottom - 1)
                d.put('T', 35, bottom - 1)
                d.put('c', 36, bottom - 2)
                d.put('i', 40, bottom - 4)
                d.wall(36, bottom - 5, 36, bottom - 4, 'g')
            d.rooms.append((x0 + 4, bottom - 3) if left else (x1 - 4, bottom - 3))
        d.put('D', x0, 26)
        d.put('D', x1, 26)

    tower_(True)
    tower_(False)

    # Keep: roof with battlements, upper floor, great hall.
    d.fill(7, 11, 34, 11, 'S')
    for x in range(8, 34):
        if (x - 8) % 3 != 2:
            d.tile(x, 10, 'S')
    d.fill(8, 12, 33, 17, '.')
    d.fill(21, 12, 21, 17, 'S')
    d.fill(8, 18, 33, 18, 'S')
    d.fill(13, 18, 14, 18, '-')
    d.fill(31, 18, 32, 18, '-')
    d.fill(8, 19, 33, 28, '.')
    d.wall(8, 12, 33, 28, 's')
    for x in (12, 18, 23, 29):
        d.wall(x, 22, x + 1, 25, 'g')
    d.wall(10, 13, 11, 14, 'g')
    d.wall(27, 13, 28, 14, 'g')

    # Great hall: chandeliers, banners, a banquet by the fireplace, bookcases.
    d.put('L', 9, 19)
    d.put('L', 28, 19)
    for x in (16, 21, 26):
        d.put('n', x, 19)
    d.put('C', 14, 27)
    d.put('T', 15, 27)
    d.put('C', 18, 27)
    d.put('F', 20, 27)
    d.put('C', 23, 27)
    d.put('T', 24, 27)
    d.put('C', 27, 27)
    d.put('c', 16, 26)
    d.put('c', 25, 26)
    d.put('K', 9, 25)
    d.put('K', 30, 25)
    # Upper left: bedroom with a clock.
    d.put('B', 9, 16)
    d.put('C', 15, 16)
    d.put('T', 16, 16)
    d.put('c', 17, 15)
    d.put('O', 19, 13)
    d.put('L', 10, 12)
    # Upper right: library.
    d.put('K', 22, 14)
    d.put('l', 25, 12)
    d.put('C', 26, 16)
    d.put('T', 27, 16)
    d.put('c', 28, 15)
    d.put('i', 33, 13)
    d.rooms += [(20, 22), (12, 13), (30, 13)]
    return d


def pagoda():
    """Three floors, each narrower, under red shingle roofs with upturned eaves."""
    d = Design('Pagoda', 26, 26)
    levels = [(4, 21), (6, 19), (8, 17)]
    foundation = 25
    d.fill(2, foundation, 23, foundation, 'S')
    floor = foundation
    ceilings = []
    for i, (left, right) in enumerate(levels):
        ceiling = floor - 6
        ceilings.append(ceiling)
        d.fill(left, ceiling + 1, left, floor - 1, 'd')
        d.fill(right, ceiling + 1, right, floor - 1, 'd')
        d.fill(left + 1, ceiling + 1, right - 1, floor - 1, '.')
        d.wall(left, ceiling + 1, right, floor - 1, 'k')
        # Eave: shingles past the walls, with upturned tips.
        d.fill(left - 3, ceiling, right + 3, ceiling, 'Y')
        d.fill(left, ceiling, right, ceiling, 'd')
        d.tile(left - 3, ceiling - 1, ')')
        d.tile(right + 3, ceiling - 1, '(')
        if i + 1 < len(levels):
            # Skirt from the eave up to the next floor's walls.
            next_left, next_right = levels[i + 1]
            for k in range(next_left - left + 2):
                y = ceiling - 1 - k
                x = left - 2 + k
                if x >= next_left:
                    break
                d.tile(x, y, '(')
                d.fill(x + 1, y, next_left - 1, y, 'Y')
                xr = right + 2 - k
                d.tile(xr, y, ')')
                d.fill(next_right + 1, y, xr - 1, y, 'Y')
        floor = ceiling
    # Top roof, up to a point.
    top = ceilings[-1]
    for r in range(top):
        y = top - 1 - r
        left, right = 6 + r, 19 - r
        if left >= right:
            break
        d.tile(left, y, '(')
        d.tile(right, y, ')')
        if left + 1 <= right - 1:
            d.fill(left + 1, y, right - 1, y, 'Y')

    c1, c2, c3 = ceilings
    # Platforms between floors.
    d.fill(12, c1, 13, c1, '-')
    d.fill(15, c2, 16, c2, '-')
    # Ground floor: doors, dinner table, bookcase, dynasty lanterns.
    d.put('D', 4, foundation - 3)
    d.put('D', 21, foundation - 3)
    d.put('C', 6, foundation - 2)
    d.put('T', 7, foundation - 2)
    d.put('c', 8, foundation - 3)
    d.put('K', 17, foundation - 4)
    d.put('y', 10, c1 + 1)
    d.put('y', 15, c1 + 1)
    d.wall(11, c1 + 2, 14, c1 + 3, 'q')
    # Middle floor: bedroom.
    d.put('C', 8, c1 - 2)
    d.put('T', 9, c1 - 2)
    d.put('c', 10, c1 - 3)
    d.put('B', 14, c1 - 2)
    d.put('y', 10, c2 + 1)
    d.put('y', 13, c2 + 1)
    d.wall(8, c2 + 2, 17, c2 + 2, 'q')
    # Top floor: tea room.
    d.put('C', 10, c2 - 2)
    d.put('T', 11, c2 - 2)
    d.put('c', 12, c2 - 3)
    d.put('y', 14, c3 + 1)
    d.wall(10, c3 + 2, 15, c3 + 2, 'q')
    d.rooms = [(12, foundation - 3), (12, c1 - 3), (12, c2 - 4)]
    return d


def wizard_tower():
    """A tall gray brick tower with round windows and a pointed blue roof, full of books and bottles."""
    d = Design('WizardTower', 14, 31)
    # Pointed roof.
    for k in range(7):
        y = 6 - k
        d.tile(k, y, '[')
        d.tile(13 - k, y, ']')
        if k + 1 <= 12 - k:
            d.fill(k + 1, y, 12 - k, y, 'U')
    d.fill(1, 7, 1, 29, 'S')
    d.fill(12, 7, 12, 29, 'S')
    d.fill(0, 30, 13, 30, 'S')
    d.fill(2, 7, 11, 29, '.')
    d.wall(1, 7, 12, 29, 's')
    floors = {24: (2, 3), 18: (10, 11), 12: (2, 3)}
    for y, (p0, p1) in floors.items():
        d.fill(1, y, 12, y, 'S')
        d.fill(p0, y, p1, y, '-')
    # Round windows.
    for top in (7, 13, 19, 25):
        d.wall(6, top, 7, top, 'g')
        d.wall(5, top + 1, 8, top + 2, 'g')
        d.wall(6, top + 3, 7, top + 3, 'g')
    # Ground floor.
    d.put('D', 1, 27)
    d.put('K', 9, 26)
    d.put('C', 5, 28)
    d.put('T', 6, 28)
    d.put('c', 7, 27)
    d.put('b', 6, 27)
    d.put('l', 8, 25)
    # Second floor: library.
    d.put('K', 4, 20)
    d.put('C', 8, 22)
    d.put('T', 9, 22)
    d.put('c', 10, 21)
    d.put('b', 9, 21)
    d.put('l', 7, 19)
    # Third floor: bedroom.
    d.put('B', 2, 16)
    d.put('T', 6, 16)
    d.put('C', 9, 16)
    d.put('c', 7, 15)
    d.put('l', 5, 13)
    # Top floor: study with a clock.
    d.put('O', 10, 7)
    d.put('C', 5, 10)
    d.put('T', 6, 10)
    d.put('c', 7, 9)
    d.put('b', 6, 9)
    d.put('l', 8, 7)
    d.rooms = [(4, 26), (3, 20), (8, 14), (4, 8)]
    return d


DESIGNS = [small_house, two_floors, tower, medieval, castle, pagoda, wizard_tower, arena]

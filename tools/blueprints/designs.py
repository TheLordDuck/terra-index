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
    'f': (3, 2),  # campfire
    'Z': (2, 2),  # wooden crate
    'P': (3, 2),  # piano
    'G': (2, 2),  # wooden sink
    'j': (1, 1),  # chain (hangs, one tile)
    'h': (1, 1),  # book (on a table)
    'o': (1, 1),  # bowl (on a table)
    'u': (1, 1),  # mug (on a table)
    'Q': (3, 4),  # throne (counts as a chair)
    'V': (3, 2),  # sofa
    'H': (4, 2),  # bathtub
    't': (1, 2),  # toilet (counts as a chair)
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
        # Text drawn on the pictures only: (x, y, text), e.g. which crafting station goes in a bay.
        self.labels = []

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


def gable(d, x0, x1, eave, wall='p'):
    """A steep red shingle roof from x0 to x1 with its eaves on row `eave`, two tiles thick, hollow inside (with a
    background wall). Returns {row: (first, last)} of the open cells under it."""
    inside = {}
    k = 0
    while True:
        y, a, b = eave - k, x0 + k, x1 - k
        if b - a < 1:
            break
        d.tile(a, y, '(')
        d.tile(b, y, ')')
        if b - a >= 2:
            d.tile(a + 1, y, 'Y')
            d.tile(b - 1, y, 'Y')
        if b - a >= 4:
            d.fill(a + 2, y, b - 2, y, '.')
            d.wall(a + 2, y, b - 2, y, wall)
            inside[y] = (a + 2, b - 2)
        k += 1
    return inside


def craftsman_house():
    """A mansion with a place for every crafting station, in the style of a wooden mountain chalet: stone below,
    wood above, three steep red gables of different sizes (the second floor and the attic live under them), a
    stone chimney. Sized to fit between the two trees of the user's base (about 83 tiles wide at the ground, 33
    high), with the tree village (tree_village) going above it. Ground floor: the forge, open to the outside on the
    left (low stone wall, campfire, hanging chains, crate), the kitchen and the workshop hall (gray brick, stone
    pillars with corbels and torches). First floor: the trophy room (throne, places for relics), the library and
    two bedrooms for the players, one with a fireplace. Under the gables: the music lounge, the inventions workshop
    with the inventor's loft above it, and the bathroom. One shaft of platforms goes up from the kitchen to the
    loft. Every empty bay is 3 wide and 4 high; the pictures say which station each one is meant for."""
    W = 79
    d = Design('Workshop', W, 31)
    G = 30                                  # ground row
    F2, F3 = 23, 17                         # floor rows of the first floor and of the rooms under the gables
    L, R = 1, W - 2                         # outer walls

    def bay(x, floor, name, high=4):
        d.labels.append((x, floor - high, name))

    def room(x0, x1, top, bottom, wall):
        d.fill(x0, top, x1, bottom, '.')
        d.wall(x0, top, x1, bottom, wall)

    def divider(x, top, bottom, block):
        """A wall between two rooms, with a door at the bottom."""
        d.fill(x, top, x, bottom - 3, block)
        d.put('D', x, bottom - 2)

    # ---- Frame: stone foundation and ground floor, wooden floors above, posts under the eaves ----
    d.fill(0, G, W - 1, G, 'E')
    d.fill(L, F2, R, F2, '#')
    d.fill(41, F2, R, F2, 'E')
    d.fill(L, F3, R, F3, '#')
    d.fill(L, F3 + 1, L, F2 - 1, '#')
    d.fill(R, F3 + 1, R, F2 - 1, '#')
    d.fill(R, F2 + 1, R, G - 1, 'E')
    d.put('D', R, G - 3)
    d.fill(0, F3, 0, G - 1, '|')
    d.fill(W - 1, F3, W - 1, G - 1, '|')
    # The shaft up: kitchen -> library -> inventions workshop -> loft.
    for (x0, x1), y in (((25, 27), F2), ((26, 28), F3), ((33, 35), 10)):
        d.fill(x0, y, x1, y, '-')

    # ---- Three gables: a small one on the left, the big one in the middle, a low one on the right ----
    music = gable(d, 0, 23, F3 - 1)
    inventions = gable(d, 24, 58, F3 - 1)
    bath = gable(d, 59, W - 1, F3 - 1, 'x')
    # The loft floor across the big gable.
    a, b = inventions[10]
    d.fill(a, 10, b, 10, '#')
    d.fill(33, 10, 35, 10, '-')
    # Chimney on the right slope, a stone slab cap.
    d.fill(74, 5, 75, 11, 'S')
    d.fill(73, 4, 76, 4, 'E')

    # ======== Ground floor ========
    top, bottom = F2 + 1, G - 1
    # Forge, open to the outside: no wall above the low stone one, and no outer wall on the left.
    d.fill(L, top, 23, bottom, '.')
    d.wall(L, bottom - 2, 23, bottom, 'x')
    d.put('f', 2, bottom - 1)
    for x, name in ((6, 'Furnace'), (10, 'Hellforge'), (14, 'Anvil'), (18, 'Mythril Anvil')):
        bay(x, G, name)
    for x in (5, 9, 13, 17, 21):
        d.put('j', x, top)
        d.put('j', x, top + 1)
    d.put('Z', 22, bottom - 1)
    d.put('i', 23, top + 2)
    divider(24, top, bottom, 'E')
    # Kitchen, under the shaft.
    room(25, 39, top, bottom, 'p')
    d.wall(29, top, 34, top + 1, 'g')
    d.put('G', 25, bottom - 1)
    bay(28, G, 'Cooking Pot')
    bay(32, G, 'Keg')
    d.put('C', 35, bottom - 1)
    d.put('T', 36, bottom - 1)
    d.put('C', 39, bottom - 1)
    d.put('o', 36, bottom - 2)
    d.put('u', 38, bottom - 2)
    d.put('L', 36, top)
    d.rooms.append((31, bottom))
    divider(40, top, bottom, 'E')
    # Workshop hall: stone arches at the corners, bays between stone pillars.
    room(41, R - 1, top, bottom, 's')
    d.tile(41, top, '<')
    d.tile(R - 1, top, '>')
    d.put('C', 41, bottom - 1)
    d.put('W', 42, bottom)
    d.put('c', 42, bottom - 1)
    hall = ['Tinkerer\'s Workshop', 'Loom', 'Sawmill', 'Heavy Work Bench', 'Autohammer', 'Imbuing Station',
            'Solidifier', 'Adamantite Forge']
    for i, x in enumerate(range(44, 73, 4)):
        d.tile(x, top, 'E')
        if i > 0:
            d.tile(x - 1, top, '>')
        d.tile(x + 1, top, '<')
        d.tile(x + 3, top, '>')
        d.put('i', x, top + 1)
        d.fill(x, top + 2, x, bottom, '|')
        if i % 3 == 1:
            d.wall(x + 1, top + 1, x + 3, top + 2, 'g')
        bay(x + 1, G, hall[i])
    d.rooms.append((58, bottom))

    # ======== First floor ========
    top, bottom = F3 + 1, F2 - 1
    # Trophy room: a throne by a table, places for boss relics, the walls free for trophies and banners.
    room(L + 1, 23, top, bottom, 'x')
    d.wall(8, top, 13, top, 'g')
    for x in (3, 7, 11):
        bay(x, F2, 'Relic')
    d.put('Q', 15, bottom - 3)
    d.put('T', 19, bottom - 1)
    d.put('c', 20, bottom - 2)
    d.put('l', 18, top)
    d.rooms.append((10, bottom))
    divider(24, top, bottom, '#')
    # Library: the shaft goes up on the left, a reading table, two bookcases, a clock.
    room(25, 50, top, bottom, 'w')
    d.wall(40, top, 48, top, 'g')
    d.put('C', 29, bottom - 1)
    d.put('T', 30, bottom - 1)
    d.put('h', 30, bottom - 2)
    d.put('c', 31, bottom - 2)
    d.put('b', 32, bottom - 2)
    d.put('l', 33, top)
    d.put('K', 34, bottom - 3)
    d.put('K', 37, bottom - 3)
    bay(40, F2, 'Crystal Ball')
    bay(43, F2, 'Alchemy Table')
    bay(46, F2, 'Bewitching Table')
    d.put('O', 49, top)
    d.rooms.append((27, bottom))
    divider(51, top, bottom, '#')
    # Two bedrooms for the players; the second one has a fireplace under the chimney.
    room(52, 63, top, bottom, 'w')
    d.wall(53, top, 56, top + 1, 'g')
    d.put('B', 52, bottom - 1)
    d.put('C', 57, bottom - 1)
    d.put('T', 58, bottom - 1)
    d.put('c', 59, bottom - 2)
    d.put('l', 58, top)
    d.put('O', 62, top)
    d.rooms.append((56, bottom))
    divider(64, top, bottom, '#')
    room(65, R - 1, top, bottom, 'k')
    d.wall(69, top, 72, top + 1, 'g')
    d.put('C', 65, bottom - 1)
    d.put('T', 66, bottom - 1)
    d.put('c', 67, bottom - 2)
    d.put('B', 69, bottom - 1)
    d.put('F', 73, bottom - 1)
    d.put('l', 71, top)
    d.rooms.append((70, bottom - 2))
    d.fill(66, F3, 68, F3, '-')

    # ======== Under the gables ========
    bottom = F3 - 1
    # Music lounge, up from the trophy room.
    d.fill(2, F3, 4, F3, '-')
    d.wall(10, 9, 13, 11, 'g')
    bay(6, F3, 'Dye Vat')
    bay(10, F3, 'Sky Mill')
    d.put('T', 14, bottom - 1)
    d.put('c', 15, bottom - 2)
    d.put('C', 17, bottom - 1)
    d.put('P', 18, bottom - 1)
    d.put('l', music[7][0], 7)
    d.rooms.append((12, 12))
    # Inventions workshop: six bays, torches between them, round windows high up.
    for i, (x, name) in enumerate(zip(range(29, 50, 4), ['Ancient Manipulator', 'Blend-O-Matic', 'Honey Dispenser',
                                                        'Ice Machine', 'Lihzahrd Furnace', 'Glass Kiln'])):
        bay(x, F3, name)
        if i < 5:
            d.put('i', x + 3, 12)
    d.put('T', 52, bottom - 1)
    d.put('c', 53, bottom - 2)
    d.put('C', 55, bottom - 1)
    d.rooms.append((40, 14))
    # The inventor's loft in the top of the big gable.
    d.wall(40, 4, 42, 5, 'g')
    bay(37, 10, 'Bone Welder')
    bay(41, 10, 'Steampunk Boiler')
    d.put('T', 44, 8)
    d.put('b', 44, 7)
    d.put('c', 45, 7)
    d.put('C', 47, 8)
    d.put('l', 41, 1)
    d.rooms.append((40, 7))
    # Bathroom (not housing), up from the second bedroom.
    d.wall(67, 11, 70, 13, 'g')
    d.put('H', 62, bottom - 1)
    d.put('G', 70, bottom - 1)
    d.put('t', 74, bottom - 1)
    d.put('l', bath[9][0], 9)
    return d


def tree_village():
    """The NPC village that goes above the craftsman's house, in the style of a reference picture the user sent: one
    big lodge hung between two living wood trunks (89 tiles apart, like the user's trees at the height of their
    branch). A wide deck with balconies and a porch at the bottom, four wooden stories that step in as they go up,
    more on the left, then a steep red gable with three more stories inside it and a stone chimney on the left. Walkways reach
    out from every story's outer doors to where the user's trees go (the user builds the trees), with a vine rope down
    each side, so any story can be reached from outside on either side; inside, one shaft of platforms climbs every
    story and the rooms of a story are joined by doors. 30 rooms. Place it centered on the same column as the
    house, right above it."""
    W, H = 109, 60
    d = Design('TreeVillage', W, H)
    SHAFT = (52, 54)                        # platforms up through every story
    DECK = 56

    # ---- Stories: (floor row, left wall, right wall), 5 high under the next one's floor. They step in as they go
    # up, more on the left than on the right, leaving terraces; the second one overhangs a porch on the right ----
    stories = [(DECK, 16, 86), (50, 20, 93), (44, 24, 80), (38, 34, 80)]
    d.fill(10, DECK, 98, DECK, '#')
    for f, x0, x1 in stories:
        d.fill(x0, f - 5, x0, f - 1, '#')
        d.fill(x1, f - 5, x1, f - 1, '#')
        d.fill(x0 + 1, f - 5, x1 - 1, f - 1, '.')
    d.fill(16, 50, 93, 50, '#')
    d.fill(20, 44, 93, 44, '#')
    d.fill(24, 38, 80, 38, '#')
    d.fill(34, 32, 80, 32, '#')
    d.wall(17, 51, 85, 55, 'p')
    d.wall(21, 45, 92, 49, 'w')
    d.wall(25, 39, 79, 43, 'k')
    d.wall(35, 33, 79, 37, 'p')

    # The red gable on top, with three more floors inside it; the attic above them is closed roof space.
    inside = gable(d, 32, 82, 31)
    gable_floors = [26, 20, 14]
    for f in gable_floors:
        a, b = inside[f]
        d.fill(a, f, b, f, '#')
    d.wall(56, 9, 58, 11, 'g')

    # The stone chimney on the left slope.
    d.fill(37, 12, 38, 24, 'S')
    d.fill(36, 11, 39, 11, 'E')

    # The shaft: platforms in every floor from the first story's ceiling up to the last gable floor.
    for f in [50, 44, 38, 32] + gable_floors[:-1]:
        d.fill(SHAFT[0], f, SHAFT[1], f, '-')

    # ---- Walkways from every story's outer doors out to where the user's trees go, and a vine rope down each side
    # from the top one to the bottom, so any story can be reached from outside on either side ----
    LEFT_ROPE, RIGHT_ROPE = 14, 95
    for y, left_end, right_start in ((50, 19, 94), (44, 23, 94), (38, 33, 81)):
        d.fill(10, y, left_end, y, '#')
        d.fill(right_start, y, 98, y, '#')
    for x in (LEFT_ROPE, RIGHT_ROPE):
        d.fill(x, 36, x, H - 1, 'r')

    # Balconies on the deck, the porch under the second story's overhang, the covered porch under the top right
    # walkway, fences on the walkways.
    d.wall(11, 55, 13, 55, 'z')
    d.wall(96, 55, 97, 55, 'z')
    d.fill(93, 51, 93, 55, '|')
    d.put('l', 90, 51)
    d.put('i', 15, 52)
    d.put('i', 87, 51)
    d.wall(17, 49, 18, 49, 'z')
    d.wall(21, 43, 22, 43, 'z')
    d.wall(86, 43, 91, 43, 'z')
    d.fill(92, 41, 92, 43, '|')
    d.put('l', 85, 39)
    d.wall(26, 37, 31, 37, 'z')
    # ---- Rooms: dividers with doors, then furniture ----
    plans = [   # floor row, divider columns, interior wall
        (DECK, [26, 37, 48, 62, 74], 'p'),
        (50, [31, 42, 60, 70, 81], 'w'),
        (44, [35, 46, 61, 70], 'k'),
        (38, [45, 60, 70], 'p'),
        (32, [44, 61, 70], 'w'),
        (26, [50, 62], 'k'),
        (20, [58], 'p'),
    ]
    reserved = set()
    for y in range(H):
        for x in range(SHAFT[0] - 1, SHAFT[1] + 2):
            reserved.add((x, y))

    def door(x, f):
        d.put('D', x, f - 3)
        for y in range(f - 3, f):
            reserved.update({(x - 1, y), (x + 1, y)})

    # Outer doors: to the balconies, and out onto the terraces.
    door(16, DECK)
    door(86, DECK)
    door(20, 50)
    door(93, 50)
    door(24, 44)
    door(80, 44)
    door(34, 38)
    door(80, 38)

    def span(f, y):
        """Open cells of the story on floor f, on row y."""
        for ff, x0, x1 in stories:
            if ff == f:
                return x0 + 1, x1 - 1
        a, b = inside[y]
        return a, b

    def open_(x, y, w, h):
        return all(d.tiles[y + dy][x + dx] == '.' and (x + dx, y + dy) not in reserved
                   for dy in range(h) for dx in range(w))

    def solid(x, y):
        return d.tiles[y][x] in '#SEYv'

    def stand(x, y, w, h):
        return open_(x, y, w, h) and all(solid(x + dx, y + h) for dx in range(w))

    pieces = [('B', 4, 2), ('K', 3, 4), ('P', 3, 2), ('V', 3, 2), ('Z', 2, 2), ('O', 2, 5), ('W', 2, 1)]
    n = 0
    for f, dividers, wall in plans:
        lo, hi = span(f, f - 1)
        if f not in [s_[0] for s_ in stories]:
            for y in range(f - 5, f):
                d.wall(*inside[y][:1], y, inside[y][1], y, wall)
        for x in dividers:
            d.fill(x, f - 5, x, f - 4, '#')
            door(x, f)
        bounds = [lo - 1] + dividers + [hi + 1]
        for a, b in zip(bounds, bounds[1:]):
            a, b = a + 1, b - 1
            d.rooms.append((a, f - 1))
            ta, tb = span(f, f - 5)
            mid = (max(a, ta) + min(b, tb)) // 2
            d.wall(mid, f - 4, mid + 1, f - 3, 'g')
            # A lantern near the middle, hanging from the floor above.
            for x in sorted(range(a, b + 1), key=lambda x: abs(x - mid)):
                if open_(x, f - 5, 1, 2) and solid(x, f - 6):
                    d.put('l', x, f - 5)
                    break
            # Chair and table, something on the table.
            for x in range(a, b - 2):
                if stand(x, f - 2, 1, 2) and stand(x + 1, f - 2, 3, 2):
                    d.put('C', x, f - 2)
                    d.put('T', x + 1, f - 2)
                    if open_(x + 2, f - 3, 1, 1):
                        d.put('cbhou'[n % 5], x + 2, f - 3)
                    break
            # One more piece where it fits, from the right.
            for k in range(len(pieces)):
                c, w, h = pieces[(n + k) % len(pieces)]
                spot = next((x for x in range(b - w + 1, a - 1, -1) if stand(x, f - h, w, h)), None)
                if spot is not None:
                    d.put(c, spot, f - h)
                    break
            n += 1

    return d


DESIGNS = [small_house, two_floors, tower, medieval, castle, pagoda, wizard_tower, craftsman_house, tree_village, arena]

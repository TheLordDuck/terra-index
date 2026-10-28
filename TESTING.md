# Testing checklist (version 0.9)

0.4 compiles since 2026-10-22 (one type error fixed), but none of 0.4 to 0.7 has been tested in game yet. 0.7 was
written without a compiler, and so were 0.8 and 0.9: build it first and copy any errors.
Tick each box as you go. Multiplayer checks need a second player (Host & Play).

## 0. Setup

- [ ] `git pull`
- [ ] Remove the old `ModSources\RecipeIndex` link and create `ModSources\TerraIndex` (command in `TerraIndex/README.md`)
- [ ] tModLoader: **Develop Mods → TerraIndex → Build + Reload** (copy any build errors to fix them)
- [ ] Disable the old `RecipeIndex` in the mod list if it still shows up
- [ ] Rebind N / [ / ] if you had changed them (keybinds are stored under the mod's internal name)

## 1. Crafting from the chests

- [ ] With a terminal and wired chests: open the index (N), pick a recipe, and the bottom row says
      "From your chests you can make N"
- [ ] **Craft**, **x10** and **Max** take the ingredients from the chests and the item comes out of the door
- [ ] A crafted weapon gets a random prefix
- [ ] Crafting station **near you**: works without wiring it
- [ ] Crafting station **far away but on the red wire**: also works
- [ ] A Hellforge on the wire counts as a Furnace; a Mythril Anvil as an Anvil
- [ ] Without the station: "Missing crafting station" message
- [ ] Recipe with a group ingredient ("Any Wood"): another kind of wood is accepted
- [ ] Walk away from the terminal: the craft row disappears
- [ ] **Multiplayer:** both players crafting at once with the same materials don't duplicate anything
- [ ] Half the ingredients in the chests and half in the inventory: "you can make" counts both, the ingredients
      show blue, and crafting uses the chests first and then the inventory
- [ ] Everything in the inventory and nothing in the chests: the terminal also crafts it
- [ ] **Multiplayer:** if the chests empty out before the craft arrives, the inventory items come back

## 2. Blue ingredients

- [ ] Ingredient in the inventory → green; only in the chests → **blue**; in neither → red
- [ ] Crafting stations turn green or red near a terminal

## 4. Crafting tree

- [ ] **Tree** tab on a complex item (Terra Blade, an armor piece...)
- [ ] Bars you already have aren't asked for again
- [ ] Wood is a base material (not "crafted from Wood Platforms")
- [ ] Below the tree: "Missing materials" added up and "Crafting stations"
- [ ] Mouse wheel scrolls; clicking a row opens that item
- [ ] Items with several recipes say "(recipe 2 of 3)"

## 5 and 18. Crafting goal and its panel

- [ ] **Set as goal** in the tree: the panel appears on the left
- [ ] Pick up a material: its count goes up on its own
- [ ] Once you have everything: chat message "You have everything to craft ...!"
- [ ] **Drag** the panel by its title
- [ ] The **-** / **+** button folds it; folded, the title turns green when ready
- [ ] Hidden while the inventory is open
- [ ] Leave and rejoin: goal, position and folding are kept
- [ ] **Remove goal** removes it

## 6. Terminal sorting

- [ ] The "Sort: Name" button cycles Amount → Rarity → Mod
- [ ] Each order looks right (amount from most to least, vanilla first when sorting by mod)

## 8. Deposit all

- [ ] **Deposit all** puts the inventory into the chests, with the items flying into the door
- [ ] The hotbar, coins, ammo and favorited items stay
- [ ] With full chests: what doesn't fit is given back, with a warning
- [ ] Empty inventory: "Nothing to deposit" message
- [ ] **Multiplayer:** no items are lost

## 10. Portable terminal

- [ ] Craft it (Terminal + 10 Luminite Bars at the Ancient Manipulator)
- [ ] Hold it and right click a terminal: "linked", and the tooltip shows the distance
- [ ] Using it far away opens the terminal
- [ ] Taking an item out far away: it appears next to you
- [ ] Shift + click: arrows point to the far chests
- [ ] Drop it or stash it: the panel closes
- [ ] Not linked: warning message
- [ ] **Multiplayer:** works far from the base (the server accepts the requests)

## 13. Item comparison

- [ ] **+ Compare** on 2-3 weapons: table with the best value in green
- [ ] Comparing accessories: their effects (tooltip text) appear below
- [ ] Adding a fourth item drops the oldest
- [ ] Right click an item in the comparison removes it

## 17. Take missing

- [ ] On a recipe with ingredients in the chests: the "In the chests, missing from your inventory" row and **Take missing**
- [ ] It only takes what is missing (carrying 2 of 5 takes 3)
- [ ] **Shift + click** takes enough for 10 crafts
- [ ] With everything in the inventory, the row doesn't show

## 27. Settings

- [ ] **Settings → Mod Configuration → Terra Index**: texts look right in Spanish
- [ ] Change the **price** and open the Steampunker's shop: new price
- [ ] Change the **reach**: the panel closes at that distance
- [ ] Turn the **portable terminal** off: it says it is disabled
- [ ] "Include the hotbar in Deposit all"
- [ ] The default sort applies when entering a world
- [ ] Chest highlight duration
- [ ] Hide the goal panel, and change how many materials it lists

## Taking items out of the terminal

- [ ] Click an item: a full stack (or all there is) comes out of the door, with no pop-up
- [ ] Right click an item: it appears on the cursor with 1; each right click adds 1
- [ ] Hold right click: the amount goes up slowly, then faster and faster, and stops at what is stored / one stack
- [ ] With an amount on the cursor, click any item: exactly that amount comes out
- [ ] Right click empty space in the panel: the cursor item disappears and the hint goes back to normal
- [ ] Right click a different item: the selection switches to it, starting at 1
- [ ] Ctrl + click shows the item's recipe in the index next to the terminal (the terminal stays open); Shift + click still highlights the chests
- [ ] Both hint lines fit in the panel (English and Spanish)

## 35. Index next to the terminal

- [ ] The terminal's **Recipes** button opens the index on its left; both are centered together and don't overlap
- [ ] Recipes, Tree, Craft and Take missing work in the index while the terminal stays open, and the terminal's list updates after crafting
- [ ] Typing in one search box doesn't type in the other; clicking one box stops typing in the other
- [ ] Taking an item out (click or picked amount) keeps both open; the list shows the new amount right away
- [ ] "Deposit all" keeps the terminal open
- [ ] Closing the terminal and opening it again brings the index back
- [ ] Closing the index with its X (or N) while the terminal is open: next time the terminal opens alone
- [ ] Esc closes both; walking away closes both
- [ ] "Deposit items" closes the index (the inventory takes its place)
- [ ] With the terminal closed, the index opens centered as before
- [ ] At a smaller resolution or a bigger UI scale, the index gets narrower instead of covering the terminal

## 35. Goal "next step" button

- [ ] Set an Iron Broadsword as the goal with only Iron Ore: near a Furnace the panel shows "Craft N x Iron Bar"; clicking it turns the ore into bars
- [ ] Then, near an Anvil, it shows "Craft 1 x Iron Broadsword", and clicking it gives the sword (with a prefix)
- [ ] Away from the station, the row says which station the next step needs
- [ ] Recipe groups ("Any Wood") use any wood you have
- [ ] Near a terminal whose chests hold the ingredients (not your inventory), the button says "(chests)" and the item comes out of the door
- [ ] A fast double click crafts only once; the button is grey for a moment
- [ ] Folded, the button is hidden; the panel is still hidden with the inventory open
- [ ] Multiplayer: the other player sees your inventory change and the crafted item

## 39. Chest effects

- [ ] Taking an item out: the chest sparks, the terminal's door opens and the item drops out of it
- [ ] Deposit all / Deposit items: the items fly into the door and stop there; nothing travels to the chests and they don't spark
- [ ] Crafting from the chests: the chests the ingredients came from spark
- [ ] Turning off "Chest effects" in the settings removes the sparks
- [ ] Multiplayer: the other player sees the sparks too

## Free space

- [ ] The terminal panel says the connected chests, free slots out of all of them and how full they are (e.g. "12 chests · 120/400 free · 70% full"), left of the Recipes and Sort buttons without overlapping them (also after changing the sort, and in every language); white below 70%, yellow from 70%, red from 90%
- [ ] Depositing or taking items updates it within half a second, also on a multiplayer client

## Stations on the wire

- [ ] The terminal shows a row "Stations on the wire:" with an icon for each crafting station the red wire touches (anvil, furnace, work bench...); hovering one shows its name
- [ ] Without stations on the wire it says "none"; adding or removing one updates the row within a second
- [ ] With many stations the row ends with "+N" instead of going out of the panel
- [ ] The items grid below still shows its last row and page bar (the panel is a bit taller)
- [ ] Ores and bars (also from other mods) are in the **Materials** tab, not in the blocks one; wood, bricks and furniture stay in blocks

## Comfort

- [ ] Using the terminal itself, at its door or a few blocks away: the door opens and throws the item in an arc that lands on you
- [ ] Through the portable terminal (far away, and also standing next to the terminal), a glowing drone comes down from the sky, hovers above you, lets the item go (sparks and a click) and flies off; you can pick the item up right away
- [ ] Same under a low ceiling: the item appears on you instead of inside the blocks
- [ ] Through the portable terminal (far or near), Deposit all / Deposit items: the door stays shut and a drone comes down, your items fly up into its claw one by one, and it flies off with them; the items are in the chests
- [ ] Multiplayer: the other player sees the drone, and can't take the item
- [ ] "Take missing" and middle click also drop the items out of the door
- [ ] Turning off "Type in the terminal's search box when it opens": N, [ and ] work right after opening the terminal
- [ ] Search "wood", close the terminal, reopen it within 30 s: the search, tab and page are still there; after 30 s, or at another terminal, it is empty
- [ ] Index open near the terminal: middle click an item in the grid and an ingredient in a recipe takes a stack out
- [ ] Middle click with no terminal in reach, or on an item that isn't stored: a message explains why
- [ ] Multiplayer: another player standing at the door can't pick up the item you asked for; after 10 s on the ground anyone can

## Blueprints (0.8)

- [ ] Craft the Blueprint (10 wood at a work bench); its tooltip explains it
- [ ] Right click: the catalog opens with 4 houses; the material list starts with Wood and includes other woods and glass (and modded woods if you have any)
- [ ] Changing house or material updates the small preview; the catalog says building is free
- [ ] Place closes the catalog; holding the item shows the see-through house standing on the tile under the mouse
- [ ] Over dirt or stone the cells turn red with a message; over grass and plants it stays green
- [ ] Left click builds it: walls, blocks, platforms, door, chairs, tables and torches all in place; smoke and a sound; nothing leaves your inventory, even with it empty
- [ ] Every room of the small house, the two-floor house and the tower is valid housing (housing query / an NPC moves in)
- [ ] Boss arena: three rows of platforms you can jump between
- [ ] Building over existing background walls keeps them
- [ ] With another material (e.g. Boreal Wood or Glass) everything uses that material
- [ ] Multiplayer: the other player sees the house; building where something is in the way says it couldn't
- [ ] Medieval house, castle, pagoda and wizard tower look like `docs/blueprints/` (roof slopes the right way round: if they are mirrored, swap the slopes in `Blueprints.TileBrushes`)
- [ ] Their fixed materials (gray brick, red brick, glass walls, shingles...) are placed, and whatever isn't in this game is simply left out
- [ ] Craftsman's house (79x31) fits between the two trees of the base and looks like `docs/blueprints/8 - Craftsman's house.png`: three red gables of different sizes (check the slopes face the right way), forge open on the left, kitchen, stone hall with arches and pillars, trophy room with a throne, library, two bedrooms (fireplace in the second), music lounge, inventions workshop, loft, bathroom, chimney on the right roof
- [ ] Each station goes in its labelled bay (Furnace, Hellforge, anvils, Cooking Pot, Keg, Tinkerer's Workshop, Loom, Sawmill, Heavy Work Bench, Autohammer, Imbuing Station, Solidifier, Adamantite Forge, Crystal Ball, Alchemy Table, Bewitching Table, Dye Vat, Sky Mill, Ancient Manipulator, Blend-O-Matic, Honey Dispenser, Ice Machine, Lihzahrd Furnace, Glass Kiln, Bone Welder, Steampunk Boiler), and relics in the trophy room
- [ ] The nine rooms (not the forge or bathroom) are valid housing; the shaft (kitchen -> library -> inventions -> loft) and second bedroom -> bathroom platforms can be jumped through; the doors open
- [ ] Tree village (109x60) built above the mansion on the same column: a lodge of stepped stories under a red gable, no trees, 30 rooms all valid housing, doors open (not blocked by furniture), the platform shaft climbs every story, the vine rope on each side reaches every walkway and each walkway leads to a story's door, fences show on the balconies and walkways
- [ ] Big tree (75x61) looks like `docs/blueprints/10 - Big tree.png`: living wood trunk and branches, leaf blocks, rounded
      edges (wood and leaves sloped the right way round), living leaf wall behind the crown, vines hanging under it
- [ ] Thick tree (75x61) is the same tree with the trunk and branches two tiles thicker, like `docs/blueprints/11 - Thick tree.png`
- [ ] Placed with its bottom row on top of a living wood trunk you built, it isn't blocked and continues it
- [ ] Furniture the wood you picked doesn't have (e.g. a clock) uses plain wood's or a vanilla one; lights never go missing
- [ ] Every NPC room of the big ones is valid housing (medieval 5, castle 11, pagoda 3, wizard tower 4)
- [ ] The catalog fits on screen with the castle selected (cost in two columns)

## 47. Herb garden, fishing pond and arena with buffs

- [ ] They show in the catalog (names translated) and look like `docs/blueprints/13`, `14` and `15`
- [ ] Herb garden: the three rows of planter boxes are placed (row by row, from the hut's wall to the post at the far end), you can jump up through them and drop down, and seeds can be planted in them
- [ ] Herb garden: the hut is valid housing, and the bottle on its table works as an alchemy station
- [ ] Fishing pond: the pond is full of water, doesn't leak, and the in-game preview shows the water in blue
- [ ] Fishing pond: fishing from the pier doesn't say the pool is too small; the hut is valid housing
- [ ] Arena with buffs: campfires stand on the platforms and the heart lanterns and stars in a bottle hang under them; standing on any row gives Campfire, Heart Lamp and Star in a Bottle
- [ ] **Multiplayer:** the other player sees the pond's water (not an empty basin)

## 48. NPC happiness in the catalog

- [ ] Under the catalog, a row of town NPC heads (hover: the NPC's name); the panel fits on screen
- [ ] Clicking a head shows "Loves / Likes / Dislikes / Hates" lines with biomes (in your language) and NPCs; clicking it again hides them
- [ ] Compare a couple with the wiki (e.g. the Guide likes the forest, the Nurse loves the Arms Dealer)
- [ ] With Calamity (or another mod adding town NPCs): its NPCs show up with their own preferences

## 49. Next upgrades tab

- [ ] The index has a Night's Edge tab right after All; its tooltip says how many items it has
- [ ] New character (copper gear): it suggests better weapons of the classes you carry, armor pieces and a pickaxe, nothing from hardmode
- [ ] After beating bosses (or in a later world) it suggests later gear; never what you already carry or wear
- [ ] Click a suggestion, Tree tab, set it as the goal: the goal panel tracks it
- [ ] The terminal's grid doesn't have this tab

## 50. "In your chests" in tooltips

- [ ] Near a terminal (or with its portable terminal), hovering an item in your inventory says "In your chests: N" in blue, if there are any
- [ ] The number follows the chests (take some out: it goes down within a second)
- [ ] Far from the terminal, or with the setting off, the line isn't there
- [ ] Also in chests, shops and the index

## 40 and 41. Languages and help

- [ ] Switch the game to each language (German, French, Portuguese, Russian, Italian, Polish, Chinese): the panels show translated texts, not keys
- [ ] Nothing overlaps in the terminal's header (the ? button, Deposit all, Deposit items) and the Recipes / Sort row, especially in German and Russian
- [ ] Hovering **?** in the terminal and in the index shows the list of controls, fully on screen
- [ ] New character: placing the first terminal shows the wiring tip (with the Red Wrench's and Mechanic's names in your language); opening it shows the help tip. Neither shows again for that character

## Radio

- [ ] Craft it at an anvil (8 Wood, 3 Iron/Lead Bars, 2 Glass) and place it on the floor: same size as a Piggy Bank
- [ ] A few seconds after placing it, one of the two clips plays; the next time, the other one
- [ ] While a clip plays, music notes float out by the antenna (and stop when it ends or is turned off)
- [ ] The gaps between clips change (sometimes ~10 s, sometimes a minute or two)
- [ ] Right click: the dial and the light go dark and it stops (also in the middle of a clip); right click again turns it on
- [ ] Walking away makes it quieter; two radios next to each other don't play at the same time
- [ ] Breaking it (on or off) drops the radio
- [ ] Multiplayer: turning it on/off is seen by the other player, and both players hear the same clip at the same time

## Performance

- [ ] With a big storage (lots of chests and item types), the terminal panel doesn't hitch every half second
- [ ] Changes made by another player still show up in an open terminal within a second (multiplayer)
- [ ] Opening a terminal right after closing the recipe index shows its items (not an empty list)
- [ ] The goal panel still updates when you pick up or use a material

## 28. Sprites

- [ ] The portable terminal sprite looks right in the inventory and when held

## Also check

- [ ] **Spanish texts:** nothing overlaps (especially the terminal's buttons and the craft row)
- [ ] **The rename:** the mod shows as "Terra Index", and terminals and chests from before still work

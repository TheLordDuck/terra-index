# Sprite tools

The Apothecary's sprite is built on the Guide's vanilla sheet, so it has his poses and 26-frame animation: a wide
grey witch hat with a bent tip, a white mask with a thin moustache and pink cheeks, dark hair, a long navy-black
dress and grey gloves (the look the user picked).

1. Extract the Guide's texture (`xnb` reads the game's compressed `.xnb` files with tModLoader's FNA.dll):
   `dotnet run --project xnb -- <Terraria>/Content/Images/NPC_22.xnb NPC_22.rgba`
2. In the same folder, run `node apothecary.js`: it writes `gen/Apothecary.rgba` and `gen/Apothecary_Head.rgba`.
3. `powershell -File topng.ps1 gen` turns them into PNGs; copy those to `TerraIndex/Content/NPCs/`.
   `sheet.ps1` lays a sheet out scaled up, with frame numbers, to look at it.

The extracted vanilla files are not kept in the repo.

The Calamity guide book (`TerraIndex/Content/Items/GuideBookItem.png`) is the vanilla Book (`Item_149.xnb`, extracted
the same way) with its green cover turned red, so it is not mistaken for the vanilla one.

## Bone Ronin

`node tools/sprites/ronin.js` (from the repo root) draws the Bone Ronin at a player's size, in both looks (ghost and
ember): the body sheets (idle, dash, iaido, cast and dying animations), the sword arms, the map icon, and
`RoninFrames.cs` with where the shoulder and the eye are in each frame. `tools/sprites/gen/ronin_preview.png` and
`ronin_zoom.png` show the result enlarged. Change the script and run it again rather than editing the PNGs.

## Mod icon

From the repo root, `python tools/sprites/icon.py` redraws `TerraIndex/icon.png` (not `icon_small.png`) from the mod's own
sprites (portable terminal, wireless teleporter) plus a drawn terminal and Calamity guide book. Rerun it after
changing any of those sprites.

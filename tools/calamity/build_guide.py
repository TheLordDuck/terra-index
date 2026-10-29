# Builds TerraIndex/Data/CalamityGuide.txt, the Calamity class guide shown in the recipe index (see
# Common/CalamityGuide.cs), from the Calamity wiki's "Class setups" guide:
#   https://calamitymod.wiki.gg/wiki/Guide:Class_setups
# The guide's gear lives in the wiki's ClassSetups Cargo table (item, class, progression, type). Item names are the
# wiki's English page names, so they are matched to internal names with Calamity's English localization (from its
# public GitHub repo) and the Terraria wiki's item table. Run from the repo root:
#   python tools/calamity/build_guide.py            (downloads into tools/calamity/cache, then builds)
#   python tools/calamity/build_guide.py --offline  (uses the cache)
# It prints the names it couldn't match; most are not items ("Any Double Jump", NPCs, enchantments).
import html
import json
import os
import re
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(ROOT, "cache")
OUT = os.path.join(ROOT, "..", "..", "TerraIndex", "Data", "CalamityGuide.txt")
UA = {"User-Agent": "TerraIndexGuideBuilder/1.0 (Terraria mod data)"}
CALAMITY_REPO = "CalamityTeam/CalamityModPublic"
CALAMITY_BRANCH = "1.4.4"

CLASSES = ["melee", "ranged", "magic", "summon", "rogue"]
# The guide's stages in order (progression ids). pre-scal and pre-exo are pre-scal-exo once one of the two is beaten.
STAGES = ["pre-boss", "pre-evil1", "pre-evil2", "pre-skeletron", "pre-wof", "pre-mech", "post-mech1", "post-mech2",
          "pre-plantera", "pre-golem", "post-golem", "pre-lunar", "pre-moonlord", "pre-provi", "pre-polter", "pre-dog",
          "pre-yharon", "pre-scal-exo", "pre-scal", "pre-exo", "endgame"]
# Cargo type -> section of the guide tab, in the order they are shown.
SECTIONS = {
    "armor": "armor",
    "weapon": "weapons", "weaponSpam": "weapons", "weaponStealth": "weapons", "minions": "weapons",
    "sentries": "weapons", "weaponSummon": "weapons", "support": "weapons",
    "accessoryMobilityPrimary": "accessories", "accessory": "accessories", "accessoryGeneral": "accessories",
    "accessorGeneral": "accessories", "accessoryMobility": "accessories", "accessoryOffense": "accessories",
    "accessoryDefense": "accessories", "accessorySpam": "accessories", "accessoryStealth": "accessories",
    "ammo": "potions", "buff": "potions", "buffOffense": "potions", "buffDefense": "potions",
    "buffMobility": "potions", "buffGeneral": "potions",
}
ROGUE_ONLY = {"weaponSpam", "weaponStealth", "accessorySpam", "accessoryStealth"}
SUMMON_ONLY = {"minions", "sentries", "weaponSummon"}


def fetch(url, path):
    if "--offline" not in sys.argv or not os.path.exists(path):
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as response:
            open(path, "wb").write(response.read())
    return open(path, encoding="utf-8").read()


def download():
    os.makedirs(os.path.join(CACHE, "calamity"), exist_ok=True)
    setups = json.loads(fetch("https://calamitymod.wiki.gg/index.php?title=Special:CargoExport&tables=ClassSetups"
                              "&fields=item,class,progression,type&limit=5000&format=json", os.path.join(CACHE, "setups.json")))
    vanilla = json.loads(fetch("https://terraria.wiki.gg/index.php?title=Special:CargoExport&tables=Items"
                               "&fields=name,internalname,type&limit=10000&format=json", os.path.join(CACHE, "vanilla.json")))
    folder = os.path.join(CACHE, "calamity")
    if "--offline" not in sys.argv:
        names = subprocess.run(["gh", "api", f"repos/{CALAMITY_REPO}/contents/Localization/en-US?ref={CALAMITY_BRANCH}",
                                "--jq", '.[] | select(.name|startswith("Mods.CalamityMod.Items.")) | .name'],
                               capture_output=True, text=True, check=True).stdout.split()
        for name in names:
            fetch(f"https://raw.githubusercontent.com/{CALAMITY_REPO}/{CALAMITY_BRANCH}/Localization/en-US/{name}",
                  os.path.join(folder, name))
    calamity = {}  # English name -> internal name
    armor = set()
    for name in sorted(os.listdir(folder)):
        text = open(os.path.join(folder, name), encoding="utf-8").read()
        for internal, display in re.findall(r"^(\w+): \{\s*\n\tDisplayName: (.+)$", text, re.M):
            display = display.strip().strip('"')
            calamity[display] = internal
            if ".Armor." in name and ".Vanity." not in name:
                armor.add(internal)
    return setups, vanilla, calamity, armor


def norm(name):
    return re.sub(r"\s+", " ", name.replace("’", "'")).strip().lower()


def item_names(cell):
    # The item's links, without the note tooltip after it (which links other items) and without images.
    cell = html.unescape(cell).split('<span class="tooltip"')[0]
    names = []
    for link in re.findall(r"\[\[([^\]]+)\]\]", cell):
        target = link.split("|")[0].split("#")[0].strip()
        if not target.lower().startswith("file:") and target not in names:
            names.append(target)
    return names


def wanted(row_class, type_, cls):
    if type_ in ROGUE_ONLY and cls != "rogue" or type_ in SUMMON_ONLY and cls != "summon":
        return False
    if type_ == "armor":
        return row_class in (cls, "all")
    return (row_class in (cls, "all")
            or row_class == "all-but-summoner" and cls != "summon"
            or row_class == "all-but-stealth" and cls != "rogue"
            or row_class == "all-but-summoner-stealth" and cls not in ("summon", "rogue"))


def main():
    setups, vanilla, calamity, armor = download()
    by_name, pieces_of_armor = {}, set()
    for row in vanilla:
        if row.get("internalname") and row["internalname"] != "None":
            ref = f"Terraria/{row['internalname']}"
            by_name.setdefault(norm(html.unescape(row["name"])), ref)
            types = row.get("type") or []
            if "armor" in types and "vanity" not in types and "set" not in types:
                pieces_of_armor.add(ref)
    for display, internal in calamity.items():
        by_name[norm(display)] = f"CalamityMod/{internal}"
        if internal in armor:
            pieces_of_armor.add(f"CalamityMod/{internal}")

    def resolve(name, cls):
        key = norm(name)
        if key in by_name:
            return [by_name[key]]
        # "Victide armor": the armor pieces named "Victide ..." (or "Squire's ..."). Calamity's class heads
        # (VictideHeadRogue...) only for the class.
        if key.endswith(" armor"):
            prefix = key[:-len(" armor")]
            others = [c for c in ("Melee", "Ranged", "Magic", "Summon", "Rogue") if c.lower() != cls]
            pieces = [ref for n, ref in by_name.items() if (n.startswith(prefix + " ") or n.startswith(prefix + "'s "))
                      and ref in pieces_of_armor and not any("Head" + c in ref for c in others)]
            return sorted(set(pieces))
        return []

    lines, missing = [], set()
    for stage in STAGES:
        for cls in CLASSES:
            for section in dict.fromkeys(SECTIONS.values()):
                seen = set()
                for row in setups:
                    if row["progression"] != stage or SECTIONS.get(row["type"]) != section or not wanted(row["class"], row["type"], cls):
                        continue
                    refs = []
                    for name in item_names(row["item"]):
                        found = resolve(name, cls)
                        if not found:
                            missing.add(name)
                        refs += [ref for ref in found if ref not in refs]
                    key = tuple(refs)
                    if refs and key not in seen:
                        seen.add(key)
                        lines.append(f"{stage}\t{cls}\t{section}\t{' '.join(refs)}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as out:
        out.write("# Generated by tools/calamity/build_guide.py from the Calamity wiki's Class setups guide. Don't edit by hand.\n")
        out.write("# stage <tab> class <tab> section <tab> items (Mod/InternalName; several are alternatives or an armor set)\n")
        out.write("\n".join(lines) + "\n")
    print(f"{len(lines)} lines written to {os.path.normpath(OUT)}")
    print(f"{len(missing)} names not matched:", ", ".join(sorted(missing)))


main()

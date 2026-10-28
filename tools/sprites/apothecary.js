// Builds the Apothecary's sheet on the Guide's 26 frames (same poses and animation, so AnimationType = Guide fits):
// hair becomes a hood, the shirt a green robe whose skirt covers the legs, plus a belt with potions.
// Works on the 2 x 2 "art pixel" grid vanilla sprites use. Output: raw RGBA files (topng.ps1 turns them into PNGs).
const fs = require("fs");
const src = fs.readFileSync("NPC_22.rgba");
const W = 40, H = src.readInt32LE(4), FH = 56, FRAMES = H / FH;
const AW = W / 2, AH = FH / 2;

const key = (r, g, b) => `${r},${g},${b}`;
const get = (x, y) => { const i = 8 + (y * W + x) * 4; return src[i + 3] ? key(src[i], src[i + 1], src[i + 2]) : null; };

const hair = new Set(["53,29,6", "217,139,47", "196,120,31", "79,44,10", "174,101,14", "110,62,14", "142,81,14"]);
const shirt = ["50,50,37", "167,166,125", "198,196,170", "124,124,97", "104,104,84", "139,139,107", "65,65,50", "117,117,93",
	"133,133,103", "148,147,113", "161,160,121", "165,164,135", "107,107,86", "197,195,169", "63,61,50", "96,93,74", "79,82,66"];
const pants = new Set(["35,38,43", "127,159,213", "95,117,151", "78,84,92", "115,131,153", "103,108,112"]);
const shoes = { "32,30,26": [38, 24, 18], "63,61,50": [92, 58, 36], "96,93,74": [128, 84, 50], "46,46,45": [38, 24, 18] };

const hood = { O: [44, 20, 56], D: [84, 40, 110], M: [120, 62, 152], L: [164, 104, 196] };
const robe = [[22, 46, 30], [38, 86, 52], [56, 120, 70], [82, 156, 94], [124, 192, 120]];
const belt = [84, 52, 30], buckle = [236, 200, 80];
const vials = [[[200, 230, 240], [220, 40, 60], [150, 20, 40]], [[200, 230, 240], [60, 120, 230], [30, 70, 160]]];

// Hair → hood shades by brightness; shirt and pants → robe shades.
const lum = k => { const [r, g, b] = k.split(",").map(Number); return 0.3 * r + 0.59 * g + 0.11 * b; };
function hairColor(k) { const l = lum(k); return l < 40 ? hood.O : l < 80 ? hood.D : l < 120 ? hood.M : hood.L; }
function robeColor(k) {
	if (k === "50,50,37" || k === "35,38,43" || k === "65,65,50") return robe[0];
	const l = lum(k);
	return l < 100 ? robe[1] : l < 135 ? robe[2] : l < 175 ? robe[3] : robe[4];
}

// The hood over frame 0's head (art rows 1-14); moved up with the head on bobbing frames.
// O outline, D/M/L shades. Only drawn over hair or empty pixels, so the face and the arms stay in front.
const HOOD = [
	"....................",
	"...........OOO......",
	".........OOMDDO.....",
	"......OOOMMMMDO.....",
	"....OOLMMMMMMDO.....",
	"...OLMMMMMMMDO......",
	"...OLMMMMMMMMDO.....",
	"..OLMMMMMMMMMDDO....",
	"..OLMMMMMMMMMDDO....",
	"..OLLOOOOOOMMDDO....",
	"..OLO......OMDDO....",
	"..OLO......OMDDO....",
	"...OO......ODDDO....",
	"...........ODDO.....",
	"............OO......",
];

function frame(f) {
	const art = [];
	for (let y = 0; y < AH; y++) {
		art.push([]);
		for (let x = 0; x < AW; x++) art[y].push(get(x * 2, f * FH + y * 2));
	}
	// Head and pants tops, to follow the bob.
	let headTop = 99, pantsTop = 99;
	for (let y = 0; y < AH; y++) for (let x = 0; x < AW; x++) {
		if (hair.has(art[y][x]) && x >= 3) headTop = Math.min(headTop, y);
		if (pants.has(art[y][x])) pantsTop = Math.min(pantsTop, y);
	}
	const dy = headTop - 4;
	const out = art.map(row => row.map(k => {
		if (!k) return null;
		if (hair.has(k)) return hood.O;
		return k.split(",").map(Number);
	}));
	for (let y = 0; y < AH; y++) for (let x = 0; x < AW; x++) {
		const k = art[y][x];
		if (!k) continue;
		if (y >= pantsTop + 2 && k in shoes) out[y][x] = shoes[k];
		else if (pants.has(k) || shirt.includes(k)) out[y][x] = robeColor(k);
	}
	// Robe skirt down to the ankles: fill the gap between the legs, flaring with them when walking, and close it
	// with outline on its hem.
	let shoesTop = AH;
	for (let y = pantsTop; y < AH; y++) for (let x = 0; x < AW; x++) if (art[y][x] in shoes) shoesTop = Math.min(shoesTop, y);
	for (let y = pantsTop; y < shoesTop; y++) {
		let l = -1, r = -1;
		for (let x = 0; x < AW; x++) if (art[y][x] && pants.has(art[y][x])) { if (l < 0) l = x; r = x; }
		if (l < 0) continue;
		for (let x = l + 1; x < r; x++) if (!art[y][x]) out[y][x] = y === shoesTop - 1 ? robe[0] : robe[1];
	}
	// Hood.
	for (let y = 0; y < HOOD.length; y++) for (let x = 0; x < AW; x++) {
		const c = HOOD[y][x], ty = y + dy;
		if (c === "." || ty < 0 || ty >= AH) continue;
		const k = art[ty][x];
		if (!k || hair.has(k)) out[ty][x] = hood[c];
	}
	// Belt two rows above the legs, over robe pixels only, with a buckle and two hanging vials.
	const by = pantsTop - 2;
	const isRobe = (x, y) => art[y] && art[y][x] && (shirt.includes(art[y][x]) || pants.has(art[y][x])) && art[y][x] !== "50,50,37" && art[y][x] !== "35,38,43";
	for (let x = 0; x < AW; x++) if (isRobe(x, by) && art[by][x] !== "50,50,37") out[by][x] = belt;
	if (isRobe(8, by)) out[by][8] = buckle;
	[[7, 0], [9, 1]].forEach(([vx, v]) => {
		if (isRobe(vx, by + 1) && isRobe(vx, by + 2)) { out[by + 1][vx] = vials[v][1]; out[by + 2][vx] = vials[v][2]; }
	});
	return out;
}

function write(file, frames, w, h) {
	const buf = Buffer.alloc(8 + w * h * 4);
	buf.writeInt32LE(w, 0); buf.writeInt32LE(h, 4);
	frames.forEach((art, f) => {
		for (let y = 0; y < art.length; y++) for (let x = 0; x < art[y].length; x++) {
			const c = art[y][x];
			if (!c) continue;
			for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) {
				const i = 8 + (((f * art.length + y) * 2 + dy) * w + x * 2 + dx) * 4;
				buf[i] = c[0]; buf[i + 1] = c[1]; buf[i + 2] = c[2]; buf[i + 3] = 255;
			}
		}
	});
	fs.writeFileSync(file, buf);
}

const frames = [];
for (let f = 0; f < FRAMES; f++) frames.push(frame(f));
fs.mkdirSync("gen", { recursive: true });
write("gen/Apothecary.rgba", frames, W, H);

// Head icon: frame 0's head (art rows 2-13, columns 2-16), without the robe's shoulders.
const isRobeColor = c => robe.some(r => r[0] === c[0] && r[1] === c[1] && r[2] === c[2]);
const head = frames[0].slice(2, 14).map(row => row.slice(2, 17).map(c => c && !isRobeColor(c) ? c : null));
write("gen/Apothecary_Head.rgba", [head], head[0].length * 2, head.length * 2);
console.log(FRAMES + " frames");

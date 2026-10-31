// Builds the Apothecary's sheet on the Guide's 26 frames (same poses and animation, so AnimationType = Guide fits),
// after the look the user picked: a grey witch hat with a bent tip, a white mask with a thin moustache and pink
// cheeks, dark hair, a long navy-black dress and grey gloves.
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
const shoes = { "32,30,26": [38, 25, 14], "63,61,50": [92, 60, 34], "96,93,74": [184, 121, 69], "46,46,45": [38, 25, 14] };
const skin = new Set(["239,132,90", "255,173,140", "247,153,115", "214,90,49", "154,75,49"]);
const skinOutline = "55,23,12";
// The Guide's face (frame 0's art rows 10-14), where everything but his clothes is painted over by the mask, so the
// talking and blinking frames don't show through. Elsewhere only his hair or nothing is painted over, so a hand
// raised to the hat stays in front.
const inFace = (x, y, dy) => x >= 5 && x <= 12 && y >= 10 + dy && y <= 14 + dy;
const isHead = (k, x, y, dy) => !k || hair.has(k) || (inFace(x, y, dy) && !shirt.includes(k) && !pants.has(k));

const C = {
	a: [38, 30, 23], f: [48, 48, 48], h: [69, 69, 69], b: [74, 74, 74], e: [93, 93, 93], c: [108, 108, 108], d: [129, 129, 129],
	g: [194, 191, 171], j: [218, 216, 203], i: [239, 239, 239], k: [255, 130, 189], l: [204, 106, 143],
};
const darkHair = [[24, 18, 14], [38, 30, 23], [58, 48, 40]];
const dress = [[18, 20, 28], [34, 38, 54], [49, 54, 77], [62, 69, 97], [75, 83, 117]];
const gloves = { [skinOutline]: C.a, dark: C.b, mid: C.e, light: C.c };

const lum = k => { const [r, g, b] = k.split(",").map(Number); return 0.3 * r + 0.59 * g + 0.11 * b; };
const hairColor = k => { const l = lum(k); return l < 40 ? darkHair[0] : l < 110 ? darkHair[1] : darkHair[2]; };
function dressColor(k) {
	if (k === "50,50,37" || k === "35,38,43" || k === "65,65,50") return dress[0];
	const l = lum(k);
	return l < 100 ? dress[1] : l < 135 ? dress[2] : l < 175 ? dress[3] : dress[4];
}
const gloveColor = k => k === skinOutline ? gloves[skinOutline] : lum(k) < 120 ? gloves.dark : lum(k) < 160 ? gloves.mid : gloves.light;

// Hat, hair and mask over frame 0's head, art rows 1-14 (the eyes on row 10, like the Guide's); moved with the head on
// bobbing frames. Letters are colours of C; only drawn over the head (see isHead).
const HEAD = [
	"...aaaaa......",
	"..abbcdba.....",
	"..abeccdca....",
	"..abbecdba....",
	"...abbbaaaa...",
	"...aaaabeccaa.",
	".aabbcddddceba",
	"afbbceeaaaaaba",
	"abbbbbaggggaha",
	".abaaagaaiaaa.",
	"..aahajiijga..",
	"...aeagkiaal..",
	"...aaajiaija..",
	"...aaaagjaaa..",
];
const HEAD_TOP = 1;

function frame(f) {
	const art = [];
	for (let y = 0; y < AH; y++) {
		art.push([]);
		for (let x = 0; x < AW; x++) art[y].push(get(x * 2, f * FH + y * 2));
	}
	let headTop = 99, pantsTop = 99;
	for (let y = 0; y < AH; y++) for (let x = 0; x < AW; x++) {
		if (hair.has(art[y][x]) && x >= 3) headTop = Math.min(headTop, y);
		if (pants.has(art[y][x])) pantsTop = Math.min(pantsTop, y);
	}
	const dy = headTop - 4;
	const out = art.map(row => row.map(k => {
		if (!k) return null;
		if (hair.has(k)) return hairColor(k);
		return k.split(",").map(Number);
	}));
	for (let y = 0; y < AH; y++) for (let x = 0; x < AW; x++) {
		const k = art[y][x];
		if (!k) continue;
		if (y >= pantsTop + 2 && k in shoes) out[y][x] = shoes[k];
		else if (pants.has(k) || shirt.includes(k)) out[y][x] = dressColor(k);
		// Hands (skin outside the face) become grey gloves.
		else if (!inFace(x, y, dy) && (skin.has(k) || k === skinOutline)) out[y][x] = gloveColor(k);
	}
	// The dress reaches the ankles.
	let shoesTop = AH;
	for (let y = pantsTop; y < AH; y++) for (let x = 0; x < AW; x++) if (art[y][x] in shoes) shoesTop = Math.min(shoesTop, y);
	for (let y = pantsTop; y < shoesTop; y++) {
		let l = -1, r = -1;
		for (let x = 0; x < AW; x++) if (art[y][x] && pants.has(art[y][x])) { if (l < 0) l = x; r = x; }
		if (l < 0) continue;
		for (let x = l + 1; x < r; x++) if (!art[y][x]) out[y][x] = y === shoesTop - 1 ? dress[0] : dress[1];
	}
	// Hat, hair and mask.
	for (let y = 0; y < HEAD.length; y++) for (let x = 0; x < HEAD[y].length; x++) {
		const c = HEAD[y][x], ty = y + HEAD_TOP + dy;
		if (c === "." || ty < 0 || ty >= AH || !isHead(art[ty][x], x, ty, dy)) continue;
		out[ty][x] = C[c];
	}
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

// Head icon: frame 0's hat and head (art rows 1-14, columns 0-14), without the dress.
const isDress = c => dress.some(r => r[0] === c[0] && r[1] === c[1] && r[2] === c[2]);
const head = frames[0].slice(1, 15).map(row => row.slice(0, 15).map(c => c && !isDress(c) ? c : null));
write("gen/Apothecary_Head.rgba", [head], head[0].length * 2, head.length * 2);
console.log(FRAMES + " frames");

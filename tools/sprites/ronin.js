// Draws the Bone Ronin at a player's size, in both looks (ghost and ember), with an animation for each thing he
// does. Run from the repo root: `node tools/sprites/ronin.js`. It writes into TerraIndex/Content/Bosses/BoneRonin/:
// - BoneRonin.png / BoneRonin_Ember.png: the body sheets, FRAMES frames of 64 x 68 stacked vertically.
// - BoneRonin_Arm.png / BoneRonin_Ember_Arm.png: the sword arm (sleeve, hand, katana), rotated by code.
// - BoneRonin_Head_Boss.png: the map icon.
// - RoninFrames.cs: where the shoulder and the eye are in each frame, and the animations' frame ranges.
// and tools/sprites/gen/ronin_preview.png, every frame of both looks enlarged, to look at them.
//
// Everything is drawn on a grid of "art pixels" that become 2 x 2 screen pixels, like vanilla sprites. Each body part
// is a shape (polygon or a thick line); a part is shaded on its own with a 4-colour ramp (outline, shadow, base, light),
// lit from the upper front, and then painted over the parts behind it. Cloth (scarf, headband tails, hair, sleeve,
// hakama hem) is moved by a "wind" and a wave that depend on the frame, which is what animates him.
const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

const OUT = path.join(__dirname, "..", "..", "TerraIndex", "Content", "Bosses", "BoneRonin");
const GEN = path.join(__dirname, "gen");

const W = 32, H = 34; // frame size in art pixels
const S = 2; // screen pixels per art pixel
const GROUND = 33; // bottom edge of the feet
const HIP = 19;

// ---------------------------------------------------------------------------------------------------------------
// Colours

const hex = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];
const ramp = (...h) => h.map(hex);

const LOOKS = {
	ghost: {
		skin: ramp("#40362b", "#a6957a", "#d8cbaa", "#f4ecd4"),
		top: ramp("#11162a", "#262e4d", "#38446e", "#56659a"),
		topBack: ramp("#0c1020", "#1b2240", "#283153", "#38446e"),
		inner: ramp("#4a4a5a", "#b8b8c8", "#dcdce6", "#f4f4fa"),
		hakama: ramp("#29283d", "#77799c", "#a1a4c3", "#c9cce0"),
		hem: [hex("#5c4c86"), hex("#3e3266")],
		wisp: [150, 190, 255],
		obi: ramp("#0e1838", "#28489a", "#3d6ad0", "#7fa6ff"),
		obiStripe: hex("#c8323e"),
		scarf: ramp("#16345c", "#3f7cc0", "#7cbcf4", "#cdeaff"),
		band: ramp("#16345c", "#3f7cc0", "#7cbcf4", "#cdeaff"),
		hair: ramp("#4c5262", "#aab2c4", "#dfe4ee", "#ffffff"),
		saya: ramp("#08080e", "#1a1a26", "#2a2a3a", "#484860"),
		gold: [hex("#f0c860"), hex("#b07a22")],
		geta: ramp("#36210f", "#6a4424", "#926238", "#b88a58"),
		eye: hex("#120c1c"),
		glow: hex("#c4ecff"),
		crack: [170, 225, 255],
		armour: false,
	},
	ember: {
		skin: ramp("#1a2616", "#4c6942", "#6d8d5c", "#93b07e"),
		top: ramp("#260606", "#6a1212", "#9c1e1e", "#d04838"),
		topBack: ramp("#1c0404", "#4e0e0e", "#741616", "#9c1e1e"),
		inner: ramp("#1a2616", "#4c6942", "#6d8d5c", "#93b07e"),
		hakama: ramp("#060608", "#18181f", "#272732", "#3c3c4c"),
		hem: [hex("#2a1010"), hex("#160808")],
		wisp: [255, 130, 50],
		obi: ramp("#1a1006", "#5a3a10", "#8a5c18", "#c89030"),
		obiStripe: hex("#e8b448"),
		scarf: ramp("#040406", "#121216", "#202028", "#36363f"),
		band: ramp("#360606", "#881010", "#c42020", "#ff5040"),
		hair: ramp("#040406", "#121216", "#202028", "#36363f"),
		saya: ramp("#08080e", "#1a1a26", "#2a2a3a", "#484860"),
		gold: [hex("#f0c860"), hex("#b07a22")],
		geta: ramp("#36210f", "#6a4424", "#926238", "#b88a58"),
		eye: hex("#1a0606"),
		glow: hex("#ffc060"),
		crack: [255, 160, 60],
		armour: true,
	},
};

// ---------------------------------------------------------------------------------------------------------------
// Canvas and shapes

class Canvas {
	constructor(w, h) { this.w = w; this.h = h; this.px = new Array(w * h).fill(null); }
	get(x, y) { return x < 0 || y < 0 || x >= this.w || y >= this.h ? null : this.px[y * this.w + x]; }
	set(x, y, c) { if (x >= 0 && y >= 0 && x < this.w && y < this.h) this.px[y * this.w + x] = c; }
}

class Mask {
	constructor(w, h) { this.w = w; this.h = h; this.m = new Uint8Array(w * h); }
	has(x, y) { return x >= 0 && y >= 0 && x < this.w && y < this.h && this.m[y * this.w + x] === 1; }
	add(x, y) { if (x >= 0 && y >= 0 && x < this.w && y < this.h) this.m[y * this.w + x] = 1; }
	del(x, y) { if (x >= 0 && y >= 0 && x < this.w && y < this.h) this.m[y * this.w + x] = 0; }
	*cells() { for (let y = 0; y < this.h; y++) for (let x = 0; x < this.w; x++) if (this.m[y * this.w + x]) yield [x, y]; }
}

function inPoly(px, py, pts) {
	let inside = false;
	for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
		const [xi, yi] = pts[i], [xj, yj] = pts[j];
		if ((yi > py) !== (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi)
			inside = !inside;
	}
	return inside;
}

function poly(mask, pts) {
	for (let y = 0; y < mask.h; y++)
		for (let x = 0; x < mask.w; x++)
			if (inPoly(x + 0.5, y + 0.5, pts))
				mask.add(x, y);
	return mask;
}

function distToSeg(px, py, [ax, ay], [bx, by]) {
	const dx = bx - ax, dy = by - ay, len = dx * dx + dy * dy;
	const t = len ? Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / len)) : 0;
	return Math.hypot(px - (ax + t * dx), py - (ay + t * dy));
}

// A line through `pts` whose half-width goes from w0 to w1.
function stroke(mask, pts, w0, w1) {
	for (let i = 0; i < pts.length - 1; i++) {
		const r = w0 + (w1 - w0) * (i / Math.max(1, pts.length - 2));
		for (let y = 0; y < mask.h; y++)
			for (let x = 0; x < mask.w; x++)
				if (distToSeg(x + 0.5, y + 0.5, pts[i], pts[i + 1]) <= r)
					mask.add(x, y);
	}
	return mask;
}

const rect = (mask, x0, y0, x1, y1) => { for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) mask.add(x, y); return mask; };

// Shades a part and paints it: edges get the outline colour, then a light rim on the top and front, a shadow band on
// the bottom and back, and the base colour inside. `soft` parts (thin cloth) get no outline where they touch something
// already painted, so they don't look cut out.
function paint(cv, mask, r, opts = {}) {
	const { soft = false, flat = false } = opts;
	for (const [x, y] of mask.cells()) {
		const edge = !mask.has(x - 1, y) || !mask.has(x + 1, y) || !mask.has(x, y - 1) || !mask.has(x, y + 1);
		let c;
		if (edge && !(soft && cv.get(x, y)))
			c = r[0];
		else if (flat)
			c = r[2];
		else if (!mask.has(x, y - 2) || !mask.has(x + 2, y) || !mask.has(x + 1, y - 1))
			c = r[3];
		else if (!mask.has(x, y + 2) || !mask.has(x - 2, y) || !mask.has(x - 1, y + 1))
			c = r[1];
		else
			c = r[2];
		cv.set(x, y, c);
	}
}

// Deterministic noise, so every run draws the same sprites.
function noise(i) { const s = Math.sin(i * 127.1 + 311.7) * 43758.5453; return s - Math.floor(s); }

// ---------------------------------------------------------------------------------------------------------------
// Poses. lean: top of the body forward (art px at the head); crouch: how far the hip drops; wind: where the cloth
// blows (x back is negative); wave: how much the cloth ripples; flare: hakama widening; arm: the far arm's pose.

const ANIMS = [
	{ name: "Idle", frames: 8, pose: t => ({ lean: 0, crouch: 0, wind: [-1, 0.25], wave: 0.55, flare: 0, arm: "hang", t }) },
	{ name: "Dash", frames: 4, pose: t => ({ lean: 3.5, crouch: 1, wind: [-1, 0.02], wave: 0.22, flare: 1, swept: 3, arm: "back", t }) },
	{ name: "Iaido", frames: 4, pose: t => ({ lean: 2, crouch: 4, wind: [-1, 0.15], wave: 0.3, flare: 2, arm: "hilt", t }) },
	{ name: "Cast", frames: 4, pose: t => ({ lean: -1, crouch: 0, wind: [-0.45, -1], wave: 0.45, flare: 2.5, arm: "raise", t }) },
	{ name: "Dying", frames: 4, pose: (t, i) => ({ lean: 1.5 + i * 0.5, crouch: 2 + i, wind: [-0.3, 1], wave: 0.25, flare: 1, arm: "hang", t, dying: (i + 1) / 4 }) },
];

// Moves a point of the standing pose into this pose: crouching lowers the upper body and squashes and widens the
// hakama; leaning shears everything above the ground.
function tf(p, [x, y]) {
	let y2, x2 = x;
	if (y <= HIP)
		y2 = y + p.crouch;
	else {
		const k = (y - HIP) / (GROUND - HIP);
		y2 = HIP + p.crouch + (y - HIP) * (GROUND - HIP - p.crouch) / (GROUND - HIP);
		x2 += (x - 16) * (p.flare * 0.09) * k;
	}
	x2 += p.lean * (GROUND - y2) / (GROUND - 2);
	return [x2, y2];
}

// A piece of cloth hanging from `from`: `n` points, each step blown by the wind and rippling with time.
function cloth(p, from, n, step, phase = 0, stiff = 1) {
	const pts = [from];
	const base = Math.atan2(p.wind[1], p.wind[0]);
	let [x, y] = from;
	for (let i = 1; i < n; i++) {
		const k = i / n;
		const a = base + Math.sin(p.t * Math.PI * 2 - i * 0.85 + phase) * p.wave * k * stiff + (1 - stiff) * 0.4 * k;
		x += Math.cos(a) * step;
		y += Math.sin(a) * step;
		pts.push([x, y]);
	}
	return pts;
}

// ---------------------------------------------------------------------------------------------------------------
// Body

function drawBody(look, p, frameIndex) {
	const L = LOOKS[look];
	const cv = new Canvas(W, H);
	const M = () => new Mask(W, H);
	const T = (x, y) => tf(p, [x, y]);
	const P = pts => pts.map(([x, y]) => T(x, y));
	const swept = p.swept || 0;

	// Scarf (ghost) or long hair (ember), behind everything.
	const neckBack = T(14, 10.2);
	if (!L.armour) {
		const a = cloth(p, neckBack, 12, 1.15, 0);
		paint(cv, stroke(M(), a, 1.25, 0.6), L.scarf, { soft: true });
		const b = cloth(p, T(14.5, 11), 9, 1.15, 1.7);
		paint(cv, stroke(M(), b, 0.9, 0.45), L.scarf, { soft: true });
	}
	else {
		for (let s = 0; s < 4; s++) {
			const strand = cloth(p, T(13.2 + s * 0.4, 3.2 + s * 1.6), 10 - s, 1.15, s * 1.3, 0.9);
			paint(cv, stroke(M(), strand, 1.15 - s * 0.12, 0.45), L.hair, { soft: true });
		}
	}

	// Headband tails, from the knot at the back of the head.
	const knot = T(12.6, 4);
	paint(cv, stroke(M(), cloth(p, knot, 7, 1.1, 0.6), 0.7, 0.45), L.band, { soft: true });
	paint(cv, stroke(M(), cloth(p, T(12.8, 4.6), 6, 1.1, 2.4), 0.6, 0.4), L.band, { soft: true });

	// Ghost: a white plume from the topknot, streaming back.
	if (!L.armour) {
		const plume = cloth(p, T(14, 1.2), 6, 1.05, 0.9, 0.7);
		paint(cv, stroke(M(), plume, 1.1, 0.5), L.hair, { soft: true });
	}

	// The far arm (darker), behind the body.
	const sway = Math.sin(p.t * Math.PI * 2) * 0.6 * p.wave;
	let sleeve, hand = null;
	if (p.arm === "hilt") {
		// Reaching across to hold the scabbard by its mouth, ready to draw.
		sleeve = P([[10.5, 11], [13.5, 10.8], [20, 16.4], [19.4, 18.4], [17, 18], [11, 14.5]]);
		hand = T(19.8, 17.6);
	}
	else if (p.arm === "raise") {
		sleeve = P([[10.4, 11.4], [13, 10.6], [12.4, 4.2], [10.6, 2.6], [8.6, 3.6], [9.2, 9]]);
		hand = T(11.6, 2.4);
	}
	else if (p.arm === "back") {
		sleeve = P([[11, 11], [13.5, 11], [12.5, 15], [5 - swept, 16.6], [5.4 - swept, 14.2], [9, 12.4]]);
	}
	else {
		sleeve = P([[10.6, 10.8], [13.6, 10.8], [14, 16.5], [12.6 - sway + p.wind[0] * 0.5, 20], [8.6 - sway + p.wind[0] * 1.2, 19.6], [9.2, 13]]);
	}
	paint(cv, poly(M(), sleeve), L.armour ? L.topBack : L.topBack);
	if (L.armour && p.arm === "hang") {
		// Shoulder plates (sode) on the far arm.
		for (let r = 0; r < 3; r++) {
			const row = P([[9.2, 12 + r * 2.2], [13.6, 12 + r * 2.2], [13.8, 13.8 + r * 2.2], [9.4, 13.8 + r * 2.2]]);
			paint(cv, poly(M(), row), L.topBack);
			const [gx, gy] = T(11.5, 13.2 + r * 2.2);
			cv.set(Math.round(gx), Math.round(gy), L.gold[1]);
		}
	}

	// Feet: bony (or green) feet on wooden geta.
	for (const fx of [12.2, 17.4]) {
		const f = T(fx + (fx > 16 ? 0.4 * p.flare : -0.4 * p.flare), GROUND - 0.1);
		const gx = Math.round(f[0]);
		const geta = rect(M(), gx, GROUND - 1, gx + 3, GROUND - 1);
		paint(cv, geta, L.geta, { flat: true });
		cv.set(gx, GROUND - 1, L.geta[0]);
		cv.set(gx + 3, GROUND - 1, L.geta[0]);
		const foot = rect(M(), gx + 1, GROUND - 2, gx + 3, GROUND - 2);
		paint(cv, foot, L.skin, { flat: true });
		cv.set(gx + 3, GROUND - 2, L.skin[1]);
	}

	// Hakama: wide, pleated, its hem ragged (ghost) and blown back.
	const hemY = GROUND - 2.2;
	const hak = P([[11.4, HIP - 0.4], [21.2, HIP - 0.4], [23.4 - swept * 0.6, hemY], [8.4 - swept, hemY]]);
	const hm = poly(M(), hak);
	// Rag the hem: each column loses 0-2 pixels from the bottom, changing with time.
	for (let x = 0; x < W; x++) {
		let bottom = -1;
		for (let y = H - 1; y >= 0; y--) if (hm.has(x, y)) { bottom = y; break; }
		if (bottom < HIP + 3)
			continue;
		const cut = Math.floor(noise(x * 3.1 + Math.floor(p.t * 8) * 0.7) * (L.armour ? 1.6 : 2.6));
		for (let k = 0; k < cut; k++)
			hm.del(x, bottom - k);
	}
	paint(cv, hm, L.hakama);
	// Pleats (folds running down and out) and the hem darkening into the ghostly colour.
	for (const px of [13.8, 16.3, 18.8]) {
		for (let y = HIP + 1; y < GROUND; y++) {
			const [x2, y2] = T(px + (px - 16.3) * (y - HIP) * 0.05, y);
			const xi = Math.round(x2 - 0.5), yi = Math.round(y2 - 0.5);
			if (hm.has(xi, yi) && hm.has(xi - 1, yi) && hm.has(xi + 1, yi))
				cv.set(xi, yi, L.hakama[1]);
		}
	}
	for (const [x, y] of hm.cells()) {
		const depth = (y - (GROUND - 5));
		if (depth >= 0 && cv.get(x, y) !== L.hakama[0])
			cv.set(x, y, depth >= 2 ? L.hem[1] : L.hem[0]);
	}
	// Wisps: a few see-through pixels trailing off the hem.
	for (let x = 0; x < W; x++) {
		let bottom = -1;
		for (let y = H - 1; y >= 0; y--) if (hm.has(x, y)) { bottom = y; break; }
		if (bottom < HIP + 3 || noise(x * 7.7 + Math.floor(p.t * 8)) < 0.55)
			continue;
		const len = 1 + Math.floor(noise(x * 1.3 + p.t * 5) * 2);
		for (let k = 1; k <= len; k++) {
			const wx = Math.round(x + p.wind[0] * k * 0.6), wy = bottom + k;
			if (!cv.get(wx, wy))
				cv.set(wx, wy, [...L.wisp, Math.round(170 / k)]);
		}
	}

	// Ember: lacquered tassets (kusazuri) over the hakama.
	if (L.armour) {
		for (const [a, b] of [[11.2, 14.2], [14.4, 17.6], [17.8, 21.4]]) {
			const flap = P([[a, HIP - 0.2], [b, HIP - 0.2], [b + 0.4, HIP + 4.6], [a - 0.4, HIP + 4.6]]);
			paint(cv, poly(M(), flap), L.top);
			for (const ly of [HIP + 1.4, HIP + 3.2]) {
				for (let lx = Math.ceil(a); lx < b; lx += 2) {
					const [gx, gy] = T(lx, ly);
					cv.set(Math.floor(gx), Math.floor(gy), L.gold[1]);
				}
			}
		}
	}

	// Upper body: kimono (ghost) or lacquered armour (ember).
	const torso = poly(M(), P([[10.8, 10.4], [20.2, 10.4], [21.8, 12.6], [21.4, 17.6], [11.4, 17.6], [10.4, 12.6]]));
	paint(cv, torso, L.top);
	if (!L.armour) {
		// The white inner kimono in a V at the collar, with the ribs showing through it.
		const v = poly(M(), P([[17.2, 10.2], [21, 10.2], [19.6, 15.4]]));
		for (const [x, y] of v.cells())
			cv.set(x, y, (y % 2 === 0) ? L.skin[2] : L.skin[1]);
		stroke(M(), P([[16.8, 10.4], [19.6, 15.6]]), 0.45, 0.45);
		const collarA = stroke(M(), P([[16.8, 10.2], [19.6, 15.6]]), 0.5, 0.5);
		const collarB = stroke(M(), P([[21.2, 10.4], [19.8, 15.4]]), 0.45, 0.45);
		for (const [x, y] of collarA.cells()) if (torso.has(x, y)) cv.set(x, y, L.inner[2]);
		for (const [x, y] of collarB.cells()) if (torso.has(x, y)) cv.set(x, y, L.inner[1]);
		// A fold down the front and the sleeve seam at the back.
		for (let y = 12; y <= 17; y++) {
			const [fx, fy] = T(15, y);
			if (torso.has(Math.floor(fx), Math.floor(fy))) cv.set(Math.floor(fx), Math.floor(fy), L.top[1]);
		}
		// The crest (mon) on the chest: a small pale circle.
		const [mx, my] = T(13.2, 12.6);
		cv.set(Math.floor(mx), Math.floor(my), L.inner[1]);
	}
	else {
		// Rows of plates laced with gold, a neck guard and a dark gap at the armpit.
		for (let r = 0; r < 3; r++) {
			const y = 12.2 + r * 1.9;
			for (let x = 11; x <= 21; x++) {
				const [gx, gy] = T(x + 0.5, y);
				const xi = Math.floor(gx), yi = Math.floor(gy);
				if (torso.has(xi, yi) && torso.has(xi, yi + 1) && torso.has(xi + 1, yi) && torso.has(xi - 1, yi)) {
					cv.set(xi, yi, L.top[0]);
					if (x % 3 === 1)
						cv.set(xi, yi + 1, r === 0 ? L.gold[0] : L.gold[1]);
				}
			}
		}
		const gorget = poly(M(), P([[14.6, 9.6], [20.4, 9.6], [20.8, 11.4], [14.4, 11.4]]));
		paint(cv, gorget, L.saya);
	}

	// Obi: a sash with a stripe, knotted at the back.
	const obi = poly(M(), P([[11.2, 16.6], [21.6, 16.6], [21.6, 19.1], [11.2, 19.1]]));
	paint(cv, obi, L.obi);
	for (let x = 12; x <= 21; x++) {
		const [sx, sy] = T(x + 0.5, 17.9);
		if (obi.has(Math.floor(sx), Math.floor(sy)) && x % 3 !== 0)
			cv.set(Math.floor(sx), Math.floor(sy), L.obiStripe);
	}
	const bow = poly(M(), P([[9.4, 16.8], [12, 17.4], [12, 18.6], [9.4, 19.6], [10.2, 18.2]]));
	paint(cv, bow, L.obi);

	// Scabbard (saya) at the hip, edge up, pointing back and down; gold at the mouth and the tip.
	const sayaFrom = T(20.4, 17.8), sayaTo = T(5.6 - swept * 0.5, 22.2 + (p.arm === "hilt" ? -1.2 : 0));
	paint(cv, stroke(M(), [sayaFrom, sayaTo], 0.75, 0.7), L.saya);
	const mouth = stroke(M(), [sayaFrom, [sayaFrom[0] - 1, sayaFrom[1] + 0.3]], 0.75, 0.75);
	for (const [x, y] of mouth.cells()) cv.set(x, y, L.gold[0]);
	const tip = stroke(M(), [sayaTo, [sayaTo[0] + 0.8, sayaTo[1] - 0.25]], 0.7, 0.7);
	for (const [x, y] of tip.cells()) cv.set(x, y, L.gold[1]);
	// The hilt of the short sword (wakizashi) beside it.
	const [wx, wy] = T(20.2, 16.4);
	cv.set(Math.floor(wx), Math.floor(wy), L.saya[2]);
	cv.set(Math.floor(wx) + 1, Math.floor(wy) - 1, L.saya[3]);
	cv.set(Math.floor(wx) + 1, Math.floor(wy), L.gold[1]);

	// Far hand.
	if (hand) {
		const hm2 = rect(M(), Math.floor(hand[0]) - 1, Math.floor(hand[1]) - 1, Math.floor(hand[0]), Math.floor(hand[1]));
		paint(cv, hm2, L.skin, { flat: true });
	}

	// Neck and head.
	paint(cv, poly(M(), P([[15.2, 8.4], [18, 8.4], [18, 10.8], [15.2, 10.8]])), L.skin, { flat: true });
	const skull = P([[12.8, 2.4], [15, 1.2], [19, 1.2], [20.8, 2.6], [21.6, 5.2], [21.8, 6.6], [20.8, 7.2], [20.6, 9.4], [16.6, 9.6], [15, 8.6], [13.2, 7.4], [12.4, 4.6]]);
	const head = poly(M(), skull);
	paint(cv, head, L.skin);
	const at = (x, y, c) => { const [a, b] = T(x + 0.5, y + 0.5); cv.set(Math.floor(a), Math.floor(b), c); };
	if (!L.armour) {
		// Skull: a deep eye socket, the nose hole, cheekbone, teeth and the jaw line.
		at(18, 4, L.eye); at(19, 4, L.eye); at(18, 5, L.eye); at(19, 5, L.eye); at(17, 5, L.skin[1]);
		at(19, 4, L.glow);
		at(21, 6, L.eye);
		at(17, 6, L.skin[3]); at(18, 6, L.skin[3]);
		for (let x = 16; x <= 20; x++) at(x, 7, x % 2 ? L.skin[0] : L.skin[3]);
		for (let x = 16; x <= 19; x++) at(x, 8, x % 2 ? L.skin[3] : L.skin[1]);
		at(15, 6, L.skin[1]); at(14, 5, L.skin[1]);
	}
	else {
		// Zombie samurai: sunken glowing eye, a scar, grey teeth bared, and a heavy brow under the hair.
		at(18, 4, L.eye); at(19, 4, L.eye); at(19, 5, L.eye); at(18, 5, L.skin[0]);
		at(19, 4, L.glow);
		at(20, 6, L.skin[0]);
		for (let x = 17; x <= 20; x++) at(x, 7, x % 2 ? L.skin[0] : hex("#b8b49a"));
		at(16, 8, L.skin[1]); at(17, 8, L.skin[1]);
		at(16, 3, L.top[2]); at(16, 4, L.top[1]); at(15, 5, L.top[2]);
		// Hair on the top of the head and a topknot.
		const cap = poly(M(), P([[12.6, 2.2], [15, 0.9], [19.4, 1], [21, 2.4], [19.6, 3.2], [14, 4.6], [12.4, 5]]));
		paint(cv, cap, L.hair);
		const [tx, ty] = T(14.5, 1.2);
		paint(cv, rect(M(), Math.floor(tx) - 1, Math.floor(ty) - 1, Math.floor(tx), Math.floor(ty)), L.hair, { flat: true });
		cv.set(Math.floor(tx), Math.floor(ty) - 1, L.band[2]);
	}
	// Headband across the forehead.
	for (let x = 13; x <= 21; x++) {
		const [bx, by] = T(x + 0.5, 3.5);
		if (head.has(Math.floor(bx), Math.floor(by)))
			cv.set(Math.floor(bx), Math.floor(by), x === 21 ? L.band[3] : x % 4 === 0 ? L.band[1] : L.band[2]);
	}
	paint(cv, rect(M(), Math.floor(knot[0]) - 1, Math.floor(knot[1]) - 1, Math.floor(knot[0]), Math.floor(knot[1])), L.band, { flat: true });

	// Dying: glowing cracks spreading over him, then pieces breaking off.
	if (p.dying) {
		const cracks = [[[17, 1.6], [16, 3.6], [17, 5], [15.6, 6.6]], [[13.4, 11], [15, 13], [14, 15], [15.6, 17]], [[19.6, 11.6], [18, 13.4], [19, 15.4]], [[13, 20], [14.4, 23], [13.2, 26]], [[19, 20.4], [18.4, 24], [19.6, 27]]];
		const shown = Math.ceil(cracks.length * p.dying);
		for (let i = 0; i < shown; i++) {
			const cm = stroke(M(), P(cracks[i]), 0.35, 0.35);
			for (const [x, y] of cm.cells())
				if (cv.get(x, y)) cv.set(x, y, [...L.crack, 255]);
		}
		for (let y = 0; y < H; y++)
			for (let x = 0; x < W; x++)
				if (cv.get(x, y) && noise(x * 13.7 + y * 7.1) < (p.dying - 0.4) * 0.5 * (y / H))
					cv.set(x, y, null);
	}

	const shoulder = T(18.2, 11.8);
	const eye = T(19.5, 4.6);
	return { cv, shoulder, eye };
}

// ---------------------------------------------------------------------------------------------------------------
// Sword arm: points right from the shoulder (at ARM_ORIGIN). A wide sleeve, the hand around the hilt, a gold guard and
// a slightly curved blade with a bright edge and a pale temper line.

const AW = 38, AH = 11;
const ARM_ORIGIN = [1, 4];

function drawArm(look) {
	const L = LOOKS[look];
	const cv = new Canvas(AW, AH);
	const M = () => new Mask(AW, AH);
	// Blade: dark spine, steel, a pale temper line, the bright edge below; it curves up a little towards the point.
	const bladeStart = 16, bladeEnd = 37;
	for (let x = bladeStart; x <= bladeEnd; x++) {
		const k = (x - bladeStart) / (bladeEnd - bladeStart);
		const lift = Math.round(k * k * 1.6);
		const yTop = 3 - lift, yEdge = 5 - lift;
		if (x === bladeEnd) { cv.set(x, yTop + 1, hex("#f4fbff")); cv.set(x - 1, yTop, hex("#283040")); continue; }
		if (x === bladeEnd - 1) { cv.set(x, yTop, hex("#8e9cb4")); cv.set(x, yTop + 1, hex("#f4fbff")); cv.set(x, yTop + 2, hex("#3a4456")); continue; }
		cv.set(x, yTop - 1, hex("#283040"));
		cv.set(x, yTop, hex("#8e9cb4"));
		cv.set(x, yTop + 1, (x + 1) % 4 < 2 ? hex("#d6ecff") : hex("#b4c4dc"));
		cv.set(x, yEdge, hex("#f4fbff"));
		cv.set(x, yEdge + 1, hex("#3a4456"));
	}
	// Habaki (gold collar) and the guard (tsuba), dark iron rimmed in gold.
	for (let y = 2; y <= 6; y++) cv.set(15, y, y === 2 || y === 6 ? L.gold[1] : L.gold[0]);
	for (let y = 0; y <= 8; y++) cv.set(14, y, y === 0 || y === 8 ? L.gold[1] : y === 1 || y === 7 ? hex("#4a4a5a") : hex("#22222c"));
	// Hilt (tsuka): the diamond-pattern wrap, a gold pommel cap.
	for (let x = 8; x <= 13; x++)
		for (let y = 3; y <= 5; y++)
			cv.set(x, y, y === 4 ? ((x % 2) ? hex("#e8e4d4") : hex("#1a1a24")) : ((x + y) % 2 ? hex("#1a1a24") : hex("#3a3a52")));
	cv.set(13, 4, L.gold[0]);
	// Hand around the hilt, by the sleeve.
	const hand = rect(M(), 6, 2, 8, 6);
	paint(cv, hand, L.skin);
	cv.set(7, 3, L.skin[3]); cv.set(7, 4, L.skin[1]); cv.set(7, 5, L.skin[1]);
	// Sleeve from the shoulder to the wrist, hanging wide below the arm.
	const sleeve = poly(M(), [[0, 1.6], [6.4, 2.2], [6.6, 6.6], [5.6, 10.4], [2, 10.6], [0, 7]]);
	paint(cv, sleeve, L.top);
	if (L.armour) {
		for (const y of [4, 7]) for (let x = 1; x <= 5; x++) if (sleeve.has(x, y) && sleeve.has(x + 1, y)) cv.set(x, y, x % 2 ? L.gold[1] : L.top[0]);
	}
	else {
		for (let y = 3; y <= 9; y++) if (sleeve.has(3, y) && sleeve.has(3, y + 1)) cv.set(3, y, L.top[1]);
		cv.set(5, 3, L.inner[1]); cv.set(5, 4, L.inner[2]); cv.set(5, 5, L.inner[1]);
	}
	const tipX = bladeEnd + 1, tipY = 4 - 1.6;
	return { cv, tip: [tipX, tipY] };
}

// ---------------------------------------------------------------------------------------------------------------
// Output

function writePng(file, w, h, rgba) {
	const raw = Buffer.alloc((w * 4 + 1) * h);
	for (let y = 0; y < h; y++) {
		raw[y * (w * 4 + 1)] = 0;
		rgba.copy(raw, y * (w * 4 + 1) + 1, y * w * 4, (y + 1) * w * 4);
	}
	const chunk = (type, data) => {
		const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
		const td = Buffer.concat([Buffer.from(type), data]);
		const crc = Buffer.alloc(4); crc.writeUInt32BE(zlib.crc32(td));
		return Buffer.concat([len, td, crc]);
	};
	const ihdr = Buffer.alloc(13);
	ihdr.writeUInt32BE(w, 0); ihdr.writeUInt32BE(h, 4); ihdr[8] = 8; ihdr[9] = 6;
	fs.writeFileSync(file, Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", ihdr), chunk("IDAT", zlib.deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]));
}

// Copies an art-pixel canvas into an RGBA buffer, `scale` screen pixels per art pixel.
function blit(buf, bw, cv, ox, oy, scale) {
	for (let y = 0; y < cv.h; y++)
		for (let x = 0; x < cv.w; x++) {
			const c = cv.get(x, y);
			if (!c) continue;
			for (let dy = 0; dy < scale; dy++)
				for (let dx = 0; dx < scale; dx++) {
					const i = ((oy + y * scale + dy) * bw + ox + x * scale + dx) * 4;
					buf[i] = c[0]; buf[i + 1] = c[1]; buf[i + 2] = c[2]; buf[i + 3] = c[3];
				}
		}
}

const FRAMES = ANIMS.reduce((n, a) => n + a.frames, 0);
const FW = W * S, FH = H * S;
const anchors = [];
const sheets = {};
for (const look of Object.keys(LOOKS)) {
	const buf = Buffer.alloc(FW * FH * FRAMES * 4);
	let f = 0;
	const frames = [];
	for (const anim of ANIMS) {
		for (let i = 0; i < anim.frames; i++, f++) {
			const pose = anim.pose(i / anim.frames, i);
			const { cv, shoulder, eye } = drawBody(look, pose, f);
			blit(buf, FW, cv, 0, f * FH, S);
			frames.push(cv);
			if (look === "ghost")
				anchors.push({ shoulder, eye });
		}
	}
	sheets[look] = frames;
	writePng(path.join(OUT, look === "ghost" ? "BoneRonin.png" : "BoneRonin_Ember.png"), FW, FH * FRAMES, buf);

	const arm = drawArm(look);
	const abuf = Buffer.alloc(AW * S * AH * S * 4);
	blit(abuf, AW * S, arm.cv, 0, 0, S);
	writePng(path.join(OUT, look === "ghost" ? "BoneRonin_Arm.png" : "BoneRonin_Ember_Arm.png"), AW * S, AH * S, abuf);
	if (look === "ghost")
		sheets.armTip = arm.tip;
}

// Map icon: the ghost's head from the first idle frame, 18 x 18 art pixels.
{
	const src = sheets.ghost[0];
	const icon = new Canvas(18, 18);
	for (let y = 0; y < 18; y++) for (let x = 0; x < 18; x++) icon.set(x, y, src.get(x + 6, y));
	const buf = Buffer.alloc(36 * 36 * 4);
	blit(buf, 36, icon, 0, 0, 2);
	writePng(path.join(OUT, "BoneRonin_Head_Boss.png"), 36, 36, buf);
}

// Preview: both looks side by side, every frame enlarged, on a grey background.
{
	fs.mkdirSync(GEN, { recursive: true });
	const P = 3, cols = 8, rows = Math.ceil(FRAMES / cols) * 2;
	const pw = cols * FW * P, ph = rows * FH * P;
	const buf = Buffer.alloc(pw * ph * 4);
	for (let i = 0; i < pw * ph; i++) { buf[i * 4] = 70; buf[i * 4 + 1] = 78; buf[i * 4 + 2] = 92; buf[i * 4 + 3] = 255; }
	["ghost", "ember"].forEach((look, li) => {
		sheets[look].forEach((cv, i) => {
			const col = i % cols, row = Math.floor(i / cols) + li * Math.ceil(FRAMES / cols);
			blit(buf, pw, cv, col * FW * P, row * FH * P, S * P);
		});
	});
	writePng(path.join(GEN, "ronin_preview.png"), pw, ph, buf);

	// Close-up: one frame of each animation, both looks, plus the arms.
	const Z = 5, picks = [0, 8, 12, 16, 22];
	const zw = picks.length * FW * Z, zh = 2 * FH * Z + AH * S * Z * 2;
	const zb = Buffer.alloc(zw * zh * 4);
	for (let i = 0; i < zw * zh; i++) { zb[i * 4] = 70; zb[i * 4 + 1] = 78; zb[i * 4 + 2] = 92; zb[i * 4 + 3] = 255; }
	["ghost", "ember"].forEach((look, li) => {
		picks.forEach((f, i) => blit(zb, zw, sheets[look][f], i * FW * Z, li * FH * Z, S * Z));
		blit(zb, zw, drawArm(look).cv, li * AW * S * Z, 2 * FH * Z, S * Z);
	});
	writePng(path.join(GEN, "ronin_zoom.png"), zw, zh, zb);
}

// RoninFrames.cs: the anchors in screen pixels from the frame centre, facing right.
{
	const v = ([x, y]) => `new(${(x * S - FW / 2).toFixed(1)}f, ${(y * S - FH / 2).toFixed(1)}f)`;
	let start = 0;
	const ranges = ANIMS.map(a => { const r = `\t\tpublic const int ${a.name}Start = ${start}, ${a.name}Frames = ${a.frames};`; start += a.frames; return r; });
	const cs = `// Generated by tools/sprites/ronin.js: don't edit by hand, change the script and run it again.
using Microsoft.Xna.Framework;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	public static class RoninFrames
	{
		public const int Count = ${FRAMES};
		public const int Width = ${FW}, Height = ${FH};
		// Feet bottom, in screen pixels below the frame centre.
		public const float FeetY = ${(GROUND * S - FH / 2).toFixed(1)}f;
${ranges.join("\n")}

		// Per frame, from the frame centre, facing right.
		public static readonly Vector2[] Shoulder = {
			${anchors.map(a => v(a.shoulder)).join(", ")},
		};
		public static readonly Vector2[] Eye = {
			${anchors.map(a => v(a.eye)).join(", ")},
		};

		// The sword arm texture: the shoulder (rotation origin) and the length to the blade's point.
		public static readonly Vector2 ArmOrigin = new(${ARM_ORIGIN[0] * S + 1}, ${ARM_ORIGIN[1] * S + 1});
		public const float ArmLength = ${((sheets.armTip[0] - ARM_ORIGIN[0]) * S).toFixed(1)}f;
	}
}
`;
	fs.writeFileSync(path.join(OUT, "RoninFrames.cs"), cs.replace(/\n/g, "\r\n"));
}
console.log(`${FRAMES} frames written`);

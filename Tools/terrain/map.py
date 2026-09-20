"""Tracing-paper route map (калька) of the playable area, in the manner of the group's copies from forestry maps.

Stylisation: the look of the group's own kalki is not documented; this imitates a period hand copy of a лесхоз sheet —
green forest wash with pencil hatching, ink contours every 20 m with the 100 m lines numbered by hand, blue ink streams,
stone-field stipple on the bare ridges, red pencil route, handwritten labels in Caveat (OFL).

The sheet carries the places of the night — tent, cedar, floor of branches, the ravine, the three on the slope — with
their names. That is a game's licence, not a document: in February 1959 nobody could have drawn them. The player needs
to know where he is going, and the height is still written «1079», as on the sheets printed before the 1963 resurvey.

Inputs are the packaged world data in the repository, so the sheet rebuilds from a fresh clone with no scratch files:
  Assets/Data/World/height_2049.r16   bare-earth DEM, uint16 over [496, 1096] m, row 0 = south
  Assets/Data/World/canopy_2049.r8    canopy height, metres, row 0 = south
  Assets/Data/World/rock_1025.r8      rock/roughness mask, row 0 = south
  Assets/Data/World/streams_1025.r8   log-scaled drainage area, row 0 = south

Output: Assets/Art/Maps/kalka_1959.png — 2048², north up, covering exactly the 4096 m world square, so the HUD maps
world position to uv linearly (row 0 of the PNG is the northern edge, which Unity loads as v = 1).

Run: python3 Tools/terrain/map.py [repo-root]        (needs numpy, scipy, scikit-image, pillow)
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy import ndimage as nd
from skimage import measure

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
DATA = ROOT / 'Assets' / 'Data' / 'World'
FONTS = ROOT / 'Assets' / 'Art' / 'Fonts'
OUT = ROOT / 'Assets' / 'Art' / 'Maps' / 'kalka_1959.png'

S = 2048                 # sheet side, pixels
HALF = 2048.0            # half the world square, metres
PX = S / (2 * HALF)      # pixels per metre on the sheet
F = 2                    # the ink layer is drawn at 2× and shrunk: that is where the antialiasing comes from
HEIGHT_MIN, HEIGHT_MAX = 496.0, 1096.0
rng = np.random.default_rng(1959)

# ── geodesy (same frame as Core/WorldData.cs: x east, z north) ───────────────────────────────────────────
A, FL = 6378137.0, 1 / 298.257223563
E2 = FL * (2 - FL)
D = math.pi / 180
LAT0, LON0 = 61.756, 59.4425
M0 = A * (1 - E2) / (1 - E2 * math.sin(LAT0 * D) ** 2) ** 1.5


def _nr(lat):
    return A / math.sqrt(1 - E2 * math.sin(lat * D) ** 2)


def ll2xz(lat, lon):
    """Degrees → world metres (x east, z north)."""
    return ((lon - LON0) * D * _nr(lat) * math.cos(lat * D), (lat - LAT0) * D * M0)


def dms(d, m, s):
    return d + m / 60 + s / 3600


# ── the sheet: world metres → pixels ─────────────────────────────────────────────────────────────────────
def cx(x, scale=1):
    return (x + HALF) * PX * scale


def cy(z, scale=1):
    """Row 0 is the northern edge, so z grows upwards on the sheet."""
    return (HALF - z) * PX * scale


def pt(x, z, scale=1):
    return (cx(x, scale), cy(z, scale))


def P(lat, lon, scale=1):
    return pt(*ll2xz(lat, lon), scale)


# ── load the packaged world ──────────────────────────────────────────────────────────────────────────────
def grid(name, res, dtype):
    raw = np.fromfile(DATA / name, dtype)
    if raw.size != res * res:
        raise SystemExit(f'{name}: expected {res}² values, got {raw.size}')
    return raw.reshape(res, res)[::-1]     # packaged row 0 = south; we want row 0 = north


def to_sheet(a, order=1):
    return nd.zoom(a.astype(np.float32), S / a.shape[0], order=order)


q = grid('height_2049.r16', 2049, '<u2')
dem = to_sheet(nd.gaussian_filter(HEIGHT_MIN + q * ((HEIGHT_MAX - HEIGHT_MIN) / 65535.0), 1.5))
chm = to_sheet(grid('canopy_2049.r8', 2049, np.uint8), order=0)
rock = to_sheet(grid('rock_1025.r8', 1025, np.uint8)) / 255.0
flow = to_sheet(grid('streams_1025.r8', 1025, np.uint8)) / 255.0

gz, gx = np.gradient(dem, 2 * HALF / S)
gz = -gz                                             # row index grows southward
slope = np.degrees(np.arctan(np.hypot(gx, gz)))
forest = nd.gaussian_filter((chm > 3).astype(np.float32), 5)

# ── paper ────────────────────────────────────────────────────────────────────────────────────────────────
yy, xx = np.mgrid[0:S, 0:S]
img = np.ones((S, S, 3)) * np.array([236, 238, 232]) / 255
fib = nd.gaussian_filter(rng.normal(0, 1, (S, S)), (0.6, 5)) * .025 + nd.gaussian_filter(rng.normal(0, 1, (S, S)), 40) * .6
img *= (1 + fib)[..., None]
for c, ax in [(S * .5, xx), (S * .5, yy)]:
    crease = np.exp(-((ax - c) / 3.0) ** 2) * .07 + np.exp(-((ax - c - 7) / 10.0) ** 2) * .03
    img *= (1 - crease)[..., None]
edge = np.minimum.reduce([xx, yy, S - 1 - xx, S - 1 - yy]).astype(float)
img *= (1 - .10 * np.exp(-edge / 60))[..., None]


def wash(mask, color, alpha=1.0):
    """Lay a colour over the sheet with per-pixel coverage — the flat washes and the pencil tone."""
    global img
    a = np.clip(mask, 0, 1)[..., None] * alpha
    img = img * (1 - a) + np.array(color)[None, None, :] / 255 * a


# ── forest: the green wash a forestry sheet is made for, then pencil hatching over it ────────────────────
# The tree line is the one thing on this map that decides the night: below it there is firewood and shelter,
# above it there is nothing. On the old sheet it was a flat green; the hatching is the copier's own hand.
wood = np.clip((forest - .30) / .28, 0, 1)
wash(wood, (146, 169, 136), .34)
hatch = ((xx + yy) % 14 < 1.6) & (wood > .45)
hatch = hatch & (nd.gaussian_filter(rng.random((S, S)), 1.2) > .47)
wash(hatch.astype(float) * .55, (80, 99, 87), .58)
# the edge of the wood, scalloped: a double dotted rule, the way a лесхоз sheet closes a stand
solid = wood > .45
solid = nd.binary_closing(solid, nd.generate_binary_structure(2, 2), iterations=6)
solid = nd.binary_opening(solid, nd.generate_binary_structure(2, 2), iterations=5)
_lab, _n = nd.label(solid)
if _n:
    _keep = np.flatnonzero(np.bincount(_lab.ravel())[1:] >= 1200) + 1
    solid = np.isin(_lab, _keep)
rim = solid ^ nd.binary_erosion(solid, iterations=2)
wash((rim & (((xx // 9) + (yy // 9)) % 2 == 0)).astype(float), (74, 94, 80), .75)
rim2 = nd.binary_dilation(solid, iterations=5) ^ nd.binary_dilation(solid, iterations=3)
wash((rim2 & (((xx // 13) + (yy // 13)) % 3 == 0)).astype(float) * .6, (86, 104, 92), .45)

# Where to write «граница леса»: the upper edge of the wood in the south-west quarter, found in the data and
# fitted with a straight line, so the note lies along the boundary instead of somewhere in the middle of it.
_cols, _rows = [], []
for _c in range(460, 1180, 20):
    _r = np.flatnonzero(solid[:, _c])
    if _r.size and 1150 < _r[0] < 1900:
        _cols.append(_c)
        _rows.append(_r[0])
if len(_cols) > 6:
    _k, _b = np.polyfit(_cols, _rows, 1)
    _mid = int(np.median(_cols))
    TREELINE = (_mid, _k * _mid + _b - 26)
    TREELINE_ANGLE = -math.degrees(math.atan(_k))
else:
    TREELINE, TREELINE_ANGLE = pt(-250, -1180), -14

# ── bare ground: stone fields (курумы) as a stipple of angular grains ────────────────────────────────────
stones = np.clip((rock - .52) / .34, 0, 1) * (1 - np.clip(forest * 3.0, 0, 1))
grain = (rng.random((S, S)) < stones * .022)
grain = nd.binary_dilation(grain, np.ones((2, 2), bool))
wash(grain.astype(float), (96, 88, 78), .50)

# ── pencil shading: the draughtsman rubbed the steep sides so the sheet reads uphill ─────────────────────
# Kept faint on purpose — a hand copy has no hypsometry, and anything stronger stops looking like pencil.
sun = np.array([-.55, .55, .63])
sun = sun / np.linalg.norm(sun)
nrm = np.stack([-gx, -gz, np.ones_like(gx)], -1)
nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
lit = np.clip((nrm @ sun), 0, 1)
wash(nd.gaussian_filter(np.clip((.66 - lit) * 1.7, 0, 1) * np.clip((slope - 3) / 18, 0, 1), 3), (112, 106, 116), .35)
wash(nd.gaussian_filter(np.clip((slope - 20) / 16, 0, 1), 2), (100, 92, 92), .19)

# ── streams: blue ink along the drainage, width by catchment ─────────────────────────────────────────────
lw = np.zeros((S, S))
for lo, rad in [(.37, 0), (.63, 1), (.89, 2)]:
    b = flow >= lo
    if rad:
        b = nd.binary_dilation(b, iterations=rad)
    lw = np.maximum(lw, b.astype(float))
wash(np.clip(nd.gaussian_filter(lw, .9) * 1.8, 0, 1), (40, 72, 146), .90)

base = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)).convert('RGBA')

# ── ink layer, drawn at 2× ───────────────────────────────────────────────────────────────────────────────
INK = (38, 34, 48, 235)
BROWN = (122, 84, 52, 235)
RED = (162, 32, 28, 242)
BLUE = (40, 70, 140, 235)
PAPER = (236, 238, 232, 255)

layer = Image.new('RGBA', (S * F, S * F), (0, 0, 0, 0))
dr = ImageDraw.Draw(layer, 'RGBA')
font_r = ImageFont.truetype(str(FONTS / 'Caveat-400.ttf'), 40 * F)
FONT_R, FONT_B = str(FONTS / 'Caveat-400.ttf'), str(FONTS / 'Caveat-700.ttf')
_cache = {}


def face(size, bold=False):
    key = (size, bold)
    if key not in _cache:
        _cache[key] = ImageFont.truetype(FONT_B if bold else FONT_R, int(size * F))
    return _cache[key]


def label(text, at, size=40, color=INK, angle=0, bold=False, halo=0):
    """A handwritten label centred on `at` (sheet pixels, 1×), optionally on a soft paper halo."""
    fnt = face(size, bold)
    box = dr.textbbox((0, 0), text, font=fnt)
    w, h = box[2] - box[0] + 24 * F, box[3] - box[1] + 30 * F
    tile = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    td = ImageDraw.Draw(tile)
    if halo:
        td.text((w / 2, h / 2), text, font=fnt, fill=PAPER, anchor='mm',
                stroke_width=int(halo * F), stroke_fill=PAPER)
    td.text((w / 2, h / 2), text, font=fnt, fill=color, anchor='mm')
    if angle:
        tile = tile.rotate(angle, resample=Image.BICUBIC, expand=True)
    layer.alpha_composite(tile, (int(at[0] * F - tile.width / 2), int(at[1] * F - tile.height / 2)))


def wobble(poly, amp=1.1, step=3):
    """Hand tremor: a smooth random offset along the line, so the pen never draws a machine's curve."""
    p = np.asarray(poly, float)[::step]
    if len(p) < 3:
        return [tuple(v) for v in p]
    d = np.diff(p, axis=0)
    tang = np.vstack([d[:1], (d[:-1] + d[1:]) / 2, d[-1:]])
    n = np.stack([-tang[:, 1], tang[:, 0]], -1)
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    off = nd.gaussian_filter1d(rng.normal(0, 1, len(p)), 9, mode='nearest') * amp * 3.0
    return [tuple(v) for v in p + n * off[:, None]]


def contours(level):
    """Contour polylines of the DEM at `level`, in sheet pixels at 1×."""
    out = []
    for c in measure.find_contours(dem, level):
        if len(c) < 22:
            continue
        out.append(np.stack([c[:, 1] * (S / dem.shape[0]), c[:, 0] * (S / dem.shape[0])], -1))
    return out


# contours every 20 m, index lines every 100 m; the numbers are written into a gap in the index line
NUMBERED = {}
for lvl in range(500, 1101, 20):
    index = lvl % 100 == 0
    width = int((2.0 if index else 1.05) * F)
    colour = (BROWN[0], BROWN[1], BROWN[2], 250 if index else 198)
    for poly in contours(float(lvl)):
        dr.line([(x * F, y * F) for x, y in wobble(poly, .9 if index else .7)], fill=colour, width=width, joint='curve')
        if index and len(poly) > 400:
            NUMBERED.setdefault(lvl, []).append(poly)

# ── height numbers on the index contours ─────────────────────────────────────────────────────────────────
# A number goes where its contour runs across the sheet, not up it: past about 50° the digits would have to be
# read bottom-to-top, and «800» turns into «008». Where the line is too steep the walk simply carries on to the
# next place along it. The places of the night are seeded into the same list, so no height prints over them.
placed = [(cx(x), cy(z)) for x, z in [
    ll2xz(dms(61, 45, 30.82), dms(59, 25, 45.97)), ll2xz(61.76217, 59.44458), ll2xz(61.76266, 59.44727),
    ll2xz(61.76351, 59.45018), ll2xz(dms(61, 45, 53.20), dms(59, 27, 17.80)),
    ll2xz(dms(61, 45, 53.93), dms(59, 27, 14.64)), ll2xz(dms(61, 44, 48.1), dms(59, 26, 58.0)),
]]


def far_enough(p, gap=360):
    return all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 > gap * gap for q in placed)


for lvl, polys in sorted(NUMBERED.items()):
    for poly in polys:
        n = len(poly)
        for i in range(90, n - 90, 60):
            p = poly[i]
            if not (120 < p[0] < S - 120 and 120 < p[1] < S - 120) or not far_enough(p):
                continue
            a, b = poly[max(0, i - 26)], poly[min(n - 1, i + 26)]
            ang = math.degrees(math.atan2(-(b[1] - a[1]), b[0] - a[0]))
            if ang > 90:
                ang -= 180
            elif ang < -90:
                ang += 180
            if abs(ang) > 50:
                continue
            label(str(lvl), tuple(p), 40, BROWN, angle=ang, bold=True, halo=3)
            placed.append(tuple(p))

# ── the group's line, in red pencil ──────────────────────────────────────────────────────────────────────
# 1 Feb, labaz → the night camp on the slope: the least-cost ski line on the DEM (Tools/terrain/route.py),
# the same array as Core/WorldData.AscentRoute. The group's actual track is unknown; the diary says ~2 km.
ASCENT = [
    (368, -1036), (348, -1016), (332, -1008), (312, -988), (292, -976), (272, -956), (252, -936), (232, -916),
    (212, -896), (204, -876), (196, -856), (176, -836), (156, -816), (144, -796), (140, -776), (128, -756),
    (120, -736), (120, -716), (112, -696), (108, -676), (100, -656), (92, -636), (88, -616), (84, -596),
    (80, -576), (60, -556), (40, -536), (40, -516), (36, -496), (16, -476), (-4, -456), (-24, -436),
    (-28, -416), (-48, -396), (-52, -376), (-72, -356), (-84, -336), (-92, -316), (-108, -296), (-128, -276),
    (-148, -256), (-168, -236), (-188, -216), (-208, -196), (-228, -176), (-248, -156), (-268, -136),
    (-288, -116), (-308, -96), (-328, -76), (-348, -56), (-368, -36), (-388, -16), (-408, 4), (-428, 24),
    (-448, 44), (-468, 64), (-488, 84), (-508, 104), (-528, 124), (-548, 144), (-568, 164), (-588, 184),
    (-608, 204), (-628, 224), (-648, 244), (-668, 264), (-688, 284),
]


def valley_line(x_from, x_to, z_lo, z_hi):
    """Bottom of the Auspiya valley: the lowest ground in a southern band, column by column — the line the
    group skied up on 28–31 January."""
    out = []
    for x in range(int(x_from), int(x_to), -40 if x_to < x_from else 40):
        c = int(round((x + HALF) * S / (2 * HALF)))
        r0, r1 = int((HALF - z_hi) * S / (2 * HALF)), int((HALF - z_lo) * S / (2 * HALF))
        col = dem[r0:r1, max(0, min(S - 1, c))]
        r = r0 + int(np.argmin(col))
        out.append((x, HALF - r * (2 * HALF / S)))
    return out


def dashes(points, colour, width, on=22, off=22, scale=1):
    """Dashed pencil line: the dash pattern runs along the whole line, not restarted at every vertex."""
    walked = 0.0
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        length = math.hypot(x1 - x0, y1 - y0)
        n = max(1, int(length / 4))
        for i in range(n):
            if ((walked + length * i / n) // on) % 2 == 0:
                dr.line([(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n),
                         (x0 + (x1 - x0) * (i + 1) / n, y0 + (y1 - y0) * (i + 1) / n)],
                        fill=colour, width=int(width * F))
        walked += length


approach = valley_line(2040, 560, -2040, -1500)
approach = [(x, z) for x, z in zip([p[0] for p in approach],
            nd.gaussian_filter1d([p[1] for p in approach], 2.0, mode='nearest'))]
labaz_xz = ll2xz(dms(61, 44, 48.1), dms(59, 26, 58.0))
approach = [pt(x, z, F) for x, z in approach] + [pt(*labaz_xz, F)]
dashes(approach, RED, 5)
dashes([pt(x, z, F) for x, z in ASCENT], RED, 5)

# ── the places of the night ──────────────────────────────────────────────────────────────────────────────
# Coordinates are the ones in Core/WorldData.cs, with their sources; the marks and names are the game's, not
# a 1959 document's. Cedar and ravine stand 70 m apart, so their names are set off on leader lines.
PLACES = [
    ('tent',   ll2xz(dms(61, 45, 30.82), dms(59, 25, 45.97)), 'палатка',        46, 'tent'),
    ('kolm',   ll2xz(61.76217, 59.44458),                     'Колмогорова',    36, 'dot'),
    ('slob',   ll2xz(61.76266, 59.44727),                     'Слободин',       36, 'dot'),
    ('dyat',   ll2xz(61.76351, 59.45018),                     'Дятлов',         36, 'dot'),
    ('cedar',  ll2xz(dms(61, 45, 53.20), dms(59, 27, 17.80)), 'кедр',           42, 'tree'),
    ('ravine', ll2xz(dms(61, 45, 53.93), dms(59, 27, 14.64)), 'овраг · настил', 38, 'cross'),
]


def place_mark(kind, x, z):
    mx, my = pt(x, z, F)
    r = 9 * F
    if kind == 'tent':
        dr.polygon([(mx - 13 * F, my + 8 * F), (mx, my - 11 * F), (mx + 13 * F, my + 8 * F)], outline=RED, width=4 * F)
    elif kind == 'tree':
        dr.line([(mx, my + 10 * F), (mx, my - 2 * F)], fill=RED, width=3 * F)
        dr.polygon([(mx - 10 * F, my - 1 * F), (mx, my - 17 * F), (mx + 10 * F, my - 1 * F)], outline=RED, width=3 * F)
    elif kind == 'cross':
        dr.line([(mx - r, my - r), (mx + r, my + r)], fill=RED, width=4 * F)
        dr.line([(mx - r, my + r), (mx + r, my - r)], fill=RED, width=4 * F)
    else:
        dr.ellipse([mx - 7 * F, my - 7 * F, mx + 7 * F, my + 7 * F], outline=RED, width=4 * F)


# Names are set by hand on a printed sheet and by search here: cedar, ravine and the floor of branches stand
# seventy metres apart, which is thirty-five pixels, so a fixed offset per place would always collide with
# something. Each name takes the nearest free spot on a ring around its mark, with a leader when it stands off.
taken = []


def free(box):
    if box[0] < 60 or box[1] < 60 or box[2] > S - 60 or box[3] > S - 60:
        return False
    return all(not (box[0] < t[2] and t[0] < box[2] and box[1] < t[3] and t[1] < box[3]) for t in taken)


RING = [(1, 0), (.85, -.5), (.85, .5), (0, -1), (0, 1), (-.85, -.5), (-.85, .5), (-1, 0)]


def name_place(text, x, z, size, mark=14):
    fnt = face(size)
    box = dr.textbbox((0, 0), text, font=fnt)
    w, h = (box[2] - box[0]) / F, (box[3] - box[1]) / F
    ax, ay = cx(x), cy(z)
    for rad in (mark + 16, mark + 44, mark + 86, mark + 142):
        for ux, uy in RING:
            lx = ax + ux * (rad + w / 2)
            ly = ay + uy * (rad + h / 2)
            b = (lx - w / 2 - 7, ly - h / 2 - 5, lx + w / 2 + 7, ly + h / 2 + 5)
            if not free(b):
                continue
            taken.append(b)
            if rad > mark + 50:
                dr.line([(ax + ux * mark) * F, (ay + uy * mark) * F,
                         (lx - ux * (w / 2 + 6)) * F, (ly - uy * (h / 2 + 4)) * F], fill=(170, 38, 34, 170), width=2 * F)
            label(text, (lx, ly), size, RED, halo=2)
            return
    label(text, (ax + mark + w / 2, ay), size, RED, halo=2)


# the line of 1–2 February, tent → cedar: the bodies lay «almost on the line», the track itself is a reconstruction
trail = [pt(*PLACES[i][1], F) for i in range(5)]
dashes(trail, (170, 38, 34, 150), 3, on=9)

for _id, (x, z), name, size, kind in PLACES:
    place_mark(kind, x, z)
    taken.append((cx(x) - 17, cy(z) - 19, cx(x) + 17, cy(z) + 19))
for _id, (x, z), name, size, kind in PLACES:
    name_place(name, x, z, size)

sx, sy = P(61.754457, 59.417964, F)
dr.polygon([(sx, sy - 16 * F), (sx - 14 * F, sy + 10 * F), (sx + 14 * F, sy + 10 * F)], outline=INK, width=3 * F)
label('выс. 1079', (cx(-1296) + 32, cy(-172) + 54), 54, bold=True, halo=2)

label('склон выс. 880', pt(1700, 880), 40, INK, angle=-52, halo=2)

px_, py_ = P(61.756323, 59.463175, F)
dr.arc([px_ - 42 * F, py_ - 26 * F, px_ + 42 * F, py_ + 26 * F], 200, 340, fill=INK, width=4 * F)
dr.arc([px_ - 42 * F, py_ - 4 * F, px_ + 42 * F, py_ + 48 * F], 20, 160, fill=INK, width=4 * F)
label('перевал', (cx(1092) + 10, cy(36) + 78), 46, halo=2)

label('р. Ауспия', pt(700, -1830), 50, BLUE, angle=-8, halo=2)
label('прит. Лозьвы', pt(760, 1720), 44, BLUE, angle=80, halo=2)
label('к Лозьве', pt(560, 1930), 38, BLUE, halo=2)
label('прит. Лозьвы', pt(-1180, 1700), 40, BLUE, angle=85, halo=2)
label('граница леса', TREELINE, 40, (74, 94, 80, 230), angle=TREELINE_ANGLE, halo=2)
label('к Отортену 10 км', pt(-1520, 1900), 36, INK, halo=2)

lx, ly = P(61.746694, 59.449444, F)
dr.rectangle([lx - 12 * F, ly - 12 * F, lx + 12 * F, ly + 12 * F], outline=RED, width=4 * F)
dr.line([(lx - 12 * F, ly - 12 * F), (lx + 12 * F, ly + 12 * F)], fill=RED, width=3 * F)
label('лабаз · ночл. 31.I', (cx(367) + 136, cy(-1037) + 8), 44, RED, halo=2)
label('лыжня 31.I', pt(1420, -1700), 40, RED, angle=-6, halo=2)

# ── printed furniture ────────────────────────────────────────────────────────────────────────────────────
dr.rectangle([40 * F, 40 * F, (S - 40) * F, (S - 40) * F], outline=(38, 34, 48, 160), width=3 * F)

nx, ny = (S - 150) * F, 170 * F
dr.line([(nx, ny + 90 * F), (nx, ny - 40 * F)], fill=INK, width=4 * F)
dr.polygon([(nx, ny - 60 * F), (nx - 14 * F, ny - 28 * F), (nx + 14 * F, ny - 28 * F)], fill=INK)
label('С', (S - 150, 170 - 95), 56, bold=True)

label('Верховья Ауспии — выс. 1079', (S / 2, 110), 50, INK, bold=True, halo=3)

# The key: once the sheet carries named places it is no longer a bare tracing, and the red marks have to say
# what they mean. It sits in the south-west corner, which is empty slope, with the contour note — moved in from
# the south-east, where the ski line ran straight through it — and a 500 m bar. A kilometre bar wanted 500 px of
# the 2048 on its own, and at the size the HUD draws the sheet the card would have eaten a quarter of the map.
LEG_X, LEG_Y, LEG_W, LEG_H = 86, S - 636, 476, 548
dr.rounded_rectangle([LEG_X * F, LEG_Y * F, (LEG_X + LEG_W) * F, (LEG_Y + LEG_H) * F], 10 * F,
                     fill=(236, 238, 232, 214), outline=(38, 34, 48, 150), width=2 * F)
_lx = LEG_X + 42
_ly = LEG_Y + 52
label('Условные знаки', (LEG_X + 158, _ly), 36, INK, bold=True)
_ly += 54


def key_row(symbol, text, colour=INK, size=28):
    """One line of the key: the symbol exactly as it is drawn on the sheet, then the name in the same hand."""
    global _ly
    symbol(_lx, _ly)
    label(text, (_lx + 40 + dr.textbbox((0, 0), text, font=face(size))[2] / (2 * F), _ly), size, colour)
    _ly += 40


def _rule(x, y, colour, width, on):
    dashes([((x - 24) * F, y * F), ((x + 24) * F, y * F)], colour, width, on=on, off=on)


key_row(lambda x, y: dr.polygon([((x - 12) * F, (y + 8) * F), (x * F, (y - 11) * F), ((x + 12) * F, (y + 8) * F)],
                                outline=RED, width=4 * F), 'палатка 1–2 февраля', RED)
key_row(lambda x, y: (dr.line([(x * F, (y + 9) * F), (x * F, (y - 2) * F)], fill=RED, width=3 * F),
                      dr.polygon([((x - 10) * F, (y - 1) * F), (x * F, (y - 16) * F), ((x + 10) * F, (y - 1) * F)],
                                 outline=RED, width=3 * F)), 'кедр у кромки леса', RED)
key_row(lambda x, y: (dr.line([((x - 9) * F, (y - 9) * F), ((x + 9) * F, (y + 9) * F)], fill=RED, width=4 * F),
                      dr.line([((x - 9) * F, (y + 9) * F), ((x + 9) * F, (y - 9) * F)], fill=RED, width=4 * F)),
        'овраг, настил', RED)
key_row(lambda x, y: dr.ellipse([(x - 7) * F, (y - 7) * F, (x + 7) * F, (y + 7) * F], outline=RED, width=4 * F),
        'где нашли', RED)
key_row(lambda x, y: (dr.rectangle([(x - 11) * F, (y - 11) * F, (x + 11) * F, (y + 11) * F], outline=RED, width=4 * F),
                      dr.line([((x - 11) * F, (y - 11) * F), ((x + 11) * F, (y + 11) * F)], fill=RED, width=3 * F)),
        'лабаз', RED)
key_row(lambda x, y: _rule(x, y, RED, 3, 11), 'путь группы', RED)
key_row(lambda x, y: _rule(x, y, (170, 38, 34, 170), 2, 5), 'линия следов', RED)
key_row(lambda x, y: dr.line([((x - 24) * F, y * F), ((x + 24) * F, y * F)], fill=BLUE, width=4 * F), 'ручей', BLUE)

_ly += 6
label('горизонтали через 20 м, утолщённые через 100', (LEG_X + LEG_W / 2, _ly), 26)

_ly += 52
bx, half_km = (LEG_X + 42) * F, 500 * PX * F
dr.line([(bx, _ly * F), (bx + half_km, _ly * F)], fill=INK, width=4 * F)
for k in range(3):
    x = bx + half_km * k / 2
    dr.line([(x, (_ly - 11) * F), (x, (_ly + 11) * F)], fill=INK, width=3 * F)
label('0', (LEG_X + 42, _ly + 34), 30)
label('250', (LEG_X + 42 + 250 * PX, _ly + 34), 30)
label('500 м', (LEG_X + 42 + 500 * PX, _ly + 34), 30)

base.alpha_composite(layer.resize((S, S), Image.LANCZOS))

# ── wear ─────────────────────────────────────────────────────────────────────────────────────────────────
sm = Image.new('L', (S, S), 0)
sd = ImageDraw.Draw(sm)
for _ in range(40):
    x, y = rng.integers(0, S, 2)
    r = rng.integers(20, 90)
    sd.ellipse([x - r, y - r, x + r, y + r], fill=int(rng.integers(4, 12)))
dark = Image.new('RGBA', (S, S), (60, 60, 70, 255))
dark.putalpha(sm.filter(ImageFilter.GaussianBlur(30)))
base.alpha_composite(dark)

OUT.parent.mkdir(parents=True, exist_ok=True)
base.convert('RGB').save(OUT, optimize=True)
print('ok', OUT, OUT.stat().st_size // 1024, 'KiB')

"""Tracing-paper route map (калька) of the playable area, in the manner of the group's copies from forestry maps.
Stylisation: the look of the group's own kalki is not documented; this imitates a period hand copy — ink contours every 20 m,
blue ink streams, pencil forest hatching, red pencil route, handwritten labels. Output: Assets/Art/Maps/kalka_1959.png (2048²,
north up, covering exactly the 4096 m world square) + labels are part of the art."""
import numpy as np, math, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy import ndimage as nd
sys.path.insert(0, '.')
from geo import *
S = 2048; px = S / 4096.0  # pixels per metre
d = np.load('dem.npy')                           # row 0 = north (z south = -2048)
chm = np.load('chm.npy').astype(np.float32)
acc = np.load('acc4.npy')
rng = np.random.default_rng(1959)
FONT = '/home/claude/fonts/Caveat-400.ttf'; FONTB = '/home/claude/fonts/Caveat-700.ttf'

def up(a, order=1): return nd.zoom(a, S / a.shape[0], order=order)
dem = up(nd.gaussian_filter(d, 1.5))
# paper: bluish-white tracing paper, fibres, faint stains, two fold creases
paper = np.ones((S, S, 3)) * np.array([236, 238, 232]) / 255
fib = nd.gaussian_filter(rng.normal(0, 1, (S, S)), (0.6, 5)) * .025 + nd.gaussian_filter(rng.normal(0, 1, (S, S)), 40) * .6
paper *= (1 + fib)[..., None]
yy, xx = np.mgrid[0:S, 0:S]
for c, ax in [(S * .5, xx), (S * .5, yy)]:
    crease = np.exp(-((ax - c) / 3.0) ** 2) * .07 + np.exp(-((ax - c - 7) / 10.0) ** 2) * .03
    paper *= (1 - crease)[..., None]
edge = np.minimum.reduce([xx, yy, S - 1 - xx, S - 1 - yy]).astype(float)
paper *= (1 - .10 * np.exp(-edge / 60))[..., None]
img = paper.copy()

def ink(mask, color, alpha=1.0):
    global img
    a = np.clip(mask, 0, 1)[..., None] * alpha
    img = img * (1 - a) + np.array(color)[None, None, :] / 255 * a

# forest: loose pencil hatching where the canopy model says forest
forest = up(nd.gaussian_filter((chm > 3).astype(np.float32), 4)) > .35
hatch = ((xx + yy) % 14 < 1.6) & forest
hatch = hatch & (nd.gaussian_filter(rng.random((S, S)), 1.2) > .47)
ink(hatch.astype(float) * .55, (86, 104, 92), .75)
fe = forest ^ nd.binary_erosion(forest, iterations=2)
ink((fe & (((xx // 9) + (yy // 9)) % 2 == 0)).astype(float), (86, 104, 92), .6)

# contours: every 20 m, index every 100 m (brown ink), anti-aliased
g = np.hypot(*np.gradient(dem))
for step, width, alpha in [(20, 1.0, .55), (100, 1.9, .85)]:
    f = dem / step
    dist = np.abs(f - np.round(f)) * step / np.maximum(g, 1e-3)   # metres-in-height → pixels
    ink(np.clip(width - dist, 0, 1), (122, 84, 52), alpha)
# streams: blue ink along the D8 flow paths (8-connected on the 4 m grid), width by drainage area
area = np.log10(acc + 1)
m4 = acc > 2.5e5
lw = np.zeros((S, S))
for lo, hi, rad in [(5.3, 6.0, 0), (6.0, 6.7, 1), (6.7, 9.0, 2)]:
    band = m4 & (area >= lo)
    b = np.kron(band, np.ones((2, 2))).astype(bool)[1:-1, 1:-1]
    if rad: b = nd.binary_dilation(b, iterations=rad)
    lw = np.maximum(lw, b.astype(float))
lw = np.clip(nd.gaussian_filter(lw, .9) * 1.8, 0, 1)
ink(lw, (44, 78, 150), .85)

im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
dr = ImageDraw.Draw(im, 'RGBA')
def P(la, lo):
    x, z = ll2xz(la, lo); return ((x + 2048) * px, (z + 2048) * px)
def xz(x, z): return ((x + 2048) * px, (z + 2048) * px)
f = lambda s: ImageFont.truetype(FONT, s); fb = lambda s: ImageFont.truetype(FONTB, s)
INK = (38, 34, 48, 235); RED = (170, 38, 34, 230); BLUE = (40, 70, 140, 235)

def label(text, at, size=40, color=INK, angle=0, bold=False, anchor='mm'):
    font = (fb if bold else f)(size)
    box = dr.textbbox((0, 0), text, font=font)
    w_, h_ = box[2] - box[0] + 20, box[3] - box[1] + 30
    t = Image.new('RGBA', (w_, h_), (0, 0, 0, 0))
    ImageDraw.Draw(t).text((w_ / 2, h_ / 2), text, font=font, fill=color, anchor='mm')
    t = t.rotate(angle, resample=Image.BICUBIC, expand=True)
    im.alpha_composite(t, (int(at[0] - t.width / 2), int(at[1] - t.height / 2))) if im.mode == 'RGBA' else None

im = im.convert('RGBA'); dr = ImageDraw.Draw(im, 'RGBA')
# summit triangle + height
sx, sy = P(61.754457, 59.417964)
dr.polygon([(sx, sy - 16), (sx - 14, sy + 10), (sx + 14, sy + 10)], outline=INK, width=3)
label('выс. 1079', (sx + 30, sy + 52), 54, bold=True)
# 880 hill (protocol: den on the slope of height 880), NE edge
hx, hy = xz(1990, -1050)
dr.polygon([(hx, hy - 12), (hx - 11, hy + 8), (hx + 11, hy + 8)], outline=INK, width=3)
label('880', (hx - 40, hy + 36), 44, bold=True)
# pass
px_, py_ = P(61.756323, 59.463175)
dr.arc([px_ - 30, py_ - 18, px_ + 30, py_ + 18], 200, 340, fill=INK, width=3)
dr.arc([px_ - 30, py_ - 2, px_ + 30, py_ + 34], 20, 160, fill=INK, width=3)
label('перевал', (px_ + 10, py_ + 60), 42)
# rivers
label('р. Ауспия', xz(700, 1830), 50, BLUE, angle=-8)
label('прит. Лозьвы', xz(760, -1720), 44, BLUE, angle=80)
label('к Лозьве', xz(560, -1930), 38, BLUE)
label('прит. Лозьвы', xz(-1180, -1700), 40, BLUE, angle=85)
# route (red pencil, dashed): labaz → tent, from the least-cost line
import json
route = [(x, -z) for x, z in json.load(open('route.json'))['route_labaz_tent']] if False else json.load(open('route.json'))['route_labaz_tent']
pts = [xz(x, z) for x, z in route]
seg = 0.0
for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
    L = math.hypot(x1 - x0, y1 - y0); n = max(1, int(L / 4))
    for i in range(n):
        s0 = seg + L * i / n
        if (s0 // 22) % 2 == 0:
            dr.line([(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n), (x0 + (x1 - x0) * (i + 1) / n, y0 + (y1 - y0) * (i + 1) / n)], fill=RED, width=5)
    seg += L
# arrival from the Auspiya valley (dashed red along the valley bottom to the labaz)
vx, vy = xz(1900, 2000); lx, ly = P(61.746694, 59.449444)
for i in range(0, 30, 2):
    t0, t1 = i / 30, (i + 1) / 30
    dr.line([(vx + (lx - vx) * t0, vy + (ly - vy) * t0), (vx + (lx - vx) * t1, vy + (ly - vy) * t1)], fill=RED, width=5)
# labaz: small square with cross
dr.rectangle([lx - 12, ly - 12, lx + 12, ly + 12], outline=RED, width=4)
dr.line([(lx - 12, ly - 12), (lx + 12, ly + 12)], fill=RED, width=3)
label('лабаз', (lx + 70, ly + 6), 44, RED)
# night camp of 1 Feb: tent symbol
tx, ty = P(61.758561, 59.429436)
dr.polygon([(tx - 16, ty + 10), (tx, ty - 14), (tx + 16, ty + 10)], outline=RED, width=4)
label('ночл. 1.II', (tx + 20, ty - 42), 44, RED)
# frame, north arrow, scale bar, title
dr.rectangle([40, 40, S - 40, S - 40], outline=(38, 34, 48, 160), width=3)
nx, ny = S - 150, 170
dr.line([(nx, ny + 90), (nx, ny - 40)], fill=INK, width=4); dr.polygon([(nx, ny - 60), (nx - 14, ny - 28), (nx + 14, ny - 28)], fill=INK)
label('С', (nx, ny - 95), 56, bold=True)
bx, by = 120, S - 130
m1000 = 1000 * px
dr.line([(bx, by), (bx + m1000, by)], fill=INK, width=4)
for k in range(0, 5):
    x = bx + m1000 * k / 4
    dr.line([(x, by - 12), (x, by + 12)], fill=INK, width=3)
label('0', (bx, by + 40), 36); label('500', (bx + m1000 / 2, by + 40), 36); label('1 км', (bx + m1000, by + 40), 36)
label('Верховья Ауспии — выс. 1079.  Калька с карты лесхоза', (S / 2 - 120, 110), 50, INK, bold=True)
label('горизонтали через 20 м', (S - 330, S - 80), 34)
# pencil smudges
sm = Image.new('L', (S, S), 0); sd = ImageDraw.Draw(sm)
for _ in range(40):
    x, y = rng.integers(0, S, 2); r = rng.integers(20, 90)
    sd.ellipse([x - r, y - r, x + r, y + r], fill=int(rng.integers(4, 12)))
sm = sm.filter(ImageFilter.GaussianBlur(30))
dark = Image.new('RGBA', (S, S), (60, 60, 70, 255)); dark.putalpha(sm)
im.alpha_composite(dark)
im.convert('RGB').save('kalka_1959.png', optimize=True)
im.convert('RGB').resize((1024, 1024), Image.LANCZOS).save('kalka_preview.png')
print('ok')

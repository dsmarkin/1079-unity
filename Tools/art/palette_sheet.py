#!/usr/bin/env python3
"""Draws docs/art/palette.svg — the colour sheet: swatches plus a night scene painted only with those colours — from
docs/art/palette.json (group, name, label, hex). Edit the JSON, run this, look at the SVG; the scene is the judge, not the squares.

    python3 Tools/art/palette_sheet.py [--png docs/art/palette.png]      # PNG needs macOS (qlmanage) and Pillow
"""
import io, json, os, random, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_JSON = os.path.join(ROOT, "docs/art/palette.json")
OUT_SVG = os.path.join(ROOT, "docs/art/palette.svg")
GROUPS = [(g["group"], [(c.get("label", c["name"]), c["hex"]) for c in g["colours"]]) for g in json.load(io.open(SRC_JSON, encoding="utf-8"))]
C = {n.replace("\n", " "): h for _, sw in GROUPS for n, h in sw}


def c(key):
    """Colour by the start of its label."""
    for k, v in C.items():
        if k.startswith(key):
            return v
    raise KeyError(key)


SNOW, SHADE, FAR, NIGHT = c("Снег на"), c("Снег в"), c("Снег вдали"), c("Небо ночь")
NEEDLE, NEEDLE_L, BARK, BIRCH, ROCK = c("Хвоя в"), c("Хвоя на"), c("Кора"), c("Берёза"), c("Камень")
SKIN, VATNIK, SHTORM, CANVAS, BOOT, WOOD, METAL = c("Кожа"), c("Ватник"), c("Штормовка"), c("Брезент"), c("Валенки"), c("Дерево"), c("Металл")
RED, BLUE = c("Красный"), c("Синий")
FIRE_IN, FIRE_OUT, TORCH, BLACK, EYES = c("Огонь ядро"), c("Огонь край"), c("Свет"), c("Чёрный"), c("Глаза")

W, H = 1500, 980
o = io.StringIO(); w = o.write
w(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Helvetica, Arial, sans-serif">\n')
w(f'<rect width="{W}" height="{H}" fill="#F4F1EA"/>\n')
w('<text x="40" y="56" font-size="30" font-weight="bold" fill="#222">1079 · Коробка цветов — черновик 1</text>\n')
w('<text x="40" y="84" font-size="16" fill="#555">Вся игра красится только этими цветами. Судить по сцене справа, а не по квадратам.</text>\n')

# --- swatches ---
x0, y, sw_w, sw_h, gap, pitch = 40, 125, 80, 58, 10, 160
for title, sws in GROUPS:
    w(f'<text x="{x0}" y="{y}" font-size="15" font-weight="bold" fill="#333">{title}</text>\n')
    for i, (name, hexv) in enumerate(sws):
        x = x0 + i * (sw_w + gap); top = y + 10
        w(f'<rect x="{x}" y="{top}" width="{sw_w}" height="{sw_h}" rx="8" fill="{hexv}" stroke="#00000022"/>\n')
        for li, line in enumerate(name.split("\n")):
            w(f'<text x="{x + sw_w/2}" y="{top + sw_h + 16 + li*13}" font-size="11" text-anchor="middle" fill="#333">{line}</text>\n')
        w(f'<text x="{x + sw_w/2}" y="{top + sw_h + 44}" font-size="10" text-anchor="middle" fill="#888">{hexv}</text>\n')
    y += pitch

# --- scene: the same colours at work ---
SX, SY, SW, SH = 790, 125, 670, 560
w(f'<defs><clipPath id="scene"><rect x="{SX}" y="{SY}" width="{SW}" height="{SH}" rx="14"/></clipPath>'
  f'<radialGradient id="fireglow"><stop offset="0" stop-color="{FIRE_OUT}" stop-opacity=".55"/><stop offset=".5" stop-color="{FIRE_OUT}" stop-opacity=".18"/><stop offset="1" stop-color="{FIRE_OUT}" stop-opacity="0"/></radialGradient>'
  f'<radialGradient id="moonglow"><stop offset="0" stop-color="{BIRCH}" stop-opacity=".22"/><stop offset="1" stop-color="{BIRCH}" stop-opacity="0"/></radialGradient></defs>\n')
w(f'<g clip-path="url(#scene)"><g transform="translate({SX},{SY})">\n')
w(f'<rect width="{SW}" height="{SH}" fill="{NIGHT}"/>\n')
rnd = random.Random(1959)
for _ in range(38):
    sx, sy = rnd.uniform(5, SW-5), rnd.uniform(5, 250); r = rnd.choice([1.0, 1.3, 1.8])
    w(f'<circle cx="{sx:.0f}" cy="{sy:.0f}" r="{r}" fill="{SNOW}" opacity="{rnd.uniform(.5,.95):.2f}"/>\n')
w(f'<circle cx="560" cy="86" r="70" fill="url(#moonglow)"/><circle cx="560" cy="86" r="24" fill="{BIRCH}"/>\n')
w(f'<polygon points="0,300 120,242 260,272 400,214 520,252 670,220 670,560 0,560" fill="{FAR}"/>\n')


def ridge_y(x):
    pts = [(0, 300), (120, 242), (260, 272), (400, 214), (520, 252), (670, 220)]
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        if xa <= x <= xb:
            return ya + (yb - ya) * (x - xa) / (xb - xa)
    return 220


for tx in range(8, 670, 22):
    base = ridge_y(tx) + 8; h = rnd.uniform(16, 30)
    w(f'<polygon points="{tx},{base-h:.0f} {tx-6},{base:.0f} {tx+6},{base:.0f}" fill="{NEEDLE}" opacity=".85"/>\n')
w(f'<polygon points="0,362 200,342 420,356 670,336 670,560 0,560" fill="{SHADE}"/>\n')


def spruce(x, base, h, lit=0):
    s = ""
    for i in range(3):
        top = base - h * (1 - i*0.27); wd = h*0.17*(1 + i*0.6); th = h*0.42
        s += f'<polygon points="{x},{top:.0f} {x-wd:.0f},{top+th:.0f} {x+wd:.0f},{top+th:.0f}" fill="{NEEDLE}"/>'
        if lit:
            s += f'<polygon points="{x},{top:.0f} {x+wd*0.7*lit:.0f},{top+th:.0f} {x},{top+th:.0f}" fill="{NEEDLE_L}"/>'
        s += f'<polygon points="{x},{top:.0f} {x-wd*0.3:.0f},{top+th*0.32:.0f} {x+wd*0.3:.0f},{top+th*0.32:.0f}" fill="{SNOW}" opacity=".9"/>'
    s += f'<rect x="{x-h*0.03:.0f}" y="{base-h*0.05:.0f}" width="{h*0.06:.0f}" height="{h*0.12:.0f}" fill="{BARK}"/>'
    s += f'<ellipse cx="{x}" cy="{base+h*0.07:.0f}" rx="{h*0.22:.0f}" ry="{h*0.05:.0f}" fill="{SNOW}" opacity=".85"/>'
    return s + "\n"


w(spruce(62, 470, 230, lit=+1)); w(spruce(150, 455, 170, lit=+1))
w(spruce(640, 480, 240, lit=-1)); w(spruce(560, 420, 120, lit=-1))
w(f'<rect x="478" y="300" width="7" height="150" fill="{BIRCH}"/>')
for by in (330, 365, 400, 430):
    w(f'<rect x="478" y="{by}" width="7" height="4" fill="{BLACK}" opacity=".8"/>')
w(f'<path d="M481,320 L455,290 M481,335 L505,300 M481,350 L462,318" stroke="{BIRCH}" stroke-width="3" fill="none"/>\n')
w(f'<path d="M20,470 Q30,425 75,428 Q120,432 118,470 Z" fill="{ROCK}"/><path d="M32,440 Q55,418 100,432 Q110,436 106,442 Q60,432 34,448 Z" fill="{SNOW}"/>\n')
w(f'<circle cx="330" cy="470" r="150" fill="url(#fireglow)"/>\n')
w(f'<ellipse cx="330" cy="472" rx="120" ry="30" fill="{SNOW}" opacity=".85"/>\n')
w(f'<polygon points="425,470 495,382 565,470" fill="{CANVAS}"/><polygon points="495,382 565,470 495,470" fill="{BLACK}" opacity=".28"/>'
  f'<polygon points="482,470 495,440 508,470" fill="{BLACK}"/><line x1="425" y1="470" x2="495" y2="382" stroke="{SNOW}" stroke-width="4"/>\n')
w(f'<rect x="300" y="462" width="60" height="9" rx="4" fill="{WOOD}" transform="rotate(-8 330 466)"/><rect x="302" y="463" width="56" height="8" rx="4" fill="{WOOD}" transform="rotate(14 330 467)"/>'
  f'<path d="M330,412 C312,432 308,446 318,460 C322,452 326,450 328,444 C330,456 340,458 342,466 C356,452 352,436 330,412 Z" fill="{FIRE_OUT}"/>'
  f'<path d="M330,432 C322,444 322,452 328,462 C334,456 338,452 338,446 C336,440 334,438 330,432 Z" fill="{FIRE_IN}"/>\n')


def hiker(x, base, h, hat, facing=1, torch=False):
    """Proportions of the figure: head a quarter of the height, no neck, big boots and mittens."""
    head = h*0.25; s = ""
    body_w = h*0.36; body_h = h*0.38; leg_h = h*0.30; boot_h = h*0.09
    s += f'<rect x="{x - facing*body_w*0.85 - body_w*0.32:.0f}" y="{base-leg_h-body_h-head*0.1:.0f}" width="{body_w*0.64:.0f}" height="{body_h*0.8:.0f}" rx="5" fill="{CANVAS}"/>'
    for sgn in (-1, 1):
        lx = x + sgn*body_w*0.22
        s += f'<rect x="{lx-body_w*0.16:.0f}" y="{base-leg_h-4:.0f}" width="{body_w*0.32:.0f}" height="{leg_h:.0f}" rx="5" fill="{SHTORM}"/>'
        s += f'<rect x="{lx-body_w*0.2:.0f}" y="{base-boot_h:.0f}" width="{body_w*0.44:.0f}" height="{boot_h:.0f}" rx="4" fill="{BOOT}"/>'
    s += f'<rect x="{x-body_w/2:.0f}" y="{base-leg_h-body_h:.0f}" width="{body_w:.0f}" height="{body_h:.0f}" rx="{body_w*0.3:.0f}" fill="{VATNIK}"/>'
    ay = base-leg_h-body_h*0.85
    for sgn in (-1, 1):
        ax = x + sgn*body_w*0.62
        s += f'<rect x="{ax-body_w*0.12:.0f}" y="{ay:.0f}" width="{body_w*0.24:.0f}" height="{body_h*0.7:.0f}" rx="5" fill="{VATNIK}"/>'
        s += f'<circle cx="{ax:.0f}" cy="{ay+body_h*0.72:.0f}" r="{body_w*0.16:.0f}" fill="{hat}"/>'
    hy = base-leg_h-body_h-head*0.55
    s += f'<circle cx="{x}" cy="{hy:.0f}" r="{head*0.55:.0f}" fill="{SKIN}"/>'
    s += f'<path d="M{x-head*0.58:.0f},{hy-head*0.05:.0f} A{head*0.58:.0f},{head*0.58:.0f} 0 0 1 {x+head*0.58:.0f},{hy-head*0.05:.0f} Z" fill="{hat}"/>'
    s += f'<rect x="{x-head*0.6:.0f}" y="{hy-head*0.12:.0f}" width="{head*1.2:.0f}" height="{head*0.16:.0f}" rx="3" fill="{hat}"/>'
    s += f'<circle cx="{x}" cy="{hy-head*0.62:.0f}" r="{head*0.14:.0f}" fill="{hat}"/>'
    s += f'<circle cx="{x+facing*head*0.18:.0f}" cy="{hy+head*0.02:.0f}" r="1.6" fill="{BLACK}"/>'
    if torch:
        tx, ty = x + facing*body_w*0.62, ay+body_h*0.72
        s += f'<rect x="{tx-3:.0f}" y="{ty-4:.0f}" width="16" height="8" rx="2" fill="{METAL}" transform="rotate({-12*facing} {tx} {ty})"/>'
        s += f'<polygon points="{tx+12:.0f},{ty-3:.0f} {tx+12:.0f},{ty+3:.0f} {tx+215:.0f},{ty+42:.0f} {tx+225:.0f},{ty-40:.0f}" fill="{TORCH}" opacity=".28"/>'
        s += f'<ellipse cx="{tx+205:.0f}" cy="{ty+38:.0f}" rx="55" ry="12" fill="{TORCH}" opacity=".55"/>'
    return s + "\n"


w(hiker(250, 470, 92, RED, facing=1, torch=True))
w(hiker(400, 470, 80, BLUE, facing=-1))
# the Menk between the right-hand spruces: tall, hunched, arms to the ground, a crown of dry branches
mx, mb = 598, 478
w(f'<g fill="{BLACK}">'
  f'<polygon points="{mx-14},{mb} {mx-10},{mb-120} {mx+10},{mb-120} {mx+14},{mb}"/>'
  f'<path d="M{mx-22},{mb-118} Q{mx-30},{mb-200} {mx-4},{mb-232} Q{mx+26},{mb-206} {mx+22},{mb-118} Z"/>'
  f'<circle cx="{mx-2}" cy="{mb-236}" r="13"/>'
  f'<polygon points="{mx-24},{mb-210} {mx-52},{mb-60} {mx-58},{mb} {mx-46},{mb} {mx-40},{mb-70} {mx-14},{mb-190}"/>'
  f'<polygon points="{mx+22},{mb-206} {mx+48},{mb-60} {mx+54},{mb} {mx+42},{mb} {mx+36},{mb-70} {mx+12},{mb-186}"/>'
  f'<polygon points="{mx-20},{mb-224} {mx-38},{mb-262} {mx-8},{mb-236}"/><polygon points="{mx+4},{mb-244} {mx+2},{mb-284} {mx+14},{mb-240}"/><polygon points="{mx+18},{mb-224} {mx+40},{mb-256} {mx+22},{mb-232}"/>'
  f'</g>'
  f'<ellipse cx="{mx-7}" cy="{mb-238}" rx="3.2" ry="2" fill="{EYES}"/><ellipse cx="{mx+4}" cy="{mb-238}" rx="3.2" ry="2" fill="{EYES}"/>\n')
w(f'<rect x="0" y="500" width="{SW}" height="60" fill="{SHADE}" opacity=".25"/>\n')
w('</g></g>\n')
w(f'<text x="{SX}" y="{SY+SH+28}" font-size="14" fill="#444">Так это выглядит вместе: ночь, снег, костёр, фонарь, палатка, Менк среди елей.</text>\n')
w(f'<text x="{SX}" y="{SY+SH+48}" font-size="13" fill="#777">Шапки и варежки — цвет игрока. В игре свет и туман затемняют всё сильнее, чем на листе.</text>\n')
w(f'<text x="{SX}" y="{SY+SH+68}" font-size="13" fill="#777">Правка: назвать цвет и сказать, каким ему быть.</text>\n')
w('</svg>\n')
io.open(OUT_SVG, "w", encoding="utf-8").write(o.getvalue())
print("colours:", sum(len(s) for _, s in GROUPS), "->", os.path.relpath(OUT_SVG, ROOT))

if "--png" in sys.argv:
    # qlmanage scales the shorter side to -s and crops to a square, so pad the canvas to a square first and crop back.
    png = os.path.abspath(sys.argv[sys.argv.index("--png") + 1]); tmp = tempfile.mkdtemp()
    sq = os.path.join(tmp, "square.svg")
    io.open(sq, "w", encoding="utf-8").write(o.getvalue().replace(f'height="{H}" viewBox="0 0 {W} {H}"', f'height="{W}" viewBox="0 0 {W} {W}"'))
    subprocess.run(["qlmanage", "-t", "-s", str(W), "-o", tmp, sq], capture_output=True)
    from PIL import Image
    Image.open(sq + ".png").crop((0, 0, W, H)).save(png); print("png ->", os.path.relpath(png, ROOT))

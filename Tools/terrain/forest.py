"""Forest composition for the 1079 world, from the data already in Assets/Data/World.

Why: the canopy model gives one crown top per 10 m window (≈62 stems/ha in the forest), species were a flat random mix,
and there was nothing under the canopy. This script rewrites the species of the detected trees and adds the layers the
canopy model cannot see. Rules come from sources on the Northern Urals (docs/MAP.md, section "Лес"):

- belts: dark coniferous taiga (spruce–fir with Siberian pine and birch) below ~620 m; a transition 620–700 m where birch,
  Siberian pine and larch gain; the tree line at 700–750 m with crooked birch, stunted spruce and larch (east slope);
- species grow in patches (stands), not as a salt-and-pepper mix: smooth noise fields per species, ~60–120 m blobs;
- valleys and stream banks: spruce/fir/birch with rowan and willows; rocky dry slopes: lighter Siberian-pine stands;
- ~5 % standing dead conifers (old-growth taiga, >400 yr), windfall under the snow;
- trees above ~660 m take tree-line forms (flagged / stunted), probability rising to 1 at 740 m.

Outputs (little-endian float32 records, x east, z north, metres):
  trees.f32       x, z, height, code      code = species + 10 * form   (species 0 spruce 1 fir 2 birch 3 Siberian pine 4 larch;
                                                                         form 0 normal 1 tree-line 2 dead snag)
  understory.f32  x, z, size, code      code = kind + yaw/360 (fraction)  kinds: see KINDS below
Run from the repo root: python3 Tools/terrain/forest.py   (numpy, scipy). Deterministic (seed 1959).
"""
import numpy as np
from scipy import ndimage as nd

W = 'Assets/Data/World'
N, HALF, STEP = 2049, 2048.0, 2.0
KINDS = ['young_spruce', 'young_fir', 'rowan', 'willow', 'juniper', 'dwarf_birch', 'windfall', 'hummock', 'birch_clump', 'young_pine']

rng = np.random.default_rng(1959)
ground = np.fromfile(f'{W}/height_2049.r16', '<u2').reshape(N, N) / 65535.0 * (1096 - 496) + 496  # row 0 = south
canopy = np.fromfile(f'{W}/canopy_2049.r8', np.uint8).reshape(N, N)
streams = nd.zoom(np.fromfile(f'{W}/streams_1025.r8', np.uint8).reshape(1025, 1025).astype(np.float32), 2049 / 1025, order=0)[:N, :N]
rock = nd.zoom(np.fromfile(f'{W}/rock_1025.r8', np.uint8).reshape(1025, 1025).astype(np.float32) / 255, 2049 / 1025, order=1)[:N, :N]
forest = nd.gaussian_filter((canopy >= 3).astype(np.float32), 3)
# distance to a real stream (drainage value ≥ 60/255 ≈ a brook), metres
brook = streams >= 60
creek_d = nd.distance_transform_edt(~brook) * STEP
# local convexity: ridges and knolls are drier (cedar), hollows wetter (fir, birch)
conv = ground - nd.gaussian_filter(ground, 12)


def field(sigma, seed):
    r = np.random.default_rng(seed).standard_normal((N, N)).astype(np.float32)
    f = nd.gaussian_filter(r, sigma)
    return (f - f.mean()) / f.std()


def cell(x, z):
    c = np.clip(np.round((x + HALF) / STEP).astype(int), 0, N - 1)
    r = np.clip(np.round((z + HALF) / STEP).astype(int), 0, N - 1)
    return r, c


def smooth(a, b, v):
    t = np.clip((v - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- canopy trees: species and form
t = np.fromfile(f'{W}/trees.f32', '<f4').reshape(-1, 4)
x, z, h = t[:, 0], t[:, 1], t[:, 2]
r, c = cell(x, z)
el, cd, cv, rk = ground[r, c], creek_d[r, c], conv[r, c], rock[r, c]

patch = {s: field(28 + 6 * s, 100 + s)[r, c] for s in range(5)}   # 56–104 m blobs
low = 1 - smooth(600, 640, el)          # dark taiga
line = smooth(680, 740, el)             # tree line
mid = 1 - low - line
mid = np.clip(mid, 0, 1)
wet = np.exp(-cd / 40)                  # near a brook
dry = np.clip(cv / 3, 0, 1) + rk        # knolls, stony ground

w = np.zeros((len(x), 5))
w[:, 0] = .42 * low + .34 * mid + .26 * line + .10 * wet            # spruce
w[:, 1] = (.33 * low + .16 * mid + .02 * line) * (1 + .6 * wet)     # fir: wet valley taiga, gone at the tree line
w[:, 2] = .09 * low + .24 * mid + .46 * line + .08 * wet            # birch
w[:, 3] = (.09 * low + .16 * mid + .10 * line) * (1 + 1.5 * dry)    # Siberian pine: drier, stony
w[:, 4] = .01 * low + .10 * mid + .16 * line                        # larch: east slope, upper belt
w[:, 2] += .25 * (h < 7) * (1 - low)                                # low crowns at the forest edge are mostly birch
for s in range(5):
    w[:, s] *= np.exp(1.1 * patch[s])
w /= w.sum(1, keepdims=True)
u = rng.random(len(x))
sp = (u[:, None] > np.cumsum(w, 1)).sum(1).clip(0, 4)

form = np.zeros(len(x), int)
form[rng.random(len(x)) < smooth(660, 740, el)] = 1
conifer = sp != 2
snag = conifer & (rng.random(len(x)) < .035 + .04 * line) & (h >= 5)
form[snag] = 2
code = sp + 10 * form
np.c_[x, z, h, code].astype('<f4').tofile(f'{W}/trees.f32')
print('trees', len(x), 'species', np.bincount(sp, minlength=5), 'tree-line', (form == 1).sum(), 'snags', (form == 2).sum())

# ---------------------------------------------------------------- understory
tree_mask = np.zeros((N, N), bool)
tree_mask[r, c] = True
near_tree = nd.binary_dilation(tree_mask, iterations=1)   # ≤2 m from a canopy stem
gap = 1 - smooth(4, 14, canopy.astype(np.float32))        # light under gaps and at edges
cols, rows = np.meshgrid(np.arange(N), np.arange(N))
gx = cols * STEP - HALF
gz = rows * STEP - HALF
inside = (np.abs(gx) < HALF - 6) & (np.abs(gz) < HALF - 6)
lowG = 1 - smooth(600, 640, ground)
lineG = smooth(680, 740, ground)
wetG = np.exp(-creek_d / 25)
spp = {s: field(28 + 6 * s, 100 + s) for s in range(5)}

recs = []


def scatter(kind, prob, size_lo, size_hi, avoid_trees=True):
    p = np.clip(prob, 0, 1) * inside
    if avoid_trees:
        p = p * ~near_tree
    m = rng.random((N, N)) < p
    k = m.sum()
    xs = gx[m] + rng.uniform(-.95, .95, k)
    zs = gz[m] + rng.uniform(-.95, .95, k)
    size = rng.uniform(size_lo, size_hi, k)
    yaw = rng.random(k) * .999
    recs.append(np.c_[xs, zs, size, kind + yaw])
    print(f'{KINDS[kind]:14s}{k:7d}')
    return k


fo = forest * (ground < 790)
# sub-canopy conifers: the canopy model sees one top per 10 m; real mature taiga holds several times more stems
scatter(0, .045 * fo * (.6 * lowG + .4) * (.6 + .4 * gap) * np.exp(.5 * spp[0]), 2.0, 11.0)
scatter(1, .030 * fo * lowG * (1 + wetG) * np.exp(.6 * spp[1]), 2.0, 9.0)
scatter(9, .004 * fo * (1 - lowG) * np.exp(.8 * spp[3]), 1.5, 5.0)
# rowan: undergrowth of the dark taiga, more in gaps and by the brooks
scatter(2, .006 * fo * (.5 + gap) * (1 + wetG), 2.0, 5.0)
# willow clumps along the brooks and in wet hollows
scatter(3, .045 * np.exp(-creek_d / 10) * (ground < 780) + .002 * wetG * forest, 1.2, 3.0)
# windfall and buried hummocks (logs, stumps and shrubs under 1.2 m of snow)
scatter(6, .0025 * fo, 5.0, 14.0)
scatter(7, .006 * fo + .0012 * (1 - forest) * (ground < 800), .5, 1.6)
# tree line: crooked multi-stem birch, juniper and dwarf birch where the wind keeps the snow thin
edge = smooth(660, 710, ground) * (1 - smooth(800, 880, ground))
scatter(8, .004 * edge * (1 - .7 * rock), 1.5, 4.0)
scatter(4, .0045 * smooth(700, 760, ground) * (1 - smooth(900, 980, ground)) * (1 - .6 * rock), .6, 1.6)
scatter(5, .0055 * smooth(720, 780, ground) * (1 - smooth(950, 1030, ground)) * (1 - .8 * rock), .4, 1.0)

u_all = np.concatenate(recs).astype('<f4')
u_all.tofile(f'{W}/understory.f32')
print('understory', len(u_all))

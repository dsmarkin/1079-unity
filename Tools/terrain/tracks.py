"""Animal tracks on the snow for the 1079 world (fiction dressing from the fauna of the Northern Urals taiga).

Species that live here and leave winter tracks: mountain hare (заяц-беляк), red fox, elk (лось), sable/marten (соболь, куница),
red squirrel, hazel grouse / capercaillie (рябчик, глухарь), willow ptarmigan above the tree line (белая куропатка), wolf.
Sources: Denezhkin Kamen reserve (sable, elk, lynx, wolverine, squirrel, capercaillie — ru.wikipedia), general Northern Urals fauna.
Trails are random walks steered by the terrain: hares along forest edges and willows with doubling back, elk along the brooks,
sable from tree to tree in the dark taiga, ptarmigan in the tundra, a few long wolf trails down the valleys. None within 400 m of
the tent (the searchers found only the group's own footprints there) or near the event sites.

Output Assets/Data/World/tracks.f32: records x, z (m), yaw (deg, direction of travel), kind
  kinds: 0 hare, 1 fox, 2 elk, 3 sable, 4 grouse, 5 ptarmigan, 6 squirrel, 7 wolf
Run from the repo root after forest.py: python3 Tools/terrain/tracks.py (numpy, scipy). Deterministic.
"""
import math
import numpy as np
from scipy import ndimage as nd

W = 'Assets/Data/World'
N, HALF, STEP = 2049, 2048.0, 2.0
rng = np.random.default_rng(1079)
ground = np.fromfile(f'{W}/height_2049.r16', '<u2').reshape(N, N) / 65535.0 * (1096 - 496) + 496
canopy = np.fromfile(f'{W}/canopy_2049.r8', np.uint8).reshape(N, N)
streams = nd.zoom(np.fromfile(f'{W}/streams_1025.r8', np.uint8).reshape(1025, 1025).astype(np.float32), 2049 / 1025, order=0)[:N, :N]
forest = nd.gaussian_filter((canopy >= 3).astype(np.float32), 3)
edge = np.clip(forest * (1 - forest) * 4, 0, 1)
creek_d = nd.distance_transform_edt(streams < 60) * STEP
gz, gx = np.gradient(nd.gaussian_filter(ground, 1.5), STEP)
slope = np.degrees(np.arctan(np.hypot(gx, gz)))
trees = np.fromfile(f'{W}/trees.f32', '<f4').reshape(-1, 4)[:, :2]

# tent and event sites (x east, z north) — keep clear
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from geo import ll2xz, dms


def site(lat, lon):
    x, zs = ll2xz(lat, lon)
    return x, -zs   # geo.py uses the web frame (z south)


TENT = site(dms(61, 45, 30.82), dms(59, 25, 45.97))
SITES = [site(dms(61, 45, 53.20), dms(59, 27, 17.80)),   # cedar
         site(dms(61, 45, 53.93), dms(59, 27, 14.64)),   # ravine
         site(61.76351, 59.45018), site(61.76266, 59.44727), site(61.76217, 59.44458),   # Dyatlov, Slobodin, Kolmogorova
         site(dms(61, 44, 48.1), dms(59, 26, 58.0))]     # labaz and the 31 Jan camp


def at(a, x, z):
    c = int(np.clip(round((x + HALF) / STEP), 0, N - 1)); r = int(np.clip(round((z + HALF) / STEP), 0, N - 1))
    return a[r, c]


def ok(x, z):
    if abs(x) > HALF - 20 or abs(z) > HALF - 20:
        return False
    if math.hypot(x - TENT[0], z - TENT[1]) < 400:
        return False
    for sx, sz in SITES:
        if math.hypot(x - sx, z - sz) < 50:
            return False
    return at(slope, x, z) < 34


def pick(weight, tries=4000):
    """Random start cell with probability ∝ weight."""
    for _ in range(tries):
        c, r = rng.integers(0, N, 2)
        if rng.random() < weight[r, c]:
            x, z = c * STEP - HALF, r * STEP - HALF
            if ok(x, z):
                return x, z
    return None


out = []


def walk(kind, start, length, step, turn, pref=None, pair=0.0, group=1, zig=0.0, back=0.0):
    """Random walk; pref(x, z, heading) returns a heading bias (radians) or None. pair: side offset alternating (m)."""
    x, z = start
    h = rng.random() * 2 * math.pi
    dist, side, n = 0.0, 1, 0
    while dist < length:
        h += rng.normal(0, turn)
        if pref is not None:
            b = pref(x, z, h)
            if b is not None:
                h += 0.15 * math.atan2(math.sin(b - h), math.cos(b - h))
        if back and rng.random() < back:   # hare doubling back (скидка)
            h += math.pi + rng.normal(0, .3)
        s = step * (0.8 + 0.4 * rng.random())
        nx, nz = x + math.sin(h) * s, z + math.cos(h) * s
        if not ok(nx, nz):
            h += math.pi / 2 * (1 if rng.random() < .5 else -1)
            dist += s
            continue
        x, z = nx, nz
        dist += s
        px, pz = x, z
        if pair:
            px += math.cos(h) * pair * side; pz -= math.sin(h) * pair * side; side = -side
        if zig:
            px += math.cos(h) * zig * math.sin(n * 1.7)
            pz -= math.sin(h) * zig * math.sin(n * 1.7)
        out.append((px, pz, math.degrees(h) % 360, kind))
        n += 1


def downhill_along_creek(x, z, h):
    # follow the brook: head roughly along the contour toward lower ground near water
    if at(creek_d, x, z) > 60:
        c = int(round((x + HALF) / STEP)); r = int(round((z + HALF) / STEP))
        r0, r1, c0, c1 = max(r - 15, 0), min(r + 15, N - 1), max(c - 15, 0), min(c + 15, N - 1)
        win = creek_d[r0:r1, c0:c1]
        rr, cc = np.unravel_index(np.argmin(win), win.shape)
        return math.atan2((c0 + cc) - c, (r0 + rr) - r)
    return None


def to_forest(x, z, h):
    return None if at(forest, x, z) > .3 else h + math.pi


w_hare = np.clip(edge * 2 + np.exp(-creek_d / 30) * .5, 0, 1) * (ground < 800) * .02
w_fox = (ground < 900) * .004
w_elk = np.exp(-creek_d / 40) * (ground < 700) * .02
w_taiga = forest * (ground < 680) * .01
w_tundra = (ground > 760) * (ground < 1000) * .01

for _ in range(420):
    s = pick(w_hare)
    if s: walk(0, s, rng.uniform(60, 260), 1.8, .35, back=.01)
for _ in range(60):
    s = pick(w_fox)
    if s: walk(1, s, rng.uniform(150, 600), .36, .08, pair=.03)
for _ in range(25):
    s = pick(w_elk)
    if s: walk(2, s, rng.uniform(100, 400), .85, .12, pref=downhill_along_creek, pair=.22)
for _ in range(130):
    s = pick(w_taiga)
    if s: walk(3, s, rng.uniform(40, 150), .75, .45, pref=to_forest)
for _ in range(160):
    s = pick(w_taiga)
    if s: walk(4, s, rng.uniform(5, 30), .2, .3, pair=.04)
for _ in range(40):
    s = pick(w_tundra)
    if s: walk(5, s, rng.uniform(20, 60), .18, .35, zig=.06)
for _ in range(260):
    s = pick(w_taiga)
    if s: walk(6, s, rng.uniform(8, 30), .8, .6)
for _ in range(4):
    s = pick(w_elk)
    if s: walk(7, s, rng.uniform(500, 1400), .7, .06, pref=downhill_along_creek, pair=.04)

arr = np.array(out, dtype='<f4')
arr.tofile(f'{W}/tracks.f32')
print('tracks', len(arr), np.bincount(arr[:, 3].astype(int), minlength=8))

"""Builds Assets/Data/World/elbrus from the DEM fetched by fetch_in_browser.js (elbrus section).

Input (working directory):
  elbrus-dem-2049-i16.bin   2049x2049 int16 metres, row 0 = SOUTH edge, 6 m spacing,
                            resampled from the AWS "terrarium" terrain tiles (zoom 14, ~7 m/px,
                            SRTM/ASTER derived) onto the local frame of elbrus_geo.py.

What it does
  * lifts the two summit domes by the documented amount (radar DEMs read ~15 m low on the snow caps
    of Elbrus: 5627 vs 5642 m on the West summit, 5607 vs 5621 m on the East one);
  * takes out the pixel noise of the resampled tiles (the source posting is ~30 m anyway);
  * derives the two surface masks the terrain splat needs: bare lava rock and ash/moraine;
  * writes height_2049.r16 (uint16, row 0 = south) + rock_1025.r8 + ash_1025.r8 + _meta.json;
  * prints the check table (station and hut elevations against published figures).

Run: python3 elbrus.py
"""
import json
import math
import os

import numpy as np
from scipy import ndimage as nd

from elbrus_geo import N, STEP, HALF, ll2xz

SRC = 'elbrus-dem-2049-i16.bin'
OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Data', 'World', 'elbrus')

# published elevations used as checks (OSM ele=* / survey figures, see docs/ELBRUS.md)
CHECKS = [
    ('Западная вершина', 43.352410, 42.437843, 5642),
    ('Восточная вершина', 43.346794, 42.453904, 5621),
    ('Седловина', 43.350413, 42.447307, 5416),
    ('Скалы Пастухова', 43.331069, 42.458679, 4650),
    ('Приют 11 / Дизель-хат', 43.313898, 42.459270, 4050),
    ('LeapRus', 43.309686, 42.457207, 3912),
    ('Гара-Баши (станция)', 43.304232, 42.460208, 3847),
    ('Бочки', 43.298937, 42.464064, 3750),
    ('Мир (станция)', 43.289537, 42.460352, 3455),
    ('Старый Кругозор', 43.274188, 42.461509, 2970),
    ('Азау (станция)', 43.266242, 42.477855, 2350),
]

# snow-cap correction: (lat, lon, published, sigma metres)
DOMES = [(43.352410, 42.437843, 5642, 420.0), (43.346794, 42.453904, 5621, 380.0)]


def grid_xz():
    a = (np.arange(N) * STEP - HALF)
    return np.meshgrid(a, a)  # x (east, columns), z (north, rows)


def main():
    d = np.fromfile(SRC, np.int16).reshape(N, N).astype(np.float64)

    # 1. de-noise: the tiles are a ~7 m resample of a ~30 m posting, so a 12 m gaussian removes
    #    only interpolation ringing and keeps every real landform.
    d = nd.gaussian_filter(d, 2.0)

    # 2. snow-cap correction on the two summits
    X, Z = grid_xz()
    for lat, lon, published, sigma in DOMES:
        sx, sz = ll2xz(lat, lon)
        r2 = (X - sx) ** 2 + (Z - sz) ** 2
        here = d[int(round((sz + HALF) / STEP)), int(round((sx + HALF) / STEP))]
        d += (published - here) * np.exp(-r2 / (2 * sigma ** 2))

    # 3. surface masks
    gz, gx = np.gradient(nd.gaussian_filter(d, 1.0), STEP)
    slope = np.degrees(np.arctan(np.hypot(gx, gz)))
    resid = d - nd.gaussian_filter(d, 5)
    rough = np.sqrt(nd.uniform_filter(resid ** 2, 7))
    elev = d

    # bare lava rock: steep or broken ground. Above the firn line only what is too steep to hold snow
    # stays black (Pastukhov rocks, the ribs above Priut, the crags under Mir); below it, rock is common.
    snowline = np.clip((elev - 3450) / 500.0, 0, 1)           # 0 below 3450 m, 1 above 3950 m
    rock = np.clip((rough - 0.5) / 1.6, 0, 1)
    rock = np.maximum(rock, np.clip((slope - 27) / 13.0, 0, 1))
    rock *= (1 - 0.72 * snowline)
    rock = np.maximum(rock, np.clip((slope - 38) / 8.0, 0, 1))  # cliffs stay bare at any height
    rock = nd.gaussian_filter(rock, 1.5)

    # ash and moraine: the flat-to-gentle volcanic debris of the lower slopes and the lateral moraines
    # of the Garabashi / Terskol glaciers — wind-scoured, snow-free in places.
    ash = np.clip(1 - rock * 1.4, 0, 1) * np.clip((3550 - elev) / 700.0, 0, 1) * np.clip((22 - slope) / 14.0, 0, 1)
    ash = nd.gaussian_filter(np.clip(ash, 0, 1), 2)

    os.makedirs(OUT, exist_ok=True)
    lo = math.floor(d.min()) - 1
    hi = math.ceil(d.max()) + 1
    q = np.round((d - lo) / (hi - lo) * 65535).astype('<u2')
    q.tofile(os.path.join(OUT, 'height_2049.r16'))
    (np.clip(rock[::2, ::2], 0, 1) * 255).astype(np.uint8).tofile(os.path.join(OUT, 'rock_1025.r8'))
    (np.clip(ash[::2, ::2], 0, 1) * 255).astype(np.uint8).tofile(os.path.join(OUT, 'ash_1025.r8'))
    json.dump({'min': lo, 'max': hi, 'n': N, 'step': STEP}, open(os.path.join(OUT, '_meta.json'), 'w'))

    def at(lat, lon):
        x, z = ll2xz(lat, lon)
        return d[int(round((z + HALF) / STEP)), int(round((x + HALF) / STEP))]

    print(f'height range {lo} … {hi} m')
    print(f'{"точка":24} {"DEM":>7} {"источник":>9} {"Δ":>6}')
    for name, lat, lon, ref in CHECKS:
        h = at(lat, lon)
        print(f'{name:24} {h:7.0f} {ref:9} {h - ref:6.0f}')
    for f in sorted(os.listdir(OUT)):
        print(f, os.path.getsize(os.path.join(OUT, f)))


if __name__ == '__main__':
    main()

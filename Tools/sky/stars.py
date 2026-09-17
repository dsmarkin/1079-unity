"""Star field for the night of 1–2 February 1959 (Tools/sky → Assets/Data/Sky/stars.f32).

Source: Yale Bright Star Catalogue, 5th revised ed. (Hoffleit & Warren, 1991), JSON copy
https://github.com/brettonw/YaleBrightStarCatalog (bsc5-short.json: RA/Dec J2000, V magnitude, temperature K).
Stars down to V 6.0 (what the eye sees on a dark mountain night) are precessed to the equinox of 1959.1 with PyEphem.
Output records (little-endian float32): x, y, z (unit vector in the equatorial frame of date: x → RA 0h, z → north pole), V, T(K).
Usage: python3 Tools/sky/stars.py path/to/bsc5-short.json   (pip install ephem)
"""
import json, math, re, sys
import ephem
import numpy as np

src = sys.argv[1] if len(sys.argv) > 1 else 'bsc5-short.json'
rows = json.load(open(src))


def hms(s):
    h, m, sec = [float(x) for x in re.findall(r'[\d.]+', s)]
    return (h + m / 60 + sec / 3600) * 15


def dms(s):
    sign = -1 if s.strip().startswith('-') else 1
    d, m, sec = [float(x) for x in re.findall(r'[\d.]+', s)]
    return sign * (d + m / 60 + sec / 3600)


out = []
for r in rows:
    try:
        v = float(r['V'])
    except (KeyError, ValueError):
        continue
    if v > 6.0:
        continue
    ra, dec = hms(r['RA']), dms(r['Dec'])
    eq = ephem.Equatorial(math.radians(ra), math.radians(dec), epoch=ephem.J2000)
    eq59 = ephem.Equatorial(eq, epoch='1959/2/1')
    a, d = float(eq59.ra), float(eq59.dec)
    k = float(r.get('K') or 6000)
    out.append((math.cos(d) * math.cos(a), math.cos(d) * math.sin(a), math.sin(d), v, k))

arr = np.array(out, dtype='<f4')
arr.tofile('Assets/Data/Sky/stars.f32')
print('stars', len(arr))
gp = ephem.Equatorial(ephem.Galactic(0, math.radians(90), epoch=ephem.J2000), epoch='1959/2/1')
gc = ephem.Equatorial(ephem.Galactic(0, 0, epoch=ephem.J2000), epoch='1959/2/1')
for name, e in (('galactic pole', gp), ('galactic centre', gc)):
    a, d = float(e.ra), float(e.dec)
    print(name, 'x y z =', round(math.cos(d) * math.cos(a), 5), round(math.cos(d) * math.sin(a), 5), round(math.sin(d), 5))

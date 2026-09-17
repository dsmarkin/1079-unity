"""Local frame of the Elbrus location: ellipsoidal equirectangular about the centre of the map.

x = east, z = north, metres; identical maths to Height1079.Core.Elbrus.Project (and to geo.py of the
Kholat map, except that z is not flipped here: row 0 of every raster is the southern edge).
"""
import math

A = 6378137.0
F = 1 / 298.257223563
E2 = F * (2 - F)
D = math.pi / 180

LAT0 = 43.30910          # centre of the playable square, between Azau and the West summit
LON0 = 42.45857
N = 2049                 # height grid nodes
STEP = 6.0               # metres between nodes
HALF = (N - 1) * STEP / 2  # 6144 m

M0 = A * (1 - E2) / (1 - E2 * math.sin(LAT0 * D) ** 2) ** 1.5


def Nr(lat):
    return A / math.sqrt(1 - E2 * math.sin(lat * D) ** 2)


def ll2xz(lat, lon):
    """WGS84 lat/lon -> local (x east, z north) in metres."""
    return ((lon - LON0) * D * Nr(lat) * math.cos(lat * D), (lat - LAT0) * D * M0)


def xz2ll(x, z):
    lat = LAT0 + z / M0 / D
    return lat, LON0 + x / (Nr(lat) * math.cos(lat * D)) / D


def dms(d, m, s):
    return d + m / 60 + s / 3600

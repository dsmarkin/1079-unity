import numpy as np, math
a=6378137; f=1/298.257223563; e2=f*(2-f); D=math.pi/180
LAT0=61.756; LON0=59.4425; N=2049; STEP=2.0; HALF=2048.0
M0=a*(1-e2)/(1-e2*math.sin(LAT0*D)**2)**1.5
def Nr(lat): return a/math.sqrt(1-e2*math.sin(lat*D)**2)
def ll2xz(lat,lon):
    return ((lon-LON0)*D*Nr(lat)*math.cos(lat*D), -(lat-LAT0)*D*M0)
def xz2ll(x,z):
    lat=LAT0-z/M0/D; return lat, LON0+x/(Nr(lat)*math.cos(lat*D))/D
def dms(d,m,s): return d+m/60+s/3600

"""Builds Assets/Data/World from the two rasters fetched by fetch_in_browser.js.

Inputs (in the working directory):
  1079-terrain-2m-dem-f32.bin  ArcticDEM v4.1 2 m mosaic (WGS84 ellipsoidal heights), 2049x2049 float32, row 0 = north
  1079-terrain-2m-chm-u8.bin   Meta/WRI 1 m canopy height (m), nearest-sampled to the same grid
Steps: +2.337 m EGM96 geoid (npm egm96-universal; constant within 0.03 m over the area), canopy bump removal under forest,
tree detection (local maxima of the canopy model >= 3 m in a 10 m window), rock mask (roughness/steepness), export.
Run: python3 package.py, then route.py (ascent line) and analysis.py (saddle, ridge profile).
"""
import numpy as np, math
from scipy import ndimage as nd
N=2049
d=np.fromfile('1079-terrain-2m-dem-f32.bin',np.float32).reshape(N,N)+2.337
c=np.fromfile('1079-terrain-2m-chm-u8.bin',np.uint8).reshape(N,N)
np.save('dem.npy',d); np.save('chm.npy',c)
res=d-nd.gaussian_filter(d,4); np.save('rough.npy',np.sqrt(nd.uniform_filter(res**2,5)).astype(np.float32))
np.save('forest.npy',nd.gaussian_filter((c>3).astype(np.float32),3))
mx=nd.maximum_filter(c.astype(np.float32),size=5); peaks=(c==mx)&(c>=3)
lab,npk=nd.label(peaks); cen=np.array(nd.center_of_mass(peaks,lab,range(1,npk+1)))
hts=c[cen[:,0].round().astype(int),cen[:,1].round().astype(int)]
np.save('trees.npy',np.c_[cen[:,1]*2-2048,cen[:,0]*2-2048,hts])
from geo import *
from scipy import ndimage as nd
import json, hashlib, os
d=np.load('dem.npy').astype(np.float64); c=np.load('chm.npy'); forest=np.load('forest.npy'); rough=np.load('rough.npy')
ops=nd.gaussian_filter(nd.grey_opening(d,size=(9,9)),2)
ground=d-forest*np.clip(d-ops,0,None)
out='pkg'; os.makedirs(out,exist_ok=True)
MIN=math.floor(ground.min())-1; MAX=math.ceil(ground.max())+1
q=np.round((ground-MIN)/(MAX-MIN)*65535).astype('<u2')
# row0 = south (Unity +z north): our arrays have row0 = north (z south = -HALF) -> flip
q[::-1].tofile(f'{out}/height_2049.r16')
c[::-1].astype(np.uint8).tofile(f'{out}/canopy_2049.r8')
# 4 m for the browser prototype (row0 = north, as before in the web z-south frame)
q4=q[::2,::2]; q4.tofile(f'{out}/height_1025_north_up.r16')
# rock mask (open ground, rough or steep), 1025, row0 south
gz,gx=np.gradient(nd.gaussian_filter(ground,1.5),2.0); slope=np.degrees(np.arctan(np.hypot(gx,gz)))
rock=np.clip((rough-0.25)/0.35,0,1)*np.clip(1-forest*2,0,1); rock=np.maximum(rock,np.clip((slope-32)/10,0,1))
rock=nd.gaussian_filter(rock,1)[::2,::2]
(np.clip(rock,0,1)*255).astype(np.uint8)[::-1].tofile(f'{out}/rock_1025.r8')
# trees (x east, z NORTH), height, filtered
t=np.load('trees.npy'); x,zs,h=t.T
j=np.clip(((zs+HALF)/2).round().astype(int),0,N-1); i=np.clip(((x+HALF)/2).round().astype(int),0,N-1)
el=ground[j,i]; keep=(el<820)&(np.abs(x)<HALF-2)&(np.abs(zs)<HALF-2)
rng=np.random.default_rng(1959)
x=x[keep]+rng.uniform(-.9,.9,keep.sum()); zs=zs[keep]+rng.uniform(-.9,.9,keep.sum()); h=h[keep]; el=el[keep]
# species (conditional on documented species list; mix is an assumption)
u=rng.random(len(x)); sp=np.zeros(len(x),np.uint8)  # 0 spruce 1 fir 2 birch 3 cedar(Siberian pine)
high=(el>700)|(h<7)
sp[high&(u<.45)]=2; sp[high&(u>=.45)&(u<.7)]=1
sp[~high&(u<.45)]=0; sp[~high&(u>=.45)&(u<.8)]=1; sp[~high&(u>=.8)&(u<.93)]=2; sp[~high&(u>=.93)]=3
sp[high&(u>=.7)]=0
arr=np.c_[x,-zs,h,sp].astype('<f4'); arr.tofile(f'{out}/trees.f32')
print('trees',len(arr),np.bincount(sp))
json.dump({'min':MIN,'max':MAX,'count':len(arr)},open(f'{out}/_meta.json','w'))
print(MIN,MAX)
# cedar canopy height
cx,cz=ll2xz(dms(61,45,53.2),dms(59,27,17.8)); jj=int((cz+HALF)/2); ii=int((cx+HALF)/2)
print('cedar chm max 6m', c[jj-3:jj+4,ii-3:ii+4].max(), 'ground',ground[jj,ii])
for f in sorted(os.listdir(out)): print(f, os.path.getsize(f'{out}/{f}'))

# Stream network for the map (run flow.py first → acc4.npy): log-scaled drainage area ≥ 2·10⁴ m², 1025², row 0 = south.
import os
if os.path.exists('acc4.npy'):
    acc = np.load('acc4.npy')
    v = np.clip((np.log10(acc) - 4.3) / (7.0 - 4.3), 0, 1); v[acc < 2e4] = 0
    (v * 255).astype(np.uint8)[::-1].tofile(f'{out}/streams_1025.r8')

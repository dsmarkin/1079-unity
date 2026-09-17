from geo import *
from scipy import ndimage as nd
import heapq, json
d=np.load('dem.npy'); c=np.load('chm.npy').astype(np.float32)
res=d-nd.gaussian_filter(d,4)
rough=np.sqrt(nd.uniform_filter(res**2,5))
forest=nd.gaussian_filter((c>3).astype(np.float32),3)
np.save('rough.npy',rough.astype(np.float32)); np.save('forest.npy',forest.astype(np.float32))
print('rough pct open',np.percentile(rough[forest<.05],[50,90,99]))
def xz(p): return ll2xz(*p)
tent=xz((61.758561,59.429436)); cedar=xz((dms(61,45,53.2),dms(59,27,17.8)))
kol=xz((61.76217,59.44458)); slo=xz((61.76266,59.44727)); dya=xz((61.76351,59.45018))
line=[tent,kol,slo,dya,cedar]
# profile along line
prof=[]; dist=0
for a,b in zip(line,line[1:]):
    L=math.hypot(b[0]-a[0],b[1]-a[1])
    for t in np.arange(0,L,2):
        x=a[0]+(b[0]-a[0])*t/L; z=a[1]+(b[1]-a[1])*t/L
        j=int((z+HALF)/2); i=int((x+HALF)/2); prof.append((dist+t,x,z,d[j,i],rough[j,i]))
    dist+=L
prof=np.array(prof)
sm=nd.gaussian_filter1d(prof[:,4],4)
pk=[k for k in range(1,len(sm)-1) if sm[k]>sm[k-1] and sm[k]>=sm[k+1] and sm[k]>np.percentile(rough[forest<.05],90)]
print('rough peaks along footprint line (m from tent, elev, rough):',[(int(prof[k,0]),int(prof[k,3]),round(sm[k],2)) for k in pk])
json.dump({'footline':[list(map(float,p)) for p in line],'ridges':[[float(prof[k,0]),float(prof[k,1]),float(prof[k,2])] for k in pk]},open('lines.json','w'))
from geo import *
from scipy import ndimage as nd
import heapq, json
d=np.load('dem.npy'); c=np.load('chm.npy').astype(np.float32); forest=np.load('forest.npy')
# saddle: along row band between summit (-1056,111)... find min-max path summit->905 height. find 905 peak: local max east
ds=nd.gaussian_filter(d,3)
mx=nd.maximum_filter(ds,size=151)
pk=np.argwhere((ds==mx)&(ds>850))
for j,i in pk: print('peak',-HALF+i*2,-HALF+j*2,round(float(ds[j,i]),1),xz2ll(-HALF+i*2,-HALF+j*2))
g=ds[::2,::2]; n=g.shape[0]
s=(int((172+HALF)/4),int((-1296+HALF)/4)); t=(int((-1050+HALF)/4),n-1)
best=np.full((n,n),-1e9); best[s]=g[s]; prev={}
pq=[(-g[s],s)]; done=np.zeros((n,n),bool)
while pq:
    v,(a,b)=heapq.heappop(pq); v=-v
    if done[a,b]: continue
    done[a,b]=True
    if (a,b)==t: break
    for da in (-1,0,1):
        for db in (-1,0,1):
            x,y=a+da,b+db
            if (da or db) and 0<=x<n and 0<=y<n and not done[x,y]:
                w=min(v,g[x,y])
                if w>best[x,y]: best[x,y]=w; prev[(x,y)]=(a,b); heapq.heappush(pq,(-w,(x,y)))
p=t; path=[]
while p!=s: path.append(p); p=prev[p]
path=path[::-1]
el=[g[q] for q in path]; k=int(np.argmin(el)); q=path[k]
sx,sz=-HALF+q[1]*4,-HALF+q[0]*4
print('saddle',sx,sz,el[k],xz2ll(sx,sz))
json.dump({'saddle':[sx,sz,float(el[k])]},open('saddle.json','w'))

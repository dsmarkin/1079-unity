from geo import *
from scipy import ndimage as nd
import heapq, json
d=np.load('dem.npy'); forest=np.load('forest.npy')
ds=nd.gaussian_filter(d,2)[::2,::2]; f=forest[::2,::2]; n=ds.shape[0]
def cell(p): x,z=ll2xz(*p); return (int(round((z+HALF)/4)),int(round((x+HALF)/4)))
S=cell((61.746694,59.449444)); T=cell((61.758561,59.429436))
dist=np.full((n,n),np.inf); dist[S]=0; prev=-np.ones((n,n,2),int); pq=[(0,S)]
while pq:
    c0,(a,b)=heapq.heappop(pq)
    if c0>dist[a,b]: continue
    if (a,b)==T: break
    for da in (-1,0,1):
        for db in (-1,0,1):
            if not(da or db): continue
            x,y=a+da,b+db
            if not(0<=x<n and 0<=y<n): continue
            L=4*math.hypot(da,db); dh=ds[x,y]-ds[a,b]; gr=dh/L
            # ski touring cost: uphill penalised quadratically; dense forest a bit slower
            cost=L*(1+(abs(gr)/0.18)**2+0.3*f[x,y])+ (1e3 if abs(gr)>0.6 else 0)
            nc=c0+cost
            if nc<dist[x,y]: dist[x,y]=nc; prev[x,y]=(a,b); heapq.heappush(pq,(nc,(x,y)))
p=T; path=[]
while tuple(p)!=S: path.append(p); p=tuple(prev[p])
path.append(S); path=path[::-1]
pts=[(-HALF+q[1]*4,-HALF+q[0]*4) for q in path][::5]+[(-HALF+T[1]*4,-HALF+T[0]*4)]
L=sum(math.hypot(b[0]-a[0],b[1]-a[1]) for a,b in zip(pts,pts[1:]))
print('route len',L,'points',len(pts))
json.dump({'route_labaz_tent':pts},open('route.json','w'))

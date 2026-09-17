import numpy as np, heapq, time
from geo import *
d=np.load('dem.npy').astype(np.float64)
g=d[::2,::2].copy()  # 4 m grid, 1025
n=g.shape[0]
t=time.time()
filled=g.copy(); closed=np.zeros_like(g,bool); pq=[]
for i in range(n):
    for j in (0,n-1):
        for (a,b) in ((i,j),(j,i)):
            if not closed[a,b]: closed[a,b]=True; heapq.heappush(pq,(g[a,b],a,b))
nb=[(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]
eps=1e-4
while pq:
    h,a,b=heapq.heappop(pq)
    for da,db in nb:
        x,y=a+da,b+db
        if 0<=x<n and 0<=y<n and not closed[x,y]:
            closed[x,y]=True
            if filled[x,y]<=h: filled[x,y]=h+eps
            heapq.heappush(pq,(filled[x,y],x,y))
print('fill',time.time()-t)
# D8 receivers
pad=np.pad(filled,1,constant_values=-1e9)
best=np.zeros((n,n)); rec=np.full((n,n),-1,np.int64)
idx=np.arange(n*n).reshape(n,n)
for k,(da,db) in enumerate(nb):
    dist=4*np.hypot(da,db)
    nbh=pad[1+da:1+da+n,1+db:1+db+n]
    s=(filled-nbh)/dist
    ia=np.clip(np.arange(n)[:,None]+da,0,n-1); ib=np.clip(np.arange(n)[None,:]+db,0,n-1)
    m=s>best; best[m]=s[m]; rec[m]=(ia*n+ib+0*idx)[m] if False else (np.broadcast_to(ia,(n,n))*n+np.broadcast_to(ib,(n,n)))[m]
order=np.argsort(-filled,axis=None)
acc=np.ones(n*n); r=rec.ravel()
for c in order:
    if r[c]>=0: acc[r[c]]+=acc[c]
acc=acc.reshape(n,n)*16  # m2
np.save('acc4.npy',acc); np.save('filled4.npy',filled)
print('done',time.time()-t, acc.max()/1e6)

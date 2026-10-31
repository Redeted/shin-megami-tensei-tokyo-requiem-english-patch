"""One line per floor: floor number, size, highest chip id, % solid.
usage: map_summary.py <dir from unpack_chapters.py>"""
import sys,glob,os,io
sys.stdout=io.TextIOWrapper(sys.stdout.buffer,encoding='utf-8')
B=sys.argv[1]
HIT=[104,127,127,127,127,120,120]
def load(p):
    d=open(p,'rb').read()
    fl=d[0] if d[0]<128 else d[0]-256
    w,h=d[1],d[2]
    n=w*h
    disp=[(d[3+i*2],d[3+i*2+1]) for i in range(n)]
    cnt=1+sum(1 for a,b in disp if b)
    o=3+n*2
    info=[tuple(d[o+i*4:o+i*4+4]) for i in range(cnt)]
    return fl,w,h,disp,info
def solid(c):
    return bool(HIT[c//7]&(1<<(c%7))) if c<49 else None
for ch in range(7):
    for p in sorted(glob.glob(f'{B}/c{ch}_10/f*.bin'),key=lambda s:int(os.path.basename(s)[1:-4])):
        fl,w,h,disp,info=load(p)
        mx=max(a for a,b in disp)
        print(f'c{ch}',os.path.basename(p),'floor',fl,f'{w}x{h}','maxchip',mx,'solid%',round(100*sum(solid(a) or 0 for a,b in disp)/(w*h)))

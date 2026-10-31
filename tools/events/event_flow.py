"""Full-opcode disassembly (all ops + args, strings shortened) of event*.bin. Jump arguments are relative: the op after the jump counts as 1.
usage: event_flow.py <c*_20 dir>"""
import sys, glob, os
LEN = [1,1,1,2,2,2,-1,1,2,2, 3,3,2,2,2,2,2,2,8,2, 2,2,2,4,2,2,2,2,2,3, 3,-1,1,1,2,2,2,1,1,4, 4,2,3,-2,3,2,1,1,1,1, 1]
NAME = {6:'MSG',31:'NAR',43:'SEL',7:'END',8:'FLAG+',9:'FLAG-',10:'IF_FLAG',11:'IFNOT_FLAG',27:'LOCAL+',28:'LOCAL-',29:'IF_LOCAL',30:'IFNOT_LOCAL',
41:'GOTO',42:'CHECKPARAM',44:'SELSKIP',18:'BATTLE',45:'UPDOWN',46:'DOWNLOAD',47:'SAVE',48:'CHECKDL',49:'GETMAG',36:'NEXTSTEP',35:'PARAM',24:'MAPMOVE',23:'GETDEVIL',21:'GETITEM',22:'GETMONEY',32:'FADEOUT',33:'FADEIN'}
SKIP = {0,1,2,3,4,5,12,13,14,15,16,17,19,20,25,26,32,33,38,39,40}
def cstr(d,p):
    e=d.index(b'\0',p); return d[p:e].decode('cp932'),e+1
for f in sorted(glob.glob(os.path.join(sys.argv[1],'event*.bin'))):
    d=open(f,'rb').read(); p=0; step=0
    print('==',os.path.basename(f),len(d))
    while p<len(d):
        op=d[p]; at=p; step+=1
        if op>=len(LEN): print('BAD',op); break
        n=LEN[op]
        if n in(-1,-2):
            p+=1; ss=[]
            for _ in range(2 if n==-1 else 4):
                s,p=cstr(d,p); ss.append(s)
            print(step,NAME[op],' | '.join(x[:28] for x in ss))
        else:
            a=list(d[p+1:p+n]); p+=n
            if op not in SKIP: print(step,NAME.get(op,'op%d'%op),a)
        if op==7: break

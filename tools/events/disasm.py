"""Disassemble Tokyo Requiem event*.bin (lengths from MegatenCanvas.Event_GotoStep) and dump the text.
usage: disasm.py <unz dir like .../c0_20> <out.tsv>
out columns: event, step, op, speaker, text   (op: msg = window, nar = full-screen narration, sel = choices)"""
import sys, glob, os
LEN = [1,1,1,2,2,2,-1,1,2,2, 3,3,2,2,2,2,2,2,8,2, 2,2,2,4,2,2,2,2,2,3, 3,-1,1,1,2,2,2,1,1,4, 4,2,3,-2,3,2,1,1,1,1, 1]
def cstr(d, p):
    e = d.index(b'\0', p); return d[p:e].decode('cp932'), e + 1
src, out = sys.argv[1], sys.argv[2]
rows, bad = [], []
for f in sorted(glob.glob(os.path.join(src, 'event*.bin'))):
    ev = os.path.basename(f)[5:8]; d = open(f, 'rb').read(); p = 0; step = 0
    while p < len(d):
        op = d[p]; step += 1
        if op >= len(LEN): bad.append((ev, p, op)); break
        n = LEN[op]
        if n in (-1, -2):
            p += 1; ss = []
            for _ in range(2 if n == -1 else 4):
                s, p = cstr(d, p); ss.append(s)
            if n == -1: rows.append((ev, step, 'msg' if op == 6 else 'nar', ss[0], ss[1]))
            else: rows.append((ev, step, 'sel', '', ' | '.join(ss)))
        else: p += n
        if op == 7: break
with open(out, 'w', encoding='utf-8', newline='\n') as o:
    for r in rows: o.write('\t'.join(map(str, r)) + '\n')
print(len(rows), 'rows; bad:', bad, '; chars:', sum(len(r[4]) for r in rows))

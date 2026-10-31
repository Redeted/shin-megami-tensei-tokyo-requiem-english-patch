"""Check every story row in the disasm.py dumps against the string tables: a translation exists, ending narration
fits 33 chars and 17 lines per row, and choice menus fit their 214 px (12 px full-width, 6 px half-width).
usage: check_events.py <strings dir> <folder with c0.tsv ... c6.tsv>"""
import glob, os, sys
sys.stdout.reconfigure(encoding="utf-8")
T = {}
for f in glob.glob(os.path.join(sys.argv[1], "*.tsv")):
    for l in open(f, encoding="utf-8"):
        p = l.rstrip("\n").split("\t")
        if not l.startswith("#") and len(p) > 1:
            T[p[0]] = p[1].strip('"') if p[1].startswith('"') else p[1]
width = lambda s: sum(12 if ord(c) > 0x7f and not 0xff61 <= ord(c) <= 0xff9f else 6 for c in s)
problems = rows = 0
for f in sorted(glob.glob(os.path.join(sys.argv[2], "c[0-9].tsv"))):
    for l in open(f, encoding="utf-8"):
        ev, step, op, spk, text = (l.rstrip("\n").split("\t") + [""] * 5)[:5]
        rows += 1
        if op == "sel":
            if "【" in text:
                for o in filter(None, (x.strip() for x in text.split(" | ")[1:])):
                    if o not in T or width(T[o]) > 214:
                        problems += 1; print(os.path.basename(f), ev, step, "choice:", o, T.get(o))
            continue
        en = T.get(text)
        if en is None:
            problems += 1; print(os.path.basename(f), ev, step, "missing:", text); continue
        if op == "nar":
            segs = en.replace("@YOUNAME", "N" * 8).replace("@HEROINE", "N" * 8).split("\\n")
            if len(segs) > 17 or any(len(s) > 33 for s in segs):
                problems += 1; print(os.path.basename(f), ev, step, "narration too long:", en)
print(rows, "rows,", problems, "problems")

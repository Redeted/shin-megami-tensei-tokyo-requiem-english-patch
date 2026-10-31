"""Dump the 30-byte item records from megaten001.bin. usage: item_records.py <megaten001.bin> <strings/names_item.tsv>"""
import sys
sys.stdout.reconfigure(encoding="utf-8")
d = open(sys.argv[1], "rb").read()
names = {}
for l in open(sys.argv[2], encoding="utf-8"):
    p = l.rstrip("\n").split("\t")
    if not l.startswith("#") and len(p) >= 3: names[int(p[0])] = p[2]
BASE = 0xd335
for i in range(155):
    b = d[BASE + i * 30:BASE + i * 30 + 30]
    print(i, b[0], names.get(i, "?"), "kind", b[17] & 0xF, "attr", b[17] >> 4, "b18-22", list(b[18:23]), "buy", (b[23] | b[24] << 8) * 10, "rest", list(b[25:30]), sep="\t")

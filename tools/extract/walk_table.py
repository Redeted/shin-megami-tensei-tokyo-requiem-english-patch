"""Walk a fixed-stride table of [id byte][16-byte SJIS name] records outward from a known name offset.
usage: walk_table.py <file> <name_offset_hex> <stride> [out.tsv]"""
import sys

sys.stdout.reconfigure(encoding="utf-8")
f, off, stride = sys.argv[1], int(sys.argv[2], 16), int(sys.argv[3])
d = open(f, "rb").read()


def name_at(p):
    if p < 1 or p + 16 > len(d):
        return None
    raw = d[p:p + 16].split(b"\0")[0]
    if not raw or d[p + len(raw):p + 16].strip(b"\0"):
        return None
    try:
        s = raw.decode("cp932")
    except UnicodeDecodeError:
        return None
    return s if all(c.isprintable() for c in s) else None


start = off
while name_at(start - stride) is not None:
    start -= stride
rows, p = [], start
while name_at(p) is not None:
    rows.append((p, d[p - 1], name_at(p)))
    p += stride
for p, i, s in rows:
    print(f"{p:#07x}\t{i:3d}\t{s}")
print(f"-- {len(rows)} records", file=sys.stderr)
if len(sys.argv) > 4:
    with open(sys.argv[4], "w", encoding="utf-8") as o:
        o.write("offset\tid\tjp\n")
        for p, i, s in rows:
            o.write(f"{p:#x}\t{i}\t{s}\n")

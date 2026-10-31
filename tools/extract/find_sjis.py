"""Search files for a Japanese string encoded as Shift-JIS (and UTF-16LE / UTF-8), also inside zlib/deflate streams.
usage: find_sjis.py <text> <file> [<file> ...]"""
import sys, zlib

sys.stdout.reconfigure(encoding="utf-8")
text = sys.argv[1]
pats = {"sjis": text.encode("cp932"), "utf16": text.encode("utf-16-le"), "utf8": text.encode("utf-8")}


def scan(label, data):
    for enc, p in pats.items():
        i = data.find(p)
        while i != -1:
            print(f"{label}: {enc} @ {i:#x}")
            i = data.find(p, i + 1)


for f in sys.argv[2:]:
    try:
        data = open(f, "rb").read()
    except OSError:
        continue
    scan(f, data)
    # try raw deflate / zlib at every plausible offset of small files only
    if len(data) < 400_000:
        for wbits in (15, -15):
            for off in range(0, min(len(data), 64)):
                try:
                    d = zlib.decompressobj(wbits).decompress(data[off:])
                except zlib.error:
                    continue
                if len(d) > 256:
                    scan(f"{f} [inflate wbits={wbits} off={off} -> {len(d)}B]", d)
                    break

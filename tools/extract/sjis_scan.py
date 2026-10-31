"""Find runs of Shift-JIS Japanese text in binary files. Prints per-file counts and sample strings.
usage: sjis_scan.py <min_chars> <samples> <file> [<file> ...]"""
import sys, re

sys.stdout.reconfigure(encoding="utf-8")
JP = re.compile(r'[぀-ヿ一-鿿！-ﾟ]')


def runs(b):
    i, n, cur, start = 0, len(b), [], 0
    while i < n:
        c = b[i]
        if (0x81 <= c <= 0x9f or 0xe0 <= c <= 0xfc) and i + 1 < n:
            try:
                ch = b[i:i + 2].decode("cp932")
                if not cur:
                    start = i
                cur.append(ch); i += 2; continue
            except UnicodeDecodeError:
                pass
        elif 0x20 <= c < 0x7f and cur:
            cur.append(chr(c)); i += 1; continue
        if cur:
            yield start, "".join(cur)
        cur = []; i += 1
    if cur:
        yield start, "".join(cur)


mn, ns = int(sys.argv[1]), int(sys.argv[2])
for f in sys.argv[3:]:
    found = [(o, s) for o, s in runs(open(f, "rb").read()) if len(JP.findall(s)) >= mn]
    print(f"== {f.split('/')[-1]}: {len(found)} strings, {sum(len(s) for _, s in found)} chars")
    for o, s in found[:ns]:
        print(f"   {o:#07x}: {s}")

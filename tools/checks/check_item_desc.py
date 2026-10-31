# usage: check_item_desc.py <strings/item_desc.tsv>
# Simulate Wrap.Apply (half-width 6 px, box text width 209 px) and flag descriptions needing more than 2 lines.
import sys

MAX_PX, HALF = 209, 6

def wrap(s):
    lines = []
    for part in s.replace("\\n", "\n").split("\n"):
        cur = ""
        for word in part.split(" "):
            cand = word if cur == "" else cur + " " + word
            if len(cand) * HALF > MAX_PX and cur:
                lines.append(cur)
                cur = word
            else:
                cur = cand
        lines.append(cur)
    return lines

bad = 0
for l in open(sys.argv[1], encoding="utf-8"):
    if not l.startswith("@desc"):
        continue
    key, text = l.rstrip("\n").split("\t")[:2]
    lines = wrap(text)
    if len(lines) > 2:
        bad += 1
        print(f"{key}: {len(lines)} lines: {lines}")
print(f"{bad} over two lines")

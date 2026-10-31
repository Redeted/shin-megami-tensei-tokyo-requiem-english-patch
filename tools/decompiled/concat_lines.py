"""List C# lines that build strings by concatenating Japanese literals with expressions.
usage: concat_lines.py <file.cs> [out.tsv]   (prints count + per-method histogram)"""
import re, sys, collections

sys.stdout.reconfigure(encoding="utf-8")
JP = re.compile(r'[぀-ヿ一-鿿！-ﾟ]')
LIT = r'"(?:[^"\\]|\\.)*"'
CONCAT = re.compile(rf'({LIT}\s*\+|\+\s*{LIT})')
METHOD = re.compile(r'^\s*public (?:virtual |override |static )*[\w<>\[\],]+ (\w+)\(')
rows, method = [], "?"
for n, line in enumerate(open(sys.argv[1], encoding="utf-8"), 1):
    m = METHOD.match(line)
    if m:
        method = m.group(1)
    if CONCAT.search(line) and any(JP.search(s) for s in re.findall(LIT, line)):
        rows.append((n, method, line.strip()))
hist = collections.Counter(m for _, m, _ in rows)
print(f"{len(rows)} concatenation lines in {len(hist)} methods")
for m, c in hist.most_common(40):
    print(f"  {c:4} {m}")
if len(sys.argv) > 2:
    with open(sys.argv[2], "w", encoding="utf-8") as o:
        for n, m, l in rows:
            o.write(f"{n}\t{m}\t{l}\n")

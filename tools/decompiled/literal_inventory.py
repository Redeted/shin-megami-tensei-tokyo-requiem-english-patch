"""Inventory of Japanese string literals per method, minus what the translation tables already cover.
usage: literal_inventory.py <file.cs> <strings_dir> <out.tsv>
Output: line TAB method TAB literal (unique literals, first occurrence), plus a per-method count summary."""
import sys, re, os, glob, collections

sys.stdout.reconfigure(encoding="utf-8")
src, sdir, out = sys.argv[1:4]
JP = re.compile(r'[぀-ヿ一-鿿！-ﾟ]')
LIT = re.compile(r'"((?:[^"\\]|\\.)*)"')
METHOD = re.compile(r'^\s*public (?:virtual |override |static )*[\w<>\[\],]+ (\w+)\(')

known = set()
for f in glob.glob(os.path.join(sdir, "*.tsv")):
    if os.path.basename(f).startswith(("names_", "patterns_")):
        continue
    for l in open(f, encoding="utf-8"):
        if l.startswith("#") or "\t" not in l:
            continue
        known.add(l.split("\t", 1)[0].replace("\\n", "\n"))

rows, seen, method = [], set(), "?"
for n, line in enumerate(open(src, encoding="utf-8"), 1):
    m = METHOD.match(line)
    if m:
        method = m.group(1)
    for lm in LIT.finditer(line):
        s = lm.group(1).encode("utf-8").decode("unicode_escape").encode("latin-1", "ignore").decode("utf-8", "ignore") if "\\u" in lm.group(1) else lm.group(1)
        if not JP.search(s) or s in seen:
            continue
        seen.add(s)
        rows.append((n, method, s, s.replace("\\n", "\n") in known))
todo = [r for r in rows if not r[3]]
with open(out, "w", encoding="utf-8") as o:
    for n, m, s, _ in todo:
        o.write(f"{n}\t{m}\t{s}\n")
print(f"{len(rows)} unique JP literals; {len(rows) - len(todo)} already in tables; {len(todo)} to do")
for m, c in collections.Counter(r[1] for r in todo).most_common(60):
    print(f"  {c:4} {m}")

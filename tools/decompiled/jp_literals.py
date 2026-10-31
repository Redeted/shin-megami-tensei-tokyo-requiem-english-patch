"""List Japanese string literals in a decompiled C# file: count, unique count, and a sample with line numbers."""
import re, sys, collections
sys.stdout.reconfigure(encoding="utf-8")

path = sys.argv[1]
JP = re.compile(r'[　-ヿ一-鿿＀-￯]')
LIT = re.compile(r'"((?:[^"\\]|\\.)*)"')
hits = []
for n, line in enumerate(open(path, encoding="utf-8"), 1):
    for m in LIT.finditer(line):
        if JP.search(m.group(1)):
            hits.append((n, m.group(1)))
uniq = collections.OrderedDict()
for n, s in hits:
    uniq.setdefault(s, n)
print(f"{len(hits)} JP literals, {len(uniq)} unique, {sum(len(s) for s in uniq)} chars")
for s, n in list(uniq.items())[: int(sys.argv[2]) if len(sys.argv) > 2 else 60]:
    print(f"{n:6}: {s}")

"""Search raw (decompressed) object data in Unity bundles for a UTF-8 string; print readable UTF-8 runs around hits.
usage: grep_bundles.py <text> <bundle> [<bundle> ...]"""
import sys, os, re
import UnityPy

sys.stdout.reconfigure(encoding="utf-8")
needle = sys.argv[1].encode("utf-8")
for f in sys.argv[2:]:
    env = UnityPy.load(f)
    for obj in env.objects:
        try:
            raw = obj.get_raw_data()
        except Exception:
            continue
        i = raw.find(needle)
        if i < 0:
            continue
        print(f"== {os.path.basename(f)} {obj.type.name} path_id={obj.path_id} @ {i}")
        chunk = raw[max(0, i - 600): i + 900]
        # Unity strings are int32 length + UTF-8 bytes; print printable UTF-8 runs
        for m in re.finditer(rb"(?:[\x20-\x7e\n]|[\xc2-\xf4][\x80-\xbf]+){4,}", chunk):
            s = m.group().decode("utf-8", "replace")
            print("   ", s.replace("\n", "\\n"))

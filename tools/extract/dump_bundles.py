"""Inventory Unity bundles/assets and export TextAssets (raw bytes) to an output folder.
usage: dump_bundles.py <out_dir> <file> [<file> ...]"""
import sys, os, collections
import UnityPy

sys.stdout.reconfigure(encoding="utf-8")
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
for f in sys.argv[2:]:
    env = UnityPy.load(f)
    types = collections.Counter()
    print(f"\n== {os.path.basename(f)}")
    for obj in env.objects:
        t = obj.type.name
        types[t] += 1
        if t in ("TextAsset", "MonoBehaviour"):
            try:
                d = obj.read()
                name = getattr(d, "m_Name", "") or f"pid{obj.path_id}"
            except Exception as e:
                print(f"   ! {t} {obj.path_id}: {e}")
                continue
            if t == "TextAsset":
                data = d.m_Script
                data = data.encode("utf-8", "surrogateescape") if isinstance(data, str) else bytes(data)
                sub = os.path.join(out, os.path.basename(f).split("_assets")[0])
                os.makedirs(sub, exist_ok=True)
                open(os.path.join(sub, name), "wb").write(data)
                print(f"   TextAsset {name}  {len(data)} bytes")
            else:
                script = ""
                try:
                    script = d.m_Script.read().m_Name
                except Exception:
                    pass
                if script and not script.startswith(("Image", "Text", "Canvas", "Button", "Layout", "Content", "Graphic", "Rect", "Mask", "Toggle", "Scroll", "EventSystem", "Outline", "Shadow")):
                    print(f"   MonoBehaviour {name} [{script}]")
    print("   types:", dict(types))

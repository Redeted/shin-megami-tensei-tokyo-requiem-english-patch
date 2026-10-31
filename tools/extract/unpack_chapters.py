"""Unzip the chapter archives exported by dump_bundles.py. chapN_megarep010.bin holds the maps (f*.bin) and
chapN_megarep020.bin the event scripts (event*.bin). The number is the zero-based chapter: chap0 becomes c0_10 and c0_20 (Chapter 1).
usage: unpack_chapters.py <folder with chap*.bin> <out_dir>"""
import glob, io, os, re, sys, zipfile
src, out = sys.argv[1:3]
for path in sorted(glob.glob(os.path.join(src, "chap*_megarep0[12]0.bin"))):
    m = re.search(r"chap(\d+)_megarep0([12])0", os.path.basename(path))
    data = open(path, "rb").read()
    if not zipfile.is_zipfile(io.BytesIO(data)):
        print("not a zip:", path); continue
    dest = os.path.join(out, f"c{int(m.group(1))}_{m.group(2)}0")
    zipfile.ZipFile(io.BytesIO(data)).extractall(dest)
    print(dest, len(os.listdir(dest)))

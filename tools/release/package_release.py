"""Build the player zip: BepInEx files, the plugin DLL and the string tables, laid out to extract into the game folder.
usage: package_release.py <folder with BepInEx installed> <TokyoRequiemEN.dll> <BepInEx LICENSE file> <out.zip>
Run from the repository root."""
import glob, os, sys, zipfile
src, dll, lic, out = sys.argv[1:5]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for name in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version"):
        z.write(os.path.join(src, name), name)
    for f in glob.glob(os.path.join(src, "BepInEx", "core", "*")):
        z.write(f, "BepInEx/core/" + os.path.basename(f))
    z.write(lic, "BepInEx/LICENSE-BepInEx.txt")
    z.write(dll, "BepInEx/plugins/TokyoRequiemEN/TokyoRequiemEN.dll")
    for f in glob.glob("strings/*.tsv"):
        z.write(f, "BepInEx/plugins/TokyoRequiemEN/strings/" + os.path.basename(f))
print(out, os.path.getsize(out), "bytes", len(zipfile.ZipFile(out).namelist()), "files")

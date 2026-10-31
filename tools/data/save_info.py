"""Read a save slot (save/game0-2): chapter, floor, level, alignment, and whether alignment is locked (global flag 255).
usage: save_info.py <save file> [...]"""
import sys
s8 = lambda b: b - 256 if b > 127 else b
for path in sys.argv[1:]:
    d = open(path, "rb").read()
    race = s8(d[526 + 238])
    side = "Law" if race >= 50 else "Chaos" if race <= -50 else "Neutral"
    print(path, "chapter", d[11] + 1, "floor", s8(d[517]), "Lv", d[526 + 18], "alignment", race, side, "locked", bool(d[448 + 31] & 0x80))

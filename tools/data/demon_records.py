"""Dump the 48-byte demon records from megaten001.bin, or only the given ids. Event battles scale HP further:
MaxHp + MaxHp * (chapter index + 1) / 2 (Battle_Initialize).
usage: demon_records.py <megaten001.bin> [id ...]"""
import sys
sys.stdout.reconfigure(encoding="utf-8")
d = open(sys.argv[1], "rb").read()
for i in map(int, sys.argv[2:]) if len(sys.argv) > 2 else range(1, 256):
    r = d[0xA336 + i * 48 - 1:0xA336 + i * 48 + 47]
    if r[1] == 0: continue
    print(i, r[1:17].split(b"\0")[0].decode("cp932", "replace"), "race", r[17], "lv", r[18], "hp", r[21] | r[22] << 8, "mp", r[23] | r[24] << 8, sep="\t")

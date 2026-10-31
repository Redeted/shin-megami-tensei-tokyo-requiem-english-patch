"""Draw one floor as text and list its special tiles. # wall, . floor, S stairs, Y camp, E event.
usage: map_view.py <f*.bin>"""
import sys
from mapfile import Map
m = Map(sys.argv[1])
print("floor", m.floor, f"{m.w}x{m.h}")
for y in range(m.h):
    row = ""
    for x in range(m.w):
        f = m.flags(x, y)[0]
        row += "S" if f & 1 else "E" if f & 4 else "Y" if f & 0x10 else "#" if m.solid(x, y) else "."
    print(f"{y:3} {row}")
for x, y, f in m.special():
    kind = "stairs to map %d (%d,%d)" % tuple(f[1:]) if f[0] & 1 else "event %d, local flag %d" % (f[1], f[2]) if f[0] & 4 else "camp"
    print(f"{x:3},{y:<3} {kind}")

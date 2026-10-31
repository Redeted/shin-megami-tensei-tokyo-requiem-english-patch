"""Flood fill: which event tiles can be reached from a start tile, optionally with tiles blocked? This shows which
events are mandatory gates. Event tiles count as walkable even when their chip is solid.
usage: reach.py <f*.bin> <x,y> [blocked x,y ...]"""
import sys
from collections import deque
from mapfile import Map
m = Map(sys.argv[1])
xy = lambda s: tuple(int(v) for v in s.split(","))
start, blocked = xy(sys.argv[2]), {xy(s) for s in sys.argv[3:]}
events = {(x, y): f[1] for x, y, f in m.special() if f[0] & 4}
seen, q = {start}, deque([start])
while q:
    x, y = q.popleft()
    for p in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
        if 0 <= p[0] < m.w and 0 <= p[1] < m.h and p not in seen and p not in blocked and (p in events or not m.solid(*p)):
            seen.add(p); q.append(p)
for p, ev in sorted(events.items(), key=lambda i: i[1]):
    print(f"event {ev:3} at {p}: {'reachable' if p in seen else 'NOT reachable'}")

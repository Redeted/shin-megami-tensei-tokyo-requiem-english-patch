"""Shared reader for the f*.bin map format: [floor][w][h], w*h pairs of (chip id, info index), then 4-byte info
records (flags, p0, p1, p2). Flags: 1 stairs, 4 event, 0x10 camp. Solid chips come from the game's Map_ChipHit mask."""
HIT = [104, 127, 127, 127, 127, 120, 120]
class Map:
    def __init__(self, path):
        d = open(path, "rb").read()
        self.floor = d[0] - 256 if d[0] > 127 else d[0]
        self.w, self.h = d[1], d[2]
        n = self.w * self.h
        self.cells = [(d[3 + i * 2], d[4 + i * 2]) for i in range(n)]
        count = 1 + sum(1 for _, k in self.cells if k)
        o = 3 + n * 2
        self.info = [list(d[o + i * 4:o + i * 4 + 4]) for i in range(count)]
    def cell(self, x, y): return self.cells[y * self.w + x]
    def flags(self, x, y): return self.info[self.cell(x, y)[1]]
    def solid(self, x, y):
        c = self.cell(x, y)[0]
        return bool(HIT[c // 7] & (1 << (c % 7)))
    def special(self):
        for y in range(self.h):
            for x in range(self.w):
                f = self.flags(x, y)
                if f[0] & (1 | 4 | 0x10):
                    yield x, y, f

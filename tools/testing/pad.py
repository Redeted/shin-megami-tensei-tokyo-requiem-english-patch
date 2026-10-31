"""Virtual DS4 pad server: reads commands from stdin, one per line.
  press <btn> [ms]   circle|cross|square|triangle|up|down|left|right|l1|r1|options|share
  wait <ms>
Keeps the pad plugged in between commands so the game doesn't drop it."""
import sys, time
import vgamepad as vg

B = vg.DS4_BUTTONS
D = vg.DS4_DPAD_DIRECTIONS
BTN = {"circle": B.DS4_BUTTON_CIRCLE, "cross": B.DS4_BUTTON_CROSS, "square": B.DS4_BUTTON_SQUARE,
       "triangle": B.DS4_BUTTON_TRIANGLE, "l1": B.DS4_BUTTON_SHOULDER_LEFT,
       "r1": B.DS4_BUTTON_SHOULDER_RIGHT, "options": B.DS4_BUTTON_OPTIONS, "share": B.DS4_BUTTON_SHARE}
DPAD = {"up": D.DS4_BUTTON_DPAD_NORTH, "down": D.DS4_BUTTON_DPAD_SOUTH,
        "left": D.DS4_BUTTON_DPAD_WEST, "right": D.DS4_BUTTON_DPAD_EAST}

pad = vg.VDS4Gamepad()
time.sleep(1.5)  # let the game enumerate the new device
print("ready", flush=True)
for line in sys.stdin:
    p = line.split()
    if not p:
        continue
    if p[0] == "wait":
        time.sleep(int(p[1]) / 1000)
    elif p[0] == "press":
        name, ms = p[1], int(p[2]) if len(p) > 2 else 120
        if name in DPAD:
            pad.directional_pad(DPAD[name]); pad.update(); time.sleep(ms / 1000)
            pad.directional_pad(D.DS4_BUTTON_DPAD_NONE)
        else:
            pad.press_button(BTN[name]); pad.update(); time.sleep(ms / 1000)
            pad.release_button(BTN[name])
        pad.update(); time.sleep(0.12)
    print("ok", line.strip(), flush=True)

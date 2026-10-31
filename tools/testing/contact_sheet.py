"""Contact sheet of phone-screen crops. usage: sheet.py <dir> <prefix> [cols]"""
import sys, glob, os
from PIL import Image, ImageDraw
d, prefix = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 6
files = sorted(f for f in glob.glob(os.path.join(d, prefix + "*.png")) if "small" not in f)
ims = []
for f in files:
    im = Image.open(f).convert("RGB")
    w, h = im.size
    im = im.crop((int(w*0.27), int(h*0.15), int(w*0.73), int(h*0.95)))
    im = im.resize((im.width*2//3, im.height*2//3))
    ImageDraw.Draw(im).text((4, 4), os.path.basename(f)[:-4], fill=(255, 255, 0))
    ims.append(im)
cw, ch = ims[0].size; rows = (len(ims)+cols-1)//cols
sheet = Image.new("RGB", (cols*cw, rows*ch))
for i, im in enumerate(ims): sheet.paste(im, ((i%cols)*cw, (i//cols)*ch))
sheet.save(os.path.join(d, prefix + "_sheet.png")); print(len(ims))

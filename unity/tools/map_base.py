"""Render Captures/map_base.png (+ _clean) from the Unity mask export (map_base.cs)."""
import numpy as np, sys
from PIL import Image, ImageDraw
from scipy import ndimage
cap = sys.argv[1] if len(sys.argv) > 1 else "../Captures/"
mk = [l.split() for l in open(cap + "map_markers.txt")]
W, H = int(mk[0][1]), int(mk[0][2])
m = np.fromfile(cap + "map_mask_0.raw", np.uint8).reshape(H, W, 3)
zone, wt, fl = m[..., 0].astype(int) - 1, m[..., 1] // 50, m[..., 2]
np.save(cap + "map_mask.npy", m)
ZC = np.array([(150, 140, 125), (120, 125, 95), (150, 120, 90), (95, 120, 110),
               (140, 135, 140), (105, 115, 80), (110, 125, 120), (165, 150, 115)], float)
img = np.zeros((H, W, 3), float)
inz = zone >= 0
img[inz] = ZC[zone[inz]]
dm = ndimage.distance_transform_edt(~inz)          # depth into the mountains (px)
mt = 75 - np.clip(dm / 60, 0, 1)[..., None] * 35
img[~inz] = np.broadcast_to(mt, (H, W, 1)).repeat(3, 2)[~inz] * np.array([1, .95, .88])
mas = (fl & 2) > 0; img[mas] = (70, 62, 55)
img[(wt == 1) | (wt == 2)] = (70, 105, 115)
img[wt == 3] = (130, 190, 50)
img[wt == 4] = (20, 20, 22)
img[(fl & 1) > 0] = (60, 40, 25)
coast = inz & ndimage.binary_dilation(~inz, iterations=2)
img[coast | ((fl & 4) > 0)] = (25, 20, 18)
base = Image.fromarray(img.astype(np.uint8))
base.save(cap + "map_base_clean.png")
d = ImageDraw.Draw(base)
col = {"gate": (230, 40, 40), "site": (240, 220, 60), "landmark": (255, 255, 255), "pod": (60, 200, 255),
       "arena": (200, 0, 160), "shrine": (255, 150, 0)}
for k, x, y in [(r[0], float(r[1]), float(r[2])) for r in mk[1:]]:
    c = col.get(k, (0, 0, 0)); rr = 9 if k in ("gate", "site", "arena") else 6
    if k.startswith("zone"): d.text((x - 10, y - 6), k.upper(), fill=(0, 0, 0)); continue
    d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=c, outline=(0, 0, 0))
base.save(cap + "map_base.png")
print(W, H)

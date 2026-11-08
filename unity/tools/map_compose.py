"""Compose Resources/UI/map_world.png: ChatGPT painting (over Captures/map_base_clean.png) re-inked with the exact layout.
usage: python map_compose.py <painting.png> [out.png]
The painting is resized to the mask size (same MapUV framing). Coastlines, water/toxic/oil and trails are drawn
again from Captures/map_mask.npy (map_base.py) so they sit exactly on the terrain; the painting supplies texture."""
import numpy as np, cv2, sys
from scipy import ndimage
cap = "../Captures/"
m = np.load(cap + "map_mask.npy"); H, W = m.shape[:2]
img = cv2.resize(cv2.imread(sys.argv[1]), (W, H), interpolation=cv2.INTER_LANCZOS4).astype(np.float32)
out = sys.argv[2] if len(sys.argv) > 2 else "../Assets/SOUL/Resources/UI/map_world.png"
zone = m[..., 0].astype(int) - 1; inz = zone >= 0; wt = m[..., 1] // 50; fl = m[..., 2]
lum = cv2.cvtColor(img.astype(np.uint8), cv2.COLOR_BGR2GRAY).astype(np.float32)[..., None] / 255
def soft(mask, blur=1.2): return cv2.GaussianBlur(mask.astype(np.float32), (0, 0), blur)[..., None]
def tint(mask, bgr, a, blur=1.2):   # colour wash that keeps the painting's light/dark texture
    global img; k = soft(mask, blur) * a; col = np.array(bgr, np.float32) * (.55 + .7 * lum)
    img = img * (1 - k) + col * k
# water bodies (drop single-cell puddle specks, keep everything a player can see from the map)
wet = ndimage.binary_opening((wt == 1) | (wt == 2), iterations=1)
tint(wet, (120, 110, 80), .45)
tint(wt == 3, (40, 200, 170), .6)
tint(wt == 4, (25, 25, 28), .75)
# trails: thin dashed ink along the real routes
tr = (fl & 1) > 0; tr = tr & ~ndimage.binary_erosion(tr, iterations=2) | ndimage.binary_erosion(tr, iterations=1)
yy, xx = np.mgrid[0:H, 0:W]; dash = ((xx + yy) % 14) < 9
tint(tr & dash, (30, 45, 65), .55, .7)
# coast + zone borders: ink line on the true outline
coast = (inz & ndimage.binary_dilation(~inz, iterations=2)) | ((fl & 4) > 0)
tint(coast, (25, 30, 40), .5, .9)
cv2.imwrite(out, np.clip(img, 0, 255).astype(np.uint8))
print("wrote", out, W, H)

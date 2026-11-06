using UnityEngine;

namespace Soul
{
    // Ground character per zone (지형 특색): patterns painted into the ground vertex colours on top of
    // the zone palette (Regions.Ground), so open ground never reads as bare dirt.
    //   Z0 ash ruins      broken pavement slabs + cracks, ash drifts, soot
    //   Z1 fallen city    asphalt slabs, pale concrete patches, rust stains
    //   Z2 skyscraper     violet dust, glass / steel glints, deep fissures
    //   Z3 scrap gorge    red-brown strata bands, rust flecks, oil pools
    //   Z4 war foundry    slag plates, sulfur crust, soot streaks
    //   Z5 dead woods     leaf litter mottling, mud, root lines
    //   Z6 toxic mire     wet mud, toxic green puddles, algae
    //   Z7 lab            concrete tiles with seams and stains
    // Purified zones keep their pattern faintly under moss. Irregular cells (Voronoi) instead of circles.
    public static class GroundLook
    {
        static Color H(int hex) => MeshBuilder.Hex(hex);
        static readonly Color Ash = H(0xa9a49c), Soot = H(0x2a2725), Asphalt = H(0x47454a), Concrete = H(0x8a8780), Rust = H(0x6e4a33),
            Violet = H(0x6a6478), Glint = H(0xa5a1b2), StrA = H(0x7a4a33), StrB = H(0x5c3624), StrC = H(0x8d5c3c), Oil = H(0x231c18),
            Slag = H(0x2e2622), Sulfur = H(0x8f7d3a), LeafA = H(0x6a4a2a), LeafB = H(0x4a3a28), Mud = H(0x2f2a22), Mire = H(0x262c1e),
            Toxic = H(0x5f8222), Algae = H(0x3d4a24), Tile = H(0x7d8284), Moss = H(0x6f8a3a);

        static float P(float x, float z, float f, float o) => Mathf.PerlinNoise(x * f + o, z * f + o * 1.37f);
        static float Hash(int x, int z, int k) { unchecked { int h = x * 73856093 ^ z * 19349663 ^ k * 83492791; h ^= h >> 13; h *= 1274126177; return ((h >> 8) & 0xffff) / 65535f; } }
        /// cellular noise: id of the nearest jittered cell point and the distance to the cell edge (m)
        static int Cells(float x, float z, float size, int k, out float edge)
        {
            float gx = x / size, gz = z / size; int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float f1 = 9, f2 = 9; int id = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                int cx = ix + dx, cz = iz + dz;
                float px = cx + .15f + .7f * Hash(cx, cz, k), pz = cz + .15f + .7f * Hash(cx, cz, k + 1);
                float d = (px - gx) * (px - gx) + (pz - gz) * (pz - gz);
                if (d < f1) { f2 = f1; f1 = d; id = cx * 7349 + cz * 3967; } else if (d < f2) f2 = d;
            }
            edge = (Mathf.Sqrt(f2) - Mathf.Sqrt(f1)) * size * .5f; return id;
        }
        static float Crack(float edge, float width) => 1 - Sim.SmoothStep(edge, width * .4f, width);

        public static Color Apply(int zone, float x, float z, Color c, bool healed)
        {
            float k = healed ? .45f : 1;   // purified: patterns soften under new growth
            switch (zone)
            {
                case 0:
                {
                    int id = Cells(x, z, 5.5f, 11, out float e);
                    float t = Hash(id, 1, 3);
                    c *= .8f + .38f * t;
                    if (t < .3f) c = Color.Lerp(c, Asphalt, .6f * k);
                    c = Color.Lerp(c, Soot, Crack(e, .8f) * .7f * k);
                    float drift = P(x * .4f, z, .03f, 4.1f) + .25f * P(x, z, .2f, 9.3f);
                    c = Color.Lerp(c, Ash, Sim.SmoothStep(drift, .66f, .9f) * .75f * k);
                    c = Color.Lerp(c, Soot, Sim.SmoothStep(P(x, z, .09f, 2.2f), .68f, .85f) * .35f * k);
                    break;
                }
                case 1:
                {
                    int id = Cells(x, z, 7f, 21, out float e); float t = Hash(id, 2, 5);
                    c = Color.Lerp(c, Asphalt, .55f * k) * (.8f + .38f * t);
                    if (t > .8f) c = Color.Lerp(c, Concrete, .7f * k);
                    c = Color.Lerp(c, Soot, Crack(e, .8f) * .7f * k);
                    c = Color.Lerp(c, Rust, Sim.SmoothStep(P(x, z, .11f, 6.6f), .74f, .9f) * .45f * k);
                    break;
                }
                case 2:
                {
                    Cells(x, z, 9f, 31, out float e);
                    c = Color.Lerp(c, Violet, .25f * k);
                    c = Color.Lerp(c, Soot, Crack(e, 1f) * .8f * k);
                    c = Color.Lerp(c, Glint, Sim.SmoothStep(P(x, z, .9f, 1.7f), .78f, .88f) * .5f * k);
                    break;
                }
                case 3:
                {
                    float band = Mathf.Sin(x * .32f + z * .11f + 4 * P(x, z, .02f, 7.7f));
                    var strata = band > .45f ? StrC : band > -.25f ? StrA : StrB;
                    c = Color.Lerp(c, strata, .75f * k);
                    c = Color.Lerp(c, Rust, Sim.SmoothStep(P(x, z, .6f, 3.9f), .74f, .84f) * .5f * k);
                    c = Color.Lerp(c, Oil, Sim.SmoothStep(P(x * 1.6f, z, .045f, 5.5f), .7f, .76f) * .75f * k);
                    break;
                }
                case 4:
                {
                    int id = Cells(x, z, 4f, 41, out float e); float t = Hash(id, 4, 7);
                    c = Color.Lerp(c, Slag, (.4f + .45f * t) * k);
                    c = Color.Lerp(c, Soot, Crack(e, .7f) * .7f * k);
                    c = Color.Lerp(c, Sulfur, Sim.SmoothStep(P(x, z, .07f, 8.8f), .72f, .8f) * .6f * k);
                    c = Color.Lerp(c, Soot, Sim.SmoothStep(P(x * .3f, z, .05f, 1.1f), .66f, .8f) * .35f * k);
                    break;
                }
                case 5:
                {
                    float leaf = P(x, z, .85f, 2.9f) + .5f * P(x, z, 2.1f, 6.1f);
                    c = Color.Lerp(c, leaf > .9f ? LeafA : LeafB, .7f * k);
                    c = Color.Lerp(c, Mud, Sim.SmoothStep(P(x, z, .06f, 4.4f), .64f, .78f) * .55f * k);
                    Cells(x, z, 3.2f, 51, out float e);
                    c = Color.Lerp(c, Soot, Crack(e, .55f) * .5f * k);
                    break;
                }
                case 6:
                {
                    c = Color.Lerp(c, Mire, .45f * k);
                    float pud = P(x, z, .07f, 3.3f) + .15f * P(x, z, .5f, 8.2f);
                    c = Color.Lerp(c, Toxic * 1.25f, Sim.SmoothStep(pud, .64f, .67f) * .9f * k);
                    c = Color.Lerp(c, Algae, Sim.SmoothStep(P(x, z, .3f, 1.9f), .62f, .8f) * .45f * k);
                    break;
                }
                case 7:
                {
                    const float T = 4; float fx = Mathf.Repeat(x, T), fz = Mathf.Repeat(z, T);
                    float seam = Mathf.Min(Mathf.Min(fx, T - fx), Mathf.Min(fz, T - fz));
                    float t = Hash(Mathf.FloorToInt(x / T), Mathf.FloorToInt(z / T), 71);
                    c = Color.Lerp(c, Tile, .45f * k) * (.93f + .12f * t);
                    c = Color.Lerp(c, Soot, (1 - Sim.SmoothStep(seam, .2f, .55f)) * .55f * k);
                    c = Color.Lerp(c, Rust, Sim.SmoothStep(P(x, z, .13f, 5.1f), .76f, .88f) * .35f * k);
                    break;
                }
            }
            if (healed) c = Color.Lerp(c, Moss, Sim.SmoothStep(P(x, z, .12f, 9.9f) + .3f * P(x, z, .5f, 2.2f), .55f, .95f) * .45f);
            c.a = 1; return c;
        }
    }
}

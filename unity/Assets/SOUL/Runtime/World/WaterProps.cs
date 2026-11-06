using UnityEngine;

namespace Soul
{
    // WATER EDGE PROPS (Codex-owned file). Placed by WorldStreamer along water shores (Terrain water depth
    // 0.03 – 0.2 m) so pools read clearly from the top-down camera. Decor only (no collision).
    //   Reeds(zone, seed): clump of reeds / cattails (mire, mud pools), 0.6 – 1.6 m, slightly leaning,
    //                      olive / straw colours; Z6 a sickly yellow-green, Z5 brown dead reeds
    //   Scum(zone, seed):  irregular floating algae / oil film patch lying flat on the surface (y 0.0 – 0.03
    //                      relative to the placement point, which is at the water surface); no round shapes
    // ≤ ~500 triangles each. Variants by `seed`. Return (cache id, builder) like TraceProps.
    public static class WaterProps
    {
        /// placeholder reeds: a few thin blades (Codex replaces)
        public static (string id, System.Func<int, PropModel> build) Reeds(int zone) => ("Reeds" + zone, s => PlaceholderReeds(s, zone));
        public static (string id, System.Func<int, PropModel> build) Scum(int zone) => ("Scum" + zone, s => PlaceholderScum(s, zone));

        static PropModel PlaceholderReeds(int seed, int zone)
        {
            var b = new MeshBuilder(seed); int n = b.RangeInt(7, 14);
            int hex = zone == 6 ? 0x7d8a3a : zone == 5 ? 0x6e5a3a : 0x6f7a46;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(b.Range(-.6f, .6f), 0, b.Range(-.6f, .6f)); float h = b.Range(.6f, 1.5f);
                var tip = p + new Vector3(b.Range(-.25f, .25f), h, b.Range(-.25f, .25f));
                b.Beam(p, tip, .025f, .008f, 3, b.Jitter(hex, .12f));
                if (b.Chance(.35f)) b.Beam(tip - (tip - p).normalized * .22f, tip - (tip - p).normalized * .02f, .045f, .04f, 4, b.Jitter(0x5a3c22, .1f));
            }
            return new PropModel { mesh = b.Build("reeds"), footprint = .8f, height = 1.5f };
        }
        static PropModel PlaceholderScum(int seed, int zone)
        {
            var b = new MeshBuilder(seed); int hex = zone == 4 ? 0x1a1a1c : 0x4f6a24;
            int n = b.RangeInt(5, 9); var c = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n, r0 = b.Range(.5f, 1.3f), r1 = b.Range(.5f, 1.3f);
                b.Tri(c + new Vector3(0, .015f, 0), new Vector3(Mathf.Sin(a0) * r0, .015f, Mathf.Cos(a0) * r0), new Vector3(Mathf.Sin(a1) * r1, .015f, Mathf.Cos(a1) * r1), b.Jitter(hex, .1f));
            }
            return new PropModel { mesh = b.Build("scum"), footprint = 1.2f, height = .05f };
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Open-world layout (sim space, metres). The original web arena sits at the centre of the
    // Ashen Ruins; three wild regions surround it in 120-degree sectors with noisy borders.
    //   0 Ashen Ruins  (r < ~95)     1 Ash Dunes (north, +Z)
    //   2 Scrap Gorge  (east/south-east)   3 Dead Woods (west/south-west)
    public class Scatter
    {
        public string id; public Func<int, PropModel> build; public float perChunk; public int variants = 6; public bool decor; public int pebbleHex;
    }
    public class Landmark { public string id; public Vector3 pos; public float yaw; public Func<int, PropModel> build; }
    public class RegionDef
    {
        public int index; public string name, title;
        public Color fog; public float fogDensity;
        public Color ambSky, ambEquator, ambGround, sun; public float sunIntensity;
        public Color ground, groundAlt, groundAccent;       // sRGB
        public List<Scatter> scatters = new List<Scatter>();
    }

    public static class Regions
    {
        public const float WORLD_RADIUS = 262;      // walkable
        public const float EDGE_RADIUS = 274;       // cliff ring
        public const float ARENA_HALF = 30;         // original web map ground (|x|,|z| < 30)
        static readonly float[] SectorCenter = { 0, 0, 120 * Mathf.Deg2Rad, -120 * Mathf.Deg2Rad };
        public static readonly List<RegionDef> All = new List<RegionDef>();
        public static readonly List<Landmark> Landmarks = new List<Landmark>();
        static Color H(int hex) => MeshBuilder.Hex(hex);

        static Regions()
        {
            All.Add(new RegionDef
            {
                index = 0, name = "잿빛 폐허", title = "ASHEN RUINS",
                fog = H(0x74726e), fogDensity = .024f, ambSky = H(0x98a9b4), ambGround = H(0x3a332e), sun = H(0xf4dcc4), sunIntensity = 2.1f,
                ground = H(0x73655d), groundAlt = H(0x63564e), groundAccent = H(0x4a3f38),   // matches the web arena soil (avg #73655d)
                scatters =
                {
                    S("RuinedBuilding", RegionProps.RuinedBuilding, 1.3f), S("ConcreteSlab", RegionProps.ConcreteSlab, 1.5f), S("RubblePile", RegionProps.RubblePile, 2.5f),
                    S("BurntCar", RegionProps.BurntCar, .8f), S("StreetLamp", RegionProps.StreetLamp, 1f), P(0x6f6a64, 4),
                },
            });
            All.Add(new RegionDef
            {
                index = 1, name = "재의 사막", title = "ASH DUNES",
                fog = H(0x9d9483), fogDensity = .017f, ambSky = H(0xa9b4b8), ambGround = H(0x5a5045), sun = H(0xffd6a6), sunIntensity = 2.6f,
                ground = H(0xa39a8a), groundAlt = H(0x8f877a), groundAccent = H(0xb3aa98),
                scatters =
                {
                    S("Dune", RegionProps.Dune, 2.4f), S("RockSpire", RegionProps.RockSpire, .8f), S("Pylon", RegionProps.Pylon, .25f), S("BoneRibs", RegionProps.BoneRibs, .4f), P(0x8f877a, 3),
                },
            });
            All.Add(new RegionDef
            {
                index = 2, name = "고철 협곡", title = "SCRAP GORGE",
                fog = H(0x7d5d47), fogDensity = .022f, ambSky = H(0xa08a7c), ambGround = H(0x3d2418), sun = H(0xff8a58), sunIntensity = 2.3f,
                ground = H(0x6e4430), groundAlt = H(0x54321f), groundAccent = H(0x3a2a22),
                scatters =
                {
                    S("ScrapHeap", RegionProps.ScrapHeap, 1.8f), S("CanyonWall", RegionProps.CanyonWall, .9f), S("ContainerStack", RegionProps.ContainerStack, .6f), S("PipeRun", RegionProps.PipeRun, .4f), P(0x5c3220, 3),
                },
            });
            All.Add(new RegionDef
            {
                index = 3, name = "죽은 숲", title = "DEAD WOODS",
                fog = H(0x5a625c), fogDensity = .022f, ambSky = H(0x7f8d93), ambGround = H(0x2c2a26), sun = H(0xc4cfd8), sunIntensity = 1.7f,
                ground = H(0x3a352f), groundAlt = H(0x2c2925), groundAccent = H(0x59604c),
                scatters =
                {
                    S("DeadTree", RegionProps.DeadTree, 6f), S("Stump", RegionProps.Stump, 2f), S("FallenLog", RegionProps.FallenLog, 1.2f), S("RootTangle", RegionProps.RootTangle, 1.5f, true), P(0x3a3430, 2),
                },
            });
            foreach (var r in All) r.ambEquator = Color.Lerp(r.ambSky, r.ambGround, .5f);
            Landmarks.Add(new Landmark { id = "RadioTower", pos = new Vector3(-45, 0, 55), yaw = .4f, build = RegionProps.RadioTower });
            Landmarks.Add(new Landmark { id = "CollapsedTower", pos = new Vector3(58, 0, -58), yaw = -.3f, build = RuinStructures.CollapsedTower });
            Landmarks.Add(new Landmark { id = "GiantRemains", pos = new Vector3(10, 0, 178), yaw = 2.3f, build = RegionProps.GiantRemains });
            Landmarks.Add(new Landmark { id = "CraneWreck", pos = new Vector3(150, 0, -88), yaw = -.7f, build = RegionProps.CraneWreck });
            Landmarks.Add(new Landmark { id = "GreatDeadTree", pos = new Vector3(-150, 0, -88), yaw = 1.1f, build = RegionProps.GreatDeadTree });
        }
        static Scatter S(string id, Func<int, PropModel> b, float perChunk, bool decor = false) => new Scatter { id = id, build = b, perChunk = perChunk, decor = decor };
        static Scatter P(int hex, float perChunk) => new Scatter { id = "Pebbles" + hex.ToString("x6"), build = s => RegionProps.Pebbles(s, hex), perChunk = perChunk, decor = true, pebbleHex = hex };

        static float N(float x, float z, float f, float ox, float oz) => (Mathf.PerlinNoise(x * f + ox, z * f + oz) - .5f) * 2;
        static float AngDist(float a, float b) => Mathf.Abs(Sim.WrapPi(a - b));
        /// smooth membership weights of the 4 regions at (x, z); sums to 1
        public static void Weights(float x, float z, float[] w)
        {
            float r = Mathf.Sqrt(x * x + z * z) + 16 * N(x, z, .012f, 3.1f, 7.7f);
            float th = Mathf.Atan2(x, z) + .32f * N(x, z, .008f, 11.3f, 5.9f);
            float ruins = 1 - Sim.SmoothStep(r, 82, 104);
            float sum = 0;
            for (int i = 1; i < 4; i++) { float d = AngDist(th, SectorCenter[i]) * Mathf.Rad2Deg; w[i] = 1 - Sim.SmoothStep(d, 50, 70); sum += w[i]; }
            for (int i = 1; i < 4; i++) w[i] = w[i] / Mathf.Max(1e-4f, sum) * (1 - ruins);
            w[0] = ruins;
        }
        static readonly float[] tmp = new float[4];
        public static int At(float x, float z) { Weights(x, z, tmp); int best = 0; for (int i = 1; i < 4; i++) if (tmp[i] > tmp[best]) best = i; return best; }
        public static int At(Vector3 p) => At(p.x, p.z);

        /// ground colour (sRGB) blended across regions, with patchy variation
        public static Color GroundColor(float x, float z, float[] w)
        {
            Weights(x, z, w);
            float n1 = Mathf.PerlinNoise(x * .07f + 40, z * .07f + 13), n2 = Mathf.PerlinNoise(x * .21f + 3, z * .21f + 91);
            Color c = Color.black;
            for (int i = 0; i < 4; i++)
            {
                if (w[i] < 1e-3f) continue; var r = All[i];
                var g = Color.Lerp(r.ground, r.groundAlt, Sim.SmoothStep(n1, .35f, .7f));
                g = Color.Lerp(g, r.groundAccent, Sim.SmoothStep(n2, .62f, .8f) * .6f);
                c += g * w[i];
            }
            float k = .93f + .14f * Mathf.PerlinNoise(x * .9f + 7, z * .9f + 2);
            return new Color(c.r * k, c.g * k, c.b * k, 1);
        }
    }
}

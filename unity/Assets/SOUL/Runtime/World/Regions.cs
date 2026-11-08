using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Look of each zone (index = ZoneGraph zone id): fog / ambient / sun, ground palette and the
    // procedural scatter. Where the zones are is ZoneWorld (board layout + border ridges).
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
        public List<Scatter> bloom = new List<Scatter>();   // extra decor once purified (own RNG, layout of the rest unchanged)
        public RegionDef healed;                             // look after the zone boss is purified
    }

    public static class Regions
    {
        public const float ARENA_HALF = 30;         // original web map ground (|x|,|z| < 30), centre of Z0
        public const float ENEMY_BOUNDS = 1300;     // radial clamp for enemies (the world rectangle does the real work)
        public static readonly List<RegionDef> All = new List<RegionDef>();
        public static readonly List<Landmark> Landmarks = new List<Landmark>();
        static Color H(int hex) => MeshBuilder.Hex(hex);

        static Regions()
        {
            // Z0 잿빛 폐허 (hub): the original look, matches the web arena soil (avg #73655d)
            Add(0, 0x74726e, .024f, 0x98a9b4, 0x3a332e, 0xf4dcc4, 2.1f, 0x73655d, 0x63564e, 0x4a3f38,
                S("RuinedBuilding", RegionProps.RuinedBuilding, 1.3f), S("ConcreteSlab", RegionProps.ConcreteSlab, 1.5f), S("RubblePile", RegionProps.RubblePile, 2.5f),
                S("BurntCar", RegionProps.BurntCar, .8f), S("StreetLamp", RegionProps.StreetLamp, 1f), P(0x6f6a64, 4));
            // Z1 무너진 도심: cooler asphalt grey, dense streets of wrecks (replaces the old Ash Dunes)
            Add(1, 0x6f7780, .021f, 0x9aa8b8, 0x34363a, 0xe8e2d6, 2.0f, 0x6a6866, 0x5a5958, 0x7a7570,
                S("RuinedBuilding", RegionProps.RuinedBuilding, 1.6f), S("ConcreteSlab", RegionProps.ConcreteSlab, 2f), S("RubblePile", RegionProps.RubblePile, 2.5f),
                S("BurntCar", RegionProps.BurntCar, 1.6f), S("StreetLamp", RegionProps.StreetLamp, 1.6f), S("Pylon", RegionProps.Pylon, .2f), P(0x66645f, 4));
            // Z2 마천루 묘지 (deep): dusk violet, toppled towers and bones
            Add(2, 0x5e5a66, .026f, 0x8a8a9e, 0x2a2830, 0xd8c8d8, 1.7f, 0x55525a, 0x47454c, 0x6b6670,
                S("ConcreteSlab", RegionProps.ConcreteSlab, 2.5f), S("RubblePile", RegionProps.RubblePile, 3f), S("RuinedBuilding", RegionProps.RuinedBuilding, 1f),
                S("BoneRibs", RegionProps.BoneRibs, .3f), S("RockSpire", RegionProps.RockSpire, .3f), P(0x5a5760, 4));
            // Z3 고철 협곡
            Add(3, 0x7d5d47, .022f, 0xa08a7c, 0x3d2418, 0xff8a58, 2.3f, 0x6e4430, 0x54321f, 0x3a2a22,
                S("ScrapHeap", RegionProps.ScrapHeap, 1.8f), S("CanyonWall", RegionProps.CanyonWall, .9f), S("ContainerStack", RegionProps.ContainerStack, .6f),
                S("PipeRun", RegionProps.PipeRun, .4f), S("RockSpire", RegionProps.RockSpire, .3f), P(0x5c3220, 3));
            // Z4 전쟁 기계 공장 (deep): furnace red, containers and pipe runs
            Add(4, 0x5e4136, .024f, 0x8e6f60, 0x2a1a14, 0xff7040, 2.4f, 0x4d3529, 0x3c2a20, 0x6a3a22,
                S("ContainerStack", RegionProps.ContainerStack, 1f), S("PipeRun", RegionProps.PipeRun, 1.2f), S("ScrapHeap", RegionProps.ScrapHeap, 1.4f),
                S("Pylon", RegionProps.Pylon, .6f), S("CanyonWall", RegionProps.CanyonWall, .3f), P(0x4a3428, 3));
            // Z5 죽은 숲
            Add(5, 0x5a625c, .022f, 0x7f8d93, 0x2c2a26, 0xc4cfd8, 1.7f, 0x3a352f, 0x2c2925, 0x59604c,
                S("DeadTree", RegionProps.DeadTree, 6f), S("Stump", RegionProps.Stump, 2f), S("FallenLog", RegionProps.FallenLog, 1.2f), S("RootTangle", RegionProps.RootTangle, 1.5f, true), P(0x3a3430, 2));
            // Z6 독성 늪 (deep): sickly green haze, drowned trees
            Add(6, 0x55663f, .028f, 0x7d9070, 0x1f2a18, 0xd6e6a0, 1.6f, 0x3c4630, 0x2f3826, 0x6b8a2a,
                S("DeadTree", RegionProps.DeadTree, 2.5f), S("Stump", RegionProps.Stump, 2f), S("FallenLog", RegionProps.FallenLog, 1f), S("RootTangle", RegionProps.RootTangle, 1.2f, true),
                S("BoneRibs", RegionProps.BoneRibs, .2f), P(0x3a4430, 2));
            // Z7 과학자의 연구소 (last): cold clinical grey-cyan
            Add(7, 0x6a7a80, .02f, 0xb0c4cc, 0x2c3236, 0xe0f0ff, 2.0f, 0x6c7072, 0x5e6366, 0x8a9496,
                S("ConcreteSlab", RegionProps.ConcreteSlab, 1.5f), S("Pylon", RegionProps.Pylon, .8f), S("PipeRun", RegionProps.PipeRun, .6f),
                S("RubblePile", RegionProps.RubblePile, 1f), S("ContainerStack", RegionProps.ContainerStack, .3f), P(0x70757a, 3));
            foreach (var r in All) foreach (var sc in r.scatters) if (sc.id == "RubblePile") sc.variants = 12;   // irregular heaps: more shapes, less repetition
            foreach (var r in All) { r.ambEquator = Color.Lerp(r.ambSky, r.ambGround, .5f); r.healed = Heal(r); }
            // Z0 tuned by hand: the ash clears to a pale sky, warm sun, moss through the cracks
            { var h = All[0].healed; h.fog = H(0xb9c2c0); h.fogDensity = .0085f; h.ambSky = H(0xb4cfe0); h.ambGround = H(0x4c5236); h.sun = H(0xffe9c8); h.sunIntensity = 2.7f;
              h.ground = H(0x6c6a4e); h.groundAlt = H(0x5a6040); h.groundAccent = H(0x7b9640); h.ambEquator = Color.Lerp(h.ambSky, h.ambGround, .5f); }
            // landmarks, placed relative to their zone centre
            LM("RadioTower", 0, -45, 55, .4f, RegionProps.RadioTower);
            LM("CollapsedTower", 0, 58, -58, -.3f, RuinStructures.CollapsedTower);
            LM("CollapsedTower", 2, 30, -60, 1.2f, RuinStructures.CollapsedTower);
            LM("GiantRemains", 2, -20, 140, 2.3f, RegionProps.GiantRemains);
            LM("CraneWreck", 4, 0, 0, -.7f, RegionProps.CraneWreck);
            LM("GreatDeadTree", 5, 20, 10, 1.1f, RegionProps.GreatDeadTree);
            LM("RadioTower", 7, 120, -40, 2.0f, RegionProps.RadioTower);
            // route guiding: the next zone's silhouette just past every passage (ZoneRoutes)
            foreach (var (pos, yaw, zone) in ZoneRoutes.PassageLandmarks()) { var m = TraceProps.Landmark(zone); Landmarks.Add(new Landmark { id = m.id, pos = pos, yaw = yaw, build = m.build }); }
        }
        static RegionDef Heal(RegionDef r)
        {
            Color L(Color a, int hex, float k) => Color.Lerp(a, H(hex), k);
            var h = new RegionDef { index = r.index, name = r.name, title = r.title,
                fog = L(r.fog, 0xc6d0cc, .55f), fogDensity = r.fogDensity * .4f, ambSky = L(r.ambSky, 0xbcd6e8, .5f), ambGround = L(r.ambGround, 0x4a5a32, .5f),
                sun = L(r.sun, 0xfff0d0, .5f), sunIntensity = r.sunIntensity * 1.25f,
                ground = L(r.ground, 0x5e6b3c, .4f), groundAlt = L(r.groundAlt, 0x4f5e33, .4f), groundAccent = H(0x7c9a40) };
            h.ambEquator = Color.Lerp(h.ambSky, h.ambGround, .5f);
            h.scatters = r.scatters;
            h.bloom.Add(S("Grass", ZoneProps.Grass, 9f, true)); h.bloom.Add(S("Flowers", ZoneProps.Flowers, 2.5f, true));
            return h;
        }
        /// the look that applies to a zone right now (purified or not)
        public static RegionDef Look(int zone) { var r = All[Mathf.Clamp(zone, 0, All.Count - 1)]; return ZoneState.Purified(r.index) ? r.healed : r; }

        static void Add(int zone, int fog, float density, int sky, int gnd, int sun, float sunI, int ground, int alt, int accent, params Scatter[] scatters)
        {
            var z = ZoneGraph.Zones[zone];
            var r = new RegionDef { index = zone, name = z.name, title = z.title, fog = H(fog), fogDensity = density, ambSky = H(sky), ambGround = H(gnd), sun = H(sun), sunIntensity = sunI,
                ground = H(ground), groundAlt = H(alt), groundAccent = H(accent) };
            r.scatters.AddRange(scatters); All.Add(r);
        }
        static void LM(string id, int zone, float dx, float dz, float yaw, Func<int, PropModel> build)
        {
            var c = ZoneWorld.Center(zone); Landmarks.Add(new Landmark { id = id, pos = new Vector3(c.x + dx, 0, c.y + dz), yaw = yaw, build = build });
        }
        static Scatter S(string id, Func<int, PropModel> b, float perChunk, bool decor = false) => new Scatter { id = id, build = b, perChunk = perChunk, decor = decor };
        static Scatter P(int hex, float perChunk) => new Scatter { id = "Pebbles" + hex.ToString("x6"), build = s => RegionProps.Pebbles(s, hex), perChunk = perChunk, decor = true, pebbleHex = hex };

        /// zone whose look applies at (x, z) (outside the board: the zone across the edge)
        public static int At(float x, float z) { int self = ZoneWorld.Sample(x, z, out int other, out _); return self >= 0 ? self : Mathf.Max(0, other); }
        public static int At(Vector3 p) => At(p.x, p.z);

        static Color Ground(RegionDef r, float n1, float n2)
        {
            var g = Color.Lerp(r.ground, r.groundAlt, Sim.SmoothStep(n1, .35f, .7f));
            return Color.Lerp(g, r.groundAccent, Sim.SmoothStep(n2, .62f, .8f) * .6f);
        }
        /// ground colour (sRGB) with patchy variation, blended toward the neighbouring zone near a border
        public static Color GroundColor(float x, float z)
        {
            int self = ZoneWorld.Sample(x, z, out int other, out float d);
            if (self < 0) { self = Mathf.Max(0, other); other = self; }
            float n1 = Mathf.PerlinNoise(x * .07f + 40, z * .07f + 13), n2 = Mathf.PerlinNoise(x * .21f + 3, z * .21f + 91);
            var c = GroundLook.Apply(self, x, z, Ground(Look(self), n1, n2), ZoneState.Purified(self));
            if (other >= 0 && other != self)
            {
                float wo = .5f * (1 - Sim.SmoothStep(d, 0, 26));
                if (wo > .01f) c = Color.Lerp(c, GroundLook.Apply(other, x, z, Ground(Look(other), n1, n2), ZoneState.Purified(other)), wo);
            }
            float tw = ZoneRoutes.TrailWeight(x, z);
            if (tw > 0) c = Color.Lerp(c, ZoneRoutes.TrailColor(self), tw * (ZoneState.Purified(self) ? .55f : .8f));
            float k = .93f + .14f * Mathf.PerlinNoise(x * .9f + 7, z * .9f + 2);
            // soaked bank around water and the darker bed under it, so the shore line reads from above
            float wet = Terrain.Wet(x, z);
            if (wet > 0)
            {
                float under = Terrain.Water(x, z, out _);
                k *= 1 - .38f * wet - .25f * Mathf.Clamp01(under / .3f);
                c = Color.Lerp(c, new Color(c.r * .85f, c.g * .95f, c.b * .8f), wet);
            }
            return new Color(c.r * k, c.g * k, c.b * k, 1);
        }
    }
}

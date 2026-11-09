using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Streams the open world in 32 m chunks around the player: ground mesh (zone-blended
    // vertex colours), border ridges + link gates (ZoneWorld / ZoneGates), scattered zone props
    // with collision and landmarks. Deterministic per chunk (seeded by its coordinates), so a chunk always
    // regenerates identically. Prop meshes are cached per (type, variant).
    public class WorldStreamer
    {
        public const float CHUNK = 32, CELL = 1;   // 1 m ground grid: room for the zone ground patterns (GroundLook)
        public int loadRadius = 3, unloadRadius = 4;
        readonly Transform root;
        readonly Material propMat, groundMat, ridgeMat;
        readonly Dictionary<int, Material> glowMats = new Dictionary<int, Material>();
        readonly List<(GameObject go, int part)> gateSolids = new List<(GameObject, int)>();
        readonly Dictionary<Vector2Int, GameObject> loaded = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<string, PropModel> cache = new Dictionary<string, PropModel>();
        public int ChunkCount => loaded.Count;
        public int PropCount;

        public WorldStreamer(Transform sceneRoot)
        {
            root = sceneRoot;
            propMat = Mats.Lit(Color.white, Color.black, .12f, .12f, true); propMat.SetFloat("_Cutaway", 1);
            groundMat = Mats.Lit(Color.white, Color.black, 0, .05f, true); groundMat.SetFloat("_CutCap", 1);   // shelves above the player on the camera side are squashed to head height (a sliced dark top), not clipped: a height field has nothing below to show
            ridgeMat = Mats.Lit(Color.white, Color.black, 0, .06f, true); ridgeMat.SetFloat("_Cutaway", 1);
            WorldCollision.chunkSize = CHUNK; WorldCollision.Clear();
        }

        PropModel Model(string id, System.Func<int, PropModel> build, int variant)
        {
            string key = id + "#" + variant;
            if (!cache.TryGetValue(key, out var m)) { m = build(Hash(id.GetHashCode(), variant, 7)); if (m.mesh) m.mesh.name = key; cache[key] = m; }
            return m;
        }
        static int Hash(int a, int b, int c) { unchecked { int h = (int)2166136261; h = (h ^ a) * 16777619; h = (h ^ b) * 16777619; h = (h ^ c) * 16777619; return h & 0x7fffffff; } }

        // chunk builds are stepped jobs (Build yields between stages) run under a per-frame time budget, nearest
        // first; chunks next to the player's own always finish at once (collision must be there before you are)
        public float budgetMs = 4;
        readonly List<(Vector2Int k, IEnumerator<bool> job)> jobs = new List<(Vector2Int, IEnumerator<bool>)>();
        readonly System.Diagnostics.Stopwatch frameWatch = new System.Diagnostics.Stopwatch();
        public int PendingBuilds => jobs.Count;
        double stepMean = 1;
        bool warmed; public static double PrewarmMs;
        /// build every structure / landmark model once, on the first full load: a first build costs 30-120 ms,
        /// far over a streaming frame budget (scatter models are small and stay lazy)
        void Prewarm()
        {
            warmed = true; var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var kv in PlotTypes) foreach (var t in kv.Value.types) for (int v = 0; v < 6; v++) Model(t.id, t.build, v);
            foreach (var lm in Regions.Landmarks) Model(lm.id, lm.build, 0);
            foreach (var site in ZoneWorld.Sites) ZoneGates.Get(site);
            PrewarmMs = sw.Elapsed.TotalMilliseconds;
        }
        /// call every frame with the player's sim position; `immediate` builds everything now
        public void Update(Vector3 center, bool immediate = false)
        {
            if (immediate && !warmed) Prewarm();
            frameWatch.Restart();
            Terrain.Focus(center);
            var c = WorldCollision.KeyOf(center.x, center.z);
            var todo = new List<Vector2Int>();
            for (int dx = -loadRadius; dx <= loadRadius; dx++) for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                if (!loaded.ContainsKey(k) && InWorld(k)) todo.Add(k);
            }
            todo.Sort((a, b) => (a - c).sqrMagnitude.CompareTo((b - c).sqrMagnitude));
            foreach (var k in todo) if (!jobs.Exists(j => j.k == k)) jobs.Add((k, Build(k)));
            jobs.Sort((a, b) => (a.k - c).sqrMagnitude.CompareTo((b.k - c).sqrMagnitude));
            // a step starts only if a typical step (running mean) still fits the budget; one step per frame always runs
            bool ran = false;
            for (int i = 0; i < jobs.Count; i++)
            {
                var (k, job) = jobs[i];
                bool urgent = immediate || (Mathf.Abs(k.x - c.x) <= 1 && Mathf.Abs(k.y - c.y) <= 1);
                bool Fits() => urgent || !ran || frameWatch.Elapsed.TotalMilliseconds + stepMean <= budgetMs;
                if (!Fits()) break;
                bool more;
                do
                {
                    double t0 = frameWatch.Elapsed.TotalMilliseconds; more = job.MoveNext(); ran = true;
                    stepMean = Mathf.Lerp((float)stepMean, (float)(frameWatch.Elapsed.TotalMilliseconds - t0), .1f);
                } while (more && Fits());
                if (!more) { jobs.RemoveAt(i); i--; }
            }
            var drop = new List<Vector2Int>();
            foreach (var k in loaded.Keys) if (Mathf.Abs(k.x - c.x) > unloadRadius || Mathf.Abs(k.y - c.y) > unloadRadius) drop.Add(k);
            jobs.RemoveAll(j => drop.Contains(j.k));
            // wall gates vanish once their part is owned (Shift+1..6 toggles them live)
            for (int i = gateSolids.Count - 1; i >= 0; i--)
            {
                var (go, part) = gateSolids[i];
                if (!go) { gateSolids.RemoveAt(i); continue; }
                bool show = !Parts.Has(part); if (go.activeSelf != show) go.SetActive(show);
            }
            foreach (var k in drop)
            {
                var gt = loaded[k].transform.Find("ground"); if (gt) Object.Destroy(gt.GetComponent<MeshFilter>().sharedMesh);
                loaded[k].SetActive(false); Object.Destroy(loaded[k]); loaded.Remove(k); WorldCollision.Unregister(k);
            }
        }
        /// drop every chunk and rebuild around the player now (a zone's look changed)
        public void RebuildAll(Vector3 center)
        {
            foreach (var kv in loaded)
            {
                var gt = kv.Value.transform.Find("ground"); if (gt) Object.Destroy(gt.GetComponent<MeshFilter>().sharedMesh);
                kv.Value.SetActive(false); Object.Destroy(kv.Value); WorldCollision.Unregister(kv.Key);   // hide now: Destroy waits for the end of the frame
            }
            loaded.Clear(); gateSolids.Clear(); jobs.Clear();
            Update(center, true);
        }
        static bool InWorld(Vector2Int k)
        {
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK;
            const float M = 40;
            return x0 + CHUNK > ZoneWorld.MinX - M && x0 < ZoneWorld.MaxX + M && z0 + CHUNK > ZoneWorld.MinZ - M && z0 < ZoneWorld.MaxZ + M;
        }

        /// time per build stage (ms, summed over all chunks built; Prof[PROF_N] = chunks) for tests
        public static readonly double[] Prof = new double[PROF_N + 1];
        public const int PROF_N = 8;   // 0 terrain tiles, 1 ground mesh, 2 ground detail, 3 water, 4 ridge/gates/landmarks/plots, 5 scatter, 6 trail props, 7 reeds/dressing/collision
        /// slowest single step so far (ms) and its stage, for tests
        public static double MaxStep; public static int MaxStepStage;
        static long Lap(int stage, long t0)
        {
            long t = System.Diagnostics.Stopwatch.GetTimestamp(); double ms = (t - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Prof[stage] += ms; if (ms > MaxStep) { MaxStep = ms; MaxStepStage = stage; }
            return t;
        }
        /// one chunk, built in steps (each `yield` hands the frame back when the streaming budget is spent)
        IEnumerator<bool> Build(Vector2Int k)
        {
            var go = new GameObject($"chunk {k.x},{k.y}"); go.transform.SetParent(root, false);
            loaded[k] = go; Prof[PROF_N]++;
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK;
            long t = System.Diagnostics.Stopwatch.GetTimestamp();
            // terrain tiles this chunk samples (its own + neighbours: ground shading reads 2.5 m across the edge), one per step
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                var tk = new Vector2Int(k.x + dx, k.y + dz);
                foreach (var _ in Terrain.BuildSteps(tk, 4)) { t = Lap(0, t); yield return true; t = System.Diagnostics.Stopwatch.GetTimestamp(); }
            }
            foreach (int stage in BuildGround(go.transform, x0, z0)) { t = Lap(stage, t); yield return true; t = System.Diagnostics.Stopwatch.GetTimestamp(); }
            var cols = new List<WorldCollision.Col>(); var surfs = new List<WorldCollision.Surf>();
            var placed = new List<Vector3>();   // x, z, footprint
            var rng = new System.Random(Hash(k.x, k.y, 1234));   // reassigned to the bloom RNG for purified decor (see below)
            float R() => (float)rng.NextDouble();
            // border ridge
            var ridge = ZoneGates.Ridge(x0, z0, CHUNK, cols);
            if (ridge)
            {
                var rg = new GameObject("ridge"); rg.transform.SetParent(go.transform, false);
                rg.AddComponent<MeshFilter>().sharedMesh = ridge;
                var rr = rg.AddComponent<MeshRenderer>(); rr.sharedMaterial = ridgeMat;
            }
            // link gates whose site falls in this chunk
            foreach (var site in ZoneWorld.Sites)
                if (site.pos.x >= x0 && site.pos.x < x0 + CHUNK && site.pos.y >= z0 && site.pos.y < z0 + CHUNK)
                    PlaceGate(go.transform, site, cols, surfs);
            // landmarks whose origin falls in this chunk
            foreach (var lm in Regions.Landmarks)
                if (lm.pos.x >= x0 && lm.pos.x < x0 + CHUNK && lm.pos.z >= z0 && lm.pos.z < z0 + CHUNK)
                {
                    var m = Model(lm.id, lm.build, 0);
                    Place(go.transform, m, lm.pos, lm.yaw, 1, cols, surfs); placed.Add(new Vector3(lm.pos.x, lm.pos.z, m.footprint));
                }
            // big climbable structures on the plot grid: placed by the chunk holding the plot centre,
            // kept clear by every chunk near it (plots are a pure function of the cell)
            int c0x = Mathf.FloorToInt((x0 - PLOT) / PLOT), c1x = Mathf.FloorToInt((x0 + CHUNK + PLOT) / PLOT);
            int c0z = Mathf.FloorToInt((z0 - PLOT) / PLOT), c1z = Mathf.FloorToInt((z0 + CHUNK + PLOT) / PLOT);
            for (int cx = c0x; cx <= c1x; cx++) for (int cz = c0z; cz <= c1z; cz++)
            {
                if (!PlotAt(cx, cz, out var pp, out var pyaw, out var ptype, out var pvar)) continue;
                var m = Model(ptype.id, ptype.build, pvar);
                placed.Add(new Vector3(pp.x, pp.z, m.footprint));
                if (pp.x >= x0 && pp.x < x0 + CHUNK && pp.z >= z0 && pp.z < z0 + CHUNK) Place(go.transform, m, pp, pyaw, 1, cols, surfs);
            }
            t = Lap(4, t); yield return true; t = System.Diagnostics.Stopwatch.GetTimestamp();
            // regular scatter: candidates are tested against the region at their own position
            // regular scatter + (purified zones) bloom decor drawn from its own RNG so the base layout never moves
            var rngBloom = new System.Random(Hash(k.x, k.y, 777)); var rngBase = rng;
            foreach (var zoneDef in Regions.All)
                for (int pass = 0; pass < 2; pass++)
                {
                    var reg = Regions.Look(zoneDef.index);
                    if (pass == 1 && !ZoneState.Purified(zoneDef.index)) continue;
                    var list = pass == 0 ? reg.scatters : reg.bloom;
                    rng = pass == 1 ? rngBloom : rngBase;
                foreach (var s in list)
                {
                    int count = Mathf.FloorToInt(s.perChunk * (pass == 1 && GrudgeParts.IsCleansed(zoneDef.index) ? 1.8f : 1) + R());   // pod-purified grudge part: the zone greens further
                    for (int i = 0; i < count * 3 && count > 0; i++)
                    {
                        float x = x0 + R() * CHUNK, z = z0 + R() * CHUNK; float yaw = R() * Mathf.PI * 2, scale = .85f + R() * .3f; int variant = (int)(R() * s.variants);
                        if (ZoneWorld.At(x, z) != zoneDef.index) continue;
                        if (pass == 0 && Mathf.Abs(x) < Regions.ARENA_HALF + 1 && Mathf.Abs(z) < Regions.ARENA_HALF + 1) continue;   // keep the original arena (bloom decor may grow there)
                        var m = Model(s.id, s.build, variant);
                        if (m.surfaces.Count > 0) scale = 1;            // climbable heights are authored; never rescale them
                        float fp = m.footprint * scale;
                        if (s.decor ? ZoneWorld.Solid(x, z) || ZoneWorld.NearestSite(new Vector3(x, 0, z), 16) != null || ZoneAreas.Blocked(x, z, -6) || ZoneRoutes.Dist(x, z) < ZoneRoutes.HALF : !ZoneWorld.Clear(x, z, fp)) continue;   // keep ridges and corridors clear
                        if (InWater(x, z, fp, s.id)) continue;
                        if (!s.decor)
                        {
                            bool clear = true;
                            foreach (var p in placed) { float dx = p.x - x, dz = p.y - z, min = p.z + fp + 1.2f; if (dx * dx + dz * dz < min * min) { clear = false; break; } }
                            foreach (var lm in Regions.Landmarks) { float dx = lm.pos.x - x, dz = lm.pos.z - z; if (dx * dx + dz * dz < 26 * 26) { clear = false; break; } }
                            if (!clear) continue;
                            placed.Add(new Vector3(x, z, fp));
                        }
                        Place(go.transform, m, new Vector3(x, 0, z), yaw, scale, s.decor ? null : cols, s.decor ? null : surfs);
                        if (--count <= 0) break;
                    }
                }
                }
            t = Lap(5, t); yield return true; t = System.Diagnostics.Stopwatch.GetTimestamp();
            // route guiding: debris lining the trails (solid) and traces on them (decor)
            var routeTmp = new List<ZoneRoutes.Placement>(); ZoneRoutes.Placements(x0, z0, CHUNK, routeTmp);   // per job: jobs interleave
            foreach (var pl in routeTmp)
            {
                if (ZoneWorld.Solid(pl.pos.x, pl.pos.z) || ZoneAreas.Blocked(pl.pos.x, pl.pos.z, -4)) continue;
                var (id, build) = pl.kind == 0 ? ZoneRoutes.EdgeModel(pl.zone, pl.seed) : TraceProps.Trace(pl.zone, pl.seed);
                var m = Model(id, build, Mathf.Abs(pl.seed) % (id == "RubblePile" ? 12 : 6));
                if (InWater(pl.pos.x, pl.pos.z, m.footprint, id)) continue;
                if (pl.kind == 0)
                {
                    bool clear = true; foreach (var p in placed) { float dx = p.x - pl.pos.x, dz = p.y - pl.pos.z, min = p.z + m.footprint + .8f; if (dx * dx + dz * dz < min * min) { clear = false; break; } }
                    if (!clear || ZoneRoutes.Dist(pl.pos.x, pl.pos.z) < ZoneRoutes.HALF + m.footprint * .6f) continue;
                    placed.Add(new Vector3(pl.pos.x, pl.pos.z, m.footprint));
                    Place(go.transform, m, pl.pos, pl.yaw, 1, cols, m.surfaces.Count > 0 ? surfs : null);
                }
                else Place(go.transform, m, pl.pos, pl.yaw, 1, null, null);
            }
            t = Lap(6, t); yield return true; t = System.Diagnostics.Stopwatch.GetTimestamp();
            // water edges: reeds on shores (WaterProps, Codex-owned meshes). Floating scum rafts are off (read as
            // debris stuck in the ground from the top-down camera, user 2026-11-07); SOUL/Water draws algae instead.
            {
                var wr = new System.Random(Hash(k.x, k.y, 919));
                for (int i = 0; i < 40; i++)
                {
                    float x = x0 + (float)wr.NextDouble() * CHUNK, z = z0 + (float)wr.NextDouble() * CHUNK; int v = wr.Next(0, 6); float yaw = (float)wr.NextDouble() * Mathf.PI * 2;
                    float d = Terrain.Water(x, z, out int wt); if (wt == Terrain.WATER_NONE && d <= 0) continue;
                    int zone = ZoneWorld.At(x, z);
                    if (d > .03f && d < .2f && wt != Terrain.WATER_OIL && wt != Terrain.WATER_CLEAN) { var (id, build) = WaterProps.Reeds(zone); Place(go.transform, Model(id, build, v), new Vector3(x, 0, z), yaw, .85f + (float)wr.NextDouble() * .4f, null, null); }
                }
            }
            // border dressing (Codex-owned look)
            var dress = BorderDressing.Dress(x0, z0, CHUNK);
            if (dress)
            {
                var dg = new GameObject("border dressing"); dg.transform.SetParent(go.transform, false);
                dg.AddComponent<MeshFilter>().sharedMesh = dress; var dr = dg.AddComponent<MeshRenderer>(); dr.sharedMaterial = ridgeMat;
            }
            WorldCollision.Register(k, cols, surfs);
            Lap(7, t);
        }
        /// props stay out of water (user 2026-11-07): centre or footprint rim in water deeper than 5 cm. Only the
        /// mire (Z6) keeps dead trees, stumps and root tangles standing in it, like a drowned forest.
        static bool InWater(float x, float z, float r, string id)
        {
            if ((id == "DeadTree" || id == "Stump" || id == "RootTangle") && ZoneWorld.At(x, z) == 6) return false;
            if (Terrain.Water(x, z, out _) > .05f) return true;
            for (int a = 0; a < 6; a++) { float t = a * Mathf.PI / 3; if (Terrain.Water(x + Mathf.Cos(t) * r, z + Mathf.Sin(t) * r, out _) > .05f) return true; }
            return false;
        }

        // ---------- plot grid for large structures (ruin zones) ----------
        const float PLOT = 48;
        static Scatter T(string id, System.Func<int, PropModel> b, float w) => new Scatter { id = id, build = b, perChunk = w };
        /// per zone: structure types (weight = perChunk) and the share of plot cells that get one
        static readonly Dictionary<int, (Scatter[] types, float fill)> PlotTypes = new Dictionary<int, (Scatter[], float)>
        {
            [0] = (new[] { T("ApartmentRuin", RuinStructures.ApartmentRuin, 3), T("ShopRow", RuinStructures.ShopRow, 2), T("ParkingDeck", RuinStructures.ParkingDeck, 1.2f), T("Overpass", RuinStructures.Overpass, 1), T("StairRuin", RuinStructures.StairRuin, 1.5f) }, .8f),
            [1] = (new[] { T("ApartmentRuin", RuinStructures.ApartmentRuin, 3), T("ShopRow", RuinStructures.ShopRow, 3), T("ParkingDeck", RuinStructures.ParkingDeck, 1.5f), T("Overpass", RuinStructures.Overpass, 1.5f), T("StairRuin", RuinStructures.StairRuin, 1) }, 1f),
            [2] = (new[] { T("ApartmentRuin", RuinStructures.ApartmentRuin, 2), T("StairRuin", RuinStructures.StairRuin, 2), T("CollapsedTower", RuinStructures.CollapsedTower, .6f) }, .45f),
        };
        /// centres of the structure plots overlapping a square (Terrain flattens under them)
        public static IEnumerable<Vector2> PlotCentres(float x0, float z0, float size)
        {
            foreach (var p in PlotPads(x0, z0, size)) yield return p.c;
        }
        /// local half extents of each structure (measured over its variants, + 1.5 m apron): the flat pad Terrain levels
        static readonly Dictionary<string, Vector2> PadHalf = new Dictionary<string, Vector2>
        {
            ["ApartmentRuin"] = new Vector2(10.7f, 11.3f), ["ShopRow"] = new Vector2(15.6f, 10.5f), ["ParkingDeck"] = new Vector2(14.6f, 9.6f),
            ["Overpass"] = new Vector2(8.1f, 21.2f), ["StairRuin"] = new Vector2(6.1f, 6.4f), ["CollapsedTower"] = new Vector2(18.4f, 11.5f),
        };
        /// structure plots overlapping a square: centre, pad half extents (structure frame), yaw
        public static IEnumerable<(Vector2 c, Vector2 half, float yaw)> PlotPads(float x0, float z0, float size)
        {
            for (int cx = Mathf.FloorToInt(x0 / PLOT); cx <= Mathf.FloorToInt((x0 + size) / PLOT); cx++)
                for (int cz = Mathf.FloorToInt(z0 / PLOT); cz <= Mathf.FloorToInt((z0 + size) / PLOT); cz++)
                    if (PlotAt(cx, cz, out var p, out var yaw, out var type, out _))
                        yield return (new Vector2(p.x, p.z), PadHalf.TryGetValue(type.id, out var h) ? h : new Vector2(12, 12), yaw);
        }
        static Material waterMat;
        // deep body colour per kind (sRGB, a = opacity when deep); SOUL/Water derives shallow tint, foam and glints
        static readonly Color[] WaterCol = { Color.clear, new Color(.2f, .27f, .31f, .78f), new Color(.12f, .15f, .08f, .88f), new Color(.17f, .36f, .08f, .94f), new Color(.02f, .02f, .025f, .95f) };
        /// water surface over the basins of this chunk (SOUL/Water: uv = depth in metres, kind; uv1.x = toxic weight)
        void BuildWater(Transform parent, float x0, float z0)
        {
            var k = WorldCollision.KeyOf(x0 + 1, z0 + 1); Terrain.Grid(k, out _, out var surf, out var depth, out var type);
            var tox = Terrain.ToxGrid(k);
            int n = 32, s = n + 1; var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var uv1 = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                int a = i * s + j, b = (i + 1) * s + j, cc = (i + 1) * s + j + 1, d = i * s + j + 1;
                if (Mathf.Max(Mathf.Max(depth[a], depth[b]), Mathf.Max(depth[cc], depth[d])) < .03f) continue;
                int ty = Mathf.Max(Mathf.Max(type[a], type[b]), Mathf.Max(type[cc], type[d])); if (ty == 0 || ty == Terrain.WATER_TOXIC) ty = Terrain.WATER_MIRE;   // sludge = mire + tox weight (uv1)
                var col = WaterCol[ty].linear; col.a = WaterCol[ty].a;
                int o = v.Count;
                v.Add(Sim.ToUnity(new Vector3(x0 + i, surf[a], z0 + j))); v.Add(Sim.ToUnity(new Vector3(x0 + i + 1, surf[b], z0 + j)));
                v.Add(Sim.ToUnity(new Vector3(x0 + i + 1, surf[cc], z0 + j + 1))); v.Add(Sim.ToUnity(new Vector3(x0 + i, surf[d], z0 + j + 1)));
                foreach (int vi in new[] { a, b, cc, d }) { c.Add(col); uv.Add(new Vector2(depth[vi], ty)); uv1.Add(new Vector2(tox[vi], 0)); }
                tri.Add(o); tri.Add(o + 2); tri.Add(o + 3); tri.Add(o); tri.Add(o + 1); tri.Add(o + 2);
            }
            if (tri.Count == 0) return;
            if (!waterMat)
            {
                var sh = Shader.Find("SOUL/Water");
                waterMat = sh ? new Material(sh) : Mats.Unlit(Color.white, Blend.Alpha, true, UnityEngine.Rendering.CullMode.Off, false);
            }
            var mesh = new Mesh { name = "water" }; mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv1); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            var g = new GameObject("water"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = waterMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        static bool PlotAt(int cx, int cz, out Vector3 pos, out float yaw, out Scatter type, out int variant)
        {
            var r = new System.Random(Hash(cx, cz, 4242));
            float jx = (float)r.NextDouble(), jz = (float)r.NextDouble(); yaw = (float)r.NextDouble() * Mathf.PI * 2; float pick = (float)r.NextDouble(); variant = r.Next(0, 6);
            pos = new Vector3((cx + .3f + jx * .4f) * PLOT, 0, (cz + .3f + jz * .4f) * PLOT);
            type = null;
            float rr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z), fill = (float)r.NextDouble();
            if (rr < 52 || !PlotTypes.TryGetValue(ZoneWorld.At(pos.x, pos.z), out var table) || fill > ZoneAreas.PlotFill(pos.x, pos.z, table.fill)) return false;   // outside the arena, inside a ruin zone
            if (!ZoneWorld.Clear(pos.x, pos.z, 22)) return false;
            foreach (var lm in Regions.Landmarks) if ((lm.pos - pos).sqrMagnitude < 34 * 34) return false;
            float total = 0; foreach (var t in table.types) total += t.perChunk;
            pick *= total; foreach (var t in table.types) { if (pick < t.perChunk) { type = t; break; } pick -= t.perChunk; }
            type ??= table.types[0];
            yaw = Mathf.Round(yaw / (Mathf.PI / 2)) * (Mathf.PI / 2) + ((float)r.NextDouble() - .5f) * .3f;   // street-grid-ish orientation
            return true;
        }

        Material Glow(int part)
        {
            if (!glowMats.TryGetValue(part, out var m)) { m = Mats.Unlit(Color.white * 2.4f, Blend.Opaque, true); glowMats[part] = m; }
            return m;
        }
        void PlaceGate(Transform parent, ZoneWorld.Site site, List<WorldCollision.Col> cols, List<WorldCollision.Surf> surfs)
        {
            var g = ZoneGates.Get(site); var pos = new Vector3(site.pos.x, 0, site.pos.y);
            var root = new GameObject($"gate {site.index} Z{site.link.a}-Z{site.link.b} {site.Label}"); root.transform.SetParent(parent, false);
            if (g.fixedPart.mesh) Place(root.transform, g.fixedPart, pos, site.yaw, 1, cols, surfs);
            else { foreach (var pc in g.fixedPart.colliders) cols.Add(WorldCollision.ToWorld(pc, pos, site.yaw, 1)); foreach (var ps in g.fixedPart.surfaces) surfs.Add(WorldCollision.ToWorld(ps, pos, site.yaw, 1)); }
            if (g.fixedGlow) PlaceMesh(root.transform, g.fixedGlow, Glow(g.part), pos, site.yaw);
            if (g.solid.mesh || g.solidGlow)
            {
                var solid = new GameObject("solid"); solid.transform.SetParent(root.transform, false);
                if (g.solid.mesh) Place(solid.transform, g.solid, pos, site.yaw, 1, null, null);
                if (g.solidGlow) PlaceMesh(solid.transform, g.solidGlow, Glow(g.part), pos, site.yaw);
                if (g.hideWhenOwned) gateSolids.Add((solid, g.part));
            }
            foreach (var pc in g.solid.colliders) cols.Add(WorldCollision.ToWorld(pc, pos, site.yaw, 1));
        }
        static void PlaceMesh(Transform parent, Mesh mesh, Material mat, Vector3 pos, float yaw)
        {
            pos.y += Terrain.Height(pos.x, pos.z);
            var g = new GameObject(mesh.name); g.transform.SetParent(parent, false);
            g.transform.localPosition = Sim.ToUnity(pos); g.transform.localRotation = Sim.YawToUnity(yaw);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Place(Transform parent, PropModel m, Vector3 pos, float yaw, float scale, List<WorldCollision.Col> cols, List<WorldCollision.Surf> surfs)
        {
            if (m == null || m.mesh == null) return;
            pos.y += Terrain.Height(pos.x, pos.z);   // everything authored at y = 0 sits on the terrain
            var g = new GameObject(m.mesh.name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = Sim.ToUnity(pos); g.transform.localRotation = Sim.YawToUnity(yaw); g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = propMat;
            mr.shadowCastingMode = m.height > .5f ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            if (cols != null) foreach (var pc in m.colliders) cols.Add(WorldCollision.ToWorld(pc, pos, yaw, scale));
            if (surfs != null && m.surfaces != null) foreach (var ps in m.surfaces) surfs.Add(WorldCollision.ToWorld(ps, pos, yaw, scale));
            PropCount++;
        }

        /// ground mesh in steps; yields the profile stage that just ran (1 mesh rows, 2 detail, 3 water)
        IEnumerable<int> BuildGround(Transform parent, float x0, float z0)
        {
            // smooth grid: one vertex colour per grid point (no per-cell facets / checkerboard)
            int n = (int)(CHUNK / CELL), s = n + 1;
            var verts = new Vector3[s * s]; var cols = new Color[s * s]; var nor = new Vector3[s * s];
            for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
            {
                float x = x0 + i * CELL, z = z0 + j * CELL; int idx = i * s + j;
                // interior vertices jitter (hash of the global grid point) so patterns do not stair-step; chunk edges stay put
                if (i > 0 && i < n && j > 0 && j < n) { int gx = Mathf.RoundToInt(x), gz = Mathf.RoundToInt(z); x += (Hash(gx, gz, 5) % 1000 / 1000f - .5f) * .5f; z += (Hash(gx, gz, 6) % 1000 / 1000f - .5f) * .5f; }
                float y = Terrain.Height(x, z);
                float gxs = Terrain.Height(x + .7f, z) - Terrain.Height(x - .7f, z), gzs = Terrain.Height(x, z + .7f) - Terrain.Height(x, z - .7f);
                verts[idx] = Sim.ToUnity(new Vector3(x, y - .012f, z)); nor[idx] = Sim.ToUnity(new Vector3(-gxs / 1.4f, 1, -gzs / 1.4f).normalized);
                // relief read from above: slope / cliff rock / foot shadow / lip light, multiplied over the zone colour
                float grade = Mathf.Sqrt(gxs * gxs + gzs * gzs) / 1.4f;
                float hollow = (Terrain.Height(x + 2.5f, z) + Terrain.Height(x - 2.5f, z) + Terrain.Height(x, z + 2.5f) + Terrain.Height(x, z - 2.5f)) * .25f - y;
                cols[idx] = GroundLook.Relief(Regions.GroundColor(x, z), grade > .75f ? ZoneWorld.At(x, z) : 0, x, y, z, grade, hollow).linear;
                if (j == n && i % 6 == 5 && i < n) yield return 1;   // vertex rows in sixths
            }
            var tris = new List<int>();
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
            {
                float cx = x0 + (i + .5f) * CELL, cz = z0 + (j + .5f) * CELL;
                if (Mathf.Abs(cx) < Regions.ARENA_HALF && Mathf.Abs(cz) < Regions.ARENA_HALF) continue;   // arena keeps its cratered ground
                int a = i * s + j, b = (i + 1) * s + j, c = (i + 1) * s + j + 1, d = i * s + j + 1;
                // sim CCW (a,d,c),(a,c,b) seen from above; flipped for Unity
                tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(b); tris.Add(c);
            }
            if (tris.Count == 0) yield break;
            yield return 1;
            var gd = GroundDetail.Decals(x0, z0, CHUNK);   // Codex-owned ground details (cracks, markings, litter)
            if (gd)
            {
                var dg = new GameObject("ground detail"); dg.transform.SetParent(parent, false);
                dg.AddComponent<MeshFilter>().sharedMesh = gd; var dr = dg.AddComponent<MeshRenderer>(); dr.sharedMaterial = propMat; dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            yield return 2;
            BuildWater(parent, x0, z0);
            yield return 3;
            var mesh = new Mesh { name = "ground", vertices = verts, colors = cols, normals = nor, triangles = tris.ToArray() };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.RecalculateBounds();
            var g = new GameObject("ground"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = groundMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

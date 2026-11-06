using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Streams the open world in 32 m chunks around the player: ground mesh (region-blended
    // vertex colours), scattered region props with collision, landmarks and the cliff ring at
    // the world edge. Deterministic per chunk (seeded by its coordinates), so a chunk always
    // regenerates identically. Prop meshes are cached per (type, variant).
    public class WorldStreamer
    {
        public const float CHUNK = 32, CELL = 2;
        public int loadRadius = 3, unloadRadius = 4, buildPerFrame = 2;
        readonly Transform root;
        readonly Material propMat, groundMat;
        readonly Dictionary<Vector2Int, GameObject> loaded = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<string, PropModel> cache = new Dictionary<string, PropModel>();
        readonly float[] w = new float[4];
        public int ChunkCount => loaded.Count;
        public int PropCount;

        public WorldStreamer(Transform parent)
        {
            root = new GameObject("Open World").transform; root.SetParent(parent, false);
            propMat = Mats.Lit(Color.white, Color.black, .12f, .12f, true); propMat.SetFloat("_Cutaway", 1);
            groundMat = Mats.Lit(Color.white, Color.black, 0, .05f, true);
            WorldCollision.chunkSize = CHUNK; WorldCollision.Clear();
        }

        PropModel Model(string id, System.Func<int, PropModel> build, int variant)
        {
            string key = id + "#" + variant;
            if (!cache.TryGetValue(key, out var m)) { m = build(Hash(id.GetHashCode(), variant, 7)); if (m.mesh) m.mesh.name = key; cache[key] = m; }
            return m;
        }
        static int Hash(int a, int b, int c) { unchecked { int h = (int)2166136261; h = (h ^ a) * 16777619; h = (h ^ b) * 16777619; h = (h ^ c) * 16777619; return h & 0x7fffffff; } }

        /// call every frame with the player's sim position; `immediate` builds everything now
        public void Update(Vector3 center, bool immediate = false)
        {
            var c = WorldCollision.KeyOf(center.x, center.z);
            var todo = new List<Vector2Int>();
            for (int dx = -loadRadius; dx <= loadRadius; dx++) for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                if (!loaded.ContainsKey(k) && InWorld(k)) todo.Add(k);
            }
            todo.Sort((a, b) => (a - c).sqrMagnitude.CompareTo((b - c).sqrMagnitude));
            int n = immediate ? todo.Count : Mathf.Min(buildPerFrame, todo.Count);
            for (int i = 0; i < n; i++) Build(todo[i]);
            var drop = new List<Vector2Int>();
            foreach (var k in loaded.Keys) if (Mathf.Abs(k.x - c.x) > unloadRadius || Mathf.Abs(k.y - c.y) > unloadRadius) drop.Add(k);
            foreach (var k in drop)
            {
                var gt = loaded[k].transform.Find("ground"); if (gt) Object.Destroy(gt.GetComponent<MeshFilter>().sharedMesh);
                Object.Destroy(loaded[k]); loaded.Remove(k); WorldCollision.Unregister(k);
            }
        }
        static bool InWorld(Vector2Int k)
        {
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK;
            float nx = Mathf.Clamp(0, x0, x0 + CHUNK), nz = Mathf.Clamp(0, z0, z0 + CHUNK);
            return nx * nx + nz * nz < (Regions.EDGE_RADIUS + 40) * (Regions.EDGE_RADIUS + 40);
        }

        void Build(Vector2Int k)
        {
            var go = new GameObject($"chunk {k.x},{k.y}"); go.transform.SetParent(root, false);
            loaded[k] = go;
            float x0 = k.x * CHUNK, z0 = k.y * CHUNK;
            BuildGround(go.transform, x0, z0);
            var cols = new List<WorldCollision.Col>(); var surfs = new List<WorldCollision.Surf>();
            var placed = new List<Vector3>();   // x, z, footprint
            var rng = new System.Random(Hash(k.x, k.y, 1234));
            float R() => (float)rng.NextDouble();
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
            // regular scatter: candidates are tested against the region at their own position
            foreach (var reg in Regions.All)
                foreach (var s in reg.scatters)
                {
                    int count = Mathf.FloorToInt(s.perChunk + R());
                    for (int i = 0; i < count * 3 && count > 0; i++)
                    {
                        float x = x0 + R() * CHUNK, z = z0 + R() * CHUNK; float yaw = R() * Mathf.PI * 2, scale = .85f + R() * .3f; int variant = (int)(R() * s.variants);
                        if (Regions.At(x, z) != reg.index) continue;
                        float rr = Mathf.Sqrt(x * x + z * z);
                        if (rr > Regions.WORLD_RADIUS - 4) continue;
                        if (Mathf.Abs(x) < Regions.ARENA_HALF + 1 && Mathf.Abs(z) < Regions.ARENA_HALF + 1) continue;   // keep the original arena
                        var m = Model(s.id, s.build, variant);
                        if (m.surfaces.Count > 0) scale = 1;            // climbable heights are authored; never rescale them
                        float fp = m.footprint * scale;
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
            // cliff ring at the edge of the world
            for (int i = 0; i < 96; i++)
            {
                float a = i / 96f * Mathf.PI * 2; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * (Regions.EDGE_RADIUS + (Hash(i, 3, 9) % 7));
                if (p.x < x0 || p.x >= x0 + CHUNK || p.z < z0 || p.z >= z0 + CHUNK) continue;
                var m = Model("CanyonWall", RegionProps.CanyonWall, i % 6);
                Place(go.transform, m, p, a + Mathf.PI / 2, 1.1f, cols, null);
            }
            WorldCollision.Register(k, cols, surfs);
        }

        // ---------- plot grid for large structures (ruins region) ----------
        const float PLOT = 48;
        static readonly Scatter[] PlotTypes =
        {
            new Scatter { id = "ApartmentRuin", build = RuinStructures.ApartmentRuin, perChunk = 3 },
            new Scatter { id = "ShopRow", build = RuinStructures.ShopRow, perChunk = 2 },
            new Scatter { id = "ParkingDeck", build = RuinStructures.ParkingDeck, perChunk = 1.2f },
            new Scatter { id = "Overpass", build = RuinStructures.Overpass, perChunk = 1 },
            new Scatter { id = "StairRuin", build = RuinStructures.StairRuin, perChunk = 1.5f },
        };
        static bool PlotAt(int cx, int cz, out Vector3 pos, out float yaw, out Scatter type, out int variant)
        {
            var r = new System.Random(Hash(cx, cz, 4242));
            float jx = (float)r.NextDouble(), jz = (float)r.NextDouble(); yaw = (float)r.NextDouble() * Mathf.PI * 2; float pick = (float)r.NextDouble(); variant = r.Next(0, 6);
            pos = new Vector3((cx + .3f + jx * .4f) * PLOT, 0, (cz + .3f + jz * .4f) * PLOT);
            type = null;
            float rr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            if (rr < 52 || Regions.At(pos.x, pos.z) != 0) return false;                    // outside the arena, inside the ruins
            foreach (var lm in Regions.Landmarks) if ((lm.pos - pos).sqrMagnitude < 30 * 30) return false;
            float total = 0; foreach (var t in PlotTypes) total += t.perChunk;
            pick *= total; foreach (var t in PlotTypes) { if (pick < t.perChunk) { type = t; break; } pick -= t.perChunk; }
            type ??= PlotTypes[0];
            yaw = Mathf.Round(yaw / (Mathf.PI / 2)) * (Mathf.PI / 2) + ((float)r.NextDouble() - .5f) * .3f;   // street-grid-ish orientation
            return true;
        }

        void Place(Transform parent, PropModel m, Vector3 pos, float yaw, float scale, List<WorldCollision.Col> cols, List<WorldCollision.Surf> surfs)
        {
            if (m == null || m.mesh == null) return;
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

        void BuildGround(Transform parent, float x0, float z0)
        {
            // smooth grid: one vertex colour per grid point (no per-cell facets / checkerboard)
            int n = (int)(CHUNK / CELL), s = n + 1;
            var verts = new Vector3[s * s]; var cols = new Color[s * s]; var nor = new Vector3[s * s];
            for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
            {
                float x = x0 + i * CELL, z = z0 + j * CELL; int idx = i * s + j;
                verts[idx] = Sim.ToUnity(new Vector3(x, -.012f, z)); nor[idx] = Vector3.up;
                cols[idx] = Regions.GroundColor(x, z, w).linear;
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
            if (tris.Count == 0) return;
            var mesh = new Mesh { name = "ground", vertices = verts, colors = cols, normals = nor, triangles = tris.ToArray() };
            mesh.RecalculateBounds();
            var g = new GameObject("ground"); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = groundMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

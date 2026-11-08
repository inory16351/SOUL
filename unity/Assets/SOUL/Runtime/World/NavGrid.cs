using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Soul
{
    // Enemy path finding (sim space). A 1 m walk grid per 32 m chunk, filled lazily as searches touch it:
    //  - a cell holds one layer per walkable floor height met there (ground under a deck, the deck itself ...);
    //    nodes are (cell, layer), so an enemy on a roof searches the roof storey
    //  - an edge (layer -> 8-neighbour) is decided once, walked in quarter steps like a character: climb at most
    //    STEP per step, drop at most DROP, no ledge, no wall at the midpoint or the far cell, no cliff face
    //  - agents are grouped by clearance radius (big wardens / bosses inflate walls and ledges)
    // Floors / ledges / walls use the same rules as WorldCollision (Floor / Ledge / Resolve) but test only the surfaces
    // and walls gathered per cell / chunk: WorldCollision scans the 3x3 chunks + every persistent piece (~70 us a call
    // next to a ruin), which is far too slow for thousands of edges.
    // A nav chunk depends on the 5x5 collision chunks around it, the persistent pieces and the owned parts; it is
    // rebuilt when any of those change and dropped when its streaming chunk unloads (Sweep).
    public static class NavGrid
    {
        public const float CELL = 1, DROP = 1.6f;
        const int N = 32;                                  // cells per chunk side (WorldCollision.chunkSize / CELL)
        public static int maxExpand = 1500;                // A* node budget per search (spread over frames by NavAgent)
        const float GREED = 1.4f;                          // weighted A*: paths up to ~40 % longer, far fewer nodes
        /// nav time allowed per frame (ms): agents that want a new path past it keep their old one a frame longer
        public static float frameBudget = .8f; internal static double frameMs; internal static int navFrame;
        // stats (tests / HANDOFF): searches, expanded nodes and time spent (ms) since the last ResetStats
        public static int searches, expanded, edgesBuilt; public static double ms;
        public static void ResetStats() { searches = expanded = edgesBuilt = 0; ms = 0; }

        class Layer { public float y, cost; public bool blocked; public byte known, ok; public readonly float[] to = new float[8]; }
        class Chunk
        {
            public List<Layer>[] cells = new List<Layer>[N * N];
            public float[][] prof; public WorldCollision.Surf[][] cellSurfs;          // class 0 only: per-cell data shared by all sizes
            public List<WorldCollision.Col> walls; public List<WorldCollision.Surf> surfs;
            public object[] deps = new object[25]; public int persist, parts, frame = int.MinValue;
        }
        static readonly Dictionary<long, Chunk> chunks = new Dictionary<long, Chunk>();
        static readonly int[] DX = { 1, 0, -1, 0, 1, -1, -1, 1 }, DZ = { 0, 1, 0, -1, 1, 1, -1, -1 };

        // WorldCollision keeps its chunk table private; read it (reference identity = "same chunk contents")
        static IDictionary wcChunks; static IList wcPersistent, wcPersistentSurfs; static FieldInfo fCols, fSurfs;
        static void Hook()
        {
            if (wcChunks != null) return;
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Static;
            wcChunks = (IDictionary)typeof(WorldCollision).GetField("chunks", F).GetValue(null);
            var p = typeof(WorldCollision).GetField("persistent", F).GetValue(null);
            fCols = p.GetType().GetField("cols"); fSurfs = p.GetType().GetField("surfs");
            wcPersistent = (IList)fCols.GetValue(p); wcPersistentSurfs = (IList)fSurfs.GetValue(p);
        }
        /// validity stamp, taken once per public query: frame + loaded set + persistent pieces + owned parts
        static int stamp;
        static void Begin()
        {
            Hook();
            stamp = Time.frameCount * 7919 + wcChunks.Count * 31 + (wcPersistent.Count + wcPersistentSurfs.Count) * 131 + Parts.owned * 1031;
        }

        /// agents share a grid per clearance radius (tenths of a metre): walls are tested at this radius
        public static int ClassOf(float radius) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(.3f, radius - .35f) * 10), 3, 20);
        static long Key(int cx, int cz, int cls) => (((long)cx + 4096) * 8192 + cz + 4096) * 32 + cls;

        static long lastKey = long.MinValue; static Chunk lastChunk;
        static Chunk GetChunk(int cx, int cz, int cls)
        {
            long key = Key(cx, cz, cls);
            if (key == lastKey && lastChunk.frame == stamp) return lastChunk;
            // checked at most once per frame and per change of the loaded set (Simulate runs many steps in one frame)
            if (!chunks.TryGetValue(key, out var c) || c.frame != stamp)
            {
                int persist = wcPersistent.Count + wcPersistentSurfs.Count;
                bool valid = c != null && c.parts == Parts.owned && c.persist == persist;
                for (int i = 0; i < 25 && valid; i++) valid = c.deps[i] == wcChunks[new Vector2Int(cx + i / 5 - 2, cz + i % 5 - 2)];
                if (!valid)
                {
                    c = new Chunk { parts = Parts.owned, persist = persist };
                    for (int i = 0; i < 25; i++) c.deps[i] = wcChunks[new Vector2Int(cx + i / 5 - 2, cz + i % 5 - 2)];
                    chunks[key] = c;
                }
                c.frame = stamp;
            }
            lastKey = key; lastChunk = c; return c;
        }
        /// drop nav chunks whose streaming chunk is gone (call now and then)
        public static void Sweep()
        {
            Begin(); var dead = new List<long>();
            foreach (var kv in chunks) if (kv.Value.deps[12] == null || !wcChunks.Contains(KeyFromLong(kv.Key))) dead.Add(kv.Key);
            foreach (var k in dead) chunks.Remove(k);
        }
        static Vector2Int KeyFromLong(long k) { k /= 32; return new Vector2Int((int)(k / 8192) - 4096, (int)(k % 8192) - 4096); }
        public static int CachedChunks => chunks.Count;
        public static void Clear() { chunks.Clear(); lastKey = long.MinValue; }

        static int Div(int a) => a >= 0 ? a / N : (a + 1) / N - 1;
        static List<Layer> Cell(int gx, int gz, int cls)
        {
            int cx = Div(gx), cz = Div(gz); var ch = GetChunk(cx, cz, cls);
            int i = (gx - cx * N) * N + gz - cz * N;
            return ch.cells[i] ?? (ch.cells[i] = new List<Layer>(1));
        }
        public static float Cx(int g) => (g + .5f) * CELL;
        public static int G(float x) => Mathf.FloorToInt(x / CELL);
        static float R(int cls) => cls * .1f;

        // ---------- walkable surfaces per cell ----------
        // The surfaces touching each 1 m cell (usually 0..3), gathered once per class-0 chunk from the 3x3 collision
        // chunks + the persistent set. Any point inside the cell is then answered from that short list.
        static readonly WorldCollision.Surf[] none = new WorldCollision.Surf[0];
        static readonly List<WorldCollision.Surf> tmpS = new List<WorldCollision.Surf>();
        static float Reach(in WorldCollision.Surf s) => s.kind == PropSurface.MOUND ? s.r : s.half.magnitude;
        static WorldCollision.Surf[] CellSurfs(int gx, int gz)
        {
            int cx = Div(gx), cz = Div(gz); var ch = GetChunk(cx, cz, 0);
            int i = (gx - cx * N) * N + gz - cz * N;
            if (ch.cellSurfs == null) ch.cellSurfs = new WorldCollision.Surf[N * N][];
            if (ch.cellSurfs[i] != null) return ch.cellSurfs[i];
            if (ch.surfs == null)
            {
                ch.surfs = new List<WorldCollision.Surf>(); float x0 = cx * N * CELL - 1, z0 = cz * N * CELL - 1, x1 = x0 + N * CELL + 2, z1 = z0 + N * CELL + 2;
                void Take(IList list) { foreach (WorldCollision.Surf s in list) { float b = Reach(s); if (s.c.x + b >= x0 && s.c.x - b <= x1 && s.c.y + b >= z0 && s.c.y - b <= z1) ch.surfs.Add(s); } }
                for (int k = 0; k < 9; k++) { var w = wcChunks[new Vector2Int(cx + k / 3 - 1, cz + k % 3 - 1)]; if (w != null) Take((IList)fSurfs.GetValue(w)); }
                Take(wcPersistentSurfs);
            }
            float cx0 = gx * CELL - .05f, cz0 = gz * CELL - .05f, cx1 = cx0 + CELL + .1f, cz1 = cz0 + CELL + .1f;
            tmpS.Clear();
            foreach (var s in ch.surfs) { float b = Reach(s); if (s.c.x + b >= cx0 && s.c.x - b <= cx1 && s.c.y + b >= cz0 && s.c.y - b <= cz1) tmpS.Add(s); }
            return ch.cellSurfs[i] = tmpS.Count == 0 ? none : tmpS.ToArray();
        }
        // same as WorldCollision.SurfHeight (private there): height of surface s at (x, z), -inf outside it
        static float SurfH(in WorldCollision.Surf s, float x, float z)
        {
            float wx = x - s.c.x, wz = z - s.c.y;
            if (s.kind == PropSurface.MOUND) { float d2 = (wx * wx + wz * wz) / (s.r * s.r); return d2 >= 1 ? float.NegativeInfinity : s.top * (1 - d2); }
            float lx = wx * s.cos - wz * s.sin, lz = wx * s.sin + wz * s.cos;
            if (Mathf.Abs(lx) > s.half.x || Mathf.Abs(lz) > s.half.y) return float.NegativeInfinity;
            if (s.kind == PropSurface.BOX) return s.top;
            return Mathf.Lerp(s.yLow, s.yHigh, (lz + s.half.y) / (2 * s.half.y));
        }
        /// = WorldCollision.Floor(x, z, footY, STEP), from the cell's surface list
        static float QFloor(float x, float z, float footY)
        {
            float best = Terrain.Height(x, z), below = footY + WorldCollision.STEP;
            foreach (var s in CellSurfs(G(x), G(z))) { float h = SurfH(s, x, z); if (h > best && h <= below && !Locked(h, s.gate, footY)) best = h; }
            return best;
        }
        /// = WorldCollision.Ledge(x, z, footY, STEP)
        static bool QLedge(float x, float z, float footY)
        {
            float head = footY + WorldCollision.HEADROOM;
            foreach (var s in CellSurfs(G(x), G(z))) { float h = SurfH(s, x, z); if (h <= head && (h > footY + WorldCollision.STEP + 1e-3f || Locked(h, s.gate, footY))) return true; }
            return false;
        }
        /// fast WorldCollision.Floor(x, z, footY) for per-frame callers (enemy drop guard)
        public static float Floor(float x, float z, float footY) { Begin(); return QFloor(x, z, footY); }

        // every surface height over a cell centre (+ its part gate): [terrain, h1, gate1, h2, gate2 ...]
        static readonly List<float> tmp = new List<float>();
        static float[] Profile(int gx, int gz)
        {
            int cx = Div(gx), cz = Div(gz); var ch = GetChunk(cx, cz, 0);
            int i = (gx - cx * N) * N + gz - cz * N;
            if (ch.prof == null) ch.prof = new float[N * N][];
            if (ch.prof[i] != null) return ch.prof[i];
            float x = Cx(gx), z = Cx(gz), t = Terrain.Height(x, z);
            tmp.Clear(); tmp.Add(t);
            foreach (var s in CellSurfs(gx, gz)) { float h = SurfH(s, x, z); if (h > t + 1e-4f) { tmp.Add(h); tmp.Add(s.gate); } }
            return ch.prof[i] = tmp.ToArray();
        }
        static bool Locked(float h, float gate, float footY) => gate != 0 && h > footY + .05f && !Parts.Has((int)gate);
        /// = WorldCollision.Floor(centre, footY, STEP)
        static float FloorP(float[] p, float footY)
        {
            float best = p[0], below = footY + WorldCollision.STEP;
            for (int k = 1; k < p.Length; k += 2) { float h = p[k]; if (h > best && h <= below && !Locked(h, p[k + 1], footY)) best = h; }
            return best;
        }
        /// = WorldCollision.Ledge(centre, footY, STEP)
        static bool LedgeP(float[] p, float footY)
        {
            float head = footY + WorldCollision.HEADROOM;
            for (int k = 1; k < p.Length; k += 2) { float h = p[k]; if (h <= head && (h > footY + WorldCollision.STEP + 1e-3f || Locked(h, p[k + 1], footY))) return true; }
            return false;
        }

        /// the layer of cell (gx, gz) whose floor is y (created and classified on first sight)
        static Layer LayerAt(int gx, int gz, float y, int cls)
        {
            var cell = Cell(gx, gz, cls);
            foreach (var l in cell) if (Mathf.Abs(l.y - y) < .12f) return l;
            var L = new Layer { y = y, blocked = Wall(Cx(gx), y, Cx(gz), R(cls)), cost = CELL / Mathf.Max(.6f, Terrain.Slow(Cx(gx), Cx(gz))) };
            // big machines keep a cell off ledges / long drops (an orthogonal neighbour that is one)
            for (int k = 0; k < 4 && !L.blocked && cls >= 6; k++)
            {
                var q = Profile(gx + DX[k], gz + DZ[k]);
                L.blocked = LedgeP(q, y) || FloorP(q, y) < y - DROP;
            }
            cell.Add(L); return L;
        }
        /// would a disc of radius r with feet at y overlap a wall (same rules as WorldCollision.Resolve)? Tested against
        /// the walls near this chunk only, gathered once (Resolve scans every persistent collider: ~15 us a call)
        static bool Wall(float x, float y, float z, float r)
        {
            if (!WorldCollision.enabled) return false;
            if (!WorldCollision.worldRect.Contains(new Vector2(x, z))) return true;
            var ch = GetChunk(Div(G(x)), Div(G(z)), 0);
            var walls = ch.walls ?? (ch.walls = Gather(Div(G(x)), Div(G(z))));
            foreach (var col in walls)
            {
                if (col.ranged && (y + WorldCollision.STEP >= col.y1 || y < col.y0 - .25f)) continue;
                if (col.gate != 0 && Parts.Has(col.gate)) continue;
                float wx = x - col.c.x, wz = z - col.c.y;
                if (!col.box) { float m = col.r + r - .05f; if (wx * wx + wz * wz < m * m) return true; continue; }
                float lx = wx * col.cos - wz * col.sin, lz = wx * col.sin + wz * col.cos;
                float ex = Mathf.Abs(lx) - col.half.x, ez = Mathf.Abs(lz) - col.half.y;
                if (ex < r - .05f && ez < r - .05f && (ex <= 0 || ez <= 0 || ex * ex + ez * ez < (r - .05f) * (r - .05f))) return true;
            }
            return false;
        }
        const float GATHER = 3;   // > the biggest agent clearance + half a cell
        static List<WorldCollision.Col> Gather(int cx, int cz)
        {
            var o = new List<WorldCollision.Col>(); float x0 = cx * N * CELL - GATHER, z0 = cz * N * CELL - GATHER, x1 = x0 + N * CELL + 2 * GATHER, z1 = z0 + N * CELL + 2 * GATHER;
            void Take(IList cols)
            {
                foreach (WorldCollision.Col c in cols)
                {
                    float b = c.box ? c.half.magnitude : c.r;
                    if (c.c.x + b >= x0 && c.c.x - b <= x1 && c.c.y + b >= z0 && c.c.y - b <= z1) o.Add(c);
                }
            }
            for (int i = 0; i < 9; i++)
            {
                var wch = wcChunks[new Vector2Int(cx + i / 3 - 1, cz + i % 3 - 1)];
                if (wch != null) Take((IList)fCols.GetValue(wch));
            }
            Take(wcPersistent);
            return o;
        }
        /// floor at cell centre for a character standing at footY (the layer it would step onto)
        public static float FloorAt(int gx, int gz, float footY) => FloorP(Profile(gx, gz), footY);

        /// can a character on layer `a` of (gx, gz) walk to neighbour d? `to` = that neighbour's floor
        static bool Edge(int gx, int gz, Layer a, int d, int cls, out float to)
        {
            int bit = 1 << d;
            if ((a.known & bit) != 0) { to = a.to[d]; return (a.ok & bit) != 0; }
            bool ok = Decide(gx, gz, a, d, cls, out to);
            a.known |= (byte)bit; if (ok) a.ok |= (byte)bit; a.to[d] = to; edgesBuilt++;
            return ok;
        }
        static bool Decide(int gx, int gz, Layer a, int d, int cls, out float to)
        {
            int nx = gx + DX[d], nz = gz + DZ[d]; float y = a.y, yMid = y; to = y;
            // walk it in quarter steps like a character does: stairs / ramps steeper than STEP per cell still climb
            for (int s = 1; s < 4; s++)
            {
                float px = Cx(gx) + DX[d] * CELL * s / 4, pz = Cx(gz) + DZ[d] * CELL * s / 4, f = QFloor(px, pz, y);
                if (f < y - DROP || QLedge(px, pz, y)) return false;
                y = f; if (s == 2) yMid = f;
            }
            var pb = Profile(nx, nz); to = FloorP(pb, y);
            if (to < y - DROP || LedgeP(pb, y)) return false;
            if (d >= 4)
            {   // diagonal: no corner cutting (both orthogonal steps must be open)
                if (!Edge(gx, gz, a, DX[d] == 1 ? 0 : 2, cls, out _) || !Edge(gx, gz, a, DZ[d] == 1 ? 1 : 3, cls, out _)) return false;
            }
            float ax = Cx(gx), az = Cx(gz), mx = (ax + Cx(nx)) / 2, mz = (az + Cx(nz)) / 2;
            float l = d >= 4 ? .7071f : 1, dx = DX[d] * l, dz = DZ[d] * l;
            if (WorldCollision.TerrainWall(ax, az, dx, dz, a.y) || WorldCollision.TerrainWall(mx, mz, dx, dz, yMid)) return false;
            if (Wall(mx, yMid, mz, R(cls))) return false;   // thin walls between the two centres
            return !LayerAt(nx, nz, to, cls).blocked;
        }

        // ---------- A* ----------
        struct Node { public int gx, gz, parent; public float y, g; public Layer l; }
        static float H(int ax, int az, int bx, int bz) { int dx = Mathf.Abs(ax - bx), dz = Mathf.Abs(az - bz); return (dx + dz + (1.4142f - 2) * Mathf.Min(dx, dz)) * CELL; }
        static double Ms(long t0) => (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        /// A resumable A* search: Step(ms) expands nodes until done or the time is used up, so an agent can spread a
        /// search over frames (the grid it builds stays cached either way).
        public class Search
        {
            readonly List<Node> nodes = new List<Node>();
            readonly Dictionary<Layer, int> seen = new Dictionary<Layer, int>();
            readonly List<(float f, int i)> heap = new List<(float, int)>();
            int cls, tx, tz, best, hit, n; float goalY, bestH;
            public bool active, done, reached;
            public void Start(Vector3 from, Vector3 goal, float radius)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); searches++; Begin();
                cls = ClassOf(radius); nodes.Clear(); seen.Clear(); heap.Clear();
                int sx = G(from.x), sz = G(from.z); tx = G(goal.x); tz = G(goal.z);
                goalY = QFloor(goal.x, goal.z, goal.y);
                var s = LayerAt(sx, sz, FloorAt(sx, sz, from.y), cls);
                nodes.Add(new Node { gx = sx, gz = sz, parent = -1, y = s.y, g = 0, l = s }); seen[s] = 0;
                Push(Est(sx, sz, s.y), 0);
                best = 0; hit = -1; bestH = float.MaxValue; n = 0; active = true; done = reached = false;
                double e = Ms(t0); ms += e; frameMs += e;
            }
            /// expand for up to `budgetMs`; true when finished (then Result gives the path)
            public bool Step(double budgetMs)
            {
                if (done) return true;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Begin(); int k = 0;
                while (heap.Count > 0 && n < maxExpand)
                {
                    if ((++k & 15) == 0 && Ms(t0) > budgetMs) break;
                    int i = Pop(); var cur = nodes[i]; n++; expanded++;
                    float hh = H(cur.gx, cur.gz, tx, tz) + Mathf.Abs(cur.y - goalY) * 2;
                    if (hh < bestH) { bestH = hh; best = i; }
                    if (Mathf.Abs(cur.gx - tx) <= 1 && Mathf.Abs(cur.gz - tz) <= 1 && Mathf.Abs(cur.y - goalY) < 1.2f) { hit = i; done = true; break; }
                    for (int d = 0; d < 8; d++)
                    {
                        if (!Edge(cur.gx, cur.gz, cur.l, d, cls, out float ny)) continue;
                        int nx = cur.gx + DX[d], nz = cur.gz + DZ[d];
                        var nl = LayerAt(nx, nz, ny, cls);
                        float g = cur.g + (d >= 4 ? 1.4142f : 1) * nl.cost;
                        if (seen.TryGetValue(nl, out int j))
                        {
                            if (nodes[j].g <= g) continue;
                            var nj = nodes[j]; nj.g = g; nj.parent = i; nodes[j] = nj;
                            Push(g + Est(nx, nz, nl.y), j); continue;   // stale heap entries are re-expanded cheaply
                        }
                        seen[nl] = nodes.Count; nodes.Add(new Node { gx = nx, gz = nz, parent = i, y = nl.y, g = g, l = nl });
                        Push(g + Est(nx, nz, nl.y), nodes.Count - 1);
                    }
                }
                if (heap.Count == 0 || n >= maxExpand) done = true;
                double e = Ms(t0); ms += e; frameMs += e;
                return done;
            }
            // estimate to the goal: grid distance + the height still to climb / descend (walkable grades are at most
            // ~45 deg, so a storey above needs about as much walking again); without it the search floods the ground
            // under a deck before it looks for the stairs
            float Est(int x, int z, float y) => (H(x, z, tx, tz) + Mathf.Abs(y - goalY)) * GREED;
            /// cell centres (with floor heights) to the goal, or to the closest reachable cell when it was not reached
            public bool Result(List<Vector3> path)
            {
                path.Clear(); reached = hit >= 0; active = false;
                for (int i = hit >= 0 ? hit : best; i > 0; i = nodes[i].parent) path.Add(new Vector3(Cx(nodes[i].gx), nodes[i].y, Cx(nodes[i].gz)));
                path.Reverse(); return reached;
            }
            void Push(float f, int i)
            {
                heap.Add((f, i)); int c = heap.Count - 1;
                while (c > 0) { int p = (c - 1) / 2; if (heap[p].f <= heap[c].f) break; var t = heap[p]; heap[p] = heap[c]; heap[c] = t; c = p; }
            }
            int Pop()
            {
                int top = heap[0].i, last = heap.Count - 1; heap[0] = heap[last]; heap.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, m = c;
                    if (l < heap.Count && heap[l].f < heap[m].f) m = l; if (r < heap.Count && heap[r].f < heap[m].f) m = r;
                    if (m == c) break; var t = heap[m]; heap[m] = heap[c]; heap[c] = t; c = m;
                }
                return top;
            }
        }
        static readonly Search sync = new Search();
        /// Path (cell centres with floor heights) from a character at `from` to `goal` for an agent of `radius`, in one go.
        /// Returns true if the goal's storey was reached; otherwise `path` leads to the closest reachable cell
        /// (empty if the start itself is closest).
        public static bool FindPath(Vector3 from, Vector3 goal, float radius, List<Vector3> path)
        {
            sync.Start(from, goal, radius); sync.Step(double.MaxValue); return sync.Result(path);
        }

        /// can a character at `from` walk straight to `to` (grid edges along the segment) and end on to's storey?
        public static bool Straight(Vector3 from, Vector3 to, float radius, float maxLen = 40)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); Begin();
            bool ok = Walk(from, to, radius, maxLen);
            double e = Ms(t0); ms += e; frameMs += e;
            return ok;
        }
        static bool Walk(Vector3 from, Vector3 to, float radius, float maxLen)
        {
            int cls = ClassOf(radius);
            int gx = G(from.x), gz = G(from.z), tx = G(to.x), tz = G(to.z);
            var l = LayerAt(gx, gz, FloorAt(gx, gz, from.y), cls);
            float dx = to.x - from.x, dz = to.z - from.z, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len > maxLen) return false;
            int steps = Mathf.CeilToInt(len / (CELL * .5f));
            for (int k = 1; k <= steps && (gx != tx || gz != tz); k++)
            {
                float f = (float)k / steps; int nx = G(from.x + dx * f), nz = G(from.z + dz * f);
                if (nx == gx && nz == gz) continue;
                int d = Dir(nx - gx, nz - gz); if (d < 0) return false;
                if (!Edge(gx, gz, l, d, cls, out float ny)) return false;
                gx = nx; gz = nz; l = LayerAt(gx, gz, ny, cls);
            }
            return Mathf.Abs(l.y - QFloor(to.x, to.z, to.y)) < 1.2f;
        }
        static int Dir(int dx, int dz) { for (int d = 0; d < 8; d++) if (DX[d] == dx && DZ[d] == dz) return d; return -1; }
    }

    // Per-enemy path follower: walk straight while the target is directly reachable, otherwise follow an A* path
    // (re-planned every ~0.5 s), or wait at the closest reachable spot when there is none.
    public class NavAgent
    {
        public bool pathing, waiting;          // following a path / no path: parked at the closest reachable cell
        public float yaw;                      // walking direction while pathing
        public readonly List<Vector3> path = new List<Vector3>();
        readonly NavGrid.Search search = new NavGrid.Search();
        float replan, directCheck, stuckT; bool direct = true, reached; Vector3 lastPos; int idx, seenFrame = -1, aim = -1, aimIdx = -1; float aimT;
        static int sweepClock;

        /// steering toward `goal`; returns the walking direction (flat unit) or zero to stand still
        public Vector3 Steer(Vector3 pos, Vector3 goal, float radius, float dt)
        {
            replan -= dt; directCheck -= dt;
            // a new nav frame starts when any agent steers a second time (works in Simulate too, where Time stands still)
            if (seenFrame == NavGrid.navFrame) { NavGrid.navFrame++; NavGrid.frameMs = 0; }
            seenFrame = NavGrid.navFrame;
            bool budget = NavGrid.frameMs < NavGrid.frameBudget;
            if (directCheck <= 0 && budget)
            {
                directCheck = .2f + Random.value * .1f;
                direct = NavGrid.Straight(pos, goal, radius, 24);
                if (++sweepClock % 600 == 0) NavGrid.Sweep();
            }
            var toGoal = Sim.FlatY(goal - pos);
            if (direct) { pathing = waiting = false; path.Clear(); search.active = false; return toGoal.sqrMagnitude > 1e-6f ? toGoal.normalized : Vector3.zero; }
            // stuck on something the grid missed: plan again now
            if (!waiting && pathing && Sim.FlatY(pos - lastPos).magnitude < .2f * dt) stuckT += dt; else stuckT = 0;   // slower than 0.2 m/s
            lastPos = pos;
            // (re)plan: the search runs in slices within the frame budget; the old path is followed meanwhile
            if ((!pathing || replan <= 0 || stuckT > .6f) && !search.active && budget) { search.Start(pos, goal, radius); stuckT = 0; }
            if (search.active && NavGrid.frameMs < NavGrid.frameBudget && search.Step(NavGrid.frameBudget - NavGrid.frameMs))
            {
                reached = search.Result(path); idx = 0; pathing = true; aimIdx = -1; replan = .45f + Random.value * .1f;
            }
            if (!pathing) { waiting = false; return Vector3.zero; }   // first search still running
            while (idx < path.Count && Sim.FlatY(path[idx] - pos).sqrMagnitude < .35f * .35f) idx++;
            if (idx >= path.Count)
            {   // end of the path: at the goal storey, close in directly; otherwise wait here
                waiting = !reached;
                if (waiting) return Vector3.zero;
                return toGoal.sqrMagnitude > 1e-6f ? toGoal.normalized : Vector3.zero;
            }
            waiting = false;
            // look ahead: aim at the farthest of the next few waypoints that can be walked to straight
            aimT -= dt;
            if (aimIdx != idx || aimT <= 0 || aim >= path.Count)
            {   // re-aimed when a waypoint is passed or every 0.15 s
                aimIdx = idx; aimT = .15f; aim = idx;
                for (int j = Mathf.Min(path.Count - 1, idx + 5); j > idx; j--)
                    if (NavGrid.Straight(pos, path[j], radius, 8)) { aim = j; break; }
            }
            var target = path[Mathf.Max(aim, idx)];
            var dir = Sim.FlatY(target - pos);
            if (dir.sqrMagnitude < 1e-6f) return Vector3.zero;
            dir.Normalize(); yaw = Mathf.Atan2(dir.x, dir.z); return dir;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul
{
    // Port of enemies.js: machines possessed by human souls. Every attack is telegraphed on the
    // ground first (red area that fills up), then lands at the end of the wind-up.
    // All positions are SIM space (three.js coordinates); Unity transforms are written via Sim.ToUnity.
    // The parent Transform passed to EnemyManager must sit at the world origin (identity).

    public class PlayerState { public Vector3 pos; public bool alive = true; public float hp = 100, max = 100; }

    // ---------- ground telegraphs ----------
    // The shape is draped over the floor (terrain slopes, stairs within REACH of the origin) and only hits a target
    // standing within REACH above / below the floor it was cast on (a roof is safe from an attack on the street).
    public class Telegraph
    {
        static readonly Color RED = new Color(1.6f, .16f, .08f, 1);
        public const float REACH = 1.5f, LIFT = .05f, STEP = .35f;
        /// origin.y = floor height the telegraph was cast on
        public readonly string kind; public readonly Vector3 origin; public readonly float yaw, duration;
        public readonly EnemyAttack opts; public float t;
        readonly GameObject group; readonly Mesh fillMesh; readonly Material baseMat, fillMat, lineMat;
        // shape = columns of rays a + b * s (s in 0..sMax, facing space u = side, v = forward); hs[c][k] = draped height at s = k * ds
        readonly Vector2[] ca, cb; readonly float[][] hs; readonly float sMax, ds; readonly int rows; readonly Vector3[] fillVerts;
        readonly Quaternion rot;

        // kind: 'line' {width, length}, 'cone' {radius, angle}, 'circle' {radius}; yaw = facing (0 = +Z)
        public Telegraph(Transform parent, string kind, Vector3 origin, float yaw, EnemyAttack opts, float duration)
        {
            float baseY = WorldCollision.Floor(origin.x, origin.z, origin.y);   // a target in mid-jump: the floor under it
            this.kind = kind; this.origin = new Vector3(origin.x, baseY, origin.z); this.yaw = yaw; this.opts = opts; this.duration = duration;
            rot = Sim.AxisAngle(Vector3.up, yaw);
            group = new GameObject("telegraph-" + kind);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Sim.ToUnity(this.origin);
            group.transform.localRotation = Sim.ToUnity(rot);
            // rays
            if (kind == "line")
            {
                float w = opts.width / 2; int n = Mathf.Max(2, Mathf.CeilToInt(opts.width / STEP)) + 1;
                ca = new Vector2[n]; cb = new Vector2[n]; sMax = opts.length;
                for (int i = 0; i < n; i++) { ca[i] = new Vector2(-w + 2 * w * i / (n - 1), 0); cb[i] = new Vector2(0, 1); }
            }
            else
            {
                bool cone = kind == "cone"; int seg = cone ? 40 : 48; sMax = opts.radius;
                float start = cone ? Mathf.PI / 2 - opts.angle / 2 : 0, len = cone ? opts.angle : Mathf.PI * 2;
                ca = new Vector2[seg + 1]; cb = new Vector2[seg + 1];
                for (int i = 0; i <= seg; i++) { float th = start + len * i / seg; cb[i] = new Vector2(Mathf.Cos(th), Mathf.Sin(th)); }
            }
            rows = Mathf.Max(2, Mathf.CeilToInt(sMax / STEP)); ds = sMax / rows;
            hs = new float[ca.Length][];
            for (int c = 0; c < ca.Length; c++) { hs[c] = new float[rows + 1]; for (int k = 0; k <= rows; k++) hs[c][k] = Ground(ca[c] + cb[c] * (k * ds)); }
            baseMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .16f), Blend.Alpha, false, CullMode.Off, false);
            fillMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .34f), Blend.Alpha, false, CullMode.Off, false);
            lineMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .9f), Blend.Alpha, false, CullMode.Off, false);
            baseMat.renderQueue = 3001; fillMat.renderQueue = 3002; lineMat.renderQueue = 3003;
            var baseMesh = Grid(); fillVerts = new Vector3[ca.Length * (rows + 1)];
            fillMesh = Grid(); fillMesh.MarkDynamic(); Fill(.001f);
            MeshChild("base", baseMesh, baseMat); MeshChild("fill", fillMesh, fillMat);
            var lr = new GameObject("edge").AddComponent<LineRenderer>();
            lr.transform.SetParent(group.transform, false);
            lr.useWorldSpace = false; lr.sharedMaterial = lineMat; lr.widthMultiplier = .035f;
            lr.alignment = LineAlignment.TransformZ; lr.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            var pts = Edge(out bool loop);
            // LineRenderer is rotated 90 deg about X: its local (x, y, z) is the group's (x, -z, y), so the height goes to -z
            var lp = new Vector3[pts.Count]; for (int i = 0; i < pts.Count; i++) { var u = Sim.ToUnity(pts[i]); lp[i] = new Vector3(u.x, u.z, -(u.y + .01f)); }
            lr.positionCount = lp.Length; lr.SetPositions(lp); lr.loop = loop;
        }
        GameObject MeshChild(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(group.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }
        static Vector3 P(Vector2 uv, float h) => new Vector3(uv.x, h, uv.y);
        /// draped height (relative to origin.y) of the floor under facing-space point uv: terrain + stairs / decks
        /// within REACH of the cast floor, clamped to that band (beyond it the attack does not reach anyway)
        float Ground(Vector2 uv)
        {
            var w = origin + Sim.Rotate(rot, new Vector3(uv.x, 0, uv.y));
            return Mathf.Clamp(WorldCollision.Floor(w.x, w.z, origin.y, REACH), origin.y - REACH, origin.y + REACH) - origin.y + LIFT;
        }
        /// height along column c at distance s (linear between the samples)
        float HAt(int c, float s) { float f = Mathf.Clamp(s / ds, 0, rows); int k = Mathf.Min(rows - 1, (int)f); return Mathf.Lerp(hs[c][k], hs[c][k + 1], f - k); }
        Mesh Grid()
        {
            int n = ca.Length, R = rows + 1; var v = new Vector3[n * R]; var tris = new List<int>();
            for (int c = 0; c < n; c++) for (int k = 0; k < R; k++) v[c * R + k] = Sim.ToUnity(P(ca[c] + cb[c] * (k * ds), hs[c][k]));
            for (int c = 0; c + 1 < n; c++) for (int k = 0; k + 1 < R; k++) { int a = c * R + k, b = a + R; tris.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 }); }
            var m = new Mesh { name = "telegraph" }; m.vertices = v; m.triangles = tris.ToArray(); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        /// fill grown to fraction p along the rays (radius / length), still lying on the floor
        void Fill(float p)
        {
            int R = rows + 1;
            for (int c = 0; c < ca.Length; c++) for (int k = 0; k < R; k++) { float s = k * ds * p; fillVerts[c * R + k] = Sim.ToUnity(P(ca[c] + cb[c] * s, HAt(c, s) + .004f)); }
            fillMesh.vertices = fillVerts; fillMesh.RecalculateBounds();
        }
        List<Vector3> Edge(out bool loop)
        {
            var pts = new List<Vector3>(); loop = true; int n = ca.Length, last = n - 1;
            Vector3 At(int c, float s) => P(ca[c] + cb[c] * s, HAt(c, s));
            void Ray(int c, bool outward) { for (int k = 0; k <= rows; k++) pts.Add(At(c, (outward ? k : rows - k) * ds)); }
            if (kind == "line")
            {   // near edge, right side, far edge, left side
                for (int c = 0; c < n; c++) pts.Add(At(c, 0));
                Ray(last, true); for (int c = last - 1; c > 0; c--) pts.Add(At(c, sMax)); Ray(0, false);
            }
            else if (kind == "cone") { loop = false; Ray(0, true); for (int c = 1; c < n; c++) pts.Add(At(c, sMax)); Ray(last, false); }
            else for (int c = 0; c < last; c++) pts.Add(At(c, sMax));
            return pts;
        }
        public bool Update(float dt)
        {
            t += dt; float p = Mathf.Min(1, t / duration);
            Fill(Mathf.Max(.001f, p));
            float blink = p > .8f ? .55f + .45f * Mathf.Sin(t * 50) : 1;
            Mats.SetOpacity(lineMat, .9f * blink); Mats.SetOpacity(fillMat, .22f + .2f * p);
            return p >= 1;
        }
        public bool Contains(Vector3 point, float pad = .35f)
        {
            if (Mathf.Abs(point.y - origin.y) > REACH) return false;   // another storey / roof / ledge far above or below
            Vector3 d = Sim.FlatY(point - origin), f = Sim.Forward(yaw), r = new Vector3(f.z, 0, -f.x);
            float along = Vector3.Dot(d, f), side = Vector3.Dot(d, r);
            if (kind == "line") return along > -pad && along < opts.length + pad && Mathf.Abs(side) < opts.width / 2 + pad;
            float dist = d.magnitude;
            if (kind == "circle") return dist < opts.radius + pad;
            if (dist > opts.radius + pad) return false;
            return dist < pad * 2 || Mathf.Abs(Mathf.Atan2(side, along)) < opts.angle / 2 + pad / Mathf.Max(dist, .3f);
        }
        public void Dispose()
        {
            foreach (var mf in group.GetComponentsInChildren<MeshFilter>()) Object.Destroy(mf.sharedMesh);
            Object.Destroy(baseMat); Object.Destroy(fillMat); Object.Destroy(lineMat); Object.Destroy(group);
        }
    }

    // ---------- enemy types ----------
    public class EnemyAttack
    {
        public string name; public float rangeMin, rangeMax, cooldown, windup; public string kind;
        public float width, length, radius, angle;            // telegraph opts
        public float damage, dash, dashTime = -1, recover; public bool atTarget;
        public bool HasDash => dash > 0;
    }
    public class EnemyType
    {
        public string prefab; public float hp, speed, turn, keep, poise, damageScale = 1; public EnemyAttack[] attacks;
        public float size = 1;          // model scale (radius / height follow)
        public string bossName;         // non-null = boss: screen HP bar instead of the floating one
        public int soul = 16;
        /// boss ranged set (used beyond Enemy.BOSS_NEAR, 50 / 50): energy bolt + charge
        public EnemyAttack[] far;
        public static readonly Dictionary<string, EnemyType> TYPES = new Dictionary<string, EnemyType>
        {
            { "crawler", new EnemyType { prefab = "Prefabs/Crawler", hp = 60, speed = 3.3f, turn = 7, keep = 1.1f, poise = 0, attacks = new[] {
                new EnemyAttack { name = "lunge", rangeMin = 2.5f, rangeMax = 6.5f, cooldown = 2.6f, windup = .75f, kind = "line", width = 1.3f, length = 5.2f, damage = 14, dash = 5.0f, dashTime = .22f, recover = .7f },
                new EnemyAttack { name = "bite", rangeMin = 0, rangeMax = 2.2f, cooldown = 1.6f, windup = .55f, kind = "cone", radius = 2.1f, angle = 1.3f, damage = 10, recover = .55f },
            } } },
            { "warden", new EnemyType { prefab = "Prefabs/Warden", hp = 240, speed = 1.55f, turn = 3.2f, keep = 1.8f, poise = 60, attacks = new[] {
                new EnemyAttack { name = "sweep", rangeMin = 0, rangeMax = 3.4f, cooldown = 3.0f, windup = .95f, kind = "cone", radius = 3.8f, angle = 2.5f, damage = 22, recover = .85f },
                new EnemyAttack { name = "slam", rangeMin = 2.5f, rangeMax = 7, cooldown = 4.5f, windup = 1.15f, kind = "circle", radius = 2.5f, damage = 30, recover = 1.0f, atTarget = true },
                new EnemyAttack { name = "thrust", rangeMin = 3, rangeMax = 7.5f, cooldown = 3.8f, windup = .85f, kind = "line", width = 1.4f, length = 6.5f, damage = 24, recover = .9f },
            }, soul = 34 } },
            // Z0 boss: lies dormant until the fight, then attacks on a global 5 s clock (melee near, bolt / charge far).
            // No tear-off (뜯기) here: the rope arm is not owned yet in Z0, so the gimmick is the energy pillars (BossPillars).
            { "ashWarden", new EnemyType { prefab = "Prefabs/AshWarden", hp = 900, speed = 1.5f, turn = 2.4f, keep = 1.6f, poise = 150, size = 1.35f, bossName = "재의 감시자", soul = 80, far = new[] {
                // scrap beast at 1.35x (~9.5 m long): slow and heavy, big wind-ups, long recoveries (user: 위압감, 2026-11-07)
                // the hanging leg tears scrap off the ground and hurls it / curls up and rolls at you
                new EnemyAttack { name = "throw", rangeMin = 0, rangeMax = 99, windup = 1.6f, kind = "circle", radius = 3.2f, damage = 26, recover = .9f, atTarget = true },
                new EnemyAttack { name = "roll", rangeMin = 0, rangeMax = 99, windup = 1.4f, kind = "line", width = 3.4f, length = 15, damage = 30, dash = 13, dashTime = .6f, recover = 1.4f },
            }, attacks = new[] {
                // melee (within BOSS_NEAR, distances from the body centre): claw pounce, bite, body / tail sweep
                new EnemyAttack { name = "claw", rangeMin = 5, rangeMax = 10, cooldown = 0, windup = 1.05f, kind = "line", width = 3, length = 9, damage = 26, dash = 4, dashTime = .25f, recover = 1.2f },
                new EnemyAttack { name = "bite", rangeMin = 0, rangeMax = 6.5f, cooldown = 0, windup = .95f, kind = "cone", radius = 7.5f, angle = .9f, damage = 30, recover = 1.1f },
                new EnemyAttack { name = "sweep", rangeMin = 0, rangeMax = 7, cooldown = 0, windup = 1.15f, kind = "cone", radius = 8, angle = 2.8f, damage = 24, recover = 1.2f },
            } } },
        };
    }

    // A model part driven like a three.js Object3D: Euler XYZ rotation (radians) + position.
    public class Part
    {
        public readonly Transform t; public Vector3 rot; public Vector3 pos; public readonly Vector3 restPos, restRot;
        public Part(Transform t)
        {
            this.t = t; rot = restRot = Sim.ToEulerXYZ(Sim.FromUnity(t.localRotation)); pos = restPos = Sim.FromUnity(t.localPosition);
        }
        public void Apply() { t.localRotation = Sim.ToUnity(Sim.EulerXYZ(rot.x, rot.y, rot.z)); t.localPosition = Sim.ToUnity(pos); }
        public static Part Of(SoulModel m, string key) { var t = m.Part(key); return t ? new Part(t) : null; }
    }

    public class Enemy
    {
        public string type; public Vector3 pos; public float yaw, radius, height; public string state = "spawn";
        public bool alive => state != "dead";
        public float rotX, rotZ, scale = .01f;
        public readonly EnemyType def; public float hp;
        readonly EnemyManager mgr; readonly GameObject go; readonly SoulModel model;
        float t, walk, poiseDmg, flash, staggerTime, curSpeed; Vector3 velocity, dashDir;
        public readonly NavAgent nav = new NavAgent();   // chase: straight at the player, or an A* path around walls / up stairs
        public EnemyAttack attack; Telegraph tele; public Telegraph Tele => tele;
        readonly Dictionary<string, float> cool = new Dictionary<string, float>();
        public float hpFrac = 1;
        // ---- boss (def.bossName != null): dormant → wake → fight; phase 1 at 0 HP goes down instead of dying ----
        public const float BOSS_NEAR = 10, BOSS_INTERVAL = 5;   // near = melee (8 m in the spec, 10 m for the 1.35x body)
        public bool IsBoss => def.bossName != null;
        public int phase = 1;
        public float bossClock;                        // global attack clock (counts down while awake)
        public Color? eyeTint;                         // phase 2: red eyes
        public float liePose;                          // 1 = lying flat (dormant / down), 0 = standing
        float wakeTime = 1, curl, rollSpin;
        readonly List<int> farBag = new List<int>();   // far attacks: 2 throws + 2 rolls per shuffled bag
        public readonly Dictionary<string, int> picks = new Dictionary<string, int>();   // attack counts (tests)
        public bool Hittable => alive && state != "spawn" && state != "dormant" && state != "wake" && state != "down";
        // parts
        readonly Part body, head, jaw, torso, pelvisP; readonly Transform eyeT, coreT;
        readonly Material eyeMat, coreMat; readonly Color eyeColor, coreColor;
        class Leg { public Part hip, knee, ankle; public int index; public float side; }
        class Arm { public Part shoulder, elbow; }
        readonly List<Leg> legs = new List<Leg>(); readonly Leg legL, legR; readonly Arm armL, armR;
        // scrap beast rig (ashWarden, Codex 12): 3 crawl legs, spine / tail chains, one giant hanging arm
        readonly bool beast; readonly Part chestP, neckP, armSh, armEl, armWr; readonly Part[] spine, tail, fingers;
        static readonly Color SOUL = new Color(3, .75f, .3f, 1);

        public Enemy(EnemyManager mgr, string type, Vector3 p, bool quiet = false)
        {
            this.mgr = mgr; this.type = type; def = EnemyType.TYPES[type];
            go = Object.Instantiate(Resources.Load<GameObject>(def.prefab), mgr.parent); go.name = type;
            model = go.GetComponent<SoulModel>(); radius = model.radius * def.size; height = model.height * def.size;
            pos = p; floorY = p.y; yaw = Random.value * Mathf.PI * 2; hp = def.hp; walk = Random.value * 10;
            body = Part.Of(model, "body"); head = Part.Of(model, "head"); jaw = Part.Of(model, "jaw"); torso = Part.Of(model, "torso"); pelvisP = Part.Of(model, "pelvis");
            eyeT = model.Part("eye"); coreT = model.Part("core");
            eyeColor = SOUL; coreColor = SOUL;
            if (eyeT && eyeT.TryGetComponent<MeshRenderer>(out var er)) { eyeMat = er.material; eyeColor = Mats.GetColor(eyeMat); }
            if (coreT && coreT.TryGetComponent<MeshRenderer>(out var cr)) { coreMat = cr.material; coreColor = Mats.GetColor(coreMat); }
            beast = model.Part("arm/shoulder") != null;
            if (type == "crawler" || beast)
            {
                int n = model.Count("legs");
                for (int i = 0; i < n; i++) legs.Add(new Leg { hip = Part.Of(model, $"legs/{i}/hip"), knee = Part.Of(model, $"legs/{i}/knee"), ankle = Part.Of(model, $"legs/{i}/ankle"), index = (int)model.Num($"legs/{i}/index"), side = model.Num($"legs/{i}/side") });
            }
            if (beast)
            {
                Part[] Chain(string k) { var o = new List<Part>(); while (model.Part($"{k}/{o.Count}")) o.Add(Part.Of(model, $"{k}/{o.Count}")); return o.ToArray(); }
                chestP = Part.Of(model, "chest"); neckP = Part.Of(model, "neck");
                armSh = Part.Of(model, "arm/shoulder"); armEl = Part.Of(model, "arm/elbow"); armWr = Part.Of(model, "arm/wrist");
                spine = Chain("spine"); tail = Chain("tail"); fingers = Chain("arm/fingers");
            }
            else if (type != "crawler")
            {
                Leg L(string k) => model.Part(k + "/hip") ? new Leg { hip = Part.Of(model, k + "/hip"), knee = Part.Of(model, k + "/knee"), ankle = Part.Of(model, k + "/ankle") } : null;
                Arm A(string k) => model.Part(k + "/shoulder") ? new Arm { shoulder = Part.Of(model, k + "/shoulder"), elbow = Part.Of(model, k + "/elbow") } : null;
                legL = L("legL"); legR = L("legR"); armL = A("armL"); armR = A("armR");
            }
            WriteRoot();
            if (!quiet) mgr.vfx?.Flash(Sim.FlatY(p, .8f), 1.2f);
        }
        public float floorY;
        public Vector3 Center() => new Vector3(pos.x, pos.y + height * .5f, pos.z);
        public void Axis(out Vector3 a, out Vector3 b) { a = new Vector3(pos.x, pos.y + .2f, pos.z); b = new Vector3(pos.x, pos.y + Mathf.Max(.4f, height - .25f), pos.z); }
        public bool Damage(float amount, Vector3 from, bool heavy = false)
        {
            if (!Hittable) return false;
            hp -= amount; flash = 1; hpFrac = hp / def.hp;
            var away = Sim.Norm(Sim.FlatY(pos - from));
            velocity += away * (type == "crawler" ? 4 : 1.2f);
            poiseDmg += amount;
            if (hp <= 0)
            {
                if (IsBoss && phase == 1) { hp = 0; hpFrac = 0; KnockDown(); return true; }   // BossPillars takes it to phase 2
                Die(); return true;
            }
            // light machines flinch out of wind-ups; the warden only breaks when its poise is exhausted
            if ((type == "crawler" && (heavy || Random.value < .35f)) || (def.poise > 0 && poiseDmg >= def.poise)) { poiseDmg = 0; ToStagger(heavy ? .9f : .45f); }
            return true;
        }
        /// remove without a kill (lost boss fight): no soul burst, no onKill; gone next update
        public void Despawn() { ClearTele(); state = "dead"; t = 99; }
        public void ToStagger(float time) { if (!Hittable) return; ClearTele(); state = "stagger"; t = 0; staggerTime = time; }
        /// boss: lying switched off (no attacks, no hits, no HP bar) until Wake
        public void Dormant() { ClearTele(); state = "dormant"; t = 0; scale = 1; liePose = 1; velocity = Vector3.zero; }
        /// stand up over `time` seconds, then fight (first attack 2.5 s later)
        public void Wake(float time) { state = "wake"; t = 0; wakeTime = time; }
        /// phase 1 knockout: falls over and stays down until Revive
        public void KnockDown() { ClearTele(); state = "down"; t = 0; velocity = Vector3.zero; }
        public void Revive(float frac, float time) { phase = 2; hp = def.hp * frac; hpFrac = frac; Wake(time); }
        public void Heal(float amount) { hp = Mathf.Min(def.hp, hp + amount); hpFrac = hp / def.hp; }
        void ClearTele() { if (tele != null) { tele.Dispose(); tele = null; } }
        public void Die()
        {
            if (state == "dead") return;
            ClearTele(); state = "dead"; t = 0;
            mgr.ReleaseSoul(Center(), def.soul);
            mgr.onKill?.Invoke(this);
        }
        public Vector3 CoreWorld() => coreT ? Sim.FromUnity(coreT.position) : Center();
        /// false once the model is gone (Dispose / scene teardown): stale references must stop reading its parts
        public bool Exists => go != null;
        /// scrap beast: the twisted fourth leg's knee (where the leftover grudge condenses after purification)
        public Vector3 LimbWorld() => armEl != null ? Sim.FromUnity(armEl.t.position) : Center();
        static float AngleTo(float from, float to) => Sim.WrapPi(to - from);

        public bool Update(float dt, PlayerState player)
        {
            t += dt; var start = pos;
            var keys = new List<string>(cool.Keys); foreach (var k in keys) cool[k] -= dt;
            // knockback / separation velocity
            pos += velocity * dt; velocity *= Mathf.Exp(-dt * 8);
            var toP = Sim.FlatY(player.pos - pos); float dist = toP.magnitude, yawToP = Mathf.Atan2(toP.x, toP.z);
            void TurnTo(float y, float rate) { yaw += AngleTo(yaw, y) * Sim.Damp(rate, dt); }
            float move = 0; string pose = "walk";
            if (IsBoss && Hittable) bossClock -= dt;
            if (state == "dormant") { pose = "lie"; liePose = 1; }
            else if (state == "wake")
            {
                pose = "lie"; liePose = 1 - Sim.SmoothStep(t, 0, wakeTime * .55f);
                float rk = Mathf.Clamp01((t - wakeTime * .5f) / (wakeTime * .5f)); roar = rk > 0 ? Mathf.Sin(rk * Mathf.PI) : 0;   // roar once standing
                if (t > wakeTime) { liePose = 0; roar = 0; state = "chase"; t = 0; bossClock = 2.5f; }
            }
            else if (state == "down") { pose = "lie"; liePose = Mathf.MoveTowards(liePose, 1, dt * 1.6f); }
            else if (state == "chase" && IsBoss)
            {
                // one attack every BOSS_INTERVAL s: melee when close, otherwise 50 % energy bolt / 50 % charge
                TurnTo(yawToP, def.turn);
                bool sameLevel = Mathf.Abs(player.pos.y - pos.y) < 1.4f;
                if (bossClock <= 0 && !mgr.passive && player.alive && sameLevel && Mathf.Abs(AngleTo(yaw, yawToP)) < .6f)
                {
                    EnemyAttack a;
                    if (dist <= BOSS_NEAR)
                    {
                        var options = new List<EnemyAttack>();
                        foreach (var m in def.attacks) if (dist >= m.rangeMin && dist <= m.rangeMax) options.Add(m);
                        a = options.Count > 0 ? options[Random.Range(0, options.Count)] : def.attacks[0];
                    }
                    else
                    {
                        if (farBag.Count == 0) { farBag.AddRange(new[] { 0, 0, 1, 1 }); for (int i = 3; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = farBag[i]; farBag[i] = farBag[j]; farBag[j] = tmp; } }
                        a = def.far[farBag[0]]; farBag.RemoveAt(0);
                    }
                    picks[a.name] = (picks.TryGetValue(a.name, out var pc) ? pc : 0) + 1;
                    attack = a; state = "windup"; t = 0; bossClock = BOSS_INTERVAL;
                    var origin = a.atTarget ? player.pos : pos + Sim.Forward(yaw) * (a.kind == "line" ? radius * .6f : .1f);
                    tele = new Telegraph(mgr.parent, a.kind, origin, yaw, a, a.windup);
                }
                else if (dist > def.keep + radius) move = def.speed * Mathf.Min(1, (dist - def.keep - radius) / 1.2f + .3f);
            }
            else if (state == "spawn")
            {
                scale = Mathf.Min(1, .01f + t * 1.6f); if (t > .65f) { scale = 1; state = "chase"; t = 0; }
                pose = "idle";
            }
            else if (state == "chase")
            {
                TurnTo(nav.pathing && !nav.waiting ? nav.yaw : yawToP, def.turn);
                var options = new List<EnemyAttack>();
                foreach (var a in def.attacks) if (dist >= a.rangeMin && dist <= a.rangeMax && !(cool.TryGetValue(a.name, out var c) && c > 0)) options.Add(a);
                bool sameLevel = Mathf.Abs(player.pos.y - pos.y) < 1.4f;   // no attacks through floors
                if (options.Count > 0 && !mgr.passive && t > .35f && player.alive && sameLevel && Mathf.Abs(AngleTo(yaw, yawToP)) < .5f)
                {
                    var a = options[Random.Range(0, options.Count)];
                    attack = a; state = "windup"; t = 0;
                    var origin = a.atTarget ? player.pos : pos + Sim.Forward(yaw) * (a.kind == "line" ? radius * .6f : .1f);
                    tele = new Telegraph(mgr.parent, a.kind, origin, yaw, a, a.windup);
                }
                else if (dist > def.keep + radius) move = def.speed * Mathf.Min(1, (dist - def.keep - radius) / 1.2f + .3f);
            }
            else if (state == "windup")
            {
                pose = "windup";
                if (tele.Update(dt))
                {
                    // resolve: is the player inside the telegraph right now?
                    if (player.alive && tele.Contains(player.pos) && Mathf.Abs(player.pos.y - pos.y) < 1.6f) mgr.onPlayerHit?.Invoke(attack.damage, pos, this);
                    mgr.Impact(this, attack);
                    ClearTele(); state = "attack"; t = 0; cool[attack.name] = attack.cooldown;
                    if (attack.HasDash) dashDir = Sim.Forward(yaw);
                }
            }
            else if (state == "attack")
            {
                pose = "attack";
                var a = attack;
                if (a.HasDash && t - dt < a.dashTime)
                {
                    float E(float x) => 1 - Mathf.Pow(1 - Mathf.Min(1, x), 3);
                    float k = E(t / a.dashTime) - E((t - dt) / a.dashTime); pos += dashDir * (a.dash * k * Terrain.Slow(pos.x, pos.z));
                }
                if (t > (a.dashTime >= 0 ? a.dashTime : .18f)) { state = "recover"; t = 0; }
            }
            else if (state == "recover") { pose = "recover"; if (t > attack.recover) { state = "chase"; t = 0; } }
            else if (state == "stagger") { pose = "stagger"; if (t > staggerTime) { state = "chase"; t = 0; } }
            else if (state == "dead")
            {
                pose = "dead";
                if (IsBoss && t < 90) liePose = Mathf.MoveTowards(liePose, 1, dt * .8f);   // the beast's wreck stays (purify scene, rope part); Despawn removes it
                else
                {
                    pos.y = floorY - Mathf.Max(0, t - 1.2f) * .5f;
                    if (t > 3) return false;
                }
            }
            if (IsBoss && pose != "lie") liePose = Mathf.MoveTowards(liePose, 0, dt * 2);
            // roll charge: curl up during the wind-up, spin like a wheel along the dash, unroll in the recovery
            bool rolling = attack != null && attack.name == "roll";
            curl = Mathf.MoveTowards(curl, rolling && (pose == "windup" || pose == "attack") ? 1 : 0, dt * (pose == "recover" ? 2.5f : 1.6f));
            if (rolling && pose == "attack") rollSpin += dt * 22; else rollSpin = Mathf.MoveTowards(rollSpin, Mathf.Ceil(rollSpin / (Mathf.PI * 2)) * Mathf.PI * 2, dt * 9);
            // path finding: blocked from the player (wall, ledge, other storey) -> follow the A* path, or wait when there is none
            var walkDir = Sim.Forward(yaw);
            if (state == "chase")
            {
                var sd = nav.Steer(pos, player.pos, radius * .8f, dt);
                if (nav.pathing) { walkDir = sd; move = nav.waiting || sd == Vector3.zero ? 0 : def.speed; }
            }
            move *= Terrain.Slow(pos.x, pos.z);   // wading
            curSpeed = Mathf.Lerp(curSpeed, move, Sim.Damp(move > curSpeed ? 3.5f : 6, dt));
            if (curSpeed > .01f)
            {   // never walk off a drop higher than NavGrid.DROP (knockback still can)
                var stepV = walkDir * (curSpeed * dt); var ahead = pos + stepV + walkDir * (radius * .5f);
                if (NavGrid.Floor(ahead.x, ahead.z, start.y) >= start.y - NavGrid.DROP) pos += stepV;
            }
            move = curSpeed;
            walk += dt * (move > 0 ? move * (type == "crawler" ? 3.2f : 1.6f) : .4f);
            // keep inside the arena
            // keep inside the world; walls and ledges (machines cannot jump: only steps / ramps)
            if (Sim.FlatY(pos).magnitude > mgr.bounds) { var fl2 = Sim.SetLength(Sim.FlatY(pos), mgr.bounds); pos.x = fl2.x; pos.z = fl2.z; }
            if (state != "dead")
            {
                var mv = WorldCollision.Move(start, pos, radius * .8f, WorldCollision.STEP); pos.x = mv.x; pos.z = mv.z;
                float fl = WorldCollision.Floor(pos.x, pos.z, start.y, WorldCollision.STEP);
                pos.y = fl < start.y - .14f ? Mathf.MoveTowards(start.y, fl, 9 * dt) : fl; floorY = pos.y;
            }
            Animate(dt, pose, move);
            WriteRoot();
            // eye / soul glow: flare on wind-up, white flash on hit
            float flare = pose == "windup" ? .8f + .6f * Mathf.Min(1, t / (attack?.windup ?? 1)) : 0;
            flash = Mathf.Max(0, flash - dt * 6);
            float on = .04f + .96f * (1 - liePose);   // switched-off machine: eyes / core nearly dark
            if (eyeMat) { var c = Color.Lerp((eyeTint ?? eyeColor) * (on * (1 + flare)), new Color(4, 4, 4, 1), flash); c.a = 1; Mats.SetColor(eyeMat, c); }
            if (coreMat) { var c = (eyeTint.HasValue ? eyeTint.Value * .6f : coreColor) * (on * (.9f + .25f * Mathf.Sin(t * 6) + flare * .6f)); c.a = 1; Mats.SetColor(coreMat, c); }
            if (alive && liePose < .5f && Random.value < dt * (type != "crawler" ? 6 * def.size : 2.5f)) mgr.Wisp(CoreWorld(), .5f);
            return true;
        }
        void WriteRoot()
        {
            var tr = go.transform;
            if (rollSpin != 0 || curl > 0)
            {   // roll about the body centre (lifted by the curl so it does not sink into the ground)
                float h = height * .5f * curl; var q = Sim.AxisAngle(Vector3.up, yaw) * Sim.AxisAngle(Vector3.right, rotX + rollSpin);
                var off = new Vector3(0, h, 0) - Sim.Rotate(q, new Vector3(0, h, 0));
                tr.localPosition = Sim.ToUnity(pos + off); tr.localRotation = Sim.ToUnity(q);
                tr.localScale = Vector3.one * scale * def.size * (1 - .25f * curl);
                return;
            }
            tr.localPosition = Sim.ToUnity(pos);
            tr.localRotation = Sim.ToUnity(Sim.EulerXYZ(rotX, yaw, rotZ));
            tr.localScale = Vector3.one * scale * def.size;
        }
        // Scrap beast: heavy 3-leg lizard crawl (legs 120 deg apart in phase, the spine S-curves, footfalls thump), the fourth
        // leg grotesquely twisted and hauled up over the back: its knee is the high point and the shin / foot hang from it,
        // swinging late like a pendulum. Dormant / down: crouched (legs folded under, head on the forepaws, the hanging leg
        // draped over the back, chain tail coiled). Wake: rises, then roars (head up, jaw wide).
        // Offsets are added to the exported rest rotations (sim Euler XYZ: +x pitches down, +y turns left, z rolls).
        public float roar; float armLag, armLagV, prevYaw; readonly float[] lastLift = new float[4];
        void AnimateBeast(float dt, string pose, float move, float wind)
        {
            float S(float x) => Mathf.Sin(x);
            float w = walk, gait = Mathf.Min(1, move / Mathf.Max(.1f, def.speed));
            string a = attack?.name; bool act = pose == "windup" || pose == "attack" || pose == "recover";
            float rec = pose == "recover" ? Mathf.Max(0, 1 - t / Mathf.Max(.2f, attack.recover)) : 0, hit = pose == "attack" ? 1 : rec;
            float k = pose == "dead" ? 1 : liePose, up = 1 - k, idle = S(t * 1.1f);
            float dip = 0;   // body dips a little while a foot is in the air
            for (int i = 0; i < legs.Count; i++)
            {
                var L = legs[i]; if (L.hip == null) continue; float sd = L.side == 0 ? (i == 1 ? 1 : -1) : L.side; bool front = i < 2;
                float ph = w * 2.2f + i * Mathf.PI * 2 / 3, lift = Mathf.Max(0, Mathf.Cos(ph)) * .5f * gait, swing = S(ph) * .5f * gait;
                if (a == "claw" && act && i == 0) { lift = 1.1f * wind * (1 - hit) + .15f * hit; swing = .7f * wind - .6f * hit; }   // front-left claw reared high, then raked down
                // footfall: dust + thump (EnemyManager.onStep)
                if (lift <= .001f && lastLift[i] > .02f && gait > .2f && L.ankle != null) mgr.onStep?.Invoke(Sim.FromUnity(L.ankle.t.position), def.size);
                dip += lift * .08f; lastLift[i] = lift;
                // crouch: knee up and out, shin folded under the body; forelegs reach forward under the head
                float cLift = front ? .55f : .6f, cSwing = front ? .45f : -.35f, cKnee = -1.15f;
                float L1 = Mathf.Lerp(lift + curl * .9f, cLift, k), sw = Mathf.Lerp(swing, cSwing, k);
                L.hip.rot.y = L.hip.restRot.y - sd * sw; L.hip.rot.z = L.hip.restRot.z + sd * L1; L.hip.Apply();
                if (L.knee != null) { L.knee.rot.z = L.knee.restRot.z + sd * Mathf.Lerp(lift * .6f + curl * .6f, cKnee, k); L.knee.Apply(); }
            }
            if (pelvisP != null) { pelvisP.pos.y = pelvisP.restPos.y - k * .72f - dip * up - curl * .2f; pelvisP.Apply(); }
            // spine / tail: travelling S-wave (lizard), sweep whips the back + tail, roll curls it, crouch coils the tail round
            float sweep = a == "sweep" && act ? (wind * .55f - hit * 1.0f) : 0;
            for (int i = 0; i < spine.Length; i++)
            {
                var p = spine[i]; if (p == null) continue;
                p.rot.y = p.restRot.y + (S(w * 2.2f - i * .9f) * .16f * gait + idle * .015f + sweep * .35f) * up + k * .12f;
                p.rot.x = p.restRot.x + curl * .55f + (pose == "stagger" ? -.12f : 0) - roar * .08f; p.Apply();
            }
            for (int i = 0; i < tail.Length; i++)
            {
                var p = tail[i]; if (p == null) continue;
                p.rot.y = p.restRot.y + (S(w * 2.2f - (i + 3) * .9f) * (.16f + i * .05f) * (gait + .25f) + S(t * 1.3f - i) * .05f + sweep * (.5f + i * .15f)) * up + k * (.45f + i * .12f);
                p.rot.x = p.restRot.x + curl * .6f + k * .05f; p.Apply();
            }
            // chest / neck / head / jaw: big rear-ups on wind-ups, lunge on bites, flinch on stagger, roar on waking
            float rear = act ? (a == "bite" ? (-.35f * wind + .5f * hit) : a == "claw" ? (-.3f * wind + .3f * hit) : -.16f * wind) : 0;
            if (chestP != null) { chestP.rot.x = chestP.restRot.x + rear * .6f + curl * .4f + (pose == "stagger" ? -.25f : 0) + idle * .02f - roar * .2f; chestP.Apply(); }
            if (neckP != null) { neckP.rot.x = neckP.restRot.x + rear + curl * .5f + k * .55f - roar * .6f; neckP.rot.y = neckP.restRot.y + S(t * .7f) * .08f * up; neckP.Apply(); }
            if (head != null) { head.rot.x = head.restRot.x + (a == "bite" && act ? .2f * hit : 0) + S(t * 1.7f) * .03f * up - k * .2f - roar * .25f + roar * .06f * S(t * 40); head.Apply(); }
            if (jaw != null) { jaw.rot.x = jaw.restRot.x + (a == "bite" && act ? .8f * wind * (1 - hit) : .06f + .05f * S(t * 2.3f)) * up + roar * .85f; jaw.Apply(); }
            // hanging leg: pendulum lag (turning swings the dangling shin out, crawling bobs it)
            float yawRate = dt > 0 ? Sim.WrapPi(yaw - prevYaw) / dt : 0; prevYaw = yaw;
            float target = -yawRate * .35f + S(w * 4.4f) * .06f * gait;
            armLagV += ((target - armLag) * 18 - armLagV * 3.5f) * dt; armLag += armLagV * dt;
            // throw: knee as the pivot, the whole leg slams down beside the body and the toes clamp the scrap (0..35 %),
            // hauls it up over the back (35..95 %), then whips it forward (attack / recover)
            float slam = 0, haul = 0, whip = 0, clamp = 0;
            if (a == "throw" && act)
            {
                if (pose == "windup") { slam = wind < .35f ? Sim.SmoothStep(wind, 0, .3f) : 1 - Sim.SmoothStep(wind, .35f, .7f); haul = Sim.SmoothStep(wind, .35f, .95f); clamp = Sim.SmoothStep(wind, .22f, .34f); }
                else { whip = hit; clamp = 1 - Sim.SmoothStep(t, 0, .08f); }
            }
            if (armSh != null)
            {
                armSh.rot.z = armSh.restRot.z - slam * 1.15f + k * .35f;   // crouched: thigh leans over the spine
                armSh.rot.x = armSh.restRot.x - haul * .75f + whip * 1.0f + curl * .9f - k * 1.34f + S(t * .6f) * .03f;   // crouched: thigh laid back ~65 deg
                armSh.rot.y = armSh.restRot.y + (a == "sweep" && act ? sweep * .5f : 0); armSh.Apply();
            }
            if (armEl != null)
            {   // the knee: the shin hangs from it and lags; it straightens to reach the ground, folds to haul, snaps on the whip
                armEl.rot.x = armEl.restRot.x - slam * .9f + haul * .55f - whip * .7f + curl * .8f - k * 1.76f + armLag * .3f;   // crouched: shin draped down behind, foot on the ground
                armEl.rot.z = armEl.restRot.z + armLag * (1 - slam) * up; armEl.Apply();
            }
            if (armWr != null) { armWr.rot.x = armWr.restRot.x - slam * .35f + whip * .4f + k * .4f + armLag * .5f; armWr.rot.z = armWr.restRot.z + armLag * .6f * up; armWr.Apply(); }
            foreach (var f in fingers) if (f != null) { f.rot.x = f.restRot.x + (slam > .3f && clamp < .5f ? -.6f : .55f * clamp) - whip * .5f * (1 - clamp) + k * .3f; f.Apply(); }
            rotX = 0; rotZ = Mathf.Lerp(rotZ, pose == "dead" ? .25f : 0, Sim.Damp(4, dt));
        }
        void Animate(float dt, string pose, float move)
        {
            float w = walk; float S(float x) => Mathf.Sin(x);
            float wind = pose == "windup" ? Mathf.Min(1, t / attack.windup) : 0;
            if (type == "crawler")
            {
                float gait = move > 0 ? 1 : .15f;
                for (int i = 0; i < legs.Count; i++)
                {
                    var L = legs[i]; if (L.hip == null) continue;
                    float ph = w * 2 + (i % 2 == 1 ? Mathf.PI : 0) + L.index * .5f;
                    L.hip.rot.x = S(ph) * .55f * gait; L.hip.rot.z = L.side * (.25f + Mathf.Max(0, Mathf.Cos(ph)) * .35f * gait + wind * .3f); L.hip.Apply();
                    if (L.knee != null) { L.knee.rot.x = -.4f - Mathf.Max(0, S(ph + 1)) * .5f * gait - wind * .3f; L.knee.Apply(); }
                }
                float rear = pose == "windup" ? wind : pose == "attack" ? 1 : pose == "recover" ? Mathf.Max(0, 1 - t * 2) : 0;
                if (body != null) { body.rot.x = -rear * .45f + (pose == "stagger" ? .3f : 0); body.pos.y = body.restPos.y + Mathf.Abs(S(w * 4)) * .04f * (move > 0 ? 1 : 0); body.Apply(); }
                if (head != null) { head.rot.x = -rear * .3f + S(t * 7) * .04f * (1 - rear); head.Apply(); }
                if (jaw != null) { jaw.rot.x = rear * .7f + (pose == "attack" ? .3f : 0); jaw.Apply(); }
                if (pose == "dead") rotZ = Mathf.Lerp(rotZ, 2.6f, Sim.Damp(5, dt));
            }
            else if (beast) AnimateBeast(dt, pose, move, wind);
            else
            {
                float gait = move > 0 ? 1 : 0;
                foreach (var (L, sd) in new[] { (legL, 1f), (legR, -1f) })
                {
                    if (L == null) continue; float ph = w * 2 + (sd > 0 ? 0 : Mathf.PI);
                    L.hip.rot.x = S(ph) * .45f * gait - .2f; L.hip.Apply();
                    if (L.knee != null) { L.knee.rot.x = .6f + Mathf.Max(0, S(ph + 1)) * .5f * gait; L.knee.Apply(); }
                    if (L.ankle != null) { L.ankle.rot.x = -.4f; L.ankle.Apply(); }
                }
                string a = attack?.name;
                // ashWarden on the old warden rig until the scrap beast export (Codex 12): borrow the warden poses
                if (a == "claw") a = "thrust"; else if (a == "bite" || a == "throw") a = "slam";
                float rec = pose == "recover" ? Mathf.Max(0, 1 - t * 1.5f) : 0, hit = pose == "attack" ? 1 : rec;
                if (torso != null)
                {
                    torso.rot.x = .12f + (a == "slam" ? (-wind * .4f + hit * .6f) : hit * .2f) + S(t * 1.3f) * .03f;
                    torso.rot.y = a == "sweep" ? (wind * .9f - hit * 1.2f) : S(w) * .08f;
                }
                if (head != null) { head.rot.x = -.1f + S(t * 2.1f) * .05f; head.Apply(); }
                foreach (var (arm, sd) in new[] { (armL, -1f), (armR, 1f) })
                {
                    if (arm == null) continue;
                    float sx = -.2f + S(w + (sd > 0 ? Mathf.PI : 0)) * .25f * gait, sz = sd * .25f, el = -.5f;
                    if (a == "slam") { sx = Mathf.Lerp(sx, -2.7f, wind) * (1 - hit) + (-.6f) * hit; el = Mathf.Lerp(-.5f, -.3f, wind); }
                    else if (a == "sweep" && sd > 0) { sx = Mathf.Lerp(sx, -1.2f, wind); sz = Mathf.Lerp(sz, 1.4f, wind) * (1 - hit) + (-.6f) * hit; }
                    else if (a == "thrust" && sd > 0) { sx = Mathf.Lerp(sx, .6f, wind) * (1 - hit) + (-1.55f) * hit; el = Mathf.Lerp(-.5f, -1.6f, wind) * (1 - hit); }
                    arm.shoulder.rot.x = sx; arm.shoulder.rot.z = sz; arm.shoulder.Apply();
                    if (arm.elbow != null) { arm.elbow.rot.x = el; arm.elbow.Apply(); }
                }
                if (pose == "stagger" && torso != null) torso.rot.x -= .35f;
                torso?.Apply();
                if (pose == "dead") rotX = Mathf.Lerp(rotX, -1.45f, Sim.Damp(3, dt));
                else if (IsBoss) rotX = -1.45f * liePose;   // dormant / down: lying on its back
            }
        }
        public void Dispose()
        {
            ClearTele();
            if (eyeMat) Object.Destroy(eyeMat); if (coreMat) Object.Destroy(coreMat);
            Object.Destroy(go);
        }
    }

    public class EnemyManager
    {
        public readonly Transform parent; public readonly Vfx vfx; public readonly float bounds;
        public System.Action<float, Vector3, Enemy> onPlayerHit; public System.Action<Vector3, float> onStep; public System.Action<Enemy, EnemyAttack, Vector3> onImpact; public System.Action<Enemy> onKill;
        public List<Enemy> list = new List<Enemy>();
        // Nothing spawns on its own: monsters are summoned. passive = they follow and take hits but
        // never attack (training-dummy mode for motion tests).
        public bool passive;
        public readonly Dictionary<string, int> want = new Dictionary<string, int> { { "crawler", 0 }, { "warden", 0 } };
        float spawnClock = 1.2f;

        class WispFx { public GameObject go; public Material mat; public Vector3 pos, v; public float life, max, size; }
        readonly List<WispFx> wisps = new List<WispFx>(), pool = new List<WispFx>();
        readonly Texture2D glow; readonly Mesh quad; readonly Transform wispRoot;

        public EnemyManager(Transform parent, Vfx vfx, float bounds = 18, Transform sceneWispRoot = null)
        {
            this.parent = parent; this.vfx = vfx; this.bounds = bounds;
            glow = Mats.Radial(64, (0, new Color(1, 220 / 255f, 180 / 255f, 1)), (.3f, new Color(1, 90 / 255f, 40 / 255f, .7f)), (1, new Color(160 / 255f, 20 / 255f, 10 / 255f, 0)));
            quad = new Mesh { name = "wisp-quad" };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateBounds();
            wispRoot = sceneWispRoot;
        }
        /// boss lying switched off at `pos`, facing `yaw` (no spawn flash / soul burst)
        public Enemy SpawnDormant(string type, Vector3 pos, float yaw)
        {
            var p = Sim.FlatY(pos); p.y = WorldCollision.Floor(p.x, p.z, pos.y, .6f);
            var e = new Enemy(this, type, p, true) { yaw = yaw }; e.Dormant(); list.Add(e); return e;
        }
        // summon at an exact spot (clamped to the arena)
        public Enemy SpawnAt(string type, Vector3 pos)
        {
            var p = Sim.FlatY(pos); if (p.magnitude > bounds - 1) p = Sim.SetLength(p, bounds - 1);
            p.y = WorldCollision.Floor(p.x, p.z, pos.y, .6f); var e = new Enemy(this, type, p); list.Add(e); ReleaseSoul(Sim.FlatY(p, .4f), 10, true); return e;
        }
        public Enemy Spawn(string type, Vector3 near)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                float a = Random.value * Mathf.PI * 2, r = 8 + Random.value * 4;
                var p = Sim.FlatY(near + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                if (p.magnitude > bounds - 1) continue;
                var e = new Enemy(this, type, p); list.Add(e); ReleaseSoul(Sim.FlatY(p, .4f), 10, true); return e;
            }
            return null;
        }
        public void Impact(Enemy enemy, EnemyAttack attack)
        {
            var p = attack.atTarget && enemy.Tele != null ? enemy.Tele.origin : enemy.pos + Sim.Forward(enemy.yaw) * 1.2f;
            onImpact?.Invoke(enemy, attack, p);
        }
        public void ReleaseSoul(Vector3 pos, int n, bool inward = false)
        {
            vfx?.Flash(pos, inward ? .8f : 1.6f);
            for (int i = 0; i < n; i++) Wisp(pos, 1.2f, inward);
        }
        public void Wisp(Vector3 pos, float power = .5f, bool inward = false)
        {
            WispFx w;
            if (pool.Count > 0) { w = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); w.go.SetActive(true); }
            else
            {
                w = new WispFx { go = new GameObject("wisp") }; w.go.transform.SetParent(wispRoot, false);
                w.go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = w.go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                w.mat = Mats.Unlit(new Color(1.6f, 1.1f, 1, 1), Blend.Add, false, CullMode.Off, false, true, glow); mr.sharedMaterial = w.mat;
            }
            var off = new Vector3((Random.value - .5f) * .4f, (Random.value - .5f) * .3f, (Random.value - .5f) * .4f);
            w.pos = pos + off; w.size = .12f + Random.value * .12f * power;
            w.v = new Vector3((Random.value - .5f) * .6f * power, .6f + Random.value * 1.2f * power, (Random.value - .5f) * .6f * power);
            if (inward) { w.v = off * -2; w.v.y = .8f; }
            w.life = 0; w.max = .8f + Random.value * 1.2f * power;
            Mats.SetOpacity(w.mat, 1);
            wisps.Add(w); PlaceWisp(w, w.size);
        }
        void PlaceWisp(WispFx w, float size)
        {
            var tr = w.go.transform; tr.localPosition = Sim.ToUnity(w.pos); tr.localScale = Vector3.one * size;
            var cam = Camera.main; if (cam) tr.rotation = cam.transform.rotation;
        }
        // blade / wave hit test against every living enemy's body axis
        public (Enemy e, float d, Vector3 point)? ClosestHit(Vector3 p, Vector3 q, float reach)
        {
            (Enemy, float, Vector3)? best = null;
            foreach (var e in list)
            {
                if (!e.Hittable) continue;
                e.Axis(out var s0, out var s1);
                float d = Sim.SegDist(p, q, s0, s1, out var a, out var b) - e.radius * .8f;
                if (d < reach && (best == null || d < best.Value.Item2)) best = (e, d, Vector3.Lerp(a, b, .4f));
            }
            return best;
        }
        public List<(Enemy e, Vector3 point)> HitsAlong(Vector3 p, Vector3 q, float reach)
        {
            var outList = new List<(Enemy, Vector3)>();
            foreach (var e in list)
            {
                if (!e.Hittable) continue;
                e.Axis(out var s0, out var s1);
                if (Sim.SegDist(p, q, s0, s1, out var a, out var b) - e.radius * .8f < reach) outList.Add((e, Vector3.Lerp(a, b, .4f)));
            }
            return outList;
        }
        public void Update(float dt, PlayerState player)
        {
            // keep the field populated (want = 0 by default: summon only)
            spawnClock -= dt;
            if (spawnClock <= 0 && player.alive)
            {
                foreach (var kv in want) { int have = list.FindAll(e => e.type == kv.Key && e.alive).Count; if (have < kv.Value) { Spawn(kv.Key, player.pos); break; } }
                spawnClock = 1.6f;
            }
            // separation between machines and from the player
            foreach (var e in list)
            {
                if (!e.alive || e.liePose > 0) continue;   // a lying boss does not get shoved
                foreach (var o in list)
                {
                    if (o == e || !o.alive) continue;
                    var d = Sim.FlatY(e.pos - o.pos); float l = d.magnitude, min = e.radius + o.radius;
                    if (l < min && l > 1e-4f) e.pos += d / l * ((min - l) * .5f);
                }
                var dp = Sim.FlatY(e.pos - player.pos); float lp = dp.magnitude, minp = e.radius + .4f;
                if (lp < minp && lp > 1e-4f) e.pos += dp / lp * (minp - lp);
            }
            for (int i = list.Count - 1; i >= 0; i--) if (!list[i].Update(dt, player)) { list[i].Dispose(); list.RemoveAt(i); }
            for (int i = wisps.Count - 1; i >= 0; i--)
            {
                var w = wisps[i]; w.life += dt; float t = w.life / w.max;
                if (t >= 1) { w.go.SetActive(false); wisps.RemoveAt(i); pool.Add(w); continue; }
                w.v.x += Mathf.Sin(w.life * 5 + i) * dt * .8f; w.pos += w.v * dt;
                Mats.SetOpacity(w.mat, Mathf.Pow(1 - t, 1.5f)); PlaceWisp(w, w.size * (1 + t * .8f));
            }
        }
        public void ClearNear(Vector3 pos, float r) { foreach (var e in list.ToArray()) if (e.alive && Vector3.Distance(e.pos, pos) < r) e.Die(); }
        public void ClearAll() { foreach (var e in list.ToArray()) if (e.alive) e.Die(); }

        // HP bars (three.js sprite 1.1 x .12 m above the head): call from OnGUI.
        public void DrawGUI(Camera cam)
        {
            if (!cam || Event.current.type != EventType.Repaint) return;
            var right = cam.transform.right;
            foreach (var e in list)
            {
                if (!e.alive || e.def.bossName != null) continue;
                var w = Sim.ToUnity(e.pos + Vector3.up * (e.height * e.scale + .45f));
                Vector3 a = cam.WorldToScreenPoint(w - right * .55f * e.scale), b = cam.WorldToScreenPoint(w + right * .55f * e.scale), c = cam.WorldToScreenPoint(w);
                if (c.z <= 0) continue;
                float width = Mathf.Max(8, Vector2.Distance(a, b)), height = width * .12f / 1.1f;
                var r = new Rect(c.x - width / 2, Screen.height - c.y - height / 2, width, height);
                var prev = GUI.color;
                GUI.color = new Color(0, 0, 0, 170 / 255f); GUI.DrawTexture(r, Texture2D.whiteTexture);
                float pad = width * 2 / 128f;
                GUI.color = new Color(1, 90 / 255f, 58 / 255f, 1);
                GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, Mathf.Max(0, e.hpFrac) * (width - 2 * pad), height - 2 * pad), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }
    }
}

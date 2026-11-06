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
    public class Telegraph
    {
        static readonly Color RED = new Color(1.6f, .16f, .08f, 1);
        public readonly string kind; public readonly Vector3 origin; public readonly float yaw, duration;
        public readonly EnemyAttack opts; public float t;
        readonly GameObject group; readonly Transform fill; readonly Material baseMat, fillMat, lineMat;

        // kind: 'line' {width, length}, 'cone' {radius, angle}, 'circle' {radius}; yaw = facing (0 = +Z)
        public Telegraph(Transform parent, string kind, Vector3 origin, float yaw, EnemyAttack opts, float duration)
        {
            this.kind = kind; this.origin = Sim.FlatY(origin); this.yaw = yaw; this.opts = opts; this.duration = duration;
            group = new GameObject("telegraph-" + kind);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = Sim.ToUnity(new Vector3(origin.x, origin.y + .045f, origin.z));
            group.transform.localRotation = Sim.ToUnity(Sim.AxisAngle(Vector3.up, yaw));
            var shape = Shape();
            baseMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .16f), Blend.Alpha, false, CullMode.Off, false);
            fillMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .34f), Blend.Alpha, false, CullMode.Off, false);
            lineMat = Mats.Unlit(new Color(RED.r, RED.g, RED.b, .9f), Blend.Alpha, false, CullMode.Off, false);
            baseMat.renderQueue = 3001; fillMat.renderQueue = 3002; lineMat.renderQueue = 3003;
            MeshChild("base", shape, baseMat);
            fill = MeshChild("fill", shape, fillMat).transform; fill.localScale = Vector3.one * .001f;
            var lr = new GameObject("edge").AddComponent<LineRenderer>();
            lr.transform.SetParent(group.transform, false);
            lr.useWorldSpace = false; lr.sharedMaterial = lineMat; lr.widthMultiplier = .035f;
            lr.alignment = LineAlignment.TransformZ; lr.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            var pts = Edge(out bool loop);
            // LineRenderer is rotated so its local XY is the ground plane: (x, y) <- (unity x, unity z)
            var lp = new Vector3[pts.Count]; for (int i = 0; i < pts.Count; i++) { var u = Sim.ToUnity(pts[i]); lp[i] = new Vector3(u.x, u.z, 0); }
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
        // shape points in facing space (u = side, v = forward), returned in sim local (u, 0, v)
        static Vector3 P(float u, float v) => new Vector3(u, 0, v);
        Mesh Shape()
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            if (kind == "line")
            {
                float w = opts.width / 2, l = opts.length;
                verts.AddRange(new[] { P(-w, 0), P(w, 0), P(w, l), P(-w, l) });
                tris.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            }
            else
            {
                bool cone = kind == "cone"; int seg = cone ? 40 : 48;
                float r = opts.radius, start = cone ? Mathf.PI / 2 - opts.angle / 2 : 0, len = cone ? opts.angle : Mathf.PI * 2;
                verts.Add(Vector3.zero);
                for (int i = 0; i <= seg; i++) { float th = start + len * i / seg; verts.Add(P(Mathf.Cos(th) * r, Mathf.Sin(th) * r)); }
                for (int i = 0; i < seg; i++) tris.AddRange(new[] { 0, i + 1, i + 2 });
            }
            var m = new Mesh { name = "telegraph" };
            var uv = new Vector3[verts.Count]; for (int i = 0; i < verts.Count; i++) uv[i] = Sim.ToUnity(verts[i]);
            m.vertices = uv; m.triangles = tris.ToArray(); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        List<Vector3> Edge(out bool loop)
        {
            var pts = new List<Vector3>(); loop = true;
            if (kind == "line") { float w = opts.width / 2, l = opts.length; pts.AddRange(new[] { P(-w, 0), P(w, 0), P(w, l), P(-w, l) }); }
            else if (kind == "cone")
            {
                float r = opts.radius, a = opts.angle; loop = false; pts.Add(Vector3.zero);
                for (int i = 0; i <= 32; i++) { float th = Mathf.PI / 2 - a / 2 + a * i / 32; pts.Add(P(Mathf.Cos(th) * r, Mathf.Sin(th) * r)); }
                pts.Add(Vector3.zero);
            }
            else for (int i = 0; i < 48; i++) { float th = Mathf.PI * 2 * i / 48; pts.Add(P(Mathf.Cos(th) * opts.radius, Mathf.Sin(th) * opts.radius)); }
            return pts;
        }
        public bool Update(float dt)
        {
            t += dt; float p = Mathf.Min(1, t / duration);
            fill.localScale = kind == "line" ? new Vector3(1, 1, Mathf.Max(.001f, p)) : Vector3.one * Mathf.Max(.001f, p);
            float blink = p > .8f ? .55f + .45f * Mathf.Sin(t * 50) : 1;
            Mats.SetOpacity(lineMat, .9f * blink); Mats.SetOpacity(fillMat, .22f + .2f * p);
            return p >= 1;
        }
        public bool Contains(Vector3 point, float pad = .35f)
        {
            Vector3 d = Sim.FlatY(point) - origin, f = Sim.Forward(yaw), r = new Vector3(f.z, 0, -f.x);
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
            // Z0 boss (vertical slice stand-in): the hollow warden at 1.6x with wider reach and a ground quake.
            // The tear-off gimmick (뜯기) comes with the first real boss (stage 5).
            { "ashWarden", new EnemyType { prefab = "Prefabs/AshWarden", hp = 900, speed = 1.9f, turn = 3.6f, keep = 2.6f, poise = 150, size = 1f, bossName = "재의 감시자", soul = 80, attacks = new[] {
                new EnemyAttack { name = "sweep", rangeMin = 0, rangeMax = 5.2f, cooldown = 2.8f, windup = .9f, kind = "cone", radius = 5.8f, angle = 2.6f, damage = 24, recover = .8f },
                new EnemyAttack { name = "slam", rangeMin = 3, rangeMax = 10, cooldown = 4.2f, windup = 1.1f, kind = "circle", radius = 3.6f, damage = 32, recover = .95f, atTarget = true },
                new EnemyAttack { name = "thrust", rangeMin = 4, rangeMax = 11, cooldown = 3.6f, windup = .85f, kind = "line", width = 2.1f, length = 10, damage = 26, recover = .9f },
                new EnemyAttack { name = "quake", rangeMin = 0, rangeMax = 6, cooldown = 9, windup = 1.4f, kind = "circle", radius = 7, damage = 28, recover = 1.4f },
            } } },
        };
    }

    // A model part driven like a three.js Object3D: Euler XYZ rotation (radians) + position.
    public class Part
    {
        public readonly Transform t; public Vector3 rot; public Vector3 pos; public readonly Vector3 restPos;
        public Part(Transform t)
        {
            this.t = t; rot = Sim.ToEulerXYZ(Sim.FromUnity(t.localRotation)); pos = restPos = Sim.FromUnity(t.localPosition);
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
        public EnemyAttack attack; Telegraph tele; public Telegraph Tele => tele;
        readonly Dictionary<string, float> cool = new Dictionary<string, float>();
        public float hpFrac = 1;
        // parts
        readonly Part body, head, jaw, torso, pelvisP; readonly Transform eyeT, coreT;
        readonly Material eyeMat, coreMat; readonly Color eyeColor, coreColor;
        class Leg { public Part hip, knee, ankle; public int index; public float side; }
        class Arm { public Part shoulder, elbow; }
        readonly List<Leg> legs = new List<Leg>(); readonly Leg legL, legR; readonly Arm armL, armR;
        static readonly Color SOUL = new Color(3, .75f, .3f, 1);

        public Enemy(EnemyManager mgr, string type, Vector3 p)
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
            if (type == "crawler")
            {
                int n = model.Count("legs");
                for (int i = 0; i < n; i++) legs.Add(new Leg { hip = Part.Of(model, $"legs/{i}/hip"), knee = Part.Of(model, $"legs/{i}/knee"), index = (int)model.Num($"legs/{i}/index"), side = model.Num($"legs/{i}/side") });
            }
            else
            {
                Leg L(string k) => model.Part(k + "/hip") ? new Leg { hip = Part.Of(model, k + "/hip"), knee = Part.Of(model, k + "/knee"), ankle = Part.Of(model, k + "/ankle") } : null;
                Arm A(string k) => model.Part(k + "/shoulder") ? new Arm { shoulder = Part.Of(model, k + "/shoulder"), elbow = Part.Of(model, k + "/elbow") } : null;
                legL = L("legL"); legR = L("legR"); armL = A("armL"); armR = A("armR");
            }
            WriteRoot();
            mgr.vfx?.Flash(Sim.FlatY(p, .8f), 1.2f);
        }
        public float floorY;
        public Vector3 Center() => new Vector3(pos.x, pos.y + height * .5f, pos.z);
        public void Axis(out Vector3 a, out Vector3 b) { a = new Vector3(pos.x, pos.y + .2f, pos.z); b = new Vector3(pos.x, pos.y + Mathf.Max(.4f, height - .25f), pos.z); }
        public bool Damage(float amount, Vector3 from, bool heavy = false)
        {
            if (!alive || state == "spawn") return false;
            hp -= amount; flash = 1; hpFrac = hp / def.hp;
            var away = Sim.Norm(Sim.FlatY(pos - from));
            velocity += away * (type == "crawler" ? 4 : 1.2f);
            poiseDmg += amount;
            if (hp <= 0) { Die(); return true; }
            // light machines flinch out of wind-ups; the warden only breaks when its poise is exhausted
            if ((type == "crawler" && (heavy || Random.value < .35f)) || (def.poise > 0 && poiseDmg >= def.poise)) { poiseDmg = 0; ToStagger(heavy ? .9f : .45f); }
            return true;
        }
        /// remove without a kill (lost boss fight): no soul burst, no onKill; gone next update
        public void Despawn() { ClearTele(); state = "dead"; t = 99; }
        public void ToStagger(float time) { ClearTele(); state = "stagger"; t = 0; staggerTime = time; }
        void ClearTele() { if (tele != null) { tele.Dispose(); tele = null; } }
        public void Die()
        {
            if (state == "dead") return;
            ClearTele(); state = "dead"; t = 0;
            mgr.ReleaseSoul(Center(), def.soul);
            mgr.onKill?.Invoke(this);
        }
        public Vector3 CoreWorld() => coreT ? Sim.FromUnity(coreT.position) : Center();
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
            if (state == "spawn")
            {
                scale = Mathf.Min(1, .01f + t * 1.6f); if (t > .65f) { scale = 1; state = "chase"; t = 0; }
                pose = "idle";
            }
            else if (state == "chase")
            {
                TurnTo(yawToP, def.turn);
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
                pos.y = floorY - Mathf.Max(0, t - 1.2f) * .5f;
                if (t > 3) return false;
            }
            move *= Terrain.Slow(pos.x, pos.z);   // wading
            curSpeed = Mathf.Lerp(curSpeed, move, Sim.Damp(move > curSpeed ? 3.5f : 6, dt));
            if (curSpeed > .01f) pos += Sim.Forward(yaw) * (curSpeed * dt);
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
            if (eyeMat) { var c = Color.Lerp(eyeColor * (1 + flare), new Color(4, 4, 4, 1), flash); c.a = 1; Mats.SetColor(eyeMat, c); }
            if (coreMat) { var c = coreColor * (.9f + .25f * Mathf.Sin(t * 6) + flare * .6f); c.a = 1; Mats.SetColor(coreMat, c); }
            if (alive && Random.value < dt * (type != "crawler" ? 6 * def.size : 2.5f)) mgr.Wisp(CoreWorld(), .5f);
            return true;
        }
        void WriteRoot()
        {
            var tr = go.transform;
            tr.localPosition = Sim.ToUnity(pos);
            tr.localRotation = Sim.ToUnity(Sim.EulerXYZ(rotX, yaw, rotZ));
            tr.localScale = Vector3.one * scale * def.size;
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
                string a = attack?.name; float rec = pose == "recover" ? Mathf.Max(0, 1 - t * 1.5f) : 0, hit = pose == "attack" ? 1 : rec;
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
        public System.Action<float, Vector3, Enemy> onPlayerHit; public System.Action<Enemy, EnemyAttack, Vector3> onImpact; public System.Action<Enemy> onKill;
        public List<Enemy> list = new List<Enemy>();
        // Nothing spawns on its own: monsters are summoned. passive = they follow and take hits but
        // never attack (training-dummy mode for motion tests).
        public bool passive;
        public readonly Dictionary<string, int> want = new Dictionary<string, int> { { "crawler", 0 }, { "warden", 0 } };
        float spawnClock = 1.2f;

        class WispFx { public GameObject go; public Material mat; public Vector3 pos, v; public float life, max, size; }
        readonly List<WispFx> wisps = new List<WispFx>(), pool = new List<WispFx>();
        readonly Texture2D glow; readonly Mesh quad; readonly Transform wispRoot;

        public EnemyManager(Transform parent, Vfx vfx, float bounds = 18)
        {
            this.parent = parent; this.vfx = vfx; this.bounds = bounds;
            glow = Mats.Radial(64, (0, new Color(1, 220 / 255f, 180 / 255f, 1)), (.3f, new Color(1, 90 / 255f, 40 / 255f, .7f)), (1, new Color(160 / 255f, 20 / 255f, 10 / 255f, 0)));
            quad = new Mesh { name = "wisp-quad" };
            quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 }; quad.RecalculateBounds();
            wispRoot = new GameObject("soul-wisps").transform; wispRoot.SetParent(parent, false);
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
                if (!e.alive || e.state == "spawn") continue;
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
                if (!e.alive || e.state == "spawn") continue;
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
                if (!e.alive) continue;
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

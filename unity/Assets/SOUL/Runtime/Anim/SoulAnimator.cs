using System;
using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Literal port of web-proto/src/anim.js (Animator). Sim space throughout; see Sim.cs.
    // Pipeline per update: updateMove | locomotion -> dynamics -> apply (blend + FK clamp)
    // -> legs (moveLegs / runLegs / plantFeet) -> weapon (arm-blade solver) -> audit.
    public class SoulAnimator
    {
        // rig dimensions (rig.js / anim.js)
        public const float REST_HEIGHT = .96f;
        public const float ARM_UPPER = .34f, ARM_FORE = .30f;
        const float ANKLE = .10f, THIGH = .43f, SHIN = .40f, UPPER = ARM_UPPER, FORE = ARM_FORE;
        const float BLADE_FROM = .12f, BLADE_END = 1.0f, BLADE_R = .05f, WRIST_MAX = 0, LAG_F = 0, LAG_D = .022f;
        const float BLADE_LEN = BLADE_END;
        static readonly float rad = Mathf.Deg2Rad;
        static readonly Vector3 up = Vector3.up;
        static readonly string[] FK_CLAMP = { "spine", "chest", "neck", "head", "Lclavicle", "Rclavicle", "LupperArm", "Lforearm" };
        static readonly string[] TORSO_ELBOW = { "pelvis", "belly", "chest" };
        static readonly string[] FOREARM_BLOCKERS = { "belly", "chest", "neck", "head" };
        static readonly float[][] RUN_KEYS = { new[] { 0f, -30, 22, -5 }, new[] { .15f, -12, 38, 5 }, new[] { .35f, 18, 20, 35 }, new[] { .60f, -8, 105, 15 }, new[] { .85f, -48, 50, -10 } };

        // events (main.js wires these)
        public Action<string> willPlay; public Action dust; public Action<Vector3> step; public Action land; public Action buffer;
        public Action<string, Move, float> impact; public Action<string, string> fx;

        public readonly CharacterRig ch;
        readonly Dictionary<string, TNode> j;
        public string name = "idle";
        public float time, worldTime, speed, phase, charge, blend = 1;
        public bool buffered, impacted;
        public float? pendingYaw;
        public Vector3 velocity, body, idleBob;
        public Dictionary<string, float> stats = new Dictionary<string, float>();
        readonly Dictionary<string, Quaternion> pose = new Dictionary<string, Quaternion>();
        Dictionary<string, Quaternion> from; Vector3? fromBody;
        float startYaw, yawOffset, runW, runPhase, runBob, roll; bool wasRunning;
        Vector3? accel; Vector3? prevTipL; public Vector3? tipVel; Vector3 tipRel = new Vector3(0, 0, 1);
        Vector3? smoothU, smoothD; Vector3? hingeL; Vector3 hinge; float dt;
        float[] dynTwist = new float[2], dynLean = new float[2], dynSide = new float[2], dynDrop = new float[2];
        class Foot { public Vector3 target, start; public bool wasStance, moving; }
        class MFoot { public Vector3 pos, from; public float yaw, fromYaw; public Step step; }
        Dictionary<string, Foot> feet = new Dictionary<string, Foot>();
        Dictionary<string, MFoot> mfeet = new Dictionary<string, MFoot>();
        class Spring { public Vector3 u, d, vu, vd; }
        Spring armSpring;
        public (Vector3 Ut, Vector3 Ft, Vector3 Dt, Vector3 u, Vector3 f, Vector3 d) dbg;

        // ---------- elevation ----------
        /// floor height at (x, z) for the feet (set by the game: WorldCollision.Floor); 0 = flat world
        public Func<float, float, float> groundAt = (x, z) => 0;
        /// true while falling off a ledge: leg IK is suspended
        public bool airborne;
        /// movement multiplier from the ground (water: Terrain.Slow); scales locomotion speed and move root motion
        public float speedScale = 1;
        float airLift, airLiftT0, airLiftT1;
        float A(Vector3 p) => ANKLE + groundAt(p.x, p.z);
        /// current jump-arc height above the take-off floor (0 when not in an arc)
        public float ArcHeight { get { var m = move; if (m?.arc == null || time <= m.arc[0] || time >= m.arc[1]) return 0; float s = (time - m.arc[0]) / (m.arc[1] - m.arc[0]); return m.arc[2] * 4 * s * (1 - s); } }
        public bool InArc { get { var m = move; return m?.arc != null && time > m.arc[0] && time < m.arc[1]; } }
        /// the root was raised by `lift` metres onto a ledge during the jump arc
        public void AirLift(float lift) { var m = move; if (m?.arc == null) return; airLift += lift; airLiftT0 = time; airLiftT1 = m.arc[1]; }

        public SoulAnimator(CharacterRig ch)
        {
            MoveDB.Load();
            this.ch = ch; j = ch.joints;
            foreach (var n in MoveDB.poseNames) pose[n] = MoveDB.Quat(MoveDB.neutral[n]);
            Apply();
        }
        public Move move => MoveDB.moves.TryGetValue(name, out var m) ? m : null;
        public bool busy => move != null;
        float RootYaw { get => ch.yaw; set { ch.yaw = value; ch.ApplyYaw(); } }
        void Stat(string k, float v) { if (!stats.TryGetValue(k, out var o) || v > o) stats[k] = v; }

        // ---------- helpers mirroring three.js world queries (always fresh) ----------
        static Vector3 WP(TNode n) { n.UpdateWorldFromRoot(false); return n.WorldPosition; }
        static Quaternion WQ(TNode n) => n.WorldQuaternion;
        static Quaternion Inv(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        static float Ease(float t, string mode) => mode == "in" ? t * t : mode == "out" ? 1 - (1 - t) * (1 - t) : mode == "smooth" ? Sim.Smooth(t) : t;
        static float LerpAngle(float a, float b, float t) => a + (Sim.Mod(b - a + Mathf.PI, Mathf.PI * 2) - Mathf.PI) * t;
        static float YawOf(Quaternion q) { var f = q * new Vector3(0, 0, 1); return Mathf.Atan2(f.x, f.z); }
        static Vector3 SlerpDir(Vector3 v, Vector3 target, float w)
        {
            if (w >= 1) return target;
            var q = Sim.FromTo(v.normalized, target);
            return (Sim.Slerp(Quaternion.identity, q, w) * v).normalized;
        }
        static Vector3 LimitAngle(Vector3 v, Vector3 refv, float maxDeg)
        {
            float ang = Sim.AngleTo(v, refv) / rad; if (ang <= maxDeg) return v;
            var axis = Vector3.Cross(refv, v); if (axis.sqrMagnitude < 1e-9f) return v;
            return Sim.ApplyAxisAngle(refv, axis.normalized, maxDeg * rad);
        }
        static int Seg(Guide[] gs, float time) { int i = 0; while (i < gs.Length - 2 && time > gs[i + 1].t) i++; return i; }
        static Vector3 SampleKey(Guide[] gs, char k, float time)
        {
            int i = Seg(gs, time);
            Guide a = gs[Mathf.Max(0, i - 1)], b = gs[i], c = i + 1 < gs.Length ? gs[i + 1] : b, d = gs[Mathf.Min(gs.Length - 1, i + 2)];
            float t = c == b ? 0 : Mathf.Clamp01((time - b.t) / (c.t - b.t));
            Vector3 G(Guide g) => k == 'u' ? g.u : k == 'f' ? g.f : g.d;
            Vector3 A = G(a), B = G(b), C = G(c), D = G(d);
            return new Vector3(Sim.Hermite(A.x, B.x, C.x, D.x, t), Sim.Hermite(A.y, B.y, C.y, D.y, t), Sim.Hermite(A.z, B.z, C.z, D.z, t)).normalized;
        }
        static float[] RunLeg(float p)
        {
            p = ((p % 1) + 1) % 1; int n = RUN_KEYS.Length;
            int i = n - 1; for (int k = 0; k < n; k++) if (p >= RUN_KEYS[k][0]) i = k;
            float[] k1 = RUN_KEYS[i], k2 = RUN_KEYS[(i + 1) % n], k0 = RUN_KEYS[(i - 1 + n) % n], k3 = RUN_KEYS[(i + 2) % n];
            float t1 = k1[0], t2 = k2[0] > t1 ? k2[0] : k2[0] + 1, t = (p - t1) / (t2 - t1);
            return new[] { Sim.Hermite(k0[1], k1[1], k2[1], k3[1], t), Sim.Hermite(k0[2], k1[2], k2[2], k3[2], t), Sim.Hermite(k0[3], k1[3], k2[3], k3[3], t) };
        }
        static float CurveAt(float[][] keys, float time)
        {
            int i = 0; while (i < keys.Length - 2 && time > keys[i + 1][0]) i++;
            float t = Mathf.Clamp01((time - keys[i][0]) / (keys[i + 1][0] - keys[i][0])); return Mathf.LerpUnclamped(keys[i][1], keys[i + 1][1], Sim.Smooth(t));
        }
        static float RootAt(Move m, float time)
        {
            var keys = m.root; if (keys == null) return 0; int i = 0; while (i < keys.Length - 2 && time > keys[i + 1][0]) i++;
            float t = Mathf.Clamp01((time - keys[i][0]) / (keys[i + 1][0] - keys[i][0])); return Mathf.LerpUnclamped(keys[i][1], keys[i + 1][1], m.rootEase == "linear" ? t : Sim.Smooth(t));
        }

        // ---------- full-body dynamics ----------
        void Dynamics(float dt)
        {
            var v = tipVel ?? Vector3.zero; var r = tipRel;
            var m = move; float after = m?.active != null ? time - m.active[1] : -1;
            float on = (busy && !m.roll ? (after < .12f ? 1 : Mathf.Lerp(1, .3f, Mathf.Min(1, (after - .12f) / .12f))) : 0) * (m != null && m.Has(m.dyn) ? m.dyn : 1);
            float rr = Mathf.Max(.15f, r.x * r.x + r.z * r.z), omega = (r.z * v.x - r.x * v.z) / rr;
            float spd = v.magnitude;
            float tTwist = on * Mathf.Clamp(omega * 9, -30, 30), tLean = on * Mathf.Clamp(v.z * 2.2f, -10, 14), tSide = on * Mathf.Clamp(-v.x * 2.0f, -12, 12), tDrop = on * Mathf.Min(.075f, spd * .007f);
            float Spring(float[] s, float target, float f, float zeta) { float w = 2 * Mathf.PI * f; s[1] += (w * w * (target - s[0]) - 2 * zeta * w * s[1]) * dt; s[0] += s[1] * dt; return s[0]; }
            float tw = Spring(dynTwist, tTwist, 3.2f, .42f), ln = Spring(dynLean, tLean, 4, .6f), sd = Spring(dynSide, tSide, 4, .6f), dr = Spring(dynDrop, tDrop, 5, .7f);
            void Add(string n, float x, float y, float z) { var e = Sim.ToEulerXYZ(pose[n]); e.x += x * rad; e.y += y * rad; e.z += z * rad; pose[n] = Sim.EulerXYZ(e.x, e.y, e.z); }
            Add("pelvis", 0, tw * .35f, 0);
            Add("spine", ln * .5f, tw * .30f, sd * .5f);
            Add("chest", ln * .5f, tw * .45f, sd * .5f);
            Add("neck", -ln * .3f, -tw * .35f, -sd * .3f);
            Add("head", -ln * .3f, -tw * .55f, -sd * .3f);
            Add("LupperArm", -tw * 1.1f, 0, -Mathf.Abs(tw) * .7f);
            Add("Lforearm", -Mathf.Abs(tw) * .4f, 0, 0);
            body.y -= dr;
        }

        // ---------- playback ----------
        public bool Play(string n)
        {
            if (!MoveDB.moves.ContainsKey(n) && n != "idle") return false;
            from = new Dictionary<string, Quaternion>(); foreach (var p in MoveDB.poseNames) from[p] = j[p].quaternion;
            fromBody = j["pelvis"].position - new Vector3(0, REST_HEIGHT, 0);
            var ankles = new Dictionary<string, Vector3>();
            foreach (var s in new[] { "L", "R" }) { var p = WP(j[s + "foot"]); p.y = Mathf.Max(p.y, A(p)); ankles[s] = p; }
            if (n == "idle") { feet = new Dictionary<string, Foot>(); foreach (var s in new[] { "L", "R" }) feet[s] = new Foot { target = ankles[s], start = ankles[s], wasStance = true, moving = true }; }
            else { mfeet = new Dictionary<string, MFoot>(); foreach (var s in new[] { "L", "R" }) mfeet[s] = new MFoot { pos = ankles[s], yaw = YawOf(WQ(j[s + "foot"])) }; }
            if (n != "idle") willPlay?.Invoke(n);
            name = n; time = 0; blend = 0; buffered = false; impacted = false; airLift = 0;
            startYaw = pendingYaw ?? RootYaw; pendingYaw = null;
            yawOffset = Sim.Mod(RootYaw - startYaw + Mathf.PI, Mathf.PI * 2) - Mathf.PI; prevTipL = null;
            velocity = Vector3.zero; speed = 0;
            if (n == "heavyCharge") charge = 0;
            if (n == "roll" || n == "backstep") dust?.Invoke();
            return true;
        }
        public void ResetFeet() { feet.Clear(); mfeet.Clear(); ResetSpring(); }
        public bool Light()
        {
            if (name == "jump" && time > .16f && time < .80f) return Play("flurry");
            if (!busy) return Play("light1");
            var m = move;
            if (m.buffer != null && time >= m.buffer[0] && time <= m.buffer[1]) { buffered = true; buffer?.Invoke(); }
            return false;
        }

        public void Update(float dt, Vector3 direction, bool running)
        {
            worldTime += dt; this.dt = dt;
            if (busy) UpdateMove(dt); else Locomotion(dt, direction, running);
            var m = move;
            blend = Mathf.Min(1, blend + dt / Mathf.Max(.12f, m != null && m.Has(m.blend) ? m.blend : .22f));
            if (dt > 0) Dynamics(dt);
            Apply();
            if (airborne) { feet.Clear(); mfeet.Clear(); wasRunning = false; }      // falling: legs stay FK
            else if (busy) MoveLegs(dt);
            else if (runW > .5f) { RunLegs(); wasRunning = true; }
            else
            {
                if (wasRunning)
                {
                    wasRunning = false; feet = new Dictionary<string, Foot>();
                    foreach (var s in new[] { "L", "R" }) { var p = WP(j[s + "foot"]); p.y = Mathf.Max(p.y, A(p)); feet[s] = new Foot { target = p, start = p, wasStance = true, moving = true }; }
                }
                PlantFeet(direction, dt);
            }
            ch.UpdateWorld(); ch.body.Update();
            Weapon(dt);
            ch.UpdateWorld(); ch.body.Update();
            Audit();
        }

        void UpdateMove(float dt)
        {
            var m = move; float previous = time; time = Mathf.Min(time + dt, m.duration);
            if (name == "heavyCharge") charge = Mathf.Min(1, time / m.duration);
            float distance = (RootAt(m, time) - RootAt(m, previous)) * speedScale;
            ch.root.position += Sim.ApplyAxisAngle(new Vector3(0, 0, distance), up, startYaw);
            if (m.yaw != null) RootYaw = startYaw + CurveAt(m.yaw, time) * rad + yawOffset * (1 - Sim.Smooth(Mathf.Min(1, time / .14f)));
            else { float dy = Sim.Mod(startYaw - RootYaw + Mathf.PI, Mathf.PI * 2) - Mathf.PI; RootYaw += dy * Sim.Damp(26, dt); }
            var keys = m.keys; int i = 0; while (i < keys.Length - 2 && time > keys[i + 1].t) i++;
            MoveKey k0 = keys[Mathf.Max(0, i - 1)], k1 = keys[i], k2 = keys[i + 1], k3 = keys[Mathf.Min(keys.Length - 1, i + 2)];
            float t = Ease(Mathf.Clamp01((time - k1.t) / (k2.t - k1.t)), k2.ease);
            foreach (var n in MoveDB.poseNames)
            {
                Quaternion a = k0.q[n], b = k1.q[n], c = k2.q[n], d = k3.q[n];
                pose[n] = Sim.Normalize(new Quaternion(Sim.Hermite(a.x, b.x, c.x, d.x, t), Sim.Hermite(a.y, b.y, c.y, d.y, t), Sim.Hermite(a.z, b.z, c.z, d.z, t), Sim.Hermite(a.w, b.w, c.w, d.w, t)));
            }
            body = new Vector3(Sim.Hermite(k0.body.x, k1.body.x, k2.body.x, k3.body.x, t), Sim.Hermite(k0.body.y, k1.body.y, k2.body.y, k3.body.y, t), Sim.Hermite(k0.body.z, k1.body.z, k2.body.z, k3.body.z, t));
            if (m.arc != null) { float t0 = m.arc[0], t1 = m.arc[1], h = m.arc[2]; if (time > t0 && time < t1) { float s = (time - t0) / (t1 - t0); body.y += h * 4 * s * (1 - s); } }
            // mounted a higher ledge mid-jump: the root was raised by airLift; fade the offset out by landing
            if (airLift > 0 && time < airLiftT1) body.y -= airLift * (airLiftT1 - time) / Mathf.Max(1e-3f, airLiftT1 - airLiftT0);
            if (!impacted && m.Has(m.impact) && time >= m.impact) { impacted = true; impact?.Invoke(name, m, charge); }
            if (m.Has(m.land) && previous < m.land && time >= m.land) land?.Invoke();
            foreach (var (at, fxName) in m.fx) if (previous < at && time >= at) fx?.Invoke(fxName, name);
            if (buffered && m.chain != null && time >= (m.Has(m.chainAt) ? m.chainAt : m.duration * .79f)) { Play(m.chain); return; }
            if (time >= m.duration && !m.loopHold) Play("idle");
        }

        float Breath()
        {
            float ph = (worldTime / 3.2f) % 1;
            return ph < .55f ? Mathf.Sin(ph / .55f * Mathf.PI / 2) * 2 - 1 : Mathf.Cos((ph - .55f) / .45f * Mathf.PI / 2) * 2 - 1;
        }
        void Locomotion(float dt, Vector3 direction, bool running)
        {
            float previousSpeed = speed, targetSpeed = direction.sqrMagnitude > .01f ? (running ? 4.2f : 1.65f) * Mathf.Min(1, direction.magnitude) * speedScale : 0;
            var desired = direction.sqrMagnitude > 1e-6f ? direction.normalized * targetSpeed : Vector3.zero;
            accel ??= Vector3.zero;
            { float w = 2 * Mathf.PI * 1.7f; accel += ((desired - velocity) * (w * w) + accel.Value * (-2 * w)) * dt; velocity += accel.Value * dt; }
            speed = velocity.magnitude; if (speed < .015f) { velocity = Vector3.zero; speed = 0; }
            ch.root.position += velocity * dt;
            if (direction.sqrMagnitude > .01f) { float targetYaw = Mathf.Atan2(direction.x, direction.z), delta = Sim.Mod(targetYaw - RootYaw + Mathf.PI, Mathf.PI * 2) - Mathf.PI; RootYaw += delta * Sim.Damp(10, dt); }
            phase += speed * dt * (running ? 4.3f : 5.4f);
            float amount = Mathf.Min(1, speed / 1.65f), still = 1 - amount, wave = Mathf.Sin(phase), b = Breath();
            float acc = dt > 0 ? Mathf.Clamp((speed - previousSpeed) / dt, -4, 5) : 0;
            var N = MoveDB.neutral;
            var angles = new Dictionary<string, Vector3>(N);
            angles["pelvis"] = new Vector3(N["pelvis"].x + speed * 2.2f, N["pelvis"].y * still + wave * 5 * amount, Mathf.Cos(phase) * 2 * amount);
            angles["spine"] = new Vector3(N["spine"].x + acc * 1.3f, N["spine"].y * still, 0);
            angles["chest"] = new Vector3(N["chest"].x + speed * 1.7f - b * 1.6f * still, N["chest"].y * still - wave * 9 * amount, -Mathf.Cos(phase) * 2 * amount);
            angles["head"] = new Vector3(N["head"].x - speed * 2 + b * .8f * still, N["head"].y * still, 0);
            angles["Lclavicle"] = new Vector3(0, 0, -b * 2 * still); angles["Rclavicle"] = new Vector3(0, 0, b * 2 * still);
            runW = Sim.SmoothStep(speed, .7f, 2.2f); runBob = 0;
            if (runW > 0)
            {
                runPhase += speed * dt / 2.2f;
                float p = runPhase, s = Mathf.Sin(2 * Mathf.PI * (p - .65f)), lean = 8 + speed * 2.2f;
                var L = RunLeg(p); var R = RunLeg(p + .5f);
                var run = new Dictionary<string, Vector3>
                {
                    { "pelvis", new Vector3(6 + speed * 1.2f, 9 * s, 3 * Mathf.Sin(2 * Mathf.PI * p)) }, { "spine", new Vector3(lean * .5f, -5 * s, 0) }, { "chest", new Vector3(lean * .5f, -12 * s, 0) },
                    { "neck", new Vector3(-lean * .4f, 6 * s, 0) }, { "head", new Vector3(-lean * .4f, 6 * s, 0) },
                    { "Lthigh", new Vector3(L[0], 0, 4) }, { "Lshin", new Vector3(L[1], 0, 0) }, { "Lfoot", new Vector3(L[2], 0, 0) },
                    { "Rthigh", new Vector3(R[0], 0, -4) }, { "Rshin", new Vector3(R[1], 0, 0) }, { "Rfoot", new Vector3(R[2], 0, 0) },
                    { "LupperArm", new Vector3(38 * s, 0, -14) }, { "Lforearm", new Vector3(-88 + 12 * s, 0, 0) },
                };
                foreach (var kv in run) { var bs = angles.TryGetValue(kv.Key, out var v0) ? v0 : N[kv.Key]; angles[kv.Key] = bs + (kv.Value - bs) * runW; }
                runBob = runW * (.035f * Mathf.Cos(4 * Mathf.PI * (p - .42f)) - .015f);
            }
            foreach (var kv in angles) if (pose.ContainsKey(kv.Key)) pose[kv.Key] = MoveDB.Quat(kv.Value);
            body = new Vector3(Mathf.Sin(worldTime * .85f) * .010f * still, MoveDB.BASE_BODY.y * (1 - amount * .6f) + b * .006f * still + Mathf.Abs(wave) * .027f * amount, 0);
            body.y += runBob - Mathf.Abs(wave) * .027f * amount * runW;
            idleBob = new Vector3(0, b * .009f * still, 0);
            time = 0; name = speed > .15f ? (running ? "run" : "walk") : "idle";
        }
        public float RunWeight => runW;

        void Apply()
        {
            float b = blend, w = b * b * b * (b * (b * 6 - 15) + 10);
            foreach (var kv in pose) j[kv.Key].quaternion = Sim.Slerp(from != null && from.TryGetValue(kv.Key, out var f) ? f : kv.Value, kv.Value, w);
            foreach (var n in FK_CLAMP) { var q = j[n].quaternion; Stat("limit " + n, Limits.ClampJoint(n, ref q)); j[n].quaternion = q; }
            var bd = fromBody.HasValue ? Vector3.LerpUnclamped(fromBody.Value, body, w) : body;
            j["pelvis"].position = new Vector3(bd.x, REST_HEIGHT + bd.y, bd.z); ch.UpdateWorld();
        }

        // ---------- legs ----------
        Vector3 FkPole(TNode upper, TNode lower, TNode end)
        {
            Vector3 a = WP(upper), k = WP(lower), e = WP(end);
            var off = k - (a + e) * .5f;
            if (off.sqrMagnitude < 1e-6f) off = Sim.AxisAngle(up, RootYaw) * new Vector3(0, 0, 1);
            return k + Sim.SetLength(off, .6f);
        }
        void SetFootWorld(string side, float yaw)
        {
            var foot = j[side + "foot"]; foot.quaternion = Inv(WQ(foot.parent)) * Sim.AxisAngle(up, yaw);
        }
        void MoveLegs(float dt)
        {
            var m = move; float t = time;
            bool air = m.roll || (m.air != null && t >= m.air[0] && t <= m.air[1]);
            foreach (var side in new[] { "L", "R" })
            {
                TNode thigh = j[side + "thigh"], shin = j[side + "shin"], foot = j[side + "foot"];
                var fk = WP(foot); var ground = fk; ground.y = Mathf.Max(ground.y, A(ground));
                float fkYaw = YawOf(WQ(foot));
                var pole = FkPole(thigh, shin, foot);
                if (!mfeet.TryGetValue(side, out var f)) mfeet[side] = f = new MFoot { pos = ground, yaw = fkYaw };
                Vector3 target; bool flat = true;
                if (air)
                {
                    f.pos = ground; f.yaw = fkYaw; f.step = null; flat = false;
                    if (fk.y >= A(fk)) continue;
                    target = ground;
                }
                else
                {
                    var st = m.steps?.Find(s => s.side == side && t >= s.a && t <= s.b);
                    if (st != null)
                    {
                        if (f.step != st) { f.step = st; f.from = f.pos; f.fromYaw = f.yaw; }
                        float p = (t - st.a) / (st.b - st.a), e = Sim.Smooth(p);
                        target = Vector3.LerpUnclamped(f.from, ground, e); target.y = Mathf.Max(target.y, A(target)) + Mathf.Sin(Mathf.PI * p) * .11f;
                        f.yaw = LerpAngle(f.fromYaw, fkYaw, e); f.pos = target;
                    }
                    else
                    {
                        f.step = null; f.pos.y = A(f.pos);
                        var hip = WP(thigh); float over = Vector3.Distance(hip, f.pos) - (THIGH + SHIN + .03f);
                        if (over > 0) { var before = f.pos; f.pos = Vector3.LerpUnclamped(f.pos, ground, Mathf.Min(1, Sim.Damp(10, dt) + over)); f.pos.y = A(f.pos); Stat("foot slide (unplanned)", Vector3.Distance(before, f.pos)); }
                        target = f.pos;
                    }
                }
                SolveTwoBone(thigh, shin, foot, target, pole, THIGH, SHIN);
                if (flat) SetFootWorld(side, f.yaw);
                var ankle = WP(foot);
                if (!air) Stat("planted foot error", Mathf.Sqrt((ankle.x - target.x) * (ankle.x - target.x) + (ankle.z - target.z) * (ankle.z - target.z)));
            }
        }
        void RunLegs()
        {
            foreach (var side in new[] { "L", "R" })
            {
                TNode thigh = j[side + "thigh"], shin = j[side + "shin"], foot = j[side + "foot"];
                var fk = WP(foot);
                if (fk.y < A(fk))
                {
                    var q = WQ(foot);
                    SolveTwoBone(thigh, shin, foot, new Vector3(fk.x, A(fk), fk.z), FkPole(thigh, shin, foot), THIGH, SHIN);
                    foot.quaternion = Inv(WQ(foot.parent)) * q;
                }
            }
        }
        void PlantFeet(Vector3 direction, float dt)
        {
            bool moving = speed > .12f; float amount = Mathf.Min(1, speed / 1.65f);
            foreach (var (side, offset) in new[] { ("L", 0f), ("R", Mathf.PI) })
            {
                float ph = Sim.Mod(phase + offset, Mathf.PI * 2) / (Mathf.PI * 2); bool stance = ph < .60f; float sign = side == "L" ? -1 : 1;
                var walk = new Vector2(sign * .19f, side == "L" ? .12f : -.10f); var st = MoveDB.STANCE_FEET[side];
                var rest = ch.LocalToWorld(new Vector3(Mathf.Lerp(st.x, walk.x, amount), ANKLE, Mathf.Lerp(st.y, walk.y, amount))); rest.y = A(rest);
                if (!feet.TryGetValue(side, out var foot)) feet[side] = foot = new Foot { target = rest, start = rest, wasStance = stance, moving = moving };
                if (moving)
                {
                    if (foot.wasStance && !stance) foot.start = foot.target;
                    if (!stance)
                    {
                        float t = (ph - .60f) / .40f; var ahead = velocity * .19f;
                        foot.target = Vector3.LerpUnclamped(foot.start, rest + ahead, Sim.Smooth(t)); foot.target.y = A(foot.target) + Mathf.Sin(Mathf.PI * t) * (speed > 2 ? .19f : .12f);
                    }
                    else if (!foot.wasStance) { foot.target.y = A(foot.target); if (speed > 2) step?.Invoke(foot.target); }
                }
                else if (foot.moving)
                {
                    foot.target = Vector3.LerpUnclamped(foot.target, rest, Sim.Damp(10, dt));
                    float d = Mathf.Sqrt((foot.target.x - rest.x) * (foot.target.x - rest.x) + (foot.target.z - rest.z) * (foot.target.z - rest.z)); foot.target.y = A(foot.target) + Mathf.Min(.07f, d * .6f);
                }
                if (Vector3.Distance(foot.target, rest) > .70f) foot.target = rest;
                foot.wasStance = stance; foot.moving = moving || Vector3.Distance(foot.target, rest) > .01f;
                var pole = ch.LocalToWorld(new Vector3(sign * .20f, .48f, 1));
                SolveTwoBone(j[side + "thigh"], j[side + "shin"], j[side + "foot"], foot.target, pole, THIGH, SHIN);
                SetFootWorld(side, RootYaw + (side == "L" ? -.45f : .12f) * (1 - amount));
            }
        }
        // rig.js solveTwoBone / aim
        public static void SolveTwoBone(TNode upper, TNode lower, TNode end, Vector3 target, Vector3 pole, float lengthA, float lengthB)
        {
            upper.UpdateWorldFromRoot(true);
            var origin = upper.WorldPosition;
            var axis = target - origin; float distance = Mathf.Clamp(axis.magnitude, .015f, lengthA + lengthB - .001f);
            axis = axis.normalized;
            var bend = pole - origin; bend -= axis * Vector3.Dot(bend, axis);
            if (bend.sqrMagnitude < .00001f) bend = new Vector3(0, 0, 1) - axis * axis.z;
            bend = bend.normalized;
            float along = (lengthA * lengthA + distance * distance - lengthB * lengthB) / (2 * distance);
            var elbow = origin + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, lengthA * lengthA - along * along));
            Aim(upper, (elbow - origin).normalized);
            upper.UpdateWorldFromRoot(true);
            var lowerOrigin = lower.WorldPosition;
            var reachable = origin + axis * distance;
            Aim(lower, (reachable - lowerOrigin).normalized);
            end.UpdateWorldFromRoot(false);
        }
        static void Aim(TNode joint, Vector3 direction)
        {
            var worldQ = WQ(joint); var current = worldQ * new Vector3(0, -1, 0);
            var next = Sim.FromTo(current, direction) * worldQ;
            joint.quaternion = Inv(WQ(joint.parent)) * next;
        }

        // ---------- arm-blade with a mechanical wrist ----------
        void Weapon(float dt)
        {
            var m = move; bool isBusy = busy;
            var S = WP(j["RupperArm"]);
            Vector3 fkE = WP(j["Rforearm"]), fkW = WP(j["Rhand"]), fkTip = WP(ch.bladeTip);
            Vector3 u = (fkE - S).normalized, f = (fkW - fkE).normalized, d = (fkTip - fkW).normalized, vel = Vector3.zero;
            var gs = isBusy ? m.weapon : MoveDB.idleGuide;
            if (gs != null)
            {
                float tm = isBusy ? time : 0;
                TNode frameNode = m?.weaponSpace == "chest" ? j["chest"] : ch.root; frameNode.UpdateWorldFromRoot(false); var frame = frameNode.matrixWorld;
                float lagD = isBusy ? (m.Has(m.lag) ? m.lag : LAG_D) : 0;
                var gu = SampleKey(gs, 'u', tm); var gd = SampleKey(gs, 'd', Mathf.Max(0, tm - lagD)); var gf = gd;
                Vector3 TipAt(float tt) => SampleKey(gs, 'u', tt) * UPPER + SampleKey(gs, 'd', Mathf.Max(0, tt - lagD)) * (FORE + BLADE_END);
                vel = (TipAt(tm + 1 / 240f) - TipAt(tm)) * 240;
                float sp = vel.magnitude; if (sp > 1e-6f) vel = frame.MultiplyVector(vel).normalized * sp;
                if (!isBusy) { gu.y += idleBob.y * 2; gu = gu.normalized; }
                if (!isBusy && runW > 0) { gu = SlerpDir(gu, new Vector3(.42f, -.88f, .20f).normalized, runW); gd = SlerpDir(gd, new Vector3(.76f, -.50f, .42f).normalized, runW); gf = gd; }
                gu = frame.MultiplyVector(gu).normalized; gf = frame.MultiplyVector(gf).normalized; gd = frame.MultiplyVector(gd).normalized;
                float w = isBusy ? Sim.Smooth(blend) : 1;
                u = SlerpDir(u, gu, w); f = SlerpDir(f, gf, w); d = SlerpDir(d, gd, w);
                bool live = isBusy && m.active != null && time >= m.active[0] - .08f && time <= m.active[1];
                SpringDirs(ref u, ref d, dt, live ? (m.Has(m.whipHz) ? m.whipHz : 17) : 11); f = d;
            }
            if (isBusy && m.yaw != null)
            {
                float om = (CurveAt(m.yaw, time + 1 / 240f) - CurveAt(m.yaw, time)) * 240 * rad;
                vel += Vector3.Cross(new Vector3(0, om, 0), S + u * UPPER + f * FORE + d * BLADE_END - ch.root.position);
            }
            d = LimitAngle(d, u, 150); f = d;
            // collisions: small rotations of blade, then upper arm, around the authored pose
            Vector3 Ut = u, Ft = f, Dt = d;
            (float pen, float cost) Score(Vector3 cu, Vector3 cf, Vector3 cd)
            {
                var e = S + cu * UPPER; var wr = e + cf * FORE;
                float pen = ArmBladePen(e, wr, cd, m);
                float lim = Mathf.Max(0, Sim.AngleTo(cu, cf) / rad - 150) + Mathf.Max(0, Sim.AngleTo(cf, cd) / rad - WRIST_MAX);
                return (pen, pen * 25 + Sim.AngleTo(cd, Dt) + Sim.AngleTo(cf, Ft) * 1.2f + Sim.AngleTo(cu, Ut) * 1.5f + lim * .05f);
            }
            var s0 = Score(Ut, Ft, Dt); Vector3 bu = Ut, bf = Ft, bdir = Dt; float bpen = s0.pen, bcost = s0.cost;
            Stat("blade in body (before fix)", bpen);
            if (bpen > 0)
            {
                Vector3 Around(Vector3 v, float ring, int k)
                {
                    var a = Mathf.Abs(v.y) < .9f ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
                    var b1 = Vector3.Cross(v, a).normalized; var b2 = Vector3.Cross(v, b1).normalized; float phi = k / 10f * Mathf.PI * 2;
                    return Sim.ApplyAxisAngle(v, b1 * Mathf.Cos(phi) + b2 * Mathf.Sin(phi), ring * rad);
                }
                foreach (var ring in new[] { 8f, 18, 30, 45, 62 })
                {
                    for (int k = 0; k < 10; k++)
                    {
                        Vector3 rd = Around(Dt, ring, k), ru = Around(Ut, ring, k);
                        foreach (var (cu, cf, cd) in new[] { (Ut, rd, rd), (ru, Dt, Dt) })
                        {
                            var s = Score(cu, cf, cd); if (s.cost < bcost) { bu = cu; bf = cf; bdir = cd; bpen = s.pen; bcost = s.cost; }
                        }
                    }
                    if (bpen == 0) break;
                }
            }
            u = bu; f = bf; d = bdir;
            if (smoothU.HasValue && !isBusy && dt > 0) { float k = Sim.Damp(16, dt); smoothU = SlerpDir(smoothU.Value, u, k); smoothD = SlerpDir(smoothD.Value, d, k); u = smoothU.Value; d = smoothD.Value; f = d; }
            smoothU = u; smoothD = d;
            // hinge-consistent arm (root-space hinge memory)
            {
                var rq = ch.root.quaternion; var rqi = Inv(rq);
                var h = Vector3.Cross(d, u); float bend = Sim.AngleTo(u, d); Vector3? prev = hingeL.HasValue ? rq * hingeL.Value : (Vector3?)null;
                if (h.magnitude < .17f) h = prev ?? rq * new Vector3(1, 0, 0);
                else
                {
                    h = h.normalized;
                    if (prev.HasValue && Vector3.Dot(h, prev.Value) < 0 && bend < .75f)
                    {
                        h = (prev.Value - d * Vector3.Dot(prev.Value, d)).normalized;
                        u = Sim.ApplyAxisAngle(d, h, Mathf.Max(bend, .12f)).normalized;
                    }
                }
                h = (h - u * Vector3.Dot(h, u)).normalized;
                float trust = Sim.SmoothStep(bend, 12 * rad, 40 * rad);
                hinge = prev.HasValue ? Vector3.LerpUnclamped(prev.Value, h, Sim.Damp(26 * trust, dt > 0 ? dt : .016f)).normalized : h;
                hingeL = rqi * hinge;
            }
            var H = (hinge - u * Vector3.Dot(hinge, u)).normalized;
            var Hd = (H - d * Vector3.Dot(H, d)).normalized;
            SetBasis(j["RupperArm"], u, H);
            SetBasis(j["Rforearm"], d, Hd);
            // edge roll: the edge leads the cut, within +-100 deg of the forearm, rate limited
            Vector3 Perp(Vector3 v) => v - d * Vector3.Dot(v, d);
            var restEdge = Perp(new Vector3(0, -1, 0)); if (restEdge.sqrMagnitude < 1e-6f) restEdge = Perp(new Vector3(0, 0, 1));
            restEdge = restEdge.normalized;
            var target = Perp(vel); float tspeed = target.magnitude;
            target = tspeed > .05f ? Vector3.LerpUnclamped(target.normalized, restEdge, Mathf.Clamp01(1 - tspeed / 3)) : restEdge;
            if (target.sqrMagnitude < 1e-6f) target = restEdge; target = target.normalized;
            float phi2 = Mathf.Atan2(Vector3.Dot(Vector3.Cross(Hd, target), d), Vector3.Dot(Hd, target));
            phi2 = Mathf.Clamp(phi2, -100 * rad, 100 * rad);
            float ddt = dt > 0 ? dt : .016f, maxStep = 900 * rad * ddt;
            roll += Mathf.Clamp((phi2 - roll) * Sim.Damp(18, ddt), -maxStep, maxStep);
            SetBasis(j["Rhand"], d, Sim.ApplyAxisAngle(Hd, d, roll));
            ch.UpdateWorld();
            dbg = (Ut, Ft, Dt, u, f, d);
            var tipL = ch.root.matrixWorld.inverse.MultiplyPoint3x4(WP(ch.bladeTip));
            if (prevTipL.HasValue && dt > 0) tipVel = (tipL - prevTipL.Value) / dt;
            prevTipL = tipL; tipRel = tipL - j["pelvis"].position;
            var fh = BladeHit(); Stat("blade in body (final)", fh?.depth ?? 0); if (fh != null) Stat("final hit: " + fh.Value.name, fh.Value.depth);
            Stat("elbow in torso (after fix)", PointPen(S + u * UPPER, .06f, TORSO_ELBOW));
            Stat("wrist hinge over 110 (deg)", Sim.AngleTo(f, d) / rad - WRIST_MAX);
            Stat("elbow R flex over 150 (deg)", Sim.AngleTo(u, f) / rad - 150);
        }
        float PointPen(Vector3 p, float r, string[] names)
        {
            float worst = 0;
            foreach (var n in names) { var c = ch.body.byName[n]; worst = Mathf.Max(worst, c.r + r - Sim.ClosestSegments(p, p, c.a, c.b, out _, out _)); }
            return worst;
        }
        float ArmBladePen(Vector3 e, Vector3 w, Vector3 d, Move m)
        {
            Vector3 p = w + d * BLADE_FROM, q = w + d * BLADE_LEN;
            float pen = ch.body.Deepest(MoveDB.ARM_BLADE_BLOCKERS, p, q, BLADE_R)?.depth ?? 0;
            pen = Mathf.Max(pen, Mathf.Max(ch.body.Deepest(FOREARM_BLOCKERS, e, w, .05f)?.depth ?? 0, PointPen(e, .06f, TORSO_ELBOW)));
            if (m == null || !m.stab) pen = Mathf.Max(pen, Mathf.Max(.02f + groundAt(q.x, q.z) - q.y, .02f + groundAt(p.x, p.z) - p.y));
            return pen;
        }
        void SpringDirs(ref Vector3 u, ref Vector3 d, float dt, float hz)
        {
            if (!(dt > 0)) return;
            armSpring ??= new Spring { u = u, d = d, vu = Vector3.zero, vd = Vector3.zero };
            float w = 2 * Mathf.PI * hz, k = Mathf.Exp(-w * dt);
            void One(ref Vector3 x, ref Vector3 v, ref Vector3 target)
            {
                var e0 = x - target; var ff = v + e0 * w;
                var e1 = e0 + ff * dt;
                v = (ff + e1 * -w) * k;
                x = (target + e1 * k).normalized; target = x;
            }
            One(ref armSpring.u, ref armSpring.vu, ref u);
            One(ref armSpring.d, ref armSpring.vd, ref d);
        }
        public void ResetSpring() { armSpring = null; accel = null; hingeL = null; roll = 0; }
        void SetBasis(TNode joint, Vector3 dir, Vector3 xAxis)
        {
            var Y = (-dir).normalized; var X = (xAxis - Y * Vector3.Dot(xAxis, Y)).normalized; var Z = Vector3.Cross(X, Y).normalized;
            var world = Sim.FromBasis(X, Y, Z);
            joint.quaternion = Inv(WQ(joint.parent)) * world;
        }
        public BodyColliders.Hit? BladeHit()
        {
            ch.sword.UpdateWorldFromRoot(false);
            Vector3 p = ch.sword.LocalToWorld(new Vector3(0, BLADE_FROM, 0)), q = ch.sword.LocalToWorld(new Vector3(0, BLADE_END, 0));
            return ch.body.Deepest(MoveDB.ARM_BLADE_BLOCKERS, p, q, BLADE_R - .01f);
        }
        /// world-space blade velocity estimate (slashInfo)
        public Vector3 TipVelWorld { get { var v = tipVel ?? Vector3.zero; float s = v.magnitude; return s > 1e-6f ? ch.root.matrixWorld.MultiplyVector(v).normalized * s : Vector3.zero; } }
        void Audit()
        {
            foreach (var (a, b, c, label) in new[] { ("Rthigh", "Rshin", "Rfoot", "knee R"), ("Lthigh", "Lshin", "Lfoot", "knee L") })
            {
                Vector3 p0 = WP(j[a]), p1 = WP(j[b]), p2 = WP(j[c]);
                Stat(label + " flex over 150 (deg)", Sim.AngleTo(p1 - p0, p2 - p1) / rad - 150);
            }
            foreach (var s in new[] { "L", "R" }) { var fp = WP(j[s + "foot"]); Stat("ankle below ground", A(fp) - .005f - fp.y); }
            float low = float.PositiveInfinity; foreach (var c in ch.body.caps) low = Mathf.Min(low, Mathf.Min(c.a.y - c.r, c.b.y - c.r));
            Stat("body below ground", ch.root.position.y - low - .01f);
        }
    }
}

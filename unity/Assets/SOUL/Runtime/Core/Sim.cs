using UnityEngine;

namespace Soul
{
    // The whole simulation (animation, combat, enemies, VFX positions) runs in "sim space":
    // the exact coordinate system of the Three.js prototype (right-handed, +Y up, a character
    // with yaw 0 faces +Z). Only the final transforms are converted for Unity:
    //   position (x, y, z)        -> (-x, y, z)
    //   quaternion (x, y, z, w)   -> (x, -y, -z, w)
    // This is a reflection conjugation, so it preserves composition: a hierarchy converted node
    // by node renders exactly like the original. The math below therefore matches three.js
    // numerically. Never use Unity's Euler / LookRotation / AngleAxis on sim data.
    public static class Sim
    {
        public const float Deg = Mathf.Deg2Rad;
        public static Vector3 ToUnity(Vector3 p) => new Vector3(-p.x, p.y, p.z);
        public static Vector3 FromUnity(Vector3 p) => new Vector3(-p.x, p.y, p.z);
        public static Quaternion ToUnity(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        public static Quaternion FromUnity(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);
        /// sim yaw (rotation.y, radians) -> Unity rotation
        public static Quaternion YawToUnity(float yaw) => ToUnity(AxisAngle(Vector3.up, yaw));

        // ---------- three.js-equivalent math ----------
        public static Quaternion AxisAngle(Vector3 axis, float angle)
        {
            float h = angle * .5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }
        /// THREE.Quaternion.setFromEuler(x, y, z, 'XYZ'), radians
        public static Quaternion EulerXYZ(float x, float y, float z)
        {
            float c1 = Mathf.Cos(x / 2), c2 = Mathf.Cos(y / 2), c3 = Mathf.Cos(z / 2);
            float s1 = Mathf.Sin(x / 2), s2 = Mathf.Sin(y / 2), s3 = Mathf.Sin(z / 2);
            return new Quaternion(
                s1 * c2 * c3 + c1 * s2 * s3,
                c1 * s2 * c3 - s1 * c2 * s3,
                c1 * c2 * s3 + s1 * s2 * c3,
                c1 * c2 * c3 - s1 * s2 * s3);
        }
        public static Quaternion EulerXYZDeg(float x, float y, float z) => EulerXYZ(x * Deg, y * Deg, z * Deg);
        /// THREE.Euler.setFromQuaternion(q, 'XYZ') -> radians
        public static Vector3 ToEulerXYZ(Quaternion q)
        {
            float x2 = q.x + q.x, y2 = q.y + q.y, z2 = q.z + q.z;
            float xx = q.x * x2, xy = q.x * y2, xz = q.x * z2, yy = q.y * y2, yz = q.y * z2, zz = q.z * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            float m11 = 1 - (yy + zz), m12 = xy - wz, m13 = xz + wy;
            float m22 = 1 - (xx + zz), m23 = yz - wx;
            float m32 = yz + wx, m33 = 1 - (xx + yy);
            Vector3 e;
            e.y = Mathf.Asin(Mathf.Clamp(m13, -1, 1));
            if (Mathf.Abs(m13) < .9999999f) { e.x = Mathf.Atan2(-m23, m33); e.z = Mathf.Atan2(-m12, m11); }
            else { e.x = Mathf.Atan2(m32, m22); e.z = 0; }
            return e;
        }
        /// THREE.Quaternion.setFromUnitVectors
        public static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            float r = Vector3.Dot(from, to) + 1;
            Quaternion q;
            if (r < 1e-6f)
            {
                r = 0;
                q = Mathf.Abs(from.x) > Mathf.Abs(from.z) ? new Quaternion(-from.y, from.x, 0, r) : new Quaternion(0, -from.z, from.y, r);
            }
            else
            {
                Vector3 c = Vector3.Cross(from, to);
                q = new Quaternion(c.x, c.y, c.z, r);
            }
            return Normalize(q);
        }
        public static Quaternion Normalize(Quaternion q)
        {
            float l = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (l < 1e-12f) return Quaternion.identity;
            return new Quaternion(q.x / l, q.y / l, q.z / l, q.w / l);
        }
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        /// THREE.Quaternion.slerp (shortest path)
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            if (t <= 0) return a; if (t >= 1) return b;
            float cos = Dot(a, b);
            if (cos < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); cos = -cos; }
            if (cos >= 1) return a;
            float sqrSin = 1 - cos * cos;
            if (sqrSin <= 1e-9f)
            {
                float s = 1 - t;
                return Normalize(new Quaternion(s * a.x + t * b.x, s * a.y + t * b.y, s * a.z + t * b.z, s * a.w + t * b.w));
            }
            float sin = Mathf.Sqrt(sqrSin), half = Mathf.Atan2(sin, cos);
            float ra = Mathf.Sin((1 - t) * half) / sin, rb = Mathf.Sin(t * half) / sin;
            return new Quaternion(a.x * ra + b.x * rb, a.y * ra + b.y * rb, a.z * ra + b.z * rb, a.w * ra + b.w * rb);
        }
        /// THREE.Quaternion.setFromRotationMatrix(makeBasis(X, Y, Z))
        public static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
            float m11 = X.x, m12 = Y.x, m13 = Z.x, m21 = X.y, m22 = Y.y, m23 = Z.y, m31 = X.z, m32 = Y.z, m33 = Z.z;
            float trace = m11 + m22 + m33; Quaternion q;
            if (trace > 0) { float s = .5f / Mathf.Sqrt(trace + 1); q = new Quaternion((m32 - m23) * s, (m13 - m31) * s, (m21 - m12) * s, .25f / s); }
            else if (m11 > m22 && m11 > m33) { float s = 2 * Mathf.Sqrt(1 + m11 - m22 - m33); q = new Quaternion(.25f * s, (m12 + m21) / s, (m13 + m31) / s, (m32 - m23) / s); }
            else if (m22 > m33) { float s = 2 * Mathf.Sqrt(1 + m22 - m11 - m33); q = new Quaternion((m12 + m21) / s, .25f * s, (m23 + m32) / s, (m13 - m31) / s); }
            else { float s = 2 * Mathf.Sqrt(1 + m33 - m11 - m22); q = new Quaternion((m13 + m31) / s, (m23 + m32) / s, .25f * s, (m21 - m12) / s); }
            return q;
        }
        public static Vector3 Rotate(Quaternion q, Vector3 v) => q * v;   // Hamilton rotation, convention-free
        public static Vector3 ApplyAxisAngle(Vector3 v, Vector3 axis, float angle) => AxisAngle(axis, angle) * v;
        public static float AngleTo(Vector3 a, Vector3 b)
        {
            float d = Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude); if (d == 0) return Mathf.PI / 2;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(a, b) / d, -1, 1));
        }
        public static Vector3 SetLength(Vector3 v, float l) { float m = v.magnitude; return m > 1e-12f ? v * (l / m) : v; }
        public static Vector3 Norm(Vector3 v) { float m = v.magnitude; return m > 1e-12f ? v / m : Vector3.zero; }

        // ---------- scalar helpers (THREE.MathUtils) ----------
        public static float Smooth(float t) => t * t * (3 - 2 * t);
        public static float SmoothStep(float x, float min, float max)
        {
            if (x <= min) return 0; if (x >= max) return 1; x = (x - min) / (max - min); return x * x * (3 - 2 * x);
        }
        public static float Mod(float n, float m) => ((n % m) + m) % m;
        public static float WrapPi(float a) => Mod(a + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
        public static float Damp(float rate, float dt) => 1 - Mathf.Exp(-rate * dt);
        public static float Hermite(float a, float b, float c, float d, float t) =>
            .5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
        public static Vector3 Forward(float yaw) => new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        public static Vector3 FlatY(Vector3 v, float y = 0) => new Vector3(v.x, y, v.z);

        // Closest points between segments p1-q1 and p2-q2 (Ericson, RTCD 5.1.9).
        public static float ClosestSegments(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r), s, t;
            if (a <= 1e-9f && e <= 1e-9f) { s = t = 0; }
            else if (a <= 1e-9f) { s = 0; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                    s = denom > 1e-9f ? Mathf.Clamp01((b * f - c * e) / denom) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            c1 = p1 + d1 * s; c2 = p2 + d2 * t;
            return Vector3.Distance(c1, c2);
        }
        public static float SegDist(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2) => ClosestSegments(p1, q1, p2, q2, out c1, out c2);
        /// closest point on segment p-q to x
        public static Vector3 ClosestOnSegment(Vector3 p, Vector3 q, Vector3 x)
        {
            Vector3 d = q - p; float t = Mathf.Clamp01(Vector3.Dot(x - p, d) / Mathf.Max(d.sqrMagnitude, 1e-9f)); return p + d * t;
        }
    }
}

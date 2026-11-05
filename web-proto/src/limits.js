import * as THREE from 'three';

// Anatomical range of motion, degrees, in the rig's authoring convention
// (local Euler 'XYZ'). Values are for the right side / centre line; the left side
// mirrors Y and Z. Bones of limbs point down -Y, spine bones point up +Y.
//   spine/neck: +X = flex forward      limbs: -X = swing forward (flexion)
//   shin: +X = knee bend               forearm: -X = elbow bend
//   thigh/upperArm: +Z = abduction (R) foot: +X = toes down
export const LIMITS = {
  spine:     { x: [-20, 45], y: [-30, 30], z: [-20, 20] },
  chest:     { x: [-25, 35], y: [-45, 45], z: [-20, 20] },
  neck:      { x: [-35, 40], y: [-45, 45], z: [-25, 25] },
  head:      { x: [-35, 35], y: [-45, 45], z: [-20, 20] },
  clavicle:  { x: [-15, 15], y: [-25, 25], z: [-10, 35] },
  upperArm:  { x: [-185, 65], y: [-90, 90], z: [-95, 180] },
  forearm:   { x: [-150, 2], y: [-85, 85], z: [-3, 3] },     // hinge + pronation
  thigh:     { x: [-125, 35], y: [-40, 40], z: [-25, 45] },
  shin:      { x: [0, 150], y: [-4, 4], z: [-3, 3] },        // hinge, no hyperextension
  foot:      { x: [-35, 50], y: [-20, 20], z: [-25, 25] },
};
// Wrist, measured relative to the forearm with the weapon socket removed:
// deviation about X (radial -, ulnar +), flexion about Z (R: flexion -), twist about Y.
export const WRIST = { radial: 25, ulnar: 40, flex: 75, ext: 65, twist: 90 };

const rad = Math.PI / 180, deg = 180 / Math.PI;
export function limitFor(name) {
  const side = name[0] === 'L' || name[0] === 'R' ? name[0] : null;
  const base = LIMITS[side ? name.slice(1, 2).toLowerCase() + name.slice(2) : name];
  if (!base) return null;
  if (side !== 'L') return base;
  const m = r => [-r[1], -r[0]];
  return { x: base.x, y: m(base.y), z: m(base.z) };
}

const euler = new THREE.Euler();
// Clamp a local quaternion in place. Returns the largest excess in degrees (0 = inside range).
export function clampJoint(name, q, apply = true) {
  const lim = limitFor(name); if (!lim) return 0;
  euler.setFromQuaternion(q, 'XYZ');
  let excess = 0;
  for (const axis of ['x', 'y', 'z']) {
    const v = euler[axis] * deg, [lo, hi] = lim[axis], c = Math.min(hi, Math.max(lo, v));
    excess = Math.max(excess, Math.abs(v - c)); euler[axis] = c * rad;
  }
  if (apply && excess > 1e-4) q.setFromEuler(euler);
  return excess;
}

// Swing-twist wrist limit (elliptical swing cone + twist). q is hand-local relative to forearm.
const twistQ = new THREE.Quaternion(), swingQ = new THREE.Quaternion();
export function clampWrist(side, q, apply = true) {
  const s = side === 'L' ? -1 : 1;
  twistQ.set(0, q.y, 0, q.w); if (twistQ.lengthSq() < 1e-9) twistQ.identity(); else twistQ.normalize();
  swingQ.copy(q).multiply(twistQ.clone().invert());
  let twist = 2 * Math.atan2(twistQ.y, twistQ.w) * deg; if (twist > 180) twist -= 360; if (twist < -180) twist += 360;
  if (swingQ.w < 0) swingQ.set(-swingQ.x, -swingQ.y, -swingQ.z, -swingQ.w);
  const angle = 2 * Math.acos(Math.min(1, swingQ.w)) * deg, sn = Math.sqrt(Math.max(1e-12, 1 - swingQ.w * swingQ.w));
  let ax = swingQ.x / sn * angle, az = swingQ.z / sn * angle * s;     // az mirrored so -az = flexion on both sides
  const a = ax >= 0 ? WRIST.ulnar : WRIST.radial, b = az >= 0 ? WRIST.ext : WRIST.flex;
  const r = Math.hypot(ax / a, az / b);
  let excess = 0;
  if (r > 1) { excess = Math.hypot(ax, az) * (1 - 1 / r); ax /= r; az /= r; }
  const ct = Math.max(-WRIST.twist, Math.min(WRIST.twist, twist)); excess = Math.max(excess, Math.abs(twist - ct));
  if (apply && excess > 1e-4) {
    const sAngle = Math.hypot(ax, az) * rad;
    const axis = new THREE.Vector3(ax, 0, az * s); if (axis.lengthSq() > 1e-12) axis.normalize(); else axis.set(1, 0, 0);
    q.setFromAxisAngle(axis, sAngle).multiply(new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), ct * rad));
  }
  return excess;
}

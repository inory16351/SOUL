import * as THREE from 'three';

export const REST_HEIGHT = .96;
// Arm bone lengths. Meshes were modeled for 0.30 / 0.28 and are stretched to fit (see anim.js).
export const ARM = { upper: .34, fore: .30, modelUpper: .30, modelFore: .28 };
export function createRig() {
  const joints = {};
  function joint(name, parent, position) {
    const j = new THREE.Object3D(); j.name = name; j.position.fromArray(position);
    joints[name] = j; parent?.add(j); return j;
  }
  const root = joint('root', null, [0, 0, 0]);
  const pelvis = joint('pelvis', root, [0, REST_HEIGHT, 0]);
  const spine = joint('spine', pelvis, [0, .15, 0]);
  const chest = joint('chest', spine, [0, .20, 0]);
  const neck = joint('neck', chest, [0, .17, 0]);
  joint('head', neck, [0, .11, 0]);
  for (const [side, sign] of [['L', -1], ['R', 1]]) {
    const clavicle = joint(side + 'clavicle', chest, [sign * .10, .055, 0]);
    const arm = joint(side + 'upperArm', clavicle, [sign * .23, -.015, 0]);
    const forearm = joint(side + 'forearm', arm, [0, -ARM.upper, 0]);
    joint(side + 'hand', forearm, [0, -ARM.fore, 0]);
    const thigh = joint(side + 'thigh', pelvis, [sign * .15, -.03, 0]);
    const shin = joint(side + 'shin', thigh, [0, -.43, 0]);
    const foot = joint(side + 'foot', shin, [0, -.40, 0]);
    joint(side + 'toe', foot, [0, -.055, .19]);
  }
  const edges = Object.values(joints).filter(j => j.parent && j.parent !== root.parent);
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(edges.length * 6), 3));
  const skeleton = new THREE.LineSegments(geometry, new THREE.LineBasicMaterial({ color: 0x8cffe2, depthTest: false, transparent: true, opacity: .9 }));
  skeleton.renderOrder = 100; skeleton.visible = false;
  return { root, joints, skeleton, updateSkeleton() {
    if (!skeleton.visible) return;
    const a = geometry.attributes.position; const v = new THREE.Vector3();
    edges.forEach((j, i) => { j.getWorldPosition(v); a.setXYZ(i * 2, v.x, v.y, v.z); j.parent.getWorldPosition(v); a.setXYZ(i * 2 + 1, v.x, v.y, v.z); });
    a.needsUpdate = true; geometry.computeBoundingSphere();
  } };
}

const down = new THREE.Vector3(0, -1, 0);
// Analytic two-bone solve in world space, with a pole to select the bend plane.
export function solveTwoBone(upper, lower, end, target, pole, lengthA, lengthB) {
  upper.updateWorldMatrix(true, true);
  const origin = upper.getWorldPosition(new THREE.Vector3());
  const axis = target.clone().sub(origin); const distance = THREE.MathUtils.clamp(axis.length(), .015, lengthA + lengthB - .001);
  axis.normalize();
  const bend = pole.clone().sub(origin); bend.addScaledVector(axis, -bend.dot(axis));
  if (bend.lengthSq() < .00001) bend.set(0, 0, 1).addScaledVector(axis, -axis.z);
  bend.normalize();
  const along = (lengthA * lengthA + distance * distance - lengthB * lengthB) / (2 * distance);
  const elbow = origin.clone().addScaledVector(axis, along).addScaledVector(bend, Math.sqrt(Math.max(0, lengthA * lengthA - along * along)));
  aim(upper, elbow.clone().sub(origin).normalize());
  upper.updateWorldMatrix(true, true);
  const lowerOrigin = lower.getWorldPosition(new THREE.Vector3());
  const reachable = origin.clone().addScaledVector(axis, distance);
  aim(lower, reachable.sub(lowerOrigin).normalize());
  end.updateWorldMatrix(true, false);
}
// Swing the bone from its current (FK) direction to the new one with the smallest
// rotation, so the authored twist survives the IK instead of being reset arbitrarily.
function aim(joint, direction) {
  joint.updateWorldMatrix(true, false);
  const worldQ = joint.getWorldQuaternion(new THREE.Quaternion());
  const current = down.clone().applyQuaternion(worldQ);
  const next = new THREE.Quaternion().setFromUnitVectors(current, direction).multiply(worldQ);
  const parentQ = joint.parent.getWorldQuaternion(new THREE.Quaternion());
  joint.quaternion.copy(parentQ.invert().multiply(next));
}

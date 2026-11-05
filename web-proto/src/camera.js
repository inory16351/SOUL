import * as THREE from 'three';

// Top-down action camera (fixed yaw, ~55 degree pitch) that follows the character, with
// mouse-wheel zoom, screen shake, and a ground-plane pick for click-to-move / aiming.
// C toggles a side view for inspecting motion.
export class FollowCamera {
  constructor(aspect) {
    this.camera = new THREE.PerspectiveCamera(38, aspect, .1, 140);
    this.side = false; this.zoom = 1; this.target = new THREE.Vector3(0, 1, 0); this.position = new THREE.Vector3(0, 11, 8);
    this.shakeAmount = 0; this.clock = 0; this.camera.position.copy(this.position);
    this.raycaster = new THREE.Raycaster(); this.ground = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  }
  toggle() { this.side = !this.side; return this.side; }
  zoomBy(delta) { this.zoom = THREE.MathUtils.clamp(this.zoom * (1 + delta), .55, 1.6); }
  shake(power) { this.shakeAmount = Math.max(this.shakeAmount, power); }
  // ndc: {x, y} in [-1, 1]; returns the point on the ground under the cursor (or null)
  pick(ndc) { this.raycaster.setFromCamera(ndc, this.camera); return this.raycaster.ray.intersectPlane(this.ground, new THREE.Vector3()); }
  update(dt, position) {
    this.clock += dt; const aspect = this.camera.aspect;
    const offset = this.side ? new THREE.Vector3(8.8, 2.5, .25) : new THREE.Vector3(0, 10.2, 7.0).multiplyScalar(this.zoom);
    if (aspect < 1.2) offset.multiplyScalar(1.2);
    const target = position.clone().add(new THREE.Vector3(0, .8, 0)); this.target.lerp(target, 1 - Math.exp(-dt * 8));
    this.position.lerp(this.target.clone().add(offset), 1 - Math.exp(-dt * 7)); this.camera.position.copy(this.position);
    this.shakeAmount *= Math.exp(-dt * 17);
    this.camera.position.x += Math.sin(this.clock * 103) * this.shakeAmount; this.camera.position.y += Math.sin(this.clock * 137) * this.shakeAmount * .6;
    this.camera.lookAt(this.target);
    this.camera.setViewOffset(window.innerWidth, window.innerHeight, -window.innerWidth * (aspect < 1.2 ? .10 : .08), 0, window.innerWidth, window.innerHeight);
  }
}

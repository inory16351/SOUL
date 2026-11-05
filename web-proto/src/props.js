import * as THREE from 'three';

// Breakable scrap objects scattered over the battlefield: rusted barrels, crates and dead
// signal lamps. A blade hit shatters them into tumbling fragments with sparks.
const RUST = 0x6b3f26, IRON = 0x4a4744, BONE = 0xb9ab8f, DARK = 0x2a2522;
function shaded(geo, color) {
  geo = geo.index ? geo.toNonIndexed() : geo; geo.computeBoundingBox();
  const p = geo.attributes.position, c = new Float32Array(p.count * 3), base = new THREE.Color(color), h = Math.max(.01, geo.boundingBox.max.y - geo.boundingBox.min.y);
  for (let i = 0; i < p.count; i++) { const k = .75 + .3 * (p.getY(i) - geo.boundingBox.min.y) / h + .06 * Math.sin(i * 12.9); c[i * 3] = base.r * k; c[i * 3 + 1] = base.g * k; c[i * 3 + 2] = base.b * k; }
  geo.setAttribute('color', new THREE.BufferAttribute(c, 3));
  const m = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: .9, metalness: .25 })); m.castShadow = m.receiveShadow = true; return m;
}
const KINDS = {
  barrel: () => { const g = new THREE.Group(); const b = shaded(new THREE.CylinderGeometry(.32, .32, .9, 10), RUST); b.position.y = .45; g.add(b); for (const y of [.18, .72]) { const r = shaded(new THREE.CylinderGeometry(.335, .335, .05, 10), DARK); r.position.y = y; g.add(r); } return { group: g, radius: .38, height: .9, hp: 1 }; },
  crate: () => { const g = new THREE.Group(); const b = shaded(new THREE.BoxGeometry(.8, .7, .8), IRON); b.position.y = .35; b.rotation.y = .3; g.add(b); const s = shaded(new THREE.BoxGeometry(.84, .08, .84), RUST); s.position.y = .5; s.rotation.y = .3; g.add(s); return { group: g, radius: .5, height: .7, hp: 2 }; },
  lamp: () => { const g = new THREE.Group(); const pole = shaded(new THREE.CylinderGeometry(.05, .07, 2.1, 6), IRON); pole.position.y = 1.05; pole.rotation.z = .12; g.add(pole);
    const head = shaded(new THREE.BoxGeometry(.32, .22, .22), DARK); head.position.set(.13, 2.08, 0); g.add(head);
    const lens = new THREE.Mesh(new THREE.SphereGeometry(.06, 8, 6), new THREE.MeshBasicMaterial({ color: new THREE.Color(2.2, .5, .2), toneMapped: false })); lens.position.set(.13, 1.98, .1); g.add(lens);
    const base = shaded(new THREE.CylinderGeometry(.22, .28, .2, 8), BONE); base.position.y = .1; g.add(base); return { group: g, radius: .3, height: 2.1, hp: 1 }; },
};

export function createBreakables(scene, vfx, count = 12, bounds = 17) {
  const list = [], debris = [];
  const kinds = ['barrel', 'barrel', 'crate', 'crate', 'lamp'];
  for (let i = 0; i < count; i++) {
    let p; for (let k = 0; k < 20; k++) { const a = Math.random() * Math.PI * 2, r = 4 + Math.random() * (bounds - 6); p = new THREE.Vector3(Math.cos(a) * r, 0, Math.sin(a) * r); if (list.every(o => o.group.position.distanceTo(p) > 2.2)) break; }
    const o = KINDS[kinds[i % kinds.length]](); o.group.position.copy(p); o.group.rotation.y = Math.random() * 6; scene.add(o.group); list.push(o);
  }
  const segDist = (p, q, x) => { const d = q.clone().sub(p), t = THREE.MathUtils.clamp(x.clone().sub(p).dot(d) / Math.max(d.lengthSq(), 1e-9), 0, 1); return p.clone().addScaledVector(d, t); };
  function shatter(o, from) {
    o.dead = true; scene.remove(o.group);
    const c = o.group.position.clone().setY(o.height * .5); vfx.burst(c, 18, 1); vfx.dust(o.group.position, 1.2);
    for (let i = 0; i < 9; i++) {
      const m = new THREE.Mesh(new THREE.BoxGeometry(.08 + Math.random() * .18, .05 + Math.random() * .12, .08 + Math.random() * .18), o.group.children[0].material);
      m.position.copy(c).add(new THREE.Vector3((Math.random() - .5) * .4, (Math.random() - .5) * o.height * .6, (Math.random() - .5) * .4)); m.castShadow = true; scene.add(m);
      const away = c.clone().sub(from).setY(0).normalize();
      debris.push({ m, v: away.multiplyScalar(2 + Math.random() * 3).add(new THREE.Vector3((Math.random() - .5) * 3, 2.5 + Math.random() * 3, (Math.random() - .5) * 3)), w: new THREE.Vector3(Math.random() * 9, Math.random() * 9, Math.random() * 9), life: 0 });
    }
    setTimeout(() => respawn(o), 14000);
  }
  function respawn(o) { o.dead = false; o.hp = o.maxHp ?? o.hp; o.group.scale.setScalar(.01); scene.add(o.group); o.grow = 0; }
  for (const o of list) o.maxHp = o.hp;
  return {
    list,
    // blade segment p-q (world) with reach: returns true if anything was struck
    hit(p, q, reach, from) {
      let any = false;
      for (const o of list) {
        if (o.dead || o.cool > 0) continue;
        const a = o.group.position.clone().setY(.1), b = o.group.position.clone().setY(o.height), x = segDist(a, b, segDist(p, q, o.group.position.clone().setY(o.height * .5)));
        const closest = segDist(p, q, x);
        if (closest.distanceTo(x) < o.radius + reach) { any = true; o.cool = .25; o.hp--; if (o.hp <= 0) shatter(o, from); else { vfx.burst(closest, 8, .6); o.wobble = 1; } }
      }
      return any;
    },
    update(dt) {
      for (const o of list) {
        o.cool = Math.max(0, (o.cool ?? 0) - dt);
        if (o.grow !== undefined) { o.grow += dt * 2; o.group.scale.setScalar(Math.min(1, o.grow)); if (o.grow >= 1) delete o.grow; }
        if (o.wobble) { o.wobble = Math.max(0, o.wobble - dt * 3); o.group.rotation.z = Math.sin(o.wobble * 30) * .08 * o.wobble; }
      }
      for (let i = debris.length - 1; i >= 0; i--) {
        const d = debris[i]; d.life += dt; d.v.y -= 9.8 * dt; d.m.position.addScaledVector(d.v, dt);
        if (d.m.position.y < .04) { d.m.position.y = .04; d.v.y *= -.3; d.v.x *= .6; d.v.z *= .6; d.w.multiplyScalar(.6); }
        d.m.rotation.x += d.w.x * dt; d.m.rotation.y += d.w.y * dt; d.m.rotation.z += d.w.z * dt;
        if (d.life > 3.5) { d.m.scale.multiplyScalar(1 - dt * 3); if (d.life > 4.5) { scene.remove(d.m); d.m.geometry.dispose(); debris.splice(i, 1); } }
      }
    },
  };
}

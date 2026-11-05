import * as THREE from 'three';

// ---------- Sharp blade with a neon edge ----------
// Keeps the curved metal blade and traces its cutting edge (+X side) with a thin white-blue
// neon line plus a soft glow sheath (HDR colours, so the bloom makes only the edge shine).
const BLUE = new THREE.Color(.20, .62, 1.0), CORE = new THREE.Color(.80, .95, 1.0);
export function createLaserBlade(character) {
  const sword = character.sword;
  character.root.updateMatrixWorld(true);
  const inv = sword.matrixWorld.clone().invert();
  // sample the blade surface in sword space and take the outermost +X point per height slice
  const bins = Array.from({ length: 12 }, () => null), v = new THREE.Vector3();
  const blade = character.blade;
  blade.updateMatrixWorld(true);
  const pos = blade.geometry.attributes.position;
  let yMin = Infinity, yMax = -Infinity; const pts = [];
  for (let i = 0; i < pos.count; i++) { v.fromBufferAttribute(pos, i).applyMatrix4(blade.matrixWorld).applyMatrix4(inv); pts.push(v.clone()); yMin = Math.min(yMin, v.y); yMax = Math.max(yMax, v.y); }
  for (const p of pts) { const b = Math.min(11, Math.floor((p.y - yMin) / (yMax - yMin + 1e-6) * 12)); if (!bins[b] || p.x > bins[b].x) bins[b] = p; }
  const edge = bins.filter(Boolean).map(p => new THREE.Vector3(p.x + .004, p.y, 0));
  edge.push(character.bladeTip.position.clone());
  const curve = new THREE.CatmullRomCurve3(edge);
  const tube = (r, color, opacity, additive) => {
    const mesh = new THREE.Mesh(new THREE.TubeGeometry(curve, 48, r, 6, false), new THREE.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false, blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending, toneMapped: false }));
    mesh.frustumCulled = false; sword.add(mesh); return mesh;
  };
  const line = tube(.0045, CORE.clone().multiplyScalar(3), 1, false);
  const glow = tube(.016, BLUE.clone().multiplyScalar(2.2), .45, true);
  const halo = tube(.04, BLUE.clone().multiplyScalar(1.1), .12, true);
  const light = new THREE.PointLight(0x4aa8ff, .9, 2.2, 2); light.position.set(.05, .55, 0); sword.add(light);
  let t = 0;
  return {
    length: character.bladeTip.position.y,
    update(dt, boost = 0) {
      t += dt;
      const flicker = 1 + Math.sin(t * 47) * .05 + Math.sin(t * 19.3) * .03;
      glow.material.opacity = (.40 + boost * .35) * flicker; halo.material.opacity = (.10 + boost * .22) * flicker;
      line.material.color.copy(CORE).multiplyScalar(3 + boost * 2);
      light.intensity = (.8 + boost * 1.8) * flicker;
    },
  };
}

// ---------- Pixel LED face ----------
// A black screen fills the hood opening; expressions are lit on a coarse LED pixel grid
// (24 x 12, nearest-neighbour, dim unlit pixels visible) so they read clearly from the
// top-down camera. Each expression is an 8 x 7 bitmap per eye (right eye mirrored).
// Cute "0_0" face: ring eyes (6 x 7) plus a tiny mouth under the gap between them.
const EYES = {
  idle:  ['.####.', '##..##', '#....#', '#....#', '#....#', '##..##', '.####.'],
  blink: ['......', '......', '......', '######', '......', '......', '......'],
  happy: ['......', '......', '.####.', '##..##', '#....#', '......', '......'],
  focus: ['######', '#....#', '#....#', '##..##', '.####.', '......', '......'],
  hurt:  ['......', '##....', '.###..', '...###', '.###..', '##....', '......'],   // squint: > <
  wide:  ['.####.', '##..##', '#.##.#', '#.##.#', '#....#', '##..##', '.####.'],
};
// thin single-row mouth lines read as '_' (short tall blocks looked like front teeth)
const MIRROR = new Set(['hurt']);   // shapes whose right eye is the mirror of the left
const MOUTH = { idle: ['######'], blink: ['######'], focus: ['########'], happy: ['#......#', '.######.'], hurt: ['#.#.#.#', '.#.#.#.'], wide: ['.####.', '#....#', '.####.'] };
const GRID_W = 24, GRID_H = 16, CELL = 10;
export function createFace(character) {
  const head = character.joints.head;
  for (const c of head.children) if (/^(wrapped-head-underlayer|overlapping-face-scarf-d|single-recessed-optic|brow-overlapping-fold)$/.test(c.name)) c.visible = false;
  const canvas = document.createElement('canvas'); canvas.width = GRID_W * CELL; canvas.height = GRID_H * CELL;
  const ctx = canvas.getContext('2d'), tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace; tex.magFilter = THREE.NearestFilter; tex.minFilter = THREE.LinearFilter; tex.generateMipmaps = false;
  const screen = new THREE.Mesh(new THREE.PlaneGeometry(.30, .20), new THREE.MeshBasicMaterial({ map: tex, toneMapped: false, color: new THREE.Color(1.15, 1.15, 1.15) }));
  screen.name = 'pixel-face-screen'; screen.position.set(0, .09, .214); screen.rotation.x = -.32; head.add(screen);
  const glow = new THREE.PointLight(0x4cb6ff, .12, .45, 2); glow.position.set(0, .06, .30); head.add(glow);
  let mood = 'idle', drawn = '', blinkAt = 2 + Math.random() * 3, blinkT = -1, t = 0, hold = 0, look = 0, lookAt = 3;
  function draw(shape, dx) {
    const key = shape + dx; if (key === drawn) return; drawn = key;
    ctx.fillStyle = '#010205'; ctx.fillRect(0, 0, canvas.width, canvas.height);
    const lit = new Set(), bmp = EYES[shape] ?? EYES.idle, mouth = MOUTH[shape] ?? MOUTH.idle;
    for (const [ox, mirror] of [[4, false], [14, MIRROR.has(shape)]]) bmp.forEach((row, y) => [...row].forEach((ch, x) => { if (ch === '#') lit.add(`${ox + dx + (mirror ? row.length - 1 - x : x)},${y + 2}`); }));
    mouth.forEach((row, y) => [...row].forEach((ch, x) => { if (ch === '#') lit.add(`${12 - (row.length >> 1) + dx + x},${y + 12}`); }));
    for (let y = 0; y < GRID_H; y++) for (let x = 0; x < GRID_W; x++) {
      const on = lit.has(`${x},${y}`);
      ctx.fillStyle = on ? '#7fe3ff' : '#08141c';
      ctx.fillRect(x * CELL + 1, y * CELL + 1, CELL - 2, CELL - 2);
      if (on) { ctx.fillStyle = '#e6fbff'; ctx.fillRect(x * CELL + 3, y * CELL + 3, CELL - 6, CELL - 6); }
    }
    tex.needsUpdate = true;
  }
  draw('idle', 0);
  return {
    canvas, preview: shape => draw(shape, 0),
    set(m, seconds = 0) { mood = m; hold = seconds; },
    update(dt, state) {
      t += dt; if (hold > 0) hold -= dt;
      const base = hold > 0 ? mood : state;
      if (base === 'idle') {
        if (t > blinkAt && blinkT < 0) blinkT = 0;
        if (blinkT >= 0) { blinkT += dt; if (blinkT > .12) { blinkT = -1; blinkAt = t + 2 + Math.random() * 3.5; } }
        if (t > lookAt) { look = [-1, 0, 0, 1][Math.floor(Math.random() * 4)]; lookAt = t + 1.5 + Math.random() * 2.5; }   // glance around
      } else look = 0;
      draw(base === 'idle' && blinkT >= 0 ? 'blink' : base, base === 'idle' ? look : 0);
      const pulse = 1 + Math.sin(t * 3) * .05; screen.material.color.setScalar(1.15 * pulse); glow.intensity = .12 * pulse;
    },
  };
}

// ---------- Torn right side of the rigid robe ----------
// Cuts the right side of the robe shells away below a jagged line that rises toward the
// right shoulder, Dune-style: vertices below the tear collapse onto the ragged edge.
export function tearRightSide(character) {
  const rules = { 'robe-hip-shell': -.30, 'robe-waist-shell': -.10, 'loose-robe-chest': .02, 'heavy-shoulder-poncho': .04, 'poncho-underlayer': .03 };
  character.root.traverse(obj => {
    if (!obj.isMesh || !(obj.name in rules)) return;
    const pos = obj.geometry.attributes.position, cut = rules[obj.name];
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
      if (x < .03) continue;
      const k = Math.min(1, (x - .03) / .12);                                   // fades in toward the right side
      const jag = Math.sin(z * 41 + x * 17) * .035 + Math.sin(z * 97) * .018;
      const line = cut + .24 * k + jag;
      if (y < line) pos.setY(i, THREE.MathUtils.lerp(y, line, k));
    }
    pos.needsUpdate = true; obj.geometry.computeVertexNormals();
  });
}

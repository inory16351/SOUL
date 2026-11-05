import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { painted } from './character.js';

function random(seed) { let n = seed; return () => { n = (n * 1664525 + 1013904223) >>> 0; return n / 4294967296; }; }
export function createScene() {
  const scene = new THREE.Scene(), rand = random(4207);
  const horizon = new THREE.Color(0x77716a);
  scene.background = horizon;
  scene.fog = new THREE.FogExp2(horizon, .026);
  scene.add(new THREE.HemisphereLight(0x8fabbc, 0x342923, .85));
  const sunOffset = new THREE.Vector3(-24, 12, -18);
  const sun = new THREE.DirectionalLight(0xffa078, 2.5);
  sun.name = 'sun'; sun.position.copy(sunOffset); sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  Object.assign(sun.shadow.camera, { left: -21, right: 21, top: 21, bottom: -21, near: .5, far: 85 });
  sun.shadow.camera.updateProjectionMatrix();
  sun.shadow.normalBias = .035; sun.shadow.bias = -.00015;
  scene.add(sun, sun.target);
  // Legacy callers also reposition `sun` each frame. Anchor the authored low
  // angle to their moving target before the shadow pass, without renderer setup.
  scene.onBeforeRender = () => {
    sun.position.copy(sun.target.position).add(sunOffset);
    sun.updateMatrixWorld(); sun.target.updateMatrixWorld();
  };

  // Vertex-painted sky; its horizon is exactly the fog colour. No textures.
  const skyGeo = new THREE.SphereGeometry(115, 32, 16);
  const skyPos = skyGeo.attributes.position, skyColors = [];
  const zenith = new THREE.Color(0x424c57), amber = new THREE.Color(0xb18a65);
  for (let i = 0; i < skyPos.count; i++) {
    const elevation = skyPos.getY(i) / 115;
    const c = horizon.clone();
    if (elevation > 0) {
      c.lerp(amber, Math.sin(Math.min(elevation / .42, 1) * Math.PI));
      c.lerp(zenith, THREE.MathUtils.smoothstep(elevation, .25, 1));
    }
    skyColors.push(c.r, c.g, c.b);
  }
  skyGeo.setAttribute('color', new THREE.Float32BufferAttribute(skyColors, 3));
  const sky = new THREE.Mesh(skyGeo, new THREE.MeshBasicMaterial({
    vertexColors: true, side: THREE.BackSide, fog: false, depthWrite: false,
  }));
  sky.name = 'dust sky'; sky.renderOrder = -10; sky.frustumCulled = false; scene.add(sky);

  // One static draw per material. Bake transforms/paint into geometry, not meshes.
  const materials = {
    concrete: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true }),
    steel: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: .83, metalness: .48, flatShading: true }),
    char: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true }),
    oil: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: .24, metalness: .32, flatShading: true }),
    cracks: new THREE.MeshBasicMaterial({ vertexColors: true }),
    skyline: new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true }),
  };
  const batches = Object.fromEntries(Object.keys(materials).map(key => [key, []]));
  const transform = new THREE.Object3D();
  function bake(key, source, color, position = [0, 0, 0], rotation = [0, 0, 0], scale = [1, 1, 1], parent) {
    const geo = source.index ? source.toNonIndexed() : source;
    if (geo !== source) source.dispose();
    geo.deleteAttribute('uv'); geo.clearGroups(); geo.computeBoundingBox();
    const p = geo.attributes.position, n = geo.attributes.normal, box = geo.boundingBox;
    const height = Math.max(.01, box.max.y - box.min.y), base = new THREE.Color(color), colors = [];
    for (let i = 0; i < p.count; i++) {
      const top = (p.getY(i) - box.min.y) / height;
      const shade = .66 + .27 * top + .08 * Math.max(0, n.getY(i)) + .035 * Math.sin(Math.floor(i / 3) * 17.13);
      colors.push(base.r * shade, base.g * shade, base.b * shade);
    }
    geo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
    transform.position.fromArray(position); transform.rotation.set(...rotation); transform.scale.fromArray(scale); transform.updateMatrix();
    geo.applyMatrix4(transform.matrix);
    if (parent) geo.applyMatrix4(parent);
    batches[key].push(geo);
  }
  function frame(x, y, z, yaw = 0, roll = 0) {
    return new THREE.Matrix4().compose(new THREE.Vector3(x, y, z),
      new THREE.Quaternion().setFromEuler(new THREE.Euler(0, yaw, roll)), new THREE.Vector3(1, 1, 1));
  }
  function box(key, color, size, pos, parent, rotation = [0, 0, 0]) {
    bake(key, new THREE.BoxGeometry(...size), color, pos, rotation, [1, 1, 1], parent);
  }
  function rod(key, color, a, b, radius, parent, tip = radius) {
    const start = new THREE.Vector3(...a), end = new THREE.Vector3(...b), delta = end.clone().sub(start);
    const geo = new THREE.CylinderGeometry(tip, radius, delta.length(), 5);
    geo.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize()));
    bake(key, geo, color, start.add(end).multiplyScalar(.5).toArray(), [0, 0, 0], [1, 1, 1], parent);
  }

  const craters = [{ x: -19, z: -5, r: 3.6, depth: .72 }, { x: 13, z: 20, r: 2.7, depth: .5 }];
  const scorches = [{ x: -7, z: -2, r: 4.5 }, { x: 6, z: 9, r: 3.2 }, { x: 11, z: -15, r: 5 }, ...craters];
  function groundHeight(x, z) {
    let y = -.055 + .012 * Math.sin(x * .7) * Math.cos(z * .6);
    for (const crater of craters) {
      const d = Math.hypot(x - crater.x, z - crater.z) / crater.r;
      if (d < 1) y -= crater.depth * (1 - d * d) ** 2;
      y += .13 * Math.exp(-(((d - 1) / .15) ** 2));
    }
    return y;
  }
  const groundGeo = new THREE.PlaneGeometry(60, 60, 100, 100);
  groundGeo.rotateX(-Math.PI / 2);
  const groundPos = groundGeo.attributes.position, groundColors = [];
  const ashGrey = new THREE.Color(0x77716a), rustBrown = new THREE.Color(0x705044), charcoal = new THREE.Color(0x302e2d);
  for (let i = 0; i < groundPos.count; i++) {
    let x = groundPos.getX(i), z = groundPos.getZ(i);
    if (Math.abs(x) < 29.9 && Math.abs(z) < 29.9) { x += (rand() - .5) * .32; z += (rand() - .5) * .32; }
    groundPos.setXYZ(i, x, groundHeight(x, z), z);
    const drift = THREE.MathUtils.clamp(.38 + .25 * Math.sin(x * .18 + Math.sin(z * .13)) + .2 * Math.cos(z * .23 - x * .08), 0, 1);
    const c = ashGrey.clone().lerp(rustBrown, drift);
    let scorch = 0;
    for (const patch of scorches) {
      const dx = (x - patch.x) / patch.r, dz = (z - patch.z) / (patch.r * .65);
      scorch = Math.max(scorch, Math.exp(-(dx * dx + dz * dz) * 1.6) * (.65 + .15 * Math.sin(x * 1.7 + z)));
    }
    c.lerp(charcoal, scorch).multiplyScalar(.93 + rand() * .12);
    groundColors.push(c.r, c.g, c.b);
  }
  groundGeo.setAttribute('color', new THREE.Float32BufferAttribute(groundColors, 3)); groundGeo.computeVertexNormals();
  const ground = new THREE.Mesh(groundGeo, materials.concrete); ground.name = 'cratered ash soil'; ground.receiveShadow = true; scene.add(ground);
  // Distant skirt hides the square terrain edge beneath the fog and city ruins.
  bake('concrete', new THREE.PlaneGeometry(220, 220), 0x625952, [0, -.85, 0], [-Math.PI / 2, 0, 0]);

  function ribbon(ax, az, bx, bz, width) {
    const length = Math.hypot(bx - ax, bz - az), dx = (bz - az) / length * width, dz = -(bx - ax) / length * width;
    const points = [[ax - dx, az - dz], [ax + dx, az + dz], [bx + dx, bz + dz], [bx - dx, bz - dz]];
    const positions = [];
    for (const i of [0, 2, 1, 0, 3, 2]) { const [x, z] = points[i]; positions.push(x, groundHeight(x, z) + .022, z); }
    const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3)); geo.computeVertexNormals();
    bake('cracks', geo, 0x292727);
  }
  // Short angular branches, not an artificial combat ring or regular grid.
  for (let i = 0; i < 125; i++) {
    let x = (rand() - .5) * 55, z = (rand() - .5) * 55, angle = rand() * Math.PI * 2;
    for (let j = 0, count = 3 + Math.floor(rand() * 6); j < count; j++) {
      angle += (rand() - .5) * 1.5;
      const nx = x + Math.cos(angle) * (.25 + rand() * .5), nz = z + Math.sin(angle) * (.25 + rand() * .5);
      if (Math.abs(nx) > 29 || Math.abs(nz) > 29) break;
      ribbon(x, z, nx, nz, .009 + rand() * .016);
      if (j === 2) ribbon(x, z, x + Math.cos(angle + 1.2) * .7, z + Math.sin(angle + 1.2) * .7, .009);
      x = nx; z = nz;
    }
  }
  function puddle(x, z, radius) {
    const vertices = [], rim = [];
    for (let i = 0; i < 16; i++) { const a = i / 16 * Math.PI * 2, r = radius * (.7 + rand() * .3); rim.push([x + Math.cos(a) * r, z + Math.sin(a) * r * .58]); }
    const y = -.022;
    for (let i = 0; i < rim.length; i++) { const a = rim[i], b = rim[(i + 1) % rim.length]; vertices.push(x, y, z, b[0], y, b[1], a[0], y, a[1]); }
    const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3)); geo.computeVertexNormals(); bake('oil', geo, 0x252729);
  }
  puddle(4.3, -6.5, 1.2); puddle(-10.5, 7, 1.8); puddle(16, -7, 1.4); puddle(-3.5, 12, .8);

  function slab(x, z, size, yaw) {
    const parent = frame(x, .06, z, yaw, .1);
    bake('concrete', new THREE.DodecahedronGeometry(1, 0), 0x999389, [0, .08, 0], [0, 0, 0], [size, .27, size * .62], parent);
    for (let i = 0; i < 4; i++) {
      const a = -size * .55 + i * size * .32;
      rod('steel', 0x754535, [a, .12, .2], [a, .17, size * .75], .024, parent);
      rod('steel', 0x754535, [a, .17, size * .75], [a + .14, .4 + rand() * .3, size * .83], .024, parent);
    }
  }
  for (const [x, z, size, yaw] of [[-12, -11, 1.7, .4], [-14, 6, 2.1, 1.2], [10, -14, 1.9, -.7], [18, 3, 2.2, .8], [-6, 19, 1.5, 2], [23, -13, 2.5, .3]]) slab(x, z, size, yaw);

  function girder(x, z, length, yaw) {
    const parent = frame(x, -.05, z, yaw, .18);
    box('steel', 0x85533e, [length, .43, .075], [0, .2, 0], parent);
    for (const y of [0, .43]) box('steel', 0x9e6548, [length, .065, .42], [0, y, 0], parent);
    for (let i = 0; i < 4; i++) box('steel', 0x553b31, [.045, .36, .35], [(i / 3 - .5) * length * .85, .21, 0], parent);
  }
  girder(-10, 12, 4.7, .55); girder(16, 8, 5.5, -1); girder(-22, -11, 6, .2); girder(8, -18, 4, -.8);

  function wall(x, z, yaw) {
    const parent = frame(x, 0, z, yaw, -.1);
    // Separate piers and lintels make genuine empty window openings.
    box('concrete', 0x8c857b, [6.6, .65, .48], [0, .25, 0], parent);
    for (const px of [-3, -1, 1, 3]) {
      const h = 2.6 + rand() * 1.2;
      box('concrete', 0xaaa195, [.5, h, .48], [px, h / 2, 0], parent);
      bake('concrete', new THREE.DodecahedronGeometry(.4), 0x918b82, [px, h, 0], [.2, .5, .2], [.7, .6, .65], parent);
      rod('steel', 0x754b38, [px, h - .1, 0], [px + .13, h + .45, .1], .027, parent);
    }
    for (const px of [-2, 0, 2]) {
      box('concrete', 0x9a9286, [1.6, .38, .48], [px, 2.45, 0], parent);
      box('concrete', 0x756e65, [1.6, .16, .65], [px, .9, 0], parent);
    }
    slab(x + Math.sin(yaw) * 1.8, z + Math.cos(yaw) * 1.8, 1.5, yaw + .5);
  }
  wall(-15, -18, -.35); wall(15, -20, .5); wall(24, 10, 1.8);

  // A fallen lattice pylon lies along the east perimeter, with sagging cut wires.
  const pylon = frame(21, .75, -5, -.4);
  const levels = [0, 2.8, 5.6, 8.4, 11];
  function corner(level, side) {
    const width = 1.3 - level / 11 * .95;
    return [(side === 0 || side === 3 ? -1 : 1) * width, (side < 2 ? -.6 : .6) * width, level - 5.5];
  }
  for (let j = 0; j < levels.length - 1; j++) for (let s = 0; s < 4; s++) {
    const a = corner(levels[j], s), b = corner(levels[j + 1], s), c = corner(levels[j], (s + 1) % 4), d = corner(levels[j + 1], (s + 1) % 4);
    rod('steel', 0x785344, a, b, .065, pylon); rod('steel', 0x624c40, a, c, .045, pylon);
    rod('steel', 0x805a47, a, d, .03, pylon); rod('steel', 0x805a47, c, b, .03, pylon);
  }
  for (const z of [2.5, 4.4]) {
    rod('steel', 0x8e6550, [-2.8, .4, z], [2.8, .4, z], .065, pylon);
    for (const side of [-1, 1]) {
      rod('steel', 0x715347, [0, -.2, z - 1], [side * 2.8, .4, z], .04, pylon);
      for (let i = 0; i < 3; i++) bake('char', new THREE.CylinderGeometry(.14, .14, .09, 6), 0x666761, [side * 2.55, .45 + i * .12, z], [0, 0, 0], [1, 1, 1], pylon);
      let last = [side * 2.55, .8, z];
      for (let i = 1; i <= 12; i++) {
        const next = [side * (2.55 + i * .18), .8 * (1 - i / 12) ** 2 - .68, z + i * .25];
        rod('char', 0x282826, last, next, .015, pylon); last = next;
      }
    }
  }

  // Crushed roof frame and empty windows read as a car, without reflective glass.
  const car = frame(-17, -.12, 11, -.7, -.12);
  box('steel', 0x815746, [1.85, .55, 4], [0, .3, 0], car);
  box('steel', 0x9a6b50, [1.8, .12, 1.15], [0, .61, -1.35], car, [.13, 0, .05]);
  box('steel', 0x6a4d40, [1.65, .13, .7], [0, .61, 1.52], car);
  box('char', 0x252827, [1.5, .1, 1.7], [0, .66, .12], car);
  for (const side of [-1, 1]) {
    rod('steel', 0x9f7257, [side * .84, .58, -.78], [side * .65, 1.27, -.32], .055, car);
    rod('steel', 0x8c5b47, [side * .84, .58, 1.15], [side * .65, 1.23, .75], .055, car);
    for (const z of [-1.2, 1.2]) bake('char', new THREE.CylinderGeometry(.36, .36, .23, 10), 0x2d2c2b, [side * .88, .22, z], [0, 0, Math.PI / 2], [1, 1, 1], car);
  }
  box('steel', 0x926a53, [1.4, .09, 1.2], [0, 1.25, .22], car, [.05, 0, -.08]);
  box('steel', 0x4c4540, [1.9, .13, .16], [0, .2, -2.05], car);
  box('steel', 0x91684d, [.06, .65, 1.1], [1.24, .48, .1], car, [0, .65, .15]);

  function deadTree(x, z, height, yaw) {
    const parent = frame(x, 0, z, yaw);
    rod('char', 0x49433e, [0, 0, 0], [.1, height * .58, .05], .24, parent, .13);
    rod('char', 0x3a3734, [.1, height * .58, .05], [-.18, height, .14], .13, parent, .035);
    for (const side of [-1, 1]) {
      const joint = [side * .72, height * .67, .1], tip = [side * 1.12, height * .86, -.24];
      rod('char', 0x443d38, [0, height * .38, 0], joint, .09, parent, .055);
      rod('char', 0x51463d, joint, tip, .055, parent, .012);
      rod('char', 0x343230, joint, [side * 1.3, height * .7, .42], .04, parent, .012);
    }
    for (let j = 0; j < 4; j++) { const a = j * Math.PI / 2; rod('char', 0x3c3834, [0, .18, 0], [Math.cos(a) * .7, .01, Math.sin(a) * .7], .09, parent, .02); }
  }
  deadTree(-11, -17, 3.6, .8); deadTree(11, 17, 3.2, 2); deadTree(-23, 1, 4.2, 1); deadTree(25, -18, 3.8, .4); deadTree(-5, -24, 2.9, 2);

  // Sparse, low chips in the playable clearing; denser rubble outside 15 metres.
  for (let i = 0; i < 260; i++) {
    const angle = rand() * Math.PI * 2, radius = i < 45 ? 2 + rand() * 12 : 16 + rand() * 12;
    const x = Math.cos(angle) * radius, z = Math.sin(angle) * radius;
    const small = i < 45, size = small ? .06 + rand() * .16 : .12 + rand() * .55;
    const height = small ? .025 + rand() * .045 : .08 + rand() * .24;
    const metal = rand() < .28;
    bake(metal ? 'steel' : 'concrete', metal ? new THREE.BoxGeometry(1, 1, 1) : new THREE.DodecahedronGeometry(1),
      metal ? 0x805640 : 0x898176, [x, groundHeight(x, z) + height * .5, z], [0, rand() * Math.PI, 0], [size * 1.5, height, size]);
  }

  // Broken, stepped tower crowns and hollow upper floors form the distant city.
  for (let i = 0; i < 42; i++) {
    const angle = i / 42 * Math.PI * 2 + (rand() - .5) * .09, radius = 39 + rand() * 28;
    const x = Math.cos(angle) * radius, z = Math.sin(angle) * radius;
    const width = 2.5 + rand() * 4, depth = 2.5 + rand() * 3, height = 5 + rand() * 14;
    const parent = frame(x, -.6, z, rand() * .3, (rand() - .5) * .06);
    box('skyline', 0x4b4b4b, [width, height * .65, depth], [0, height * .325, 0], parent);
    for (const side of [-1, 1]) {
      box('skyline', 0x55514d, [width * .22, height * .35, depth], [side * width * .39, height * .8, 0], parent);
      box('skyline', 0x4d4c4b, [width * .24, .5 + rand() * 2, depth * .6], [side * width * .35, height, .1], parent);
    }
    for (let y = height * .66; y < height * .93; y += 1.6) box('skyline', 0x595550, [width, .18, depth], [0, y, 0], parent);
    if (i % 3 === 0) rod('skyline', 0x464849, [0, height * .9, 0], [.3, height + 2, .2], .07, parent);
  }

  for (const [key, geometries] of Object.entries(batches)) {
    const geo = mergeGeometries(geometries, false);
    for (const source of geometries) source.dispose();
    geo.computeBoundingSphere();
    const mesh = new THREE.Mesh(geo, materials[key]); mesh.name = `ruins ${key}`;
    mesh.castShadow = !['oil', 'cracks', 'skyline'].includes(key); mesh.receiveShadow = key !== 'cracks'; scene.add(mesh);
  }

  const ashCount = 360, ashPositions = new Float32Array(ashCount * 3), ashColors = new Float32Array(ashCount * 3);
  const fallSpeeds = new Float32Array(ashCount), phases = new Float32Array(ashCount);
  for (let i = 0; i < ashCount; i++) {
    ashPositions.set([(rand() - .5) * 38, rand() * 13, (rand() - .5) * 38], i * 3);
    const c = new THREE.Color(0xb5aaa0).multiplyScalar(.55 + rand() * .45); ashColors.set([c.r, c.g, c.b], i * 3);
    fallSpeeds[i] = .13 + rand() * .22; phases[i] = rand() * Math.PI * 2;
  }
  const ashGeo = new THREE.BufferGeometry();
  ashGeo.setAttribute('position', new THREE.BufferAttribute(ashPositions, 3).setUsage(THREE.DynamicDrawUsage));
  ashGeo.setAttribute('color', new THREE.BufferAttribute(ashColors, 3));
  const ash = new THREE.Points(ashGeo, new THREE.PointsMaterial({
    vertexColors: true, size: .035, transparent: true, opacity: .52, depthWrite: false, sizeAttenuation: true,
  }));
  ash.name = 'drifting ash'; ash.frustumCulled = false; scene.add(ash);
  let ambientTime = 0;
  const ambientCenter = new THREE.Vector3();
  const wrap = (value, span) => THREE.MathUtils.euclideanModulo(value, span);
  // Call once per game frame. Particles stay in world space during normal movement;
  // only those crossing the player-centred box wrap to its opposite edge.
  function updateAmbient(dt, center = ambientCenter) {
    const step = Number.isFinite(dt) ? THREE.MathUtils.clamp(dt, 0, .1) : 0;
    ambientTime += step;
    if (center && Number.isFinite(center.x) && Number.isFinite(center.z)) ambientCenter.set(center.x, Number.isFinite(center.y) ? center.y : 0, center.z);
    for (let i = 0; i < ashCount; i++) {
      const k = i * 3, phase = phases[i];
      ashPositions[k] = ambientCenter.x - 19 + wrap(ashPositions[k] + (.09 + Math.sin(ambientTime * .6 + phase) * .13) * step - ambientCenter.x + 19, 38);
      ashPositions[k + 1] = ambientCenter.y + wrap(ashPositions[k + 1] - fallSpeeds[i] * step - ambientCenter.y, 13);
      ashPositions[k + 2] = ambientCenter.z - 19 + wrap(ashPositions[k + 2] + Math.cos(ambientTime * .4 + phase) * .1 * step - ambientCenter.z + 19, 38);
    }
    ashGeo.attributes.position.needsUpdate = true;
    sky.position.copy(ambientCenter);
    sun.target.position.copy(ambientCenter); sun.position.copy(ambientCenter).add(sunOffset);
  }
  const dummy = createDummy(scene);
  dummy.root.visible = false;
  return { scene, sun, dummy, updateAmbient, bounds: 18 };
}
function createDummy(scene) {
  const root=new THREE.Group();root.position.set(0,0,2.15);scene.add(root);
  const base=painted(new THREE.CylinderGeometry(.48,.58,.14,8),0x656d63);base.position.y=.045;root.add(base);
  const pivot=new THREE.Group();pivot.position.y=.12;root.add(pivot);
  function part(geo,color,pos){const m=painted(geo,color);m.position.fromArray(pos);pivot.add(m);return m;}
  part(new THREE.CylinderGeometry(.075,.11,1.5,7),0x634531,[0,.75,0]);
  part(new THREE.CylinderGeometry(.28,.23,.67,9),0x9a7751,[0,1.03,0]);
  const cross=part(new THREE.CylinderGeometry(.065,.065,1.05,6),0x5d422e,[0,1.23,0]);cross.rotation.z=Math.PI/2;
  part(new THREE.IcosahedronGeometry(.22,1),0xc5a675,[0,1.61,0]);
  for(const y of [.78,1.03,1.29]) part(new THREE.CylinderGeometry(.284,.284,.045,9),0x4e4433,[0,y,0]);
  const target=part(new THREE.CylinderGeometry(.18,.18,.035,16),0x9c4332,[0,1.12,-.25]);target.rotation.x=Math.PI/2;
  const inner=part(new THREE.CylinderGeometry(.09,.09,.04,16),0xcaa66d,[0,1.12,-.275]);inner.rotation.x=Math.PI/2;
  let angle=0, velocity=0, twist=0;
  return {root,position:root.position,hit(power=1){velocity+=power*3.8;twist+=power*.8;},update(dt){let remaining=Math.min(dt,.05);while(remaining>0){const step=Math.min(remaining,1/120);velocity+=(-angle*75-velocity*7)*step;angle+=velocity*step;remaining-=step;}twist*=Math.exp(-dt*5);pivot.rotation.x=THREE.MathUtils.clamp(angle,-.5,.5);pivot.rotation.z=Math.sin(angle*2)*.17;pivot.rotation.y=twist;}};
}

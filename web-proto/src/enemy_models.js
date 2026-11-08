import * as THREE from 'three';

// All geometry is baked into its joint's local space. Animation pivots have
// identity rotations, +Z is forward, and the root sits on the ground.
const PALETTE = {
  rust: [0x38241c, 0xa76136], iron: [0x202a28, 0x68766b],
  bone: [0x635d49, 0xd4cbb0], oil: [0x080a09, 0x292c28],
  edge: [0x44483f, 0xaba88e], cloth: [0x101413, 0x343930],
  concrete: [0x4c4945, 0x8f8b86], ash: [0x34312e, 0x6a6762],
  ashCloth: [0x32312f, 0x85817a],
};

function pivot(parent, name, position = [0, 0, 0]) {
  const joint = new THREE.Object3D();
  joint.name = name;
  joint.position.fromArray(position);
  if (parent) parent.add(joint);
  return joint;
}

function box(size, position, rotation = [0, 0, 0]) {
  const geometry = new THREE.BoxGeometry(...size);
  geometry.applyMatrix4(new THREE.Matrix4().makeRotationFromEuler(new THREE.Euler(...rotation)));
  return geometry.translate(...position);
}

function rod(a, b, radius = .025, endRadius = radius, sides = 6) {
  const start = new THREE.Vector3(...a), end = new THREE.Vector3(...b);
  const direction = end.clone().sub(start);
  const geometry = new THREE.CylinderGeometry(endRadius, radius, direction.length(), sides, 1);
  geometry.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(
    new THREE.Vector3(0, 1, 0), direction.normalize(),
  ));
  const center = start.add(end).multiplyScalar(.5);
  return geometry.translate(center.x, center.y, center.z);
}

function pebble(position, scale, detail = 0) {
  return new THREE.IcosahedronGeometry(1, detail).scale(...scale).translate(...position);
}

// Sheet-metal polygon in XY, with its thickness centred at z.
function plate(points, depth, z = 0) {
  const shape = new THREE.Shape();
  shape.moveTo(...points[0]);
  for (let i = 1; i < points.length; i++) shape.lineTo(...points[i]);
  shape.closePath();
  return new THREE.ExtrudeGeometry(shape, {
    depth, steps: 1, bevelEnabled: false, curveSegments: 1,
  }).translate(0, 0, z - depth / 2);
}

// Join differently painted scraps into a single draw call per moving section.
// No BufferGeometryUtils dependency; all triangles get flat face normals.
function scrap(parent, name, pieces, material) {
  const positions = [], colors = [];
  for (const [source, paint] of pieces) {
    const geometry = source.index ? source.toNonIndexed() : source;
    geometry.computeBoundingBox();
    const p = geometry.attributes.position, bounds = geometry.boundingBox;
    const span = Math.max(.001, bounds.max.y - bounds.min.y);
    const [low, high] = PALETTE[paint];
    const bottom = new THREE.Color(low), top = new THREE.Color(high), color = new THREE.Color();
    for (let i = 0; i < p.count; i++) {
      const x = p.getX(i), y = p.getY(i), z = p.getZ(i);
      const gradient = (y - bounds.min.y) / span;
      const mottling = Math.sin(x * 37 + y * 19 + z * 29) * Math.cos(z * 41 - y * 23);
      color.copy(bottom).lerp(top, THREE.MathUtils.clamp(.22 + .65 * gradient + .12 * mottling, 0, 1));
      positions.push(x, y, z);
      colors.push(color.r, color.g, color.b);
    }
    if (geometry !== source) geometry.dispose();
    source.dispose();
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geometry.computeVertexNormals();
  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = name;
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function materials() {
  return {
    metal: new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: .88, metalness: .48 }),
    cloth: new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness: 1, metalness: 0, side: THREE.DoubleSide }),
  };
}

function glow(parent, name, position, scale, color) {
  // Each eye/core owns its material so AI tint changes stay independent.
  const mesh = new THREE.Mesh(pebble([0, 0, 0], scale, 1), new THREE.MeshBasicMaterial({
    color: new THREE.Color(...color), toneMapped: false,
  }));
  mesh.name = name;
  mesh.position.fromArray(position);
  mesh.castShadow = true;
  parent.add(mesh);
  return mesh;
}

function bearing(position, radius = .065) {
  return rod([position[0] - .035, position[1], position[2]],
    [position[0] + .035, position[1], position[2]], radius, radius, 8);
}

// A folded ribbon with two uneven torn tips; the folds are static geometry.
function rag(x, y, z, width, length, lean = 0) {
  const vertices = [], indices = [];
  for (let row = 0; row < 5; row++) {
    const t = row / 4;
    for (let col = 0; col < 3; col++) {
      const torn = row === 4 ? [0, .13, .045][col] * length : 0;
      vertices.push(x + (col / 2 - .5) * width * (1 - .28 * t) + lean * t,
        y - length * t + torn, z + Math.sin(row * 1.8 + col * 1.3) * .035);
    }
  }
  for (let row = 0; row < 4; row++) for (let col = 0; col < 2; col++) {
    const a = row * 3 + col;
    indices.push(a, a + 3, a + 1, a + 1, a + 3, a + 4);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  geometry.setIndex(indices);
  return geometry;
}

/** Scrap Hound: six blade-footed pipe limbs around a broken oil-drum engine. */
export function createCrawler() {
  const { metal, cloth } = materials();
  const root = pivot(null, 'scrap-hound');
  const body = pivot(root, 'body', [0, .46, 0]);
  const chassis = [
    [box([.34, .13, .91], [0, -.075, -.17]), 'oil'],
    [box([.38, .13, .29], [0, .12, -.39], [.05, 0, -.07]), 'rust'],
    [box([.29, .12, .22], [.035, .13, .10], [0, .08, .08]), 'iron'],
    [rod([0, -.02, -.80], [0, -.02, -.57], .22, .22, 8), 'rust'],
    [rod([-.13, .12, -.50], [-.13, .30, -.54], .045), 'oil'],
    [rod([.12, .13, -.43], [.15, .25, -.48], .035), 'iron'],
  ];
  // Interrupted barrel hoops leave wide sight lines onto the trapped ember.
  for (const z of [-.51, -.23, .05]) {
    for (const side of [-1, 1]) {
      const path = [[side * .12, -.16, z], [side * .24, -.07, z],
        [side * .25, .09, z], [side * .14, .21, z]];
      for (let i = 0; i < path.length - 1; i++) chassis.push([rod(path[i], path[i + 1], .027), 'rust']);
    }
  }
  for (const side of [-1, 1]) {
    chassis.push([box([.07, .14, .22], [side * .23, .03, -.42], [0, 0, side * .12]), 'iron']);
    chassis.push([rod([side * .19, -.06, -.57], [side * .19, .02, .29], .024), 'edge']);
  }
  scrap(body, 'split-drum-and-engine', chassis, metal);
  const core = glow(body, 'soul-core', [0, .055, -.15], [.12, .13, .26], [2.1, .075, .025]);

  const head = pivot(body, 'head', [0, .21, .50]);
  scrap(head, 'neck-and-cracked-mask', [
    [rod([0, -.06, -.23], [0, -.025, -.035], .065), 'iron'],
    [pebble([0, .04, .035], [.22, .18, .20]), 'oil'],
    [plate([[-.19, -.07], [-.20, .13], [-.11, .23], [.12, .21], [.205, .09], [.16, -.10], [.04, -.14], [-.10, -.12]], .055, .19), 'bone'],
    [rod([-.15, .165, .22], [-.065, .095, .221], .005), 'oil'],
    [rod([-.065, .095, .221], [-.10, .025, .221], .005), 'oil'],
    [rod([.16, -.085, .22], [.11, -.025, .221], .005), 'oil'],
    [rod([0, .035, .215], [0, .035, .24], .082, .075, 10), 'iron'],
    [box([.075, .04, .08], [-.16, -.10, .11]), 'rust'],
    [box([.075, .04, .08], [.16, -.10, .11]), 'rust'],
  ], metal);
  const eye = glow(head, 'single-lens', [0, .035, .255], [.052, .052, .027], [4, .72, .12]);
  const jaw = pivot(head, 'jaw', [0, -.105, .035]);
  const teeth = [
    [box([.28, .045, .23], [0, -.07, .12], [.06, 0, 0]), 'rust'],
    [rod([-.15, 0, 0], [-.14, -.07, .21], .025), 'iron'],
    [rod([.15, 0, 0], [.14, -.07, .21], .025), 'iron'],
  ];
  for (let i = 0; i < 7; i++) {
    const x = (i - 3) * .038;
    teeth.push([plate([[x - .017, -.05], [x + .016, -.05], [x + .008, .016 + (i % 2) * .013]], .035, .225), 'edge']);
  }
  scrap(jaw, 'hinged-saw-jaw', teeth, metal);
  scrap(body, 'snagged-shroud', [[rag(-.15, .13, -.60, .16, .28, -.03), 'cloth']], cloth);

  const legs = [];
  for (const side of [-1, 1]) for (let index = 0; index < 3; index++) {
    const spread = (index - 1) * .12;
    const hip = pivot(body, `hip-${side}-${index}`, [side * .22, .02, -.40 + index * .34]);
    const kneeOffset = [side * .25, -.15, spread];
    const knee = pivot(hip, `knee-${side}-${index}`, kneeOffset);
    scrap(hip, 'bent-upper-pipe', [
      [bearing([0, 0, 0], .06), 'iron'],
      [rod([0, 0, 0], [side * .16, .045, spread * .5], .034), 'rust'],
      [rod([side * .16, .045, spread * .5], kneeOffset, .028), 'iron'],
      [box([.095, .06, .09], [side * .17, .01, spread * .5], [0, 0, side * .5]), 'bone'],
    ], metal);
    scrap(knee, 'shin-and-blade-tip', [
      [bearing([0, 0, 0], .045), 'oil'],
      [rod([0, 0, 0], [side * .12, -.22, spread * .6], .023, .017), 'iron'],
      [plate([[side * .09, -.19], [side * .18, -.33], [side * .18, -.26], [side * .14, -.18]], .045, spread * .6), 'edge'],
    ], metal);
    legs.push({ hip, knee, side, index });
  }
  return { root, radius: .75, height: .9, parts: { body, head, jaw, eye, core, legs } };
}

/** Hollow Warden: an empty rebar reliquary balanced on reverse-hocked legs. */
export function createWarden() {
  const { metal, cloth } = materials();
  const root = pivot(null, 'hollow-warden');
  const pelvis = pivot(root, 'pelvis', [0, 1.05, 0]);
  scrap(pelvis, 'pelvic-yoke', [
    [rod([-.25, 0, 0], [.25, 0, 0], .075), 'iron'],
    [box([.28, .14, .21], [0, .04, -.025], [0, 0, .08]), 'rust'],
    [plate([[-.19, .08], [.17, .06], [.12, -.13], [-.10, -.16]], .035, .12), 'bone'],
  ], metal);
  const torso = pivot(pelvis, 'torso', [0, .25, 0]);
  const cage = [
    [rod([0, -.22, -.11], [0, .90, -.13], .043), 'iron'],
    [rod([-.09, -.18, -.06], [-.13, .83, -.10], .019), 'rust'],
    [rod([.09, -.18, -.06], [.13, .83, -.10], .019), 'rust'],
    [rod([-.43, .80, 0], [.43, .80, 0], .047), 'iron'],
    [box([.21, .09, .18], [-.34, .82, 0], [0, 0, -.18]), 'rust'],
    [box([.19, .08, .19], [.34, .81, 0], [0, .13, .13]), 'bone'],
  ];
  // Open-ended ribs curl forward without a solid chest shell or sternum.
  for (let row = 0; row < 5; row++) {
    const y = .22 + row * .125, width = .18 + Math.sin(row / 4 * Math.PI) * .09;
    for (const side of [-1, 1]) {
      const points = [[side * .04, y + .045, -.13], [side * width, y + .065, -.06],
        [side * (width + .025), y, .09], [side * .09, y - .045, .19]];
      for (let i = 0; i < 3; i++) cage.push([rod(points[i], points[i + 1], .018 + (row % 2) * .004), row % 2 ? 'rust' : 'iron']);
    }
    cage.push([box([.10, .045, .075], [0, y, -.135]), 'edge']);
  }
  scrap(torso, 'open-rebar-ribcage', cage, metal);
  const core = glow(torso, 'captive-soul', [0, .46, .025], [.115, .225, .10], [3.2, .13, .045]);
  scrap(torso, 'core-restraints', [
    [rod([-.10, .72, .04], [.075, .22, .10], .012), 'oil'],
    [rod([.10, .72, .04], [-.075, .22, .10], .012), 'oil'],
    [rod([0, .86, -.12], [0, 1.10, 0], .045), 'edge'],
  ], metal);
  const head = pivot(torso, 'head', [0, 1.12, .025]);
  scrap(head, 'fractured-welding-mask', [
    [pebble([0, .06, -.015], [.175, .22, .14]), 'oil'],
    [plate([[-.17, .19], [-.09, .28], [.14, .25], [.20, .12], [.13, -.15], [.04, -.22], [-.12, -.16], [-.19, .04]], .045, .12), 'bone'],
    [plate([[-.18, .18], [-.09, .265], [.15, .24], [.19, .17], [.06, .195]], .055, .15), 'rust'],
    [rod([.11, .235, .146], [.045, .14, .146], .005), 'oil'],
    [rod([.045, .14, .146], [.075, .075, .146], .005), 'oil'],
    [rod([-.12, -.15, .146], [-.055, -.08, .146], .005), 'oil'],
    [rod([-.055, -.08, .146], [-.075, -.025, .146], .005), 'oil'],
    [rod([-.025, .04, .14], [-.025, .04, .175], .073, .065, 8), 'iron'],
    [box([.08, .12, .055], [.105, -.11, .13], [0, 0, -.15]), 'iron'],
  ], metal);
  const eye = glow(head, 'single-lens', [-.025, .04, .19], [.048, .043, .024], [4.3, .65, .10]);

  function arm(side, label) {
    const shoulder = pivot(torso, `${label}-shoulder`, [side * .41, .80, 0]);
    const elbow = pivot(shoulder, `${label}-elbow`, [side * .12, -.43, .015]);
    scrap(shoulder, `${label}-upper-arm`, [
      [bearing([0, 0, 0], .085), 'oil'],
      [rod([0, -.025, 0], [side * .12, -.43, .015], .029), 'iron'],
      [rod([side * .065, -.07, -.04], [side * .17, -.38, -.025], .019), 'rust'],
      [box([.10, .15, .10], [side * .04, -.12, 0], [0, 0, side * .20]), 'bone'],
    ], metal);
    scrap(elbow, `${label}-forearm`, [
      [bearing([0, 0, 0], .06), 'rust'],
      [rod([0, 0, 0], [side * .08, -.35, .045], .032, .024), 'iron'],
      [rod([-side * .045, -.055, .045], [side * .025, -.25, .08], .014), 'edge'],
    ], metal);
    const blade = pivot(elbow, `${label}-blade`, [side * .08, -.35, .045]);
    const outline = [[-.045, .035], [.075, -.02], [.10, -.36], [.28, -.81], [.30, -1.08],
      [.105, -.84], [-.065, -.40], [-.08, -.08]].map(([x, y]) => [x * side, y]);
    const edge = [[.10, -.36], [.28, -.81], [.30, -1.08], [.23, -.79], [.055, -.37]]
      .map(([x, y]) => [x * side, y]);
    scrap(blade, `${label}-welded-scythe`, [
      [plate(outline, .035), 'rust'], [plate(edge, .039, .002), 'edge'],
      [box([.15, .07, .075], [side * .015, -.12, 0], [0, 0, side * .12]), 'iron'],
      [box([.12, .035, .055], [side * .025, -.33, 0], [0, 0, side * .20]), 'bone'],
    ], metal);
    return { shoulder, elbow, blade };
  }

  function leg(side, label) {
    const hip = pivot(pelvis, `${label}-hip`, [side * .20, 0, 0]);
    const knee = pivot(hip, `${label}-knee`, [side * .045, -.44, .14]);
    const ankle = pivot(knee, `${label}-ankle`, [side * .025, -.37, -.31]);
    scrap(hip, `${label}-thigh`, [
      [bearing([0, 0, 0], .065), 'oil'],
      [rod([0, 0, 0], [side * .045, -.44, .14], .035), 'iron'],
      [rod([side * .055, -.065, -.04], [side * .085, -.32, .065], .037, .029), 'rust'],
      [rod([side * .085, -.32, .065], [side * .09, -.43, .12], .016), 'edge'],
    ], metal);
    scrap(knee, `${label}-reverse-shin`, [
      [bearing([0, 0, 0], .07), 'rust'],
      [plate([[-.065, .06], [.07, .04], [.055, -.09], [-.04, -.11]], .05, .055), 'bone'],
      [rod([0, 0, 0], [side * .025, -.37, -.31], .024), 'iron'],
      [rod([-side * .045, -.035, -.01], [-side * .025, -.22, -.17], .031), 'oil'],
      [rod([-side * .025, -.22, -.17], [-side * .02, -.37, -.30], .013), 'edge'],
    ], metal);
    const foot = [
      [bearing([0, 0, 0], .047), 'iron'],
      [rod([0, 0, 0], [0, -.19, .10], .029), 'rust'],
    ];
    for (const toe of [-1, 1]) {
      foot.push([rod([0, -.18, .08], [toe * .075, -.215, .27], .023, .012), 'iron']);
      const claw = plate([[-.03, -.18], [.035, -.24], [.05, -.20], [.015, -.15]], .13);
      claw.rotateY(Math.PI / 2).translate(toe * .075, 0, .27);
      foot.push([claw, 'edge']);
    }
    scrap(ankle, `${label}-talon-foot`, foot, metal);
    return { hip, knee, ankle };
  }

  const armL = arm(-1, 'left'), armR = arm(1, 'right');
  const legL = leg(-1, 'left'), legR = leg(1, 'right');
  scrap(torso, 'shoulder-shroud', [
    [rag(-.35, .82, -.09, .20, .64, -.10), 'cloth'],
    [rag(.35, .82, -.105, .18, .48, .08), 'cloth'],
    [rag(-.08, .77, -.18, .18, .74, .035), 'cloth'],
  ], cloth);
  scrap(pelvis, 'hip-rags', [
    [rag(-.19, .07, .075, .15, .44, -.055), 'cloth'],
    [rag(.20, .055, -.03, .13, .55, .06), 'cloth'],
    [rag(0, .02, -.15, .18, .38, -.04), 'cloth'],
  ], cloth);
  return {
    root, radius: .95, height: 2.7,
    parts: { pelvis, torso, head, eye, core, armL, armR, legL, legR },
  };
}

/** Warden of Ash: a hateful soul wearing the carcass of a scrap-machine beast. */
export function createAshWarden() {
  const { metal, cloth } = materials();
  const root = pivot(null, 'ash-warden');

  function ring(position, major, tube, rotation = [Math.PI / 2, 0, 0], segments = 10) {
    const geometry = new THREE.TorusGeometry(major, tube, 5, segments);
    geometry.applyMatrix4(new THREE.Matrix4().makeRotationFromEuler(new THREE.Euler(...rotation)));
    return geometry.translate(...position);
  }

  function addJoint(parent, name, radius) {
    scrap(parent, name, [
      [bearing([0, 0, 0], radius), 'oil'],
      [ring([0, 0, 0], radius * .82, radius * .18, [0, Math.PI / 2, 0], 8), 'rust'],
    ], metal);
  }

  function topPlate(points, thickness, y) {
    return plate(points, thickness).rotateX(Math.PI / 2).translate(0, y, 0);
  }

  function clawBlade(x, z, length, width, yaw = 0) {
    const geometry = plate([
      [-width * .50, 0], [width * .50, 0], [width * .42, length * .42],
      [width * .18, length * .82], [0, length], [-width * .22, length * .72],
    ], .10);
    return geometry.rotateX(Math.PI / 2).rotateY(yaw).translate(x, -.10, z);
  }

  function soulPair(parent, name, left, right, scale, color) {
    const positions = [];
    for (const point of [left, right]) {
      const original = pebble(point, scale, 1);
      const source = original.index ? original.toNonIndexed() : original;
      positions.push(...source.attributes.position.array);
      if (source !== original) source.dispose();
      original.dispose();
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.computeVertexNormals();
    const mesh = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial({
      color: new THREE.Color(...color), toneMapped: false,
    }));
    mesh.name = name;
    mesh.castShadow = true;
    parent.add(mesh);
    return mesh;
  }

  // A long, low spine. Every transform lives in geometry or position so Unity
  // can layer procedural rotations over identity rest rotations and scales.
  const pelvis = pivot(root, 'pelvis', [0, 1.08, -1.65]);
  scrap(pelvis, 'pelvis-scrap-cradle', [
    [box([1.62, .62, 1.42], [0, .06, -.08], [.02, -.04, -.02]), 'oil'],
    [box([1.36, .42, 1.18], [-.08, .35, -.02], [-.04, .08, .04]), 'rust'],
    [topPlate([[-.78, -.68], [-.58, .63], [-.20, .79], [.45, .72], [.78, .38], [.70, -.60]], .11, .64), 'iron'],
    [topPlate([[-.66, -.64], [-.18, -.77], [.08, -.55], [-.02, .18], [-.42, .43], [-.72, .15]], .08, .70), 'bone'],
    [rod([-.73, .42, -.52], [.70, .40, -.47], .105), 'edge'],
    [rod([-.66, -.20, .50], [.65, -.18, .44], .09), 'rust'],
    [ring([.61, .42, -.28], .31, .095, [0, Math.PI / 2, 0], 10), 'oil'],
    [box([.30, .22, .38], [.56, .63, .20], [.08, -.12, .13]), 'rust'],
  ], metal);
  scrap(pelvis, 'pelvis-caught-rags', [
    [rag(-.43, .25, -.60, .31, .60, -.08), 'ashCloth'],
    [rag(.35, .21, -.55, .24, .45, .06), 'ash'],
  ], cloth);

  const s0 = pivot(pelvis, 's0', [0, .08, .62]);
  const s1 = pivot(s0, 's1', [0, .06, .72]);
  const s2 = pivot(s1, 's2', [0, .06, .72]);
  const spine = [s0, s1, s2];
  for (let i = 0; i < spine.length; i++) {
    const section = spine[i];
    const width = 1.72 + i * .10;
    scrap(section, `spine-${i}-vertebra`, [
      [box([width, .62, 1.00], [0, .05, .34], [.02, i % 2 ? .025 : -.025, 0]), 'oil'],
      [rod([0, -.05, -.22], [0, .03, .90], .18, .16, 8), i === 1 ? 'rust' : 'iron'],
      [topPlate([[-width * .48, -.14], [-width * .36, .78], [-.08, .91], [.12, .72], [.46 * width, .66], [.49 * width, -.18]], .09, .41), i === 1 ? 'rust' : 'iron'],
      [topPlate([[-width * .42, -.16], [-width * .48, .44], [-width * .18, .64], [-.05, .38], [-.12, -.13]], .07, .48), 'bone'],
      [rod([-.69 - i * .03, .12, -.14], [-.75 - i * .03, .10, .82], .065), 'rust'],
      [rod([.69 + i * .03, .10, -.14], [.75 + i * .03, .12, .82], .065), 'edge'],
      [box([.30, .18, .36], [i % 2 ? -.56 : .54, .39, .22], [.05, .15, -.08]), 'rust'],
    ], metal);
  }

  const torso = pivot(s2, 'torso', [0, .06, .45]);
  const ribs = [
    [box([1.38, .70, 1.12], [0, .02, .05], [.02, 0, -.02]), 'oil'],
    [rod([0, -.18, -.46], [0, .17, .72], .15), 'iron'],
    [box([.72, .45, .72], [.22, .32, -.05], [-.08, .12, .06]), 'rust'],
  ];
  for (let row = 0; row < 5; row++) {
    const z = -.38 + row * .25;
    for (const side of [-1, 1]) {
      const reach = .88 - Math.abs(row - 2) * .035;
      ribs.push([rod([side * .12, .23, z], [side * reach, .34, z + .01], .075), row % 2 ? 'rust' : 'iron']);
      ribs.push([rod([side * reach, .34, z + .01], [side * .74, -.26, z + .10], .070), 'edge']);
      ribs.push([box([.23, .18, .31], [side * .76, .18, z + .04], [0, side * .08, side * .18]), row === 2 ? 'bone' : 'rust']);
    }
  }
  scrap(torso, 'open-engine-ribcage', ribs, metal);
  const machineJunk = [
    [box([1.18, .40, .72], [0, .54, -.05], [.03, -.04, .01]), 'iron'],
    [box([.48, .34, .48], [-.46, .56, -.06], [.08, -.12, -.04]), 'rust'],
    [ring([.68, .42, .06], .27, .08, [0, Math.PI / 2, 0], 10), 'iron'],
    [ring([-.51, .47, .28], .21, .065, [0, 0, 0], 9), 'rust'],
    [rod([-.70, .15, -.30], [-.62, .66, -.24], .070, .050), 'oil'],
    [rod([.64, .15, -.28], [.54, .67, -.18], .060, .042), 'rust'],
    [topPlate([[-.78, -.46], [-.52, .57], [-.12, .69], [.13, .54], [.75, .48], [.84, -.36]], .08, .82), 'bone'],
  ];
  for (let i = 0; i < 8; i++) {
    const angle = i / 8 * Math.PI * 2;
    machineJunk.push([box([.10, .08, .08], [-.29 + Math.cos(angle) * .23,
      .47 + Math.sin(angle) * .23, .24], [0, 0, angle]), i % 3 ? 'rust' : 'edge']);
  }
  for (let i = 0; i < 3; i++) {
    const x = (i - 1) * .36;
    machineJunk.push([rod([x, .69, -.27], [x + (i - 1) * .05, 1.38 + (i === 1 ? .10 : 0), -.30], .12, .095, 8), i === 1 ? 'iron' : 'rust']);
    machineJunk.push([ring([x + (i - 1) * .05, 1.38 + (i === 1 ? .10 : 0), -.30], .13, .035, [0, 0, 0], 8), 'oil']);
  }
  scrap(torso, 'engine-junk', machineJunk, metal);

  const chest = pivot(torso, 'chest', [0, .12, .55]);
  scrap(chest, 'cracked-furnace-chest', [
    [box([1.62, .76, .94], [0, .00, .10], [.03, 0, .01]), 'oil'],
    [plate([[-.80, .40], [-.52, .58], [-.13, .54], [-.05, .18], [-.18, -.34], [-.70, -.28]], .13, .55), 'rust'],
    [plate([[-.02, .54], [.62, .47], [.82, .12], [.63, -.35], [.17, -.31], [.05, .15]], .13, .56), 'iron'],
    [topPlate([[-.78, -.34], [-.62, .43], [-.18, .61], [.03, .43], [.66, .50], [.82, -.24]], .10, .48), 'bone'],
    [rod([-.76, -.16, -.31], [.75, -.12, -.27], .085), 'edge'],
    [rod([-.69, .45, -.23], [.65, .48, -.18], .065), 'rust'],
    [ring([.64, .13, .44], .23, .060, [0, Math.PI / 2, 0], 10), 'iron'],
  ], metal);
  const core = glow(chest, 'core', [0, .03, .59], [.36, .38, .16], [5.4, .24, .035]);
  scrap(chest, 'core-cage-and-seams', [
    [rod([-.34, .45, .63], [.26, -.34, .64], .035), 'oil'],
    [rod([.35, .42, .63], [-.25, -.31, .64], .035), 'oil'],
    [ring([0, .03, .62], .43, .055, [0, 0, 0], 10), 'rust'],
    [plate([[-.15, .40], [-.06, .45], [.02, .19], [-.03, .02]], .016, .672), 'rust'],
    [plate([[.06, -.02], [.14, .02], [.06, -.27], [.01, -.35]], .016, .674), 'rust'],
  ], metal);

  const neck = pivot(chest, 'neck', [0, .10, .54]);
  scrap(neck, 'cable-neck', [
    [box([1.22, .48, .78], [0, .01, .16], [.02, 0, 0]), 'oil'],
    [rod([-.36, -.17, -.18], [-.29, .09, .49], .11, .075), 'iron'],
    [rod([.36, -.17, -.18], [.29, .09, .49], .11, .075), 'rust'],
    [rod([-.08, -.12, -.17], [.07, .23, .51], .065, .045), 'oil'],
    [topPlate([[-.58, -.18], [-.42, .48], [-.10, .61], [.12, .48], [.55, .42], [.61, -.14]], .09, .35), 'bone'],
  ], metal);

  const head = pivot(neck, 'head', [0, .05, .45]);
  scrap(head, 'welded-jackal-skull', [
    [box([1.18, .65, .78], [0, .03, .25], [.02, 0, 0]), 'oil'],
    [topPlate([[-.57, -.10], [-.48, .55], [-.23, .76], [.05, .62], [.52, .58], [.62, .14], [.42, -.12]], .11, .40), 'bone'],
    [plate([[-.58, .31], [-.28, .53], [.17, .48], [.57, .25], [.52, -.18], [.10, -.27], [-.48, -.20]], .11, .67), 'bone'],
    [plate([[-.54, .25], [-.24, .46], [-.04, .15], [-.15, -.19], [-.49, -.17]], .07, .74), 'rust'],
    [box([.86, .40, .93], [0, -.08, .92], [-.02, 0, 0]), 'rust'],
    [topPlate([[-.39, -.22], [-.43, .66], [-.18, 1.28], [0, 1.55], [.28, 1.25], [.42, .54], [.34, -.18]], .09, .16), 'bone'],
    [box([.55, .31, .48], [0, -.12, 1.47], [.02, 0, 0]), 'edge'],
    [rod([-.43, .15, .48], [-.30, -.02, 1.64], .060, .032), 'iron'],
    [rod([.43, .15, .48], [.30, -.02, 1.64], .060, .032), 'iron'],
    [topPlate([[-.51, .21], [-.76, -.43], [-.62, -.84], [-.35, -.35], [-.19, .17]], .10, .49).rotateY(-.18), 'bone'],
    [topPlate([[.17, .18], [.35, -.36], [.66, -.82], [.78, -.38], [.51, .23]], .10, .47).rotateY(.18), 'bone'],
    [box([.17, .12, .18], [-.48, .23, .70], [0, 0, -.12]), 'rust'],
    [box([.17, .12, .18], [.48, .23, .70], [0, 0, .12]), 'rust'],
  ], metal);
  const eye = soulPair(head, 'eye', [-.31, .10, .73], [.31, .10, .73], [.105, .080, .050], [4.8, .45, .08]);

  const jaw = pivot(head, 'jaw', [0, -.20, .45]);
  const jawPieces = [
    [box([.82, .22, 1.28], [0, -.11, .64], [.06, 0, 0]), 'oil'],
    [rod([-.39, .06, 0], [-.31, -.08, 1.28], .070, .035), 'rust'],
    [rod([.39, .06, 0], [.31, -.08, 1.28], .070, .035), 'iron'],
    [topPlate([[-.40, .02], [-.31, 1.26], [0, 1.43], [.31, 1.26], [.40, .02]], .08, -.02), 'rust'],
  ];
  for (let i = 0; i < 12; i++) {
    const side = i % 2 ? 1 : -1;
    const x = side * (.24 + (i % 4) * .025);
    const z = .15 + Math.floor(i / 2) * .19;
    const tooth = plate([[-.065, .02], [.065, .02], [.025, .24 + (i % 3) * .04]], .08, z);
    tooth.translate(x, -.09, 0);
    jawPieces.push([tooth, 'bone']);
  }
  scrap(jaw, 'hinged-scrap-jaw', jawPieces, metal);

  // Four vertebra pivots let the tail curl tightly around the body for the roll.
  const t0 = pivot(pelvis, 't0', [0, .05, -.75]);
  const t1 = pivot(t0, 't1', [.08, -.02, -.76]);
  const t2 = pivot(t1, 't2', [-.10, -.04, -.72]);
  const t3 = pivot(t2, 't3', [.08, -.06, -.67]);
  const tail = [t0, t1, t2, t3];
  const tailLengths = [.78, .75, .70, .82];
  for (let i = 0; i < tail.length; i++) {
    const radius = .27 - i * .035;
    const links = [];
    for (let link = 0; link < 3; link++) {
      const z = -.10 - link * tailLengths[i] * .28;
      const rotation = link % 2 ? [Math.PI / 2, Math.PI / 2, 0] : [Math.PI / 2, 0, 0];
      links.push([ring([0, .01, z], radius, radius * .24, rotation, 9), link % 2 ? 'iron' : 'rust']);
    }
    links.push([box([radius * 1.65, radius * .60, .19], [0, .04, -.26], [0, 0, i % 2 ? .20 : -.16]), 'edge']);
    links.push([rod([-.11, .05, -.08], [.10, .04, -tailLengths[i] * .88], .030), 'rust']);
    scrap(tail[i], `tail-${i}-heavy-chain`, links, metal);
  }

  function makeLeg(parent, side, index, hipPosition, kneePosition, anklePosition) {
    const hip = pivot(parent, `hip${index}`, hipPosition);
    const knee = pivot(hip, `knee${index}`, kneePosition);
    const ankle = pivot(knee, `ankle${index}`, anklePosition);
    const claw = pivot(ankle, `claw${index}`, [0, -.09, .08]);
    addJoint(hip, `hip-${index}-bearing`, .25);
    addJoint(knee, `knee-${index}-bearing`, .22);
    addJoint(ankle, `ankle-${index}-bearing`, .17);
    scrap(hip, `leg-${index}-splayed-thigh`, [
      [rod([0, 0, 0], [kneePosition[0], kneePosition[1], kneePosition[2]], .24, .20, 8), 'iron'],
      [rod([0, .13, -.08], [kneePosition[0] * .92, kneePosition[1] + .13, kneePosition[2] - .08], .085, .065), 'rust'],
      [box([.58, .34, .70], [kneePosition[0] * .46, kneePosition[1] * .46, kneePosition[2] * .46], [.10, side * .20, side * .18]), index === 2 ? 'rust' : 'edge'],
      [box([.46, .24, .50], [kneePosition[0] * .72, kneePosition[1] * .72 + .08, kneePosition[2] * .72], [-.08, side * .13, -side * .15]), 'bone'],
      [rod([-side * .17, -.02, -.06], [kneePosition[0] - side * .16, kneePosition[1] + .06, kneePosition[2] - .04], .060, .045), 'oil'],
    ], metal);
    scrap(knee, `leg-${index}-piston-shin`, [
      [rod([0, 0, 0], [anklePosition[0], anklePosition[1], anklePosition[2]], .20, .15, 8), 'rust'],
      [rod([-side * .15, -.04, .01], [anklePosition[0] - side * .15, anklePosition[1] + .07, anklePosition[2]], .075, .050), 'oil'],
      [box([.52, .30, .66], [anklePosition[0] * .48, anklePosition[1] * .46, anklePosition[2] * .48], [.14, -side * .18, side * .08]), 'iron'],
      [plate([[-.27, .16], [.24, .12], [.29, -.34], [.05, -.62], [-.22, -.44]], .17, .05), 'bone'],
    ], metal);
    scrap(ankle, `leg-${index}-foot-yoke`, [
      [box([.70, .30, .62], [0, -.09, .18], [.04, 0, side * .08]), 'oil'],
      [topPlate([[-.35, -.10], [-.30, .49], [0, .64], [.34, .47], [.37, -.08]], .09, .10), 'bone'],
      [rod([-.31, -.04, .12], [.31, -.04, .12], .080), 'rust'],
    ], metal);
    const clawPieces = [];
    for (const toe of [-1, 0, 1]) {
      const x = toe * .22;
      clawPieces.push([rod([toe * .15, .03, 0], [x, -.06, .40], .070, .040), 'iron']);
      clawPieces.push([clawBlade(x, .34, toe === 0 ? .88 : .76, .22, -toe * .16), 'bone']);
    }
    scrap(claw, `leg-${index}-blade-claws`, clawPieces, metal);
    return { hip, knee, ankle, claw, side, index };
  }

  const legs = [
    makeLeg(chest, -1, 0, [-.62, -.05, .05], [-.85, -.62, .12], [.12, -.62, .42]),
    makeLeg(chest, 1, 1, [.62, -.05, .05], [.85, -.62, .12], [-.12, -.62, .42]),
    makeLeg(pelvis, -1, 2, [-.62, -.02, .10], [-.82, -.48, .18], [.10, -.40, .35]),
  ];

  // The rear-right leg is grotesquely hypertrophied and hooked above the body.
  // Its animation pivots remain the throwing-arm contract, but the visible shin,
  // ankle and foot hang below the high knee instead of forming a smooth tail arc.
  const shoulder = pivot(pelvis, 'shoulder', [.58, .30, -.20]);
  const upper = pivot(shoulder, 'upper');
  const elbow = pivot(upper, 'elbow', [.82, 2.70, .58]);
  const fore = pivot(elbow, 'fore');
  const wrist = pivot(fore, 'wrist', [-.60, 2.05, 1.62]);
  addJoint(shoulder, 'arm-hip-drum', .42);
  addJoint(elbow, 'arm-wrong-way-knee-drum', .38);
  scrap(upper, 'twisted-overgrown-thigh', [
    [rod([0, 0, 0], [.82, 2.70, .58], .40, .32, 9), 'iron'],
    [rod([-.24, .03, -.16], [.55, 2.55, .37], .12, .085), 'oil'],
    [rod([.24, .00, .14], [1.04, 2.46, .75], .11, .070), 'rust'],
    [box([.82, .38, .84], [.24, .66, .12], [.35, -.10, -.24]), 'rust'],
    [box([.92, .42, .78], [.52, 1.34, .30], [-.30, .14, .22]), 'bone'],
    [box([.80, .36, .88], [.73, 2.03, .45], [.42, -.12, -.18]), 'edge'],
    [ring([.30, .87, .17], .43, .10, [0, Math.PI / 2, .32], 10), 'rust'],
    [ring([.50, 1.45, .31], .41, .095, [0, Math.PI / 2, -.38], 10), 'oil'],
    [ring([.68, 2.05, .44], .37, .090, [0, Math.PI / 2, .46], 10), 'rust'],
    [topPlate([[-.39, .34], [-.33, 1.16], [-.12, 1.52], [.22, 1.30], [.35, .42], [.16, .03]], .12, .46).rotateZ(-.30), 'bone'],
  ], metal);
  scrap(upper, 'torn-thigh-cables', [
    [rag(.12, 1.72, -.24, .38, .88, .13), 'ashCloth'],
  ], cloth);
  scrap(fore, 'hyperextended-hanging-shin', [
    [rod([0, 0, 0], [-.38, -1.22, .66], .32, .26, 9), 'rust'],
    [rod([.23, -.05, -.08], [-.14, -1.22, .55], .11, .075), 'oil'],
    [rod([-.24, .02, .12], [-.66, -1.08, .78], .10, .060), 'iron'],
    [box([.78, .40, .88], [-.17, -.48, .27], [-.32, .18, .14]), 'iron'],
    [box([.72, .36, .76], [-.34, -.98, .57], [.28, -.16, -.20]), 'bone'],
    [ring([-.39, -1.20, .66], .31, .085, [0, Math.PI / 2, -.30], 9), 'oil'],
    [rod([-.18, -.35, .10], [-.60, 2.05, 1.62], .070, .045), 'rust'],
    [rod([.12, -.18, -.04], [-.46, 1.82, 1.43], .045, .025), 'oil'],
    [plate([[-.39, -.10], [.05, -.14], [-.04, -.78], [-.31, -1.35], [-.62, -1.14], [-.54, -.39]], .18, .25), 'rust'],
  ], metal);
  scrap(wrist, 'mangled-ankle-and-foot', [
    [ring([0, -3.24, -1.50], .34, .10, [0, Math.PI / 2, 0], 10), 'oil'],
    [box([.96, .42, .82], [0, -3.31, -1.34], [.06, 0, -.04]), 'rust'],
    [topPlate([[-.46, -.20], [-.40, .60], [-.08, .82], [.42, .55], [.48, -.16]], .12, -3.05).translate(0, 0, -1.56), 'bone'],
    [rod([-.42, -3.21, -1.48], [.42, -3.21, -1.48], .095), 'edge'],
    [rod([-.30, -3.30, -1.22], [-.52, -2.65, -1.05], .055, .025), 'oil'],
    [rod([.25, -3.34, -1.18], [.48, -2.82, -.96], .050, .020), 'rust'],
  ], metal);

  const fingers = [];
  for (let i = 0; i < 3; i++) {
    const x = (i - 1) * .31;
    const finger = pivot(wrist, `finger${i}`, [x, -.22, .30 + (i === 1 ? .12 : 0)]);
    const spread = (i - 1) * .18;
    scrap(finger, `overgrown-foot-toe-${i}`, [
      [rod([0, -3.02, -1.58], [spread, -3.48, -1.02], .13, .085, 8), i === 1 ? 'iron' : 'rust'],
      [rod([spread, -3.48, -1.02], [spread * 1.35, -3.68, -.48], .10, .045, 8), 'edge'],
      [plate([[-.14, -3.36], [.15, -3.42], [.17, -4.13], [.03, -4.34], [-.13, -4.02]], .16, -.57 + spread), 'bone'],
    ], metal);
    fingers.push(finger);
  }
  const grip = pivot(wrist, 'grip', [0, -.72, .20]);
  const arm = { shoulder, upper, elbow, fore, wrist, fingers, grip };

  return {
    root, radius: 2.4, height: 3.2,
    parts: { pelvis, torso, chest, neck, head, jaw, eye, core, spine, tail, legs, arm },
  };
}

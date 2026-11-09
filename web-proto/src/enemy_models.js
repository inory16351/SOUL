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

// Z0 field roster. Rest joints deliberately match crawler / warden exactly;
// all asymmetry and lean is baked into scrap geometry, never joint rotations.
function fieldHoop(position, radius, tube, arc = Math.PI * 2, yaw = 0) {
  return new THREE.TorusGeometry(radius, tube, 4, 8, arc)
    .rotateY(yaw).translate(...position);
}

function fieldCrawlerLegs(body, metal, heavy) {
  const legs = [];
  for (const side of [-1, 1]) for (let index = 0; index < 3; index++) {
    const spread = (index - 1) * .12;
    const hip = pivot(body, `hip-${side}-${index}`, [side * .22, .02, -.40 + index * .34]);
    const kneeOffset = [side * .25, -.15, spread];
    const knee = pivot(hip, `knee-${side}-${index}`, kneeOffset);
    const upper = [
      [bearing([0, 0, 0], heavy ? .072 : .043), 'oil'],
      [rod([0, 0, 0], [side * .17, .065, spread * .5], heavy ? .048 : .018, heavy ? .04 : .013, 5), 'rust'],
      [rod([side * .17, .065, spread * .5], kneeOffset, heavy ? .035 : .013, heavy ? .03 : .010, 5), 'iron'],
    ];
    const lower = [
      [bearing([0, 0, 0], heavy ? .052 : .025), 'iron'],
      [rod([0, 0, 0], [side * .055, -.23, -.015], heavy ? .029 : .012, .008, 5), 'edge'],
      [rod([side * .055, -.23, -.015], [side * .055, -.33, .005], heavy ? .019 : .009, .002, 4), 'iron'],
    ];
    if (heavy) {
      upper.push([plate([[0, .035], [side * .15, .10], [side * .24, -.08], [side * .16, -.13]], .08, spread * .4), 'iron']);
      upper.push([rod([side * .025, -.03, -.06], [side * .21, -.12, spread - .04], .014, .014, 4), 'edge']);
      lower.push([plate([[-.045, .01], [.04, -.02], [.05, -.16], [0, -.23], [-.04, -.13]], .055, .025), 'rust']);
    }
    scrap(hip, heavy ? 'armoured-hock' : 'wire-upper-leg', upper, metal);
    scrap(knee, heavy ? 'staking-claw' : 'needle-foot', lower, metal);
    legs.push({ hip, knee, side, index });
  }
  return legs;
}

/** Ash mite: a squat dented pressure bulb with six fine needles and feeding pincers. */
export function createAshMite() {
  const { metal } = materials();
  const root = pivot(null, 'ash-mite');
  const body = pivot(root, 'body', [0, .46, 0]);
  const shell = [
    [pebble([0, .055, -.12], [.34, .32, .43], 1), 'ash'],
    [pebble([-.08, .13, -.11], [.29, .23, .33]), 'rust'],
    [rod([0, .29, -.17], [0, .34, -.17], .14, .12, 8), 'iron'],
    [box([.11, .025, .045], [.025, .355, -.17], [0, .3, 0]), 'edge'],
    [box([.16, .07, .15], [.20, .16, -.19], [.12, .15, -.2]), 'oil'],
  ];
  // Crimped seams interrupt the bulb silhouette; no canine spine or muzzle.
  for (const z of [-.32, -.05]) {
    shell.push([fieldHoop([0, .04, z], .29, .018, Math.PI * 1.65), 'iron']);
  }
  for (let i = 0; i < 3; i++) {
    shell.push([rod([-.16 + i * .10, .29, -.29], [-.14 + i * .10, .32, -.03], .008, .005, 4), 'oil']);
  }
  scrap(body, 'dented-can-abdomen', shell, metal);
  const core = glow(body, 'soul-core', [.22, .18, -.15], [.04, .065, .06], [.9, .65, .09]);
  const head = pivot(body, 'head', [0, .21, .50]);
  scrap(head, 'feeding-sensor', [
    [rod([0, -.08, -.25], [0, -.06, -.02], .105, .075, 6), 'oil'],
    [pebble([0, -.025, -.02], [.135, .10, .115]), 'iron'],
    [fieldHoop([0, .035, .055], .066, .019), 'rust'],
    [rod([-.08, .02, 0], [-.18, .09, .10], .011, .005, 4), 'edge'],
    [rod([.08, .02, 0], [.17, .06, .12], .011, .005, 4), 'edge'],
  ], metal);
  const eye = glow(head, 'single-lens', [0, .035, .067], [.049, .049, .026], [1.9, 1.55, .12]);
  const jaw = pivot(head, 'jaw', [0, -.105, .035]);
  const mouth = [[rod([0, 0, -.02], [0, -.04, .175], .023, .003, 5), 'edge']];
  for (const s of [-1, 1]) {
    mouth.push([rod([s * .075, .005, -.035], [s * .11, -.035, .095], .022, .014, 5), 'rust']);
    mouth.push([rod([s * .11, -.035, .095], [s * .035, -.045, .17], .014, .002, 4), 'edge']);
  }
  scrap(jaw, 'pincers-and-proboscis', mouth, metal);
  const legs = fieldCrawlerLegs(body, metal, false);
  return { root, radius: .75, height: .9, parts: { body, head, jaw, eye, core, legs } };
}

/** Pack Alpha: a big scrap-built dog, kin to the Z0 boss (welded plates, engine block, exhaust spine). Four legs
 *  in trot order (front-left, front-right, rear-right, rear-left) so the hound rig's alternating phase gives a
 *  diagonal trot; Unity flags it `quad` (no insect splay). Built at the crawler's scale (Unity size 2.6). */
export function createHoundAlpha() {
  const { metal } = materials();
  const root = pivot(null, 'hound-alpha');
  const body = pivot(root, 'body', [0, .5, 0]);

  // Geometry-only helpers: the rig stays identity-rotated at the x5 Unity scale.
  function alphaPath(pieces, points, r0, r1, paint = 'oil', sides = 5) {
    for (let i = 0; i < points.length - 1; i++) {
      const a = i / (points.length - 1), b = (i + 1) / (points.length - 1);
      pieces.push([rod(points[i], points[i + 1], r0 + (r1 - r0) * a,
        r0 + (r1 - r0) * b, sides), paint]);
    }
  }
  function alphaPlate(points, depth, position, rotation = [0, 0, 0]) {
    return plate(points, depth).rotateX(rotation[0]).rotateY(rotation[1])
      .rotateZ(rotation[2]).translate(...position);
  }
  function alphaScale(position, width, length, rotation = [0, 0, 0]) {
    return alphaPlate([[-.48 * width, -.45 * length], [.30 * width, -.51 * length],
      [.51 * width, -.19 * length], [.43 * width, .22 * length],
      [.14 * width, .49 * length], [-.08 * width, .36 * length],
      [-.37 * width, .44 * length], [-.52 * width, .06 * length]], .012, position,
    [Math.PI / 2 + rotation[0], rotation[1], rotation[2]]);
  }
  function alphaLink(position, radius, yaw = 0) {
    return new THREE.TorusGeometry(radius, radius * .22, 3, 6, Math.PI * 1.87).rotateY(yaw)
      .scale(.76, 1.18, 1).translate(...position);
  }

  const chassis = [
    [pebble([0, .07, .18], [.19, .22, .29]), 'oil'],
    [pebble([0, .06, -.12], [.105, .12, .25]), 'oil'],
    [pebble([0, .07, -.38], [.18, .16, .19]), 'iron'],
    [pebble([-.025, .245, .22], [.20, .095, .23]), 'iron'],
  ];
  // The back rises into the withers; each vertebra changes pitch along the arc.
  const spine = [[0, .17, -.53], [.012, .19, -.39], [-.01, .18, -.24],
    [0, .22, -.08], [-.012, .29, .08], [0, .32, .23], [0, .26, .38]];
  alphaPath(chassis, spine, .038, .053, 'iron', 6);
  for (let i = 0; i < spine.length - 1; i++) {
    const p = spine[i];
    chassis.push([alphaScale([p[0], p[1] + .025, p[2]], .13 + i * .012, .18,
      [-.12 + i * .045, (i % 2 ? 1 : -1) * .16, .06]), i % 3 === 0 ? 'rust' : 'iron']);
  }
  // Open, bowed pipe ribs around a small engine: gaps expose the captive ember.
  for (let row = 0; row < 7; row++) {
    const z = -.20 + row * .085, w = .145 + Math.sin(row / 6 * Math.PI) * .10;
    const top = .17 + Math.sin(row / 6 * Math.PI) * .12;
    for (const s of [-1, 1]) {
      alphaPath(chassis, [[s * .045, top, z - .025], [s * w * .76, top - .015, z],
        [s * w, .07, z + .025], [s * w * .82, -.11, z + .04],
        [s * .08, -.18 + (row === 0 ? .08 : 0), z + .025]], .020, .013,
      (row + (s === 1 ? 1 : 0)) % 3 === 0 ? 'rust' : 'iron');
      // Missing lower right shingle leaves the piston and ribs exposed.
      if (!(s === 1 && row === 3)) chassis.push([alphaScale([s * w * .79, top - .005, z],
        .14, .145, [.16, s * .17, -s * .62]), row % 3 === 1 ? 'rust' : 'iron']);
    }
  }
  // Scapulae overlap as sloping car panels, broad at the front, narrow at the waist.
  for (const s of [-1, 1]) {
    for (let row = 0; row < 3; row++) {
      chassis.push([alphaScale([s * (.13 + row * .025), .29 - row * .055, .27 - row * .075],
        .19, .24, [-.25, s * (.20 + row * .10), -s * (.48 + row * .20)]),
      row === 1 && s === -1 ? 'rust' : 'iron']);
    }
    chassis.push([alphaScale([s * .12, .17, -.40], .18, .26, [.13, -s * .25, -s * .55]), 'iron']);
    chassis.push([pebble([s * .16, .11, -.37], [.055, .10, .12]), 'rust']);
    // Curved coolant lines, an exposed ram and hanging chain draped against the flank.
    alphaPath(chassis, [[s * .16, .15, .31], [s * .245, .07, .22],
      [s * .235, -.035, .08], [s * .18, -.10, -.05]], .011, .008, 'oil');
    chassis.push([rod([s * .20, .065, -.12], [s * .24, -.07, .08], .018, .018), 'oil']);
    chassis.push([rod([s * .24, -.07, .08], [s * .21, -.14, .21], .009, .009), 'edge']);
    for (let i = 0; i < 5; i++) chassis.push([alphaLink([s * (.245 - i * .008),
      -.025 - Math.sin(i / 4 * Math.PI) * .075, -.035 - i * .037], .019,
    i % 2 * Math.PI / 2), i % 2 ? 'oil' : 'rust']);
    // Split exhausts bend back like hackles; dark open mouths, no smoke/glow mesh.
    const x = s * .10, tip = [s * .135, .405 - (s === 1 ? .035 : 0), .13];
    alphaPath(chassis, [[x, .21, .29], [s * .13, .32, .24], tip], .028, .022, 'rust', 6);
    chassis.push([fieldHoop([0, 0, 0], .025, .006).rotateX(Math.PI / 2 - .30)
      .translate(...tip), 'iron']);
    chassis.push([pebble(tip, [.018, .006, .019]), 'oil']);
    // Soot bands and weld studs sit on the shoulder tops, visible from above.
    alphaPath(chassis, [[s * .07, .323, .28], [s * .12, .31, .22],
      [s * .17, .26, .16]], .006, .009, 'oil', 4);
    for (let i = 0; i < 3; i++) chassis.push([pebble([s * (.09 + i * .04),
      .32 - i * .027, .31 - i * .05], [.009, .008, .009]), 'edge']);
  }
  // Back-swept rebar and oil-black cable mane follow several staggered arcs.
  for (let i = 0; i < 7; i++) for (const s of [-1, 1]) {
    const z = .35 - i * .075, h = .28 - i * .014, x = s * (.07 + (i % 3) * .024);
    alphaPath(chassis, [[x, h, z], [x + s * .025, h + .075, z - .045],
      [x + s * .035, h + .11, z - .115], [x + s * .025, h + .085, z - .19]],
    .013, .0025, i % 3 === 0 ? 'rust' : 'oil', 4);
  }
  // A ripped tyre on one haunch and radiator fins tucked below the breast.
  chassis.push([fieldHoop([-.185, .07, -.39], .10, .025, Math.PI * 1.55, Math.PI / 2), 'oil']);
  for (let i = 0; i < 5; i++) {
    chassis.push([box([.16 - Math.abs(i - 2) * .018, .012, .027],
      [0, -.13 + i * .039, .385], [-.18, .05, (i - 2) * .035]), 'iron']);
  }
  alphaPath(chassis, [[-.10, -.13, .36], [-.12, -.01, .39], [-.08, .10, .35]], .012, .009, 'rust');
  alphaPath(chassis, [[.10, -.13, .36], [.12, -.01, .39], [.08, .10, .35]], .012, .009, 'iron');
  // The tail drops from the sacrum, then curls up; broad root becomes a thin whip.
  const tail = [[0, .13, -.51], [.015, .075, -.61], [.045, -.015, -.70],
    [.075, -.075, -.79], [.12, -.08, -.88], [.16, -.025, -.96],
    [.17, .065, -.99], [.14, .145, -.965], [.10, .185, -.92]];
  alphaPath(chassis, tail, .041, .005, 'oil');
  for (let i = 1; i < tail.length - 1; i++) {
    const p = tail[i], r = .042 - i * .004;
    chassis.push([pebble(p, [r, r * .86, r * 1.3]), i % 2 ? 'iron' : 'rust']);
    chassis.push([alphaScale([p[0], p[1] + r * .65, p[2]], r * 2.1, r * 2.8,
      [i < 4 ? -.48 : .65, i * .12, -.12]), 'iron']);
  }
  // Closed thin scraps share the body batch while retaining torn cloth outlines.
  chassis.push([alphaPlate([[-.035, .02], [.038, 0], [.027, -.12], [.009, -.09],
    [-.005, -.15], [-.027, -.13]], .003, [-.22, .01, -.18], [.2, .5, -.18]), 'ash']);
  chassis.push([alphaPlate([[-.03, 0], [.04, .015], [.025, -.13], [.003, -.10],
    [-.02, -.16]], .003, [.20, .02, -.31], [-.2, -.6, .2]), 'cloth']);
  scrap(body, 'arched-engine-ribs-armour-mane-and-tail', chassis, metal);
  const core = glow(body, 'soul-core', [0, -.015, .31], [.063, .085, .056], [3, .32, .065]);

  // Heavy neck descends from the withers into a low, forward hunting skull.
  const head = pivot(body, 'head', [0, .14, .50]);
  const skull = [
    [pebble([0, .015, .025], [.125, .105, .14]), 'oil'],
    [pebble([0, -.025, .19], [.079, .065, .15]), 'oil'],
    [pebble([0, -.045, .365], [.057, .038, .045]), 'oil'],
  ];
  alphaPath(skull, [[0, .065, -.23], [0, .035, -.16], [0, -.01, -.07], [0, 0, .025]],
    .105, .083, 'oil', 7);
  for (let i = 0; i < 4; i++) {
    skull.push([fieldHoop([0, 0, 0], .104 - i * .007, .016, Math.PI * 1.77)
      .rotateZ(.35 + i * .19).rotateX(-.25).translate(0, .045 - i * .013, -.20 + i * .05),
    i % 2 ? 'rust' : 'iron']);
  }
  for (const s of [-1, 1]) {
    alphaPath(skull, [[s * .07, .10, -.20], [s * .12, .07, -.12],
      [s * .13, -.025, -.035], [s * .11, -.07, .04]], .014, .009, 'oil');
    skull.push([rod([s * .085, -.07, -.19], [s * .10, -.08, -.05], .020, .014), 'iron']);
    // Side cheek plates and long tapered muzzle rails have changing angles.
    skull.push([alphaPlate([[-.06, .055], [.03, .095], [.13, .04], [.12, -.06],
      [.035, -.095], [-.065, -.035]], .018, [s * .105, 0, .015], [0, -s * Math.PI / 2, s * .08]), 'iron']);
    alphaPath(skull, [[s * .105, -.015, .09], [s * .078, -.03, .20],
      [s * .052, -.05, .34]], .023, .012, s === -1 ? 'rust' : 'iron');
    // Recessed sockets under a slanted brow; the right lens is dull geometry.
    skull.push([pebble([s * .10, .038, .12], [.039, .027, .033]), 'oil']);
    skull.push([rod([s * .055, .077, .135], [s * .132, .058, .09], .021, .025, 5), 'iron']);
    if (s === 1) skull.push([pebble([.109, .043, .145], [.022, .011, .014]), 'bone']);
    // Ears lean backwards in two folds, one cropped by a ragged tear.
    skull.push([alphaPlate([[-.036, 0], [.032, -.005], [.047, .084], [.012, .145],
      [-.022, .10]], .012, [s * .09, .075, -.035], [-.65, s * .24, -s * .23]), 'iron']);
    skull.push([alphaPlate([[-.023, 0], [.023, .008], [.005, s === -1 ? .087 : .052],
      [-.012, s === -1 ? .068 : .045]], .008, [s * .12, .165, -.095],
    [-1.0, s * .22, -s * .28]), s === -1 ? 'rust' : 'ash']);
    skull.push([pebble([s * .117, -.065, .015], [.022, .027, .026]), 'rust']);
    // Side-on triangular blades form a saw-toothed upper bite, with long canines.
    for (let i = 0; i < 5; i++) {
      const z = .09 + i * .055, x = s * (.085 - i * .008);
      skull.push([alphaPlate([[-.013, .006], [.014, 0], [.007, i === 1 ? -.082 : -.037 - (i % 2) * .012]],
        .012, [x, -.060 - i * .002, z], [.07, Math.PI / 2, -s * .12]), i === 1 ? 'bone' : 'edge']);
    }
  }
  for (let i = 0; i < 4; i++) {
    skull.push([alphaScale([i % 2 ? .008 : -.008, .084 - i * .031, .015 + i * .085],
      .21 - i * .038, .15, [-.22, i % 2 ? .12 : -.11, i % 2 ? .06 : -.05]),
    i === 1 ? 'rust' : 'iron']);
  }
  // Nasal seam and nostrils stop the snout reading as a rectangular gun barrel.
  alphaPath(skull, [[0, .10, .025], [-.008, .07, .12], [.006, .035, .22], [0, -.005, .33]],
    .008, .004, 'oil', 4);
  for (const s of [-1, 1]) skull.push([pebble([s * .031, -.036, .392], [.015, .010, .006]), 'oil']);
  // Broken organ pipes ring an open resonator below the neck, facing the muzzle.
  for (let i = 0; i < 8; i++) {
    const a = i * Math.PI / 4, x = Math.cos(a) * .069, y = -.145 + Math.sin(a) * .062;
    skull.push([rod([x, y, -.10], [x * 1.12, y - .008, -.015 + (i % 3) * .015], .014, .011, 5), 'rust']);
  }
  skull.push([fieldHoop([0, -.145, -.06], .078, .010, Math.PI * 1.85), 'iron']);
  scrap(head, 'low-wolf-skull-cable-neck-and-resonator', skull, metal);
  const eye = glow(head, 'visor-eyes', [-.109, .043, .145], [.024, .012, .017], [4, .42, .07]);
  const throat = glow(head, 'throat', [0, -.145, -.035], [.047, .043, .025], [2.6, .65, .10]);
  throat.visible = false;

  const jaw = pivot(head, 'jaw', [0, -.085, .035]);
  const lower = [[pebble([0, -.047, .16], [.063, .023, .14]), 'oil']];
  for (const s of [-1, 1]) {
    alphaPath(lower, [[s * .10, 0, 0], [s * .088, -.035, .08],
      [s * .062, -.047, .21], [s * .045, -.035, .325]], .019, .010, s === 1 ? 'iron' : 'rust');
    lower.push([alphaPlate([[0, .005], [.12, -.025], [.30, -.025], [.33, -.05],
      [.22, -.074], [.07, -.065], [-.015, -.02]], .014, [s * .065, 0, 0], [0, -Math.PI / 2, 0]), 'iron']);
    for (let i = 0; i < 5; i++) lower.push([alphaPlate([[-.011, 0], [.013, 0],
      [-.003, i === 2 ? .049 : .027]], .011, [s * (.077 - i * .007), -.035, .065 + i * .052],
    [0, Math.PI / 2, s * .15]), i === 2 ? 'bone' : 'edge']);
    alphaPath(lower, [[s * .057, -.05, .20], [s * .063, -.10, .21],
      [s * .049, -.17 + (s === 1 ? .03 : 0), .235], [s * .04, -.18, .26]],
    .0045, .002, 'oil', 4);
  }
  lower.push([alphaScale([0, -.039, .275], .10, .12, [.12, .06, 0]), 'rust']);
  scrap(jaw, 'hinged-tapered-fang-jaw-and-drool-wires', lower, metal);

  // Closed, bowed muscle armour: chipped perimeter, raised centre, dark seams
  // between overlapping shells. Geometry is baked; this never rotates a joint.
  function alphaLegPlate(position, width, length, bulge, side, pitch) {
    const vertices = [], indices = [];
    for (let layer = 0; layer < 2; layer++) {
      for (let row = 0; row < 4; row++) for (let col = 0; col < 3; col++) {
        const t = row / 3, u = col - 1;
        const taper = [ .76, 1, .85, .46 ][row];
        const chip = row === 2 && col === 2 ? .11 : 0;
        vertices.push((u * .5 * taper - chip) * width,
          (.5 - t + (row === 3 && col === 1 ? .075 : 0)) * length,
          bulge * Math.sin(Math.PI * t) * (1 - .75 * Math.abs(u)) - layer * .014);
      }
    }
    for (let row = 0; row < 3; row++) for (let col = 0; col < 2; col++) {
      const a = row * 3 + col, b = a + 3;
      indices.push(a, b, a + 1, a + 1, b, b + 1);
      indices.push(a + 12, a + 13, b + 12, a + 13, b + 13, b + 12);
    }
    const rim = [0, 3, 6, 9, 10, 11, 8, 5, 2, 1];
    for (let i = 0; i < rim.length; i++) {
      const a = rim[i], b = rim[(i + 1) % rim.length];
      indices.push(a, a + 12, b, b, a + 12, b + 12);
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
    geometry.setIndex(indices);
    return geometry.rotateY(side * Math.PI / 2).rotateX(pitch).translate(...position);
  }

  const legs = [];
  // FL, FR, RR, RL. Anatomical offsets are baked; all three joints stay neutral.
  for (const [side, index] of [[-1, 0], [1, 0], [1, 1], [-1, 1]]) {
    const front = index === 0;
    const hip = pivot(body, 'hip-' + side + '-' + index, [side * .17, -.01, front ? .30 : -.35]);
    const kneeOffset = [side * .01, front ? -.17 : -.16, front ? -.06 : .09];
    const ankleOffset = [0, front ? -.21 : -.17, front ? .03 : -.13];
    const knee = pivot(hip, 'knee-' + side + '-' + index, kneeOffset);
    const ankle = pivot(knee, 'ankle-' + side + '-' + index, ankleOffset);
    // The extra bulk sits outward of the unchanged hip: the medial surface
    // stays clear of the centre plane even during full sagittal joint bends.
    const upper = [
      [bearing([0, 0, 0], front ? .104 : .124), 'oil'],
      [pebble([side * (front ? .030 : .045), front ? -.040 : -.035, front ? -.018 : .028],
        [front ? .143 : .1743, front ? .120 : .125, front ? .1496 : .1785], 1), 'iron'],
      [rod([side * .018, 0, 0], kneeOffset, front ? .1034 : .1323, .0672, 6), 'rust'],
    ];
    // Broad scapular crown / rounded haunch blends into the body side, then
    // overlapping muscle plates taper down toward the backward elbow / stifle.
    for (let i = 0; i < 3; i++) {
      const t = .02 + i * .30;
      upper.push([alphaLegPlate([side * ((front ? .120 : .151) - i * .028),
        .025 + kneeOffset[1] * t, kneeOffset[2] * t],
      (front ? .265 : .310) - i * .060, .150 - i * .016, .045 - i * .008,
      side, front ? -.34 : .52), i === 1 ? 'rust' : 'iron']);
    }
    alphaPath(upper, [[-side * .055, .01, -.070], [-side * .060, -.075, front ? -.113 : -.020],
      [kneeOffset[0], kneeOffset[1], kneeOffset[2] - .048]], .022, .014, 'oil');
    upper.push([rod([side * .095, -.02, .075], [side * .085, -.10, front ? .041 : .120], .034, .034, 5), 'oil']);
    upper.push([rod([side * .085, -.10, front ? .041 : .120],
      [kneeOffset[0] + side * .028, kneeOffset[1], kneeOffset[2] + .043], .016, .016, 5), 'edge']);
    scrap(hip, front ? 'scapula-and-backward-upper-arm' : 'heavy-thigh-and-forward-stifle', upper, metal);

    const shin = [
      [bearing([0, 0, 0], .0612), 'rust'],
      [pebble([side * .025, -.008, .012], [.053, .055, .057]), 'iron'],
      [rod([0, 0, 0], ankleOffset, .054, .036, 6), 'iron'],
      [rod([side * .0414, -.015, .0324], [side * .0414, ankleOffset[1] * .61,
        ankleOffset[2] * .61 + .0324], .0234, .0234, 5), 'oil'],
      [rod([side * .0414, ankleOffset[1] * .61, ankleOffset[2] * .61 + .0324],
        [0, ankleOffset[1], ankleOffset[2] + .0216], .0126, .0126, 5), 'edge'],
    ];
    // Forearm / gaskin armour wraps the load-bearing ram; lower lames narrow.
    for (let i = 0; i < 2; i++) {
      const t = .24 + i * .35;
      shin.push([alphaLegPlate([side * (.045 - i * .010), ankleOffset[1] * t,
        ankleOffset[2] * t], .108 - i * .024, .125 - i * .020, .023 - i * .006,
      side, front ? -.14 : .65), i === 0 ? 'iron' : 'rust']);
    }
    // Paired heavy cables follow the back, leaving the inner side uncluttered.
    for (const cable of [-1, 1]) alphaPath(shin,
      [[cable * .020, -.008, -.045], [cable * .022, ankleOffset[1] * .55,
        ankleOffset[2] * .55 - .0486], [cable * .016, ankleOffset[1], ankleOffset[2] - .0396]],
    .0144, .0108, 'oil');
    scrap(knee, front ? 'near-vertical-radius-and-ulna' : 'back-swept-shin-and-achilles', shin, metal);

    const floor = front ? -.11 : -.16, pawZ = front ? .05 : .04;
    const foot = [
      [bearing([0, 0, 0], .0486), 'iron'],
      [pebble([side * .025, -.010, -.005], [.041, .045, .047]), 'rust'],
      [rod([0, 0, 0], [0, floor + .045, pawZ], .0396, .0522, 5), 'iron'],
      [pebble([0, floor + .0435, pawZ], [.087, .0435, .093]), 'oil'],
      [alphaScale([0, floor + .0885, pawZ], .1575, .165, [.08, side * .10, 0]), 'rust'],
      // Broad clipped sole supports the pad; its bottom is exactly floor.
      [alphaPlate([[-.073, -.055], [.068, -.058], [.087, .018], [.052, .072],
        [-.051, .075], [-.086, .015]], .016, [0, floor + .008, pawZ], [Math.PI / 2, 0, 0]), 'oil'],
      [alphaLegPlate([side * .034, floor * .40, pawZ * .40], .074, front ? .080 : .120,
        .019, side, front ? -.40 : -.25), 'iron'],
    ];
    for (const cable of [-1, 1]) alphaPath(foot,
      [[cable * .018, .005, -.0324], [cable * .019, floor * .52, -.020],
        [cable * .020, floor + .0465, pawZ]], .0126, .009, 'oil');
    // The rear-facing dew-claw spur stays on the wrist / hock, above the sole.
    alphaPath(foot, [[side * .034, -.015, -.014], [side * .057, -.035, -.0615],
      [side * .0525, -.057, -.078]], .018, .003, 'edge', 4);
    for (let toe = -1; toe <= 1; toe++) {
      const x = toe * .0555, z = pawZ + .0525 - Math.abs(toe) * .012;
      foot.push([pebble([x, floor + .0375, z], [.0375, .0345, .0555]), 'iron']);
      // Splayed long blade claws and sole plates retain exact ground contact.
      foot.push([alphaPlate([[-.0195, -.0285], [.0195, -.024], [.027, .033],
        [.0075, .117 - Math.abs(toe) * .018], [-.009, .087], [-.021, .021]], .018,
      [x, floor + .009, z + .0315], [Math.PI / 2, toe * .18, 0]), 'edge']);
    }
    scrap(ankle, front ? 'forward-pastern-dewclaw-and-paw' : 'long-metatarsus-hock-spur-and-paw', foot, metal);
    legs.push({ hip, knee, ankle, side, index });
  }
  return { root, radius: .75, height: .9, parts: { body, head, jaw, eye, core, legs, throat } };
}

function fieldBipedLeg(pelvis, metal, side, label, executor) {
  const hip = pivot(pelvis, `${label}-hip`, [side * .20, 0, 0]);
  const knee = pivot(hip, `${label}-knee`, [side * .045, -.44, .14]);
  const ankle = pivot(knee, `${label}-ankle`, [side * .025, -.37, -.31]);
  scrap(hip, `${label}-load-strut`, [
    [bearing([0, 0, 0], .10), 'oil'],
    [rod([0, -.025, 0], [side * .045, -.44, .14], .065, .045, 6), 'iron'],
    [box([.13, .27, .15], [side * .02, -.19, .065], [-.25, 0, side * .08]), executor ? 'iron' : 'rust'],
    [rod([-side * .07, -.03, -.04], [-side * .035, -.37, .06], .023, .017, 5), 'edge'],
  ], metal);
  scrap(knee, `${label}-hydraulic-shin`, [
    [bearing([0, 0, 0], .085), 'rust'],
    [rod([0, 0, 0], [side * .025, -.37, -.31], .055, .038, 6), 'iron'],
    [rod([side * .08, -.035, -.01], [side * .10, -.24, -.18], .033, .033, 5), 'oil'],
    [rod([side * .10, -.24, -.18], [side * .07, -.37, -.30], .017, .017, 5), 'edge'],
    [plate([[-.09, .04], [.085, .06], [.075, -.12], [-.07, -.15]], .065, .045), executor ? 'iron' : 'rust'],
  ], metal);
  const foot = [[rod([0, 0, 0], [0, -.17, .04], .055, .08, 6), 'iron']];
  for (const toe of [-1, 0, 1]) {
    const x = toe * (executor ? .10 : .18), z = toe === 0 ? .30 : -.12;
    foot.push([rod([0, -.12, .04], [x, -.20, z], .037, .022, 5), 'rust']);
    foot.push([box([.115, .045, .14], [x, -.2175, z], [0, toe * .25, 0]), 'iron']);
    foot.push([rod([x, -.18, z], [x, -.235, z + .045], .016, .005, 4), 'edge']);
  }
  scrap(ankle, executor ? `${label}-splayed-sabatons` : `${label}-tripod-anchor-foot`, foot, metal);
  return { hip, knee, ankle };
}

function fieldBipedArm(torso, metal, side, label, executor) {
  const shoulder = pivot(torso, `${label}-shoulder`, [side * .41, .80, 0]);
  const elbow = pivot(shoulder, `${label}-elbow`, [side * .12, -.43, .015]);
  const blade = pivot(elbow, `${label}-blade`, [side * .08, -.35, .045]);
  const upper = [
    [bearing([0, 0, 0], .12), 'oil'],
    [rod([0, -.02, 0], [side * .12, -.43, .015], .055, .04, 6), 'iron'],
    [plate([[-.16, .10], [.13, .12], [.19, -.08], [.10, -.22], [-.14, -.17]], .25, .035), executor ? 'iron' : 'rust'],
    [rod([side * .055, -.11, -.11], [side * .16, -.38, -.05], .021, .016, 5), 'rust'],
  ];
  const fore = [
    [bearing([0, 0, 0], .08), 'rust'],
    [rod([0, 0, 0], [side * .08, -.35, .045], .044, .036, 6), 'iron'],
    [box([.12, .18, .15], [side * .05, -.23, .025], [0, 0, side * .12]), 'oil'],
  ];
  if (executor && side > 0) {
    // The right slam arm is splinted: broken outer sleeve, exposed actuator, three clamps.
    upper.push([box([.075, .35, .07], [.17, -.23, .015], [0, 0, .10]), 'rust']);
    fore.push([rod([.13, .02, -.025], [.17, -.35, .04], .021, .021, 5), 'edge']);
    for (let i = 0; i < 3; i++) {
      fore.push([box([.24, .045, .19], [.065, -.045 - i * .12, .025]), 'rust']);
      fore.push([rod([.18, -.045 - i * .12, .025], [.20, -.045 - i * .12, .025], .027, .027, 5), 'edge']);
    }
  }
  scrap(shoulder, `${label}-shoulder-and-upper-arm`, upper, metal);
  scrap(elbow, `${label}-forearm`, fore, metal);
  if (executor) {
    const silhouette = [[-.12, .04], [.13, .015], [.16, -.36], [.11, -.58], [.19, -.92], [.12, -1.22], [-.015, -1.30], [-.10, -.97], [-.14, -.35]];
    const bladePieces = [
      [plate(silhouette.map(([x, y]) => [x * side, y]), .10), 'iron'],
      [plate([[.13, -.08], [.16, -.36], [.11, -.58], [.19, -.92], [.12, -1.22], [.08, -1.05], [.09, -.39]].map(([x, y]) => [x * side, y]), .105), 'edge'],
      [box([.27, .09, .16], [0, -.06, 0]), 'rust'],
      [rod([0, -.16, -.065], [side * .015, -.92, -.065], .027, .013, 5), 'rust'],
    ];
    for (let i = 0; i < 4; i++) bladePieces.push([rod([-.055 * side, -.22 - i * .20, .05], [-.055 * side, -.22 - i * .20, .065], .019, .019, 5), 'rust']);
    scrap(blade, `${label}-chipped-execution-blade`, bladePieces, metal);
  } else if (side > 0) {
    // Long barrel is local +Z at the *original* blade pivot, not a rotated joint.
    const gun = [
      [box([.23, .20, .25], [0, .015, .045]), 'iron'],
      [rod([0, .025, .10], [0, .025, .58], .078, .058, 8), 'rust'],
      [rod([0, .025, .581], [0, .025, .585], .044, .044, 8), 'oil'],
      [fieldHoop([0, .025, .59], .056, .014), 'iron'],
      [rod([-.07, -.07, .07], [-.07, -.07, .44], .019, .014, 5), 'edge'],
      [box([.13, .12, .18], [-.12, -.015, -.01], [0, 0, -.08]), 'rust'],
    ];
    for (const z of [.20, .35, .48]) gun.push([fieldHoop([0, .025, z], .073, .012), 'iron']);
    scrap(blade, 'forward-rusted-cannon', gun, metal);
  } else {
    scrap(blade, 'short-breaching-hammer', [
      [rod([0, .04, 0], [0, -.23, .035], .035, .03, 6), 'iron'],
      [plate([[-.16, -.17], [.13, -.14], [.16, -.32], [.06, -.39], [-.17, -.34]], .23, .04), 'rust'],
      [box([.06, .19, .25], [-.135, -.26, .04], [0, 0, -.1]), 'edge'],
    ], metal);
  }
  return { shoulder, elbow, blade };
}

/** Rooted sentry: tripod shoes, a hunched breech housing and an unmistakable cannon. */
export function createSentry() {
  const { metal } = materials();
  const root = pivot(null, 'rusted-sentry');
  const pelvis = pivot(root, 'pelvis', [0, 1.05, 0]);
  scrap(pelvis, 'heavy-anchoring-yoke', [
    [rod([-.26, 0, 0], [.26, 0, 0], .14, .14, 8), 'iron'],
    [box([.43, .22, .34], [0, .035, 0]), 'rust'],
    [rod([0, .12, 0], [0, .36, 0], .135, .11, 8), 'oil'],
  ], metal);
  const torso = pivot(pelvis, 'torso', [0, .25, 0]);
  const shell = [
    [pebble([0, .56, -.095], [.36, .42, .30]), 'iron'],
    [plate([[-.28, .15], [.28, .19], [.34, .66], [.20, .88], [-.23, .84], [-.35, .51]], .18, .16), 'rust'],
    [box([.37, .15, .38], [0, .82, -.12], [.18, 0, .04]), 'iron'],
    [rod([-.24, .31, -.17], [-.22, .77, -.27], .045, .035, 6), 'rust'],
    [rod([.23, .33, -.17], [.26, .73, -.22], .04, .04, 6), 'oil'],
    [rod([0, .83, .02], [0, 1.10, .035], .055, .055, 6), 'edge'],
  ];
  for (let i = 0; i < 4; i++) shell.push([box([.28, .022, .04], [0, .42 + i * .075, .265]), 'oil']);
  scrap(torso, 'hunched-turret-casing', shell, metal);
  const core = glow(torso, 'soul-core', [0, .45, -.365], [.07, .10, .035], [1.2, .75, .12]);
  const head = pivot(torso, 'head', [0, 1.12, .025]);
  scrap(head, 'hooded-rangefinder', [
    [box([.30, .22, .27], [0, .005, -.035]), 'oil'],
    [plate([[-.20, -.12], [-.20, .13], [-.12, .25], [.16, .22], [.21, .08], [.19, -.13], [.12, -.08], [.12, .10], [-.12, .10], [-.12, -.12]], .26, -.015), 'iron'],
    [fieldHoop([0, .015, .13], .087, .023), 'rust'],
    [box([.28, .055, .15], [0, .14, .09], [.12, 0, 0]), 'rust'],
  ], metal);
  const eye = glow(head, 'single-lens', [0, .015, .15], [.062, .06, .032], [2.3, 1.75, .12]);
  const armL = fieldBipedArm(torso, metal, -1, 'left', false);
  const armR = fieldBipedArm(torso, metal, 1, 'right', false);
  const legL = fieldBipedLeg(pelvis, metal, -1, 'left', false);
  const legR = fieldBipedLeg(pelvis, metal, 1, 'right', false);
  return { root, radius: .95, height: 2.7, parts: { pelvis, torso, head, eye, core, armL, armR, legL, legR } };
}

/** Executor: shield front, torn-open back and a visibly splinted right slam arm. */
// Executor-only geometry. Shared sentry limb helpers above remain untouched.
function executorPath(pieces, points, r0, r1, paint = 'oil', sides = 5) {
  for (let i = 0; i < points.length - 1; i++) {
    const a = i / (points.length - 1), b = (i + 1) / (points.length - 1);
    pieces.push([rod(points[i], points[i + 1], r0 + (r1 - r0) * a,
      r0 + (r1 - r0) * b, sides), paint]);
  }
}

function executorSheet(points, depth, position, rotation = [0, 0, 0]) {
  return plate(points, depth).rotateX(rotation[0]).rotateY(rotation[1])
    .rotateZ(rotation[2]).translate(...position);
}

// A closed, faceted bowed plate, with staggered chips along the lower edge.
// The middle row/column give the armour actual curvature, including on its top.
function executorShingle(position, width, height, bow, rotation = [0, 0, 0], depth = .025) {
  const vertices = [], indices = [];
  for (let face = 0; face < 2; face++) for (let row = 0; row < 3; row++) {
    for (let col = 0; col < 5; col++) {
      const u = col / 2 - 1, t = row / 2;
      vertices.push(u * width * .5 * (row === 1 ? 1 : .84),
        height * (.5 - t + (row === 2 ? [.04, 0, .08, -.02, .06][col] : 0)),
        bow * (1 - u * u) + (face === 0 ? depth : -depth) * .5);
    }
  }
  for (let row = 0; row < 2; row++) for (let col = 0; col < 4; col++) {
    const a = row * 5 + col;
    indices.push(a, a + 5, a + 1, a + 1, a + 5, a + 6,
      a + 15, a + 16, a + 20, a + 16, a + 21, a + 20);
  }
  const rim = [0, 1, 2, 3, 4, 9, 14, 13, 12, 11, 10, 5];
  for (let i = 0; i < rim.length; i++) {
    const a = rim[i], b = rim[(i + 1) % rim.length];
    indices.push(a, b, a + 15, b, b + 15, a + 15);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  geometry.setIndex(indices);
  return geometry.rotateX(rotation[0]).rotateY(rotation[1]).rotateZ(rotation[2]).translate(...position);
}

function executorLink(position, radius, yaw = 0) {
  return new THREE.TorusGeometry(radius, radius * .23, 3, 6)
    .scale(.8, 1.15, 1).rotateY(yaw).translate(...position);
}

function executorLeg(pelvis, metal, side, label) {
  // Identical joint positions to fieldBipedLeg: existing warden gait/IK contract.
  const hip = pivot(pelvis, label + '-hip', [side * .20, 0, 0]);
  const knee = pivot(hip, label + '-knee', [side * .045, -.44, .14]);
  const ankle = pivot(knee, label + '-ankle', [side * .025, -.37, -.31]);
  const thigh = [[bearing([0, 0, 0], .11), 'oil'],
    [pebble([side * .025, -.17, .045], [.105, .19, .105]), 'iron'],
    [rod([0, 0, 0], [side * .045, -.44, .14], .072, .045, 6), 'rust']];
  for (let i = 0; i < 3; i++) thigh.push([executorShingle([side * (.015 + i * .013),
    -.10 - i * .105, .10 + i * .028], .22 - i * .025, .18, .043,
  [-.25, side * .18, -side * .10]), i === 1 ? 'rust' : 'iron']);
  executorPath(thigh, [[-side * .08, -.035, -.04], [-side * .075, -.21, -.005],
    [side * .015, -.40, .11]], .025, .013, 'oil');
  thigh.push([rod([side * .09, -.08, -.035], [side * .11, -.27, .035], .024, .024, 5), 'oil']);
  thigh.push([rod([side * .11, -.27, .035], [side * .08, -.43, .12], .012, .012, 5), 'edge']);
  scrap(hip, label + '-layered-load-bearing-thigh', thigh, metal);
  const shin = [[bearing([0, 0, 0], .089), 'rust'],
    [rod([0, 0, 0], [side * .025, -.37, -.31], .060, .036, 6), 'iron'],
    [executorShingle([0, -.025, .08], .235, .24, .062, [-.15, side * .12, 0]), 'iron'],
    [rod([0, .025, .14], [side * .025, .08, .29], .035, .003, 5), 'edge'],
    [rod([side * .09, -.03, -.02], [side * .11, -.24, -.18], .033, .033, 5), 'oil'],
    [rod([side * .11, -.24, -.18], [side * .06, -.37, -.29], .016, .016, 5), 'edge']];
  for (let i = 0; i < 2; i++) shin.push([executorShingle([side * .02,
    -.17 - i * .095, -.085 - i * .085], .15, .16, .028, [.70, side * .20, 0]), 'iron']);
  executorPath(shin, [[-side * .07, -.02, -.04], [-side * .075, -.20, -.21],
    [0, -.37, -.33]], .018, .012, 'oil');
  scrap(knee, label + '-spiked-knee-and-reverse-shin', shin, metal);
  const foot = [[bearing([0, 0, 0], .058), 'iron'],
    [rod([0, 0, 0], [0, -.17, .07], .045, .065, 6), 'rust'],
    [executorShingle([0, -.11, .08], .16, .20, .04, [-.32, 0, 0]), 'iron']];
  for (let toe = -1; toe <= 1; toe++) {
    const x = toe * .095, z = .13 + (toe === 0 ? .075 : 0);
    foot.push([rod([0, -.13, .04], [x, -.19, z], .035, .025, 5), 'oil']);
    foot.push([executorSheet([[-.045, -.09], [.044, -.085], [.052, .06], [.017, .17],
      [-.026, .15], [-.051, .025]], .034, [x, -.223, z], [Math.PI / 2, toe * .20, 0]), 'edge']);
    foot.push([executorShingle([x, -.18, z], .10, .20, .025,
      [-Math.PI / 2, 0, -toe * .20]), 'iron']);
  }
  scrap(ankle, label + '-splayed-lamellar-sabatons', foot, metal);
  return { hip, knee, ankle };
}

function executorArm(torso, metal, side, label) {
  const shoulder = pivot(torso, label + '-shoulder', [side * .41, .80, 0]);
  const elbow = pivot(shoulder, label + '-elbow', [side * .12, -.43, .015]);
  const blade = pivot(elbow, label + '-blade', [side * .08, -.35, .045]);
  const upper = [[bearing([0, 0, 0], .15), 'oil'],
    [pebble([side * .015, .12, -.015], [.235, .23, .23]), 'oil'],
    [rod([0, -.015, 0], [side * .12, -.43, .015], .09, .052, 7), 'iron']];
  // Overlapping domed car-panel pauldrons, carried by the moving shoulder.
  for (let i = 0; i < 3; i++) {
    upper.push([executorShingle([side * (.035 + i * .037), .29 - i * .075, .015],
      .46 + i * .045, .44 + i * .025, .10, [-Math.PI / 2, 0, -side * (.10 + i * .13)]),
    i === 1 ? 'rust' : 'iron']);
    upper.push([executorShingle([side * (.055 + i * .03), -.12 - i * .10, .075],
      .20 - i * .018, .16, .04, [.05, side * .25, side * .12]), 'iron']);
    const x = side * (.06 + i * .07), z = -.13 + i * .13;
    executorPath(upper, [[x, .31 - i * .025, z], [x + side * .04, .44 - i * .012, z - .045],
      [x + side * .075, .55 - i * .04, z - .10]], .030, .003, i === 1 ? 'rust' : 'edge');
    upper.push([pebble([side * (.12 + i * .065), .12 - i * .05, .245], [.020, .018, .015]), 'rust']);
  }
  executorPath(upper, [[-side * .08, .02, -.11], [-side * .065, -.14, -.14],
    [side * .06, -.37, -.05]], .027, .016, 'oil');
  const fore = [[bearing([0, 0, 0], .09), 'rust'],
    [rod([0, 0, 0], [side * .08, -.35, .045], .055, .043, 6), 'iron'],
    [rod([-side * .055, -.02, .055], [side * .025, -.25, .11], .027, .027, 5), 'oil'],
    [rod([side * .025, -.25, .11], [side * .08, -.35, .055], .012, .012, 5), 'edge']];
  for (let i = 0; i < 3; i++) {
    // An absent middle sleeve exposes the snapped right-arm actuator.
    if (!(side > 0 && i === 1)) fore.push([executorShingle([side * (.015 + i * .025),
      -.07 - i * .11, .09], .19, .16, .04, [.10, side * .18, side * .15]), 'iron']);
  }
  executorPath(fore, [[-side * .05, .02, -.04], [-side * .09, -.12, -.09],
    [side * .015, -.30, -.06], [side * .08, -.35, .01]], .021, .012, 'oil');
  if (side > 0) {
    executorPath(upper, [[.15, -.08, -.02], [.21, -.23, -.015], [.19, -.39, .015]], .032, .026, 'rust');
    fore.push([rod([.14, .015, -.025], [.19, -.34, .04], .025, .025, 5), 'edge']);
    fore.push([executorSheet([[-.04, .06], [.04, .04], [.03, -.03], [.005, .004],
      [-.012, -.05], [-.03, -.02]], .025, [.10, -.13, .065], [.1, .8, -.2]), 'rust']);
    for (let i = 0; i < 3; i++) {
      fore.push([fieldHoop([0, 0, 0], .10, .018, Math.PI * 1.8)
        .rotateX(Math.PI / 2).rotateZ(.15).translate(.035 + i * .025, -.045 - i * .12, .02), 'rust']);
      fore.push([rod([.15 + i * .025, -.045 - i * .12, .02],
        [.19 + i * .025, -.045 - i * .12, .02], .024, .024, 5), 'edge']);
    }
  }
  scrap(shoulder, label + '-domed-spiked-pauldron-and-upper-arm', upper, metal);
  scrap(elbow, side > 0 ? 'right-snapped-sleeve-splint-and-clamps' : 'left-layered-gauntlet-and-tendons', fore, metal);
  // Curved inward sweep keeps the hooked tips inside the .95 m body footprint.
  // Blade bottom is -1.30 below the unchanged 1.32 m wrist pivot.
  const outline = [[-.09, .06], [.12, .02], [.17, -.24], [.22, -.49], [.21, -.58],
    [.17, -.60], [.21, -.64], [.18, -.86], [.09, -1.08], [-.04, -1.23],
    [-.20, -1.30], [-.12, -1.10], [-.07, -.88], [-.10, -.70],
    [-.065, -.67], [-.12, -.62], [-.14, -.38]];
  const bladePieces = [[executorSheet(outline.map(([x, y]) => [x * side, y]), .085, [0, 0, 0]), 'iron'],
    [executorSheet([[.13, -.22], [.18, -.48], [.17, -.57], [.135, -.60], [.17, -.65],
      [.14, -.85], [.055, -1.06], [-.065, -1.21], [-.20, -1.30], [-.04, -1.13],
      [.09, -.82], [.105, -.47]].map(([x, y]) => [x * side, y]), .09, [0, 0, 0]), 'edge']];
  executorPath(bladePieces, [[-side * .15, .005, 0], [-side * .07, -.07, .02],
    [side * .08, -.075, .02], [side * .19, -.015, 0]], .037, .028, 'rust', 6);
  executorPath(bladePieces, [[-side * .025, -.12, -.052], [side * .02, -.42, -.052],
    [side * .055, -.73, -.052], [0, -1.02, -.045]], .025, .011, 'rust');
  for (let i = 0; i < 5; i++) {
    const y = -.20 - i * .17, x = side * (.01 + Math.sin(i * .7) * .04);
    bladePieces.push([pebble([x, y, .052], [.016, .019, .013]), 'rust']);
    if (i % 2 === 0) executorPath(bladePieces, [[x, y, .047], [x - side * .035, y - .06, .047],
      [x - side * .025, y - .10, .047]], .004, .002, 'oil', 4);
  }
  scrap(blade, label + '-hooked-notched-execution-blade', bladePieces, metal);
  return { shoulder, elbow, blade };
}

export function createExecutor() {
  const { metal, cloth } = materials();
  const root = pivot(null, 'broken-executor');
  const pelvis = pivot(root, 'pelvis', [0, 1.05, 0]);
  const yoke = [[rod([-.25, 0, 0], [.25, 0, 0], .10, .10, 8), 'oil'],
    [pebble([0, .015, -.015], [.265, .14, .18]), 'iron']];
  for (const s of [-1, 1]) {
    yoke.push([executorShingle([s * .15, -.045, .13], .31, .30, .045,
      [.12, s * .32, -s * .13]), 'iron']);
    executorPath(yoke, [[s * .25, .04, .03], [s * .29, -.12, .06],
      [s * .23, -.23, .12]], .028, .018, 'rust');
  }
  yoke.push([executorShingle([0, .035, .205], .15, .11, .018, [0, 0, .08]), 'rust']);
  scrap(pelvis, 'overlapping-armoured-pelvic-yoke', yoke, metal);
  const torso = pivot(pelvis, 'torso', [0, .25, 0]);
  const armour = [
    // An opaque inner vessel backs the front shingles. It does not seal the rear aperture.
    [pebble([0, .56, .065], [.365, .48, .235], 1), 'oil'],
    [rod([0, .53, -.245], [0, .53, -.265], .225, .225, 10), 'oil'],
  ];
  executorPath(armour, [[0, -.16, -.03], [0, .15, -.09], [0, .55, -.08],
    [0, .91, .035], [0, 1.04, .15]], .085, .064, 'iron', 6);
  // Broad curved breast, tilted forward over a narrow waist, with overlapping lames.
  for (let row = 0; row < 4; row++) {
    const y = .26 + row * .19, width = .49 + row * .095;
    armour.push([executorShingle([row % 2 ? .009 : -.012, y, .23 + row * .018],
      width, .29, .085, [-.10 - row * .035, 0, row % 2 ? .025 : -.03], .035),
    row === 1 ? 'rust' : 'iron']);
    for (const s of [-1, 1]) {
      armour.push([pebble([s * width * .35, y + .06, .28 + row * .024], [.018, .018, .018]), 'rust']);
      executorPath(armour, [[s * width * .30, y + .065, .32 + row * .02],
        [s * width * .29, y - .02, .33 + row * .02],
        [s * width * .25, y - .07, .325 + row * .02]], .005, .003, 'oil', 4);
    }
  }
  for (const s of [-1, 1]) {
    // Bent ribs curl around the flanks, then fracture away from the rear core.
    for (let row = 0; row < 4; row++) {
      const y = .26 + row * .16, w = .27 + Math.sin(row / 3 * Math.PI) * .075;
      executorPath(armour, [[s * .16, y, .17], [s * w, y + .06, .05],
        [s * (w + .015), y + .045, -.20], [s * .24, y, -.34],
        [s * .17, y - .045, -.43]], .033, .013, row % 2 ? 'rust' : 'iron');
      armour.push([executorSheet([[-.065, .09], [.06, .07], [.075, -.05],
        [.025, -.02], [.035, -.12], [-.045, -.07]], .025,
      [s * .28, y, -.29], [.13, -s * .70, s * .16]), 'iron']);
    }
    // The exhausts bend away from the neck. Their ends are soot-dark, not glowing.
    const tip = [s * .32, s < 0 ? 1.29 : 1.23, -.39];
    executorPath(armour, [[s * .25, .40, -.27], [s * .32, .69, -.34],
      [s * .30, 1.02, -.39], tip], .053, .040, 'rust', 7);
    armour.push([fieldHoop([0, 0, 0], .045, .011).rotateX(Math.PI / 2)
      .translate(...tip), 'iron']);
    armour.push([pebble(tip, [.032, .007, .032]), 'oil']);
    for (let i = 0; i < 2; i++) armour.push([fieldHoop([0, 0, 0], .056, .012)
      .rotateX(Math.PI / 2).translate(s * .31, .75 + i * .23, -.36), 'oil']);
    executorPath(armour, [[s * .19, .77, -.35], [s * .23, .64, -.44],
      [s * .21, .36, -.45], [s * .11, .22, -.35]], .016, .010, 'oil');
  }
  // Rim and side walls recess the core into a rear-facing cup; front armour occludes it.
  armour.push([new THREE.CylinderGeometry(.19, .19, .18, 10, 1, true)
    .rotateX(Math.PI / 2).scale(1, 1.22, 1).translate(0, .53, -.34), 'oil']);
  armour.push([fieldHoop([0, .53, -.39], .20, .027, Math.PI * 1.78), 'rust']);
  executorPath(armour, [[-.05, .72, -.43], [.01, .59, -.442], [-.02, .52, -.443]], .010, .006, 'oil', 4);
  executorPath(armour, [[.035, .48, -.442], [-.012, .40, -.435], [.025, .35, -.425]], .006, .011, 'oil', 4);
  scrap(torso, 'bowed-breastplate-bent-ribs-open-core-cup-and-stacks', armour, metal);
  const core = glow(torso, 'exposed-back-core', [0, .53, -.36], [.125, .17, .075], [1.5, .45, 2.4]);

  const trophies = [];
  for (let i = 0; i < 11; i++) {
    const u = i / 10;
    trophies.push([executorLink([-.30 + u * .60, .73 - Math.sin(u * Math.PI) * .19,
      .365 + Math.sin(u * Math.PI) * .035], .032, i % 2 * Math.PI / 2), i % 3 ? 'rust' : 'iron']);
  }
  for (const s of [-1, 1]) {
    for (let i = 0; i < 4; i++) trophies.push([executorLink([s * (.22 + i * .008),
      .47 - i * .052, .36 + i * .012], .026, i % 2 * Math.PI / 2), 'oil']);
    const x = s * .25, y = s < 0 ? .13 : .18;
    // Small long-muzzled hound skulls: dead sockets, bent ears and broken fangs.
    trophies.push([pebble([x, y, .38], [.077, .085, .052]), 'bone']);
    trophies.push([executorShingle([x, y - .05, .415], .11, .18, .025, [-.25, s * .16, s * .20], .015), 'iron']);
    for (const ear of [-1, 1]) {
      trophies.push([executorSheet([[-.024, 0], [.021, .005], [ear * .024, .09]], .012,
        [x + ear * .055, y + .04, .38], [-.25, 0, ear * .25]), 'rust']);
      trophies.push([pebble([x + ear * .037, y + .01, .427], [.021, .014, .010]), 'oil']);
      trophies.push([rod([x + ear * .028, y - .09, .44], [x + ear * .020, y - .14, .45], .012, .002, 4), 'edge']);
    }
    trophies.push([executorSheet([[-.04, .04], [.04, .03], [.032, -.06], [0, -.035], [-.02, -.08]],
      .012, [s * .34, .32, .32], [.2, s * .35, -.2]), 'rust']);
  }
  scrap(torso, 'execution-chain-hound-skulls-and-torn-trophies', trophies, metal);

  const head = pivot(torso, 'head', [0, 1.12, .025]);
  const helm = [[pebble([0, -.045, .005], [.145, .17, .135]), 'oil'],
    [executorShingle([0, .105, .135], .31, .16, .035, [-.18, 0, 0]), 'iron'],
    [executorShingle([0, -.105, .145], .29, .23, .045, [.12, 0, .04]), 'iron'],
    [executorSheet([[-.16, .045], [-.09, .08], [.10, .068], [.16, .04], [.10, .047],
      [-.08, .052]], .032, [0, .012, .178], [0, 0, -.02]), 'rust']];
  for (const s of [-1, 1]) {
    helm.push([executorShingle([s * .128, -.055, .09], .13, .24, .025, [0, s * .8, -s * .18]), 'rust']);
    executorPath(helm, [[s * .11, .11, -.025], [s * .19, .19, -.06],
      [s * .22, .265 - (s > 0 ? .035 : 0), -.12], [s * .17, .245, -.18]], .031, .003, 'edge');
    executorPath(helm, [[s * .09, -.05, .185], [s * .08, -.13, .19],
      [s * .045, -.20, .18]], .006, .003, 'oil', 4);
  }
  helm.push([executorSheet([[-.10, .06], [-.08, .18], [.01, .23], [.09, .12], [.08, -.03]],
    .027, [0, 0, -.015], [0, Math.PI / 2, 0]), 'iron']);
  scrap(head, 'sunken-horned-helm-slit-and-bevor', helm, metal);
  const eye = glow(head, 'single-lens', [-.035, .025, .19], [.085, .012, .018], [1.5, .28, .08]);
  const armL = executorArm(torso, metal, -1, 'left');
  const armR = executorArm(torso, metal, 1, 'right');
  const legL = executorLeg(pelvis, metal, -1, 'left');
  const legR = executorLeg(pelvis, metal, 1, 'right');
  const mantle = [];
  for (const s of [-1, 1]) {
    mantle.push([rag(0, 0, 0, .28, .48, s * .025).rotateX(.85).rotateZ(-s * .45)
      .translate(s * .27, 1.12, -.10), 'cloth']);
    mantle.push([rag(s * .42, .90, -.22, .18, .79, s * .06), 'cloth']);
    mantle.push([rag(s * .33, .49, -.27, .085, .63, s * .09), 'ashCloth']);
  }
  scrap(torso, 'torn-shoulder-mantle-and-trailing-strips', mantle, cloth);
  scrap(head, 'split-executioner-hood', [
    [executorSheet([[-.195, -.12], [-.19, .13], [-.10, .20], [.10, .19], [.19, .11],
      [.19, -.14], [.14, -.10], [.14, .10], [.06, .145], [-.08, .14], [-.145, .08], [-.145, -.10]],
    .13, [0, 0, -.025]), 'cloth'],
    [rag(-.13, .09, -.10, .10, .30, -.025), 'cloth'],
    [rag(.13, .08, -.10, .085, .26, .035), 'cloth'],
  ], cloth);
  scrap(pelvis, 'long-ragged-split-tabard-and-loincloth', [
    [rag(-.065, -.09, .23, .19, .82, -.035), 'cloth'],
    [rag(.075, -.10, .24, .15, .70, .055), 'cloth'],
    [rag(-.16, -.06, -.16, .14, .56, -.05), 'cloth'],
    [rag(.17, -.08, -.17, .11, .48, .04), 'ashCloth'],
  ], cloth);
  // Sink the helm forward between the pauldrons without moving any rig pivot.
  head.traverse(node => { if (node.isMesh) node.geometry.translate(0, -.08, .14); });
  return { root, radius: .95, height: 2.7, parts: { pelvis, torso, head, eye, core, armL, armR, legL, legR } };
}

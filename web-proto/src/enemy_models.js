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

/** Warden of Ash: a walking furnace held together by the ruins it has consumed. */
export function createAshWarden() {
  const { metal, cloth } = materials();
  const concrete = metal.clone();
  concrete.roughness = 1;
  concrete.metalness = .04;
  const root = pivot(null, 'ash-warden');
  // Warden joint offsets baked at 1.6x; all pivots retain identity rotation/scale.
  const pelvis = pivot(root, 'pelvis', [0, 1.68, 0]);
  const torso = pivot(pelvis, 'torso', [0, .40, 0]);
  scrap(pelvis, 'ash-pelvic-frame', [
    [rod([-.40, 0, 0], [.40, 0, 0], .10), 'iron'],
    [box([.48, .19, .32], [0, .055, -.055], [0, 0, .07]), 'rust'],
    [rod([-.30, .10, .20], [.28, .06, .20], .035), 'edge'],
  ], metal);
  scrap(pelvis, 'broken-foundation-apron', [
    [plate([[-.34, .14], [-.04, .12], [-.075, -.14], [-.20, -.24], [-.33, -.13]], .15, .18), 'concrete'],
    [plate([[.025, .10], [.33, .16], [.37, -.04], [.25, -.20], [.09, -.15]], .13, .15), 'ash'],
  ], concrete);

  const frame = [
    [rod([0, -.34, -.23], [0, 1.43, -.25], .075), 'iron'],
    [rod([-.14, -.28, -.12], [-.23, 1.30, -.19], .035), 'rust'],
    [rod([.14, -.28, -.12], [.22, 1.30, -.19], .035), 'rust'],
    [rod([-.66, 1.28, 0], [.66, 1.28, 0], .075), 'iron'],
    [rod([0, 1.36, -.19], [0, 1.75, .015], .07), 'edge'],
  ];
  for (let row = 0; row < 3; row++) {
    const y = .30 + row * .30;
    for (const side of [-1, 1]) {
      frame.push([rod([side * .10, y, -.25], [side * .40, y + .07, -.10], .033), 'rust']);
      frame.push([rod([side * .40, y + .07, -.10], [side * .35, y -.04, .25], .032), 'iron']);
    }
  }
  scrap(torso, 'furnace-rebar-frame', frame, metal);
  const core = glow(torso, 'captive-soul', [0, .736, .10], [.27, .40, .25], [5.2, .38, .055]);
  // Separate slabs leave a jagged central fissure and an open top over the ember.
  scrap(torso, 'split-concrete-furnace', [
    [plate([[-.49, 1.19], [-.18, 1.26], [-.055, 1.08], [-.12, .87], [-.055, .70],
      [-.13, .56], [-.08, .33], [-.30, .20], [-.46, .38]], .19, .32), 'concrete'],
    [plate([[.12, 1.16], [.40, 1.22], [.51, 1.02], [.43, .78], [.46, .40],
      [.23, .23], [.06, .37], [.13, .59], [.07, .77], [.16, .96]], .17, .34), 'ash'],
    [plate([[-.35, 1.21], [-.43, 1.00], [-.34, .31], [-.06, .20], [.31, .29],
      [.42, .80], [.32, 1.19], [.08, 1.27]], .16, -.31), 'ash'],
    [pebble([-.30, 1.25, -.18], [.22, .12, .23]), 'concrete'],
    [pebble([.31, 1.23, -.12], [.20, .10, .20]), 'concrete'],
  ], concrete);
  const straps = [
    [box([.77, .09, .12], [0, .27, .28], [0, .04, -.06]), 'rust'],
    [plate([[-.47, 1.10], [-.30, 1.16], [-.24, .97], [-.40, .85]], .035, .43), 'rust'],
    [plate([[.26, .51], [.44, .55], [.40, .37], [.21, .30]], .035, .435), 'iron'],
    [rod([-.23, 1.24, -.08], [-.29, 1.47, -.04], .024), 'rust'],
    [rod([-.29, 1.47, -.04], [-.41, 1.49, .025], .021, .012), 'rust'],
    [rod([.29, 1.20, -.11], [.37, 1.40, -.18], .024, .015), 'iron'],
    // Charred fissures and rust runs sit on the slab faces, never on the glow.
    [plate([[-.34, .96], [-.30, .96], [-.26, .71], [-.28, .54]], .006, .419), 'oil'],
    [plate([[.32, 1.08], [.36, 1.07], [.31, .79], [.29, .69]], .006, .429), 'rust'],
  ];
  for (const side of [-1, 1]) for (const y of [.32, 1.06]) {
    straps.push([rod([side * .34, y, .42], [side * .34, y, .46], .037, .037, 6), 'edge']);
  }
  scrap(torso, 'furnace-straps-and-scorch', straps, metal);

  const head = pivot(torso, 'head', [0, 1.792, .04]);
  scrap(head, 'sunken-furnace-mask', [
    [pebble([0, -.025, -.025], [.26, .24, .22]), 'oil'],
    [plate([[-.27, .12], [-.11, .19], [.24, .13], [.29, -.04], [.17, -.27],
      [.035, -.32], [-.22, -.23]], .09, .20), 'concrete'],
    [plate([[-.29, .115], [-.12, .18], [.25, .12], [.24, .035], [-.23, .015]], .08, .26), 'iron'],
    [plate([[.04, -.10], [.21, -.07], [.16, -.25], [.06, -.29]], .055, .26), 'rust'],
    [rod([-.035, -.065, .24], [-.035, -.065, .30], .093, .08, 8), 'oil'],
    [rod([-.18, -.20, .25], [-.13, -.09, .25], .012), 'oil'],
  ], metal);
  const eye = glow(head, 'single-lens', [-.035, -.065, .32], [.061, .049, .032], [4.3, .65, .10]);
  const crown = [];
  // A low, broken rooftop aerial crown fits below y=4.32, rather than adding height.
  for (let i = 0; i < 5; i++) {
    const x = (i - 2) * .12, top = [.31, .42, .34, .39, .27][i];
    crown.push([rod([x, .10, -.09], [x - .025, top - .06, -.12], .027, .022, 5), 'rust']);
    crown.push([rod([x - .025, top - .06, -.12], [x + .045, top, -.16], .022, .011, 5), 'iron']);
  }
  crown.push([rod([-.25, .23, -.11], [.25, .20, -.11], .022, .019, 5), 'rust']);
  crown.push([plate([[-.24, .11], [-.18, .28], [-.10, .19], [-.06, .10]], .08, -.02), 'concrete']);
  scrap(head, 'broken-rebar-and-aerial-crown', crown, metal);

  // Each bearing is a removable arm-owned assembly: two armour collars with
  // a hot exposed gap. Upper arms and rags begin below it, leaving the top visible.
  function joint(parent, name, radius) {
    scrap(parent, `${name}-split-bearing`, [
      [rod([-radius, 0, 0], [-radius * .38, 0, 0], radius, radius, 8), 'iron'],
      [rod([radius * .38, 0, 0], [radius, 0, 0], radius, radius, 8), 'rust'],
      [rod([-radius * 1.08, 0, 0], [radius * 1.08, 0, 0], radius * .32, radius * .32, 6), 'oil'],
    ], metal);
    glow(parent, `${name}-hot-seam`, [0, 0, 0], [radius * .42, radius * .91, radius * .91], [2.8, .22, .035]);
  }
  function arm(side, label) {
    const shoulder = pivot(torso, `${label}-shoulder`, [side * .656, 1.28, 0]);
    const elbow = pivot(shoulder, `${label}-elbow`, [side * .192, -.688, .024]);
    const blade = pivot(elbow, `${label}-blade`, [side * .128, -.56, .072]);
    joint(shoulder, `${label}-shoulder`, .22);
    joint(elbow, `${label}-elbow`, .16);
    scrap(shoulder, `${label}-upper-arm`, [
      [rod([0, -.14, 0], [side * .192, -.60, .024], .062), 'iron'],
      [rod([side * .12, -.20, -.09], [side * .25, -.55, -.07], .037, .028), 'rust'],
      [box([.28, .09, .29], [side * .10, -.30, .015], [0, .12, side * .22]), 'rust'],
    ], metal);
    const shell = plate([[-.17, -.23], [.14, -.20], [.24, -.32], [.20, -.43],
      [.05, -.48], [-.12, -.41]].map(([x, y]) => [side * x, y]), .30);
    shell.rotateZ(side * .16).translate(side * .10, 0, 0);
    scrap(shoulder, `${label}-chipped-upper-arm-shell`, [[shell, side < 0 ? 'concrete' : 'ash']], concrete);
    scrap(elbow, `${label}-forearm`, [
      [rod([0, -.10, 0], [side * .128, -.56, .072], .057, .045), 'iron'],
      [rod([-side * .075, -.16, .08], [side * .04, -.43, .13], .026, .019), 'edge'],
      [plate([[-.13, -.19], [.12, -.17], [.22, -.38], [.12, -.49], [-.10, -.42]], .18, -.015), 'rust'],
      [box([.25, .065, .23], [side * .09, -.43, .04], [0, 0, side * .15]), 'iron'],
    ], metal);
    // Long salvaged shear blades preserve the warden's reach inside the 1.52 m radius.
    const outline = [[-.12, .04], [.13, -.04], [.17, -.48], [.29, -.91], [.28, -1.08],
      [.45, -1.48], [.46, -1.70], [.20, -1.39], [-.07, -.73], [-.14, -.21]];
    const edge = [[.17, -.48], [.29, -.91], [.28, -1.08], [.45, -1.48], [.46, -1.70],
      [.36, -1.45], [.19, -1.03], [.21, -.89], [.11, -.51]];
    scrap(blade, `${label}-ruin-shear`, [
      [plate(outline.map(([x, y]) => [side * x, y]), .075), 'rust'],
      [plate(edge.map(([x, y]) => [side * x, y]), .082, .003), 'edge'],
      [box([.28, .10, .14], [side * .015, -.17, .015], [0, 0, side * .13]), 'iron'],
      [rod([side * .02, -.24, .065], [side * .12, -.74, .065], .032, .022), 'oil'],
      [rod([side * .06, -.35, .075], [side * .06, -.35, .10], .039, .039, 6), 'edge'],
    ], metal);
    scrap(shoulder, `${label}-ash-arm-rag`, [
      [rag(-side * .10, -.26, -.18, .18, side < 0 ? .40 : .30, -side * .04), 'ashCloth'],
    ], cloth);
    return { shoulder, elbow, blade };
  }
  function leg(side, label) {
    const hip = pivot(pelvis, `${label}-hip`, [side * .32, 0, 0]);
    const knee = pivot(hip, `${label}-knee`, [side * .072, -.704, .224]);
    const ankle = pivot(knee, `${label}-ankle`, [side * .04, -.592, -.496]);
    scrap(hip, `${label}-reinforced-thigh`, [
      [rod([-.10, 0, 0], [.10, 0, 0], .12, .12, 8), 'oil'],
      [rod([0, -.06, 0], [side * .072, -.704, .224], .065), 'iron'],
      [rod([side * .11, -.11, -.06], [side * .15, -.53, .11], .055, .042), 'rust'],
      [box([.27, .085, .28], [side * .055, -.30, .07], [.25, 0, side * .10]), 'iron'],
    ], metal);
    scrap(hip, `${label}-thigh-slab`, [
      [plate([[-.13, -.18], [.13, -.14], [.17, -.34], [.07, -.56], [-.12, -.48]], .16, .16), 'ash'],
    ], concrete);
    scrap(knee, `${label}-reverse-shin`, [
      [rod([-.13, 0, 0], [.13, 0, 0], .13, .13, 8), 'rust'],
      [rod([0, -.05, -.04], [side * .04, -.592, -.496], .055), 'iron'],
      [rod([-side * .09, -.07, -.03], [-side * .04, -.40, -.30], .05), 'oil'],
      [rod([-side * .04, -.40, -.30], [-side * .03, -.58, -.48], .024), 'edge'],
      [plate([[-.16, .08], [.14, .06], [.18, -.10], [.05, -.20], [-.12, -.14]], .09, .13), 'concrete'],
    ], metal);
    const foot = [
      [rod([-.09, 0, 0], [.09, 0, 0], .09, .09, 6), 'iron'],
      [rod([0, -.015, 0], [0, -.28, .16], .065, .045), 'rust'],
      [box([.29, .09, .31], [0, -.29, .20], [.10, 0, side * .07]), 'iron'],
    ];
    for (const toe of [-1, 1]) {
      foot.push([rod([toe * .06, -.27, .15], [toe * .13, -.34, .43], .042, .025), 'iron']);
      const claw = plate([[-.09, -.30], [.15, -.384], [.12, -.27], [-.045, -.23]], .075);
      claw.rotateY(Math.PI / 2).translate(toe * .13, 0, .40);
      foot.push([claw, 'edge']);
    }
    scrap(ankle, `${label}-rubble-crushing-foot`, foot, metal);
    return { hip, knee, ankle };
  }
  const armL = arm(-1, 'left'), armR = arm(1, 'right');
  const legL = leg(-1, 'left'), legR = leg(1, 'right');
  scrap(torso, 'ash-mantle', [
    [rag(-.30, 1.19, -.43, .30, 1.10, -.10), 'ashCloth'],
    [rag(.26, 1.18, -.42, .26, .84, .12), 'ashCloth'],
    [rag(-.04, 1.10, -.45, .24, 1.30, .08), 'ash'],
  ], cloth);
  scrap(pelvis, 'ash-hip-rags', [
    [rag(-.27, .06, .10, .25, .73, -.09), 'ashCloth'],
    [rag(.28, .08, -.05, .21, .91, .10), 'ashCloth'],
    [rag(0, .02, -.25, .29, .64, -.07), 'ash'],
  ], cloth);
  return {
    root, radius: 1.52, height: 4.32,
    parts: { pelvis, torso, head, eye, core, armL, armR, legL, legR },
  };
}

import * as THREE from 'three';
import { createRig } from './rig.js';

const palette = { steel: 0x777c74, edge: 0xaaa796, dark: 0x696357, leather: 0x9b896a, trim: 0x8f8879, canvas: 0xc5b694, lining: 0x938674, hide: 0x605347, void: 0x11120f, joint: 0x393c39, rust: 0x906044, stitch: 0x554a3a };
export function painted(geometry, color, roughness = .85, metalness = .12) {
  if (geometry.index) geometry = geometry.toNonIndexed();
  if (!geometry.attributes.normal) geometry.computeVertexNormals();
  geometry.computeBoundingBox();
  const p = geometry.attributes.position, n = geometry.attributes.normal;
  const colors = new Float32Array(p.count * 3), base = new THREE.Color(color);
  const bounds = geometry.boundingBox, height = Math.max(.01, bounds.max.y - bounds.min.y);
  const width = Math.max(.01, bounds.max.x - bounds.min.x), centerX = (bounds.min.x + bounds.max.x) * .5;
  for (let i = 0; i < p.count; i++) {
    const top = (p.getY(i) - bounds.min.y) / height;
    const outside = Math.min(1, Math.abs(p.getX(i) - centerX) * 2 / width);
    const brush = .018 * Math.sin(Math.floor(i / 3) * 17.13);
    const shade = .68 + .24 * top + .07 * outside + .12 * Math.max(0, n.getY(i)) + brush;
    colors[i * 3] = base.r * shade; colors[i * 3 + 1] = base.g * shade; colors[i * 3 + 2] = base.b * shade;
  }
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  const material = new THREE.MeshStandardMaterial({ vertexColors: true, flatShading: true, roughness, metalness });
  // Restrained view-dependent edge lift, tinted by the painted vertex colors.
  // Keep the ordinary emissive channel free for the existing weapon charge VFX.
  material.onBeforeCompile = shader => {
    shader.fragmentShader = shader.fragmentShader.replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
      float clothRim = pow(1.0 - clamp(dot(normal, normalize(vViewPosition)), 0.0, 1.0), 3.0);
      totalEmissiveRadiance += diffuseColor.rgb * (0.025 + 0.16 * clothRim);`);
  };
  material.customProgramCacheKey = () => 'painted-edge-lift-v1';
  const mesh = new THREE.Mesh(geometry, material);
  mesh.castShadow = true; mesh.receiveShadow = true; return mesh;
}

// Bevels use real piece dimensions, so even narrow trims have substantial edges.
function chamferedBox([width, height, depth]) {
  const bevel = Math.min(width, height, depth) * .18;
  const x = width / 2 - bevel, y = height / 2 - bevel;
  const shape = new THREE.Shape();
  shape.moveTo(-x, -y); shape.lineTo(x, -y); shape.lineTo(x, y); shape.lineTo(-x, y); shape.closePath();
  const geometry = new THREE.ExtrudeGeometry(shape, { depth: depth - 2 * bevel, bevelEnabled: true, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 1, steps: 1, curveSegments: 1 });
  geometry.translate(0, 0, -depth / 2 + bevel); return geometry;
}

// Closed polygon loops form cloth shells with real thickness at their openings.
function loft(loops, cap = false, closed = true) {
  const vertices = loops.flat(2), indices = [], count = loops[0].length;
  for (let row = 0; row < loops.length - 1; row++) for (let i = 0; i < count - (closed ? 0 : 1); i++) {
    const a = row * count + i, b = row * count + (i + 1) % count;
    indices.push(a, b, a + count, b, b + count, a + count);
  }
  if (cap) {
    const last = (loops.length - 1) * count;
    for (let i = 1; i < count - 1; i++) indices.push(last, last + i, last + i + 1);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3)); geometry.setIndex(indices); geometry.computeVertexNormals(); return geometry;
}
function hoodLoop(width, height, centerY, z) {
  return Array.from({ length: 20 }, (_, i) => {
    const angle = i * Math.PI * 2 / 20, crown = Math.cos(angle);
    // Broad rounded crown, slack cheeks and a softly sagging brow: no apex.
    const sag = z > .18 ? .026 * Math.max(0, crown) ** 6 : 0;
    return [Math.sin(angle) * width + .009 * crown, centerY + crown * height - sag + .008 * Math.sin(angle * 3), z + .012 * Math.sin(angle * 3)];
  });
}

function robeLoop(width, depth, y) {
  return Array.from({ length: 16 }, (_, i) => {
    const a = i * Math.PI * 2 / 16;
    const fold = 1 + .055 * Math.cos(a * 6 + y * 2) + .025 * Math.sin(a * 3);
    return [Math.sin(a) * width * fold, y, Math.cos(a) * depth * fold];
  });
}
function tornLoop(width, depth, y) {
  return robeLoop(width, depth, y).map(([x, h, z], i) => [x, h + [0, -.022, .009, -.038, -.008, .014, -.027][i % 7], z]);
}
function weatherCanvas(mesh) {
  const p = mesh.geometry.attributes.position, colors = mesh.geometry.attributes.color;
  for (let i = 0; i < p.count; i++) {
    const stain = Math.sin(p.getX(i) * 31 + p.getY(i) * 17) * Math.cos(p.getZ(i) * 27 - p.getY(i) * 21);
    const shade = .92 + .08 * stain;
    colors.setXYZ(i, colors.getX(i) * shade, colors.getY(i) * shade, colors.getZ(i) * shade);
  }
  return mesh;
}

// Wear belongs to the shell vertices, not floating decals. Edge chips expose
// oxidized metal; a few broad, deterministic stains interrupt the faded paint.
function weatherShell(mesh) {
  const geo = mesh.geometry, p = geo.attributes.position, colors = geo.attributes.color;
  geo.computeBoundingBox(); const bounds = geo.boundingBox;
  const size = bounds.getSize(new THREE.Vector3()), center = bounds.getCenter(new THREE.Vector3());
  const rust = new THREE.Color(palette.rust), metal = new THREE.Color(palette.joint), c = new THREE.Color();
  for (let i = 0; i < p.count; i++) {
    const x = (p.getX(i) - center.x) / Math.max(.001, size.x) * 2;
    const y = (p.getY(i) - center.y) / Math.max(.001, size.y) * 2;
    const z = (p.getZ(i) - center.z) / Math.max(.001, size.z) * 2;
    const edge = Math.abs(x) + Math.abs(y) + Math.abs(z) > 2.3;
    const stain = Math.sin(x * 8 + y * 5 - z * 3) * Math.cos(y * 11 + z * 6);
    c.fromBufferAttribute(colors, i);
    if (stain > .55) c.lerp(rust, .72);
    else if (edge && stain > -.2) c.lerp(metal, .52);
    colors.setXYZ(i, c.r, c.g, c.b);
  }
  return mesh;
}

export function createCharacter() {
  const rig = createRig(), j = rig.joints;
  function attach(parent, name, geometry, color, cloth = false) {
    const mesh = painted(geometry, palette[color] ?? color, cloth ? .96 : .7, cloth ? 0 : .25);
    if (!cloth && ['steel', 'edge'].includes(color)) weatherShell(mesh);
    mesh.name = name;
    if (cloth) {
      mesh.material.side = THREE.DoubleSide;
      weatherCanvas(mesh);
    }
    parent.add(mesh); return mesh;
  }
  function piece(parent, name, kind, scale, pos, color, rotation = [0, 0, 0]) {
    const geo = kind === 'box' ? chamferedBox(scale) : kind === 'cylinder' ? new THREE.CylinderGeometry(.5, .58, 1, 8) : new THREE.IcosahedronGeometry(.5, 1);
    const mesh = attach(parent, name, geo, color, ['dark', 'leather', 'trim', 'canvas', 'lining', 'hide'].includes(color));
    if (kind !== 'box') mesh.scale.fromArray(scale);
    mesh.position.fromArray(pos); mesh.rotation.set(...rotation); return mesh;
  }
  function jointBearing(parent, name, diameter) {
    piece(parent, `${name}-ball`, 'sphere', [diameter, diameter, diameter], [0, 0, 0], 'joint');
    const ring = attach(parent, `${name}-bearing`, new THREE.TorusGeometry(diameter * .43, .014, 4, 10), 'joint');
    ring.rotation.y = Math.PI / 2;
  }
  function patch(parent, name, size, pos, color, rotation = [0, 0, 0]) {
    const panel = new THREE.Group(); panel.name = name; panel.position.fromArray(pos); panel.rotation.set(...rotation); parent.add(panel);
    const shape = new THREE.Shape();
    shape.moveTo(-size[0] * .5, -size[1] * .41); shape.lineTo(size[0] * .43, -size[1] * .5);
    shape.lineTo(size[0] * .5, size[1] * .37); shape.lineTo(-size[0] * .44, size[1] * .5); shape.closePath();
    attach(panel, `${name}-cloth`, new THREE.ShapeGeometry(shape), color, true);
    for (const sign of [-1, 1]) for (let i = 0; i < 4; i++) {
      piece(panel, `${name}-stitch-${sign}-${i}`, 'box', [.014, .004, .004], [(i / 3 - .5) * size[0] * .76, sign * size[1] * (.36 + .03 * Math.sin(i * 4)), .006], 'stitch', [0, 0, .3 + i * .17]);
    }
  }
  function bandage(parent, name, pos, scale) {
    for (let i = 0; i < 2; i++) piece(parent, `${name}-${i}`, 'cylinder', [scale[0], .033, scale[1]], [0, pos + i * .035, 0], 'leather', [0, 0, i ? -.06 : .06]);
  }
  function rippedShell(parent, name, loops, color) {
    // +X is the robot's RIGHT. Remove a whole side sector, through every
    // layer; the two irregular open boundaries must never be stitched closed.
    const count = loops[0].length, first = Math.floor(count / 8), last = Math.ceil(count * 3 / 8);
    const open = loops.map((loop, row) => Array.from({ length: count - last + first + 1 }, (_, k) => {
      const p = [...loop[(last + k) % count]];
      if (k === 0 || k === count - last + first) {
        p[0] += [0, -.027, .016, -.018][row % 4];
        p[1] += [0, -.018, .011, -.024][row % 4];
      }
      return p;
    }));
    attach(parent, name, loft(open, false, false), color, true);
    for (let row = 1; row < open.length; row++) for (const edge of [0, open[0].length - 1]) {
      const p = new THREE.Vector3(...open[row][edge]);
      for (let thread = 0; thread < 2; thread++) {
        const start = p.clone().add(new THREE.Vector3(0, thread * .012, 0));
        const end = start.clone().add(new THREE.Vector3(.012 + thread * .008, -.026 - row * .005, edge ? .008 : -.008));
        attach(parent, `${name}-broken-thread`, new THREE.TubeGeometry(new THREE.LineCurve3(start, end), 1, .0017, 3, false), 'lining', true);
      }
    }
  }
  // Slack overlapping under-robes preserve the rig's slim, unchanged lengths.
  rippedShell(j.pelvis, 'robe-hip-shell', [robeLoop(.27, .195, .15), robeLoop(.29, .215, .015), robeLoop(.29, .21, -.145)], 'canvas');
  rippedShell(j.spine, 'robe-waist-shell', [robeLoop(.25, .18, .20), robeLoop(.235, .18, .025), robeLoop(.25, .18, -.19)], 'lining');
  rippedShell(j.chest, 'loose-robe-chest', [robeLoop(.26, .18, .18), robeLoop(.33, .22, .065), robeLoop(.32, .22, -.115), tornLoop(.26, .20, -.25)], 'canvas');
  piece(j.chest, 'exposed-right-rib-core', 'box', [.12, .36, .24], [.205, -.035, 0], 'joint');
  for (let i = 0; i < 3; i++) piece(j.chest, `exposed-right-rib-plate-${i}`, 'box', [.11, .082, .27], [.235, .075 - i * .10, 0], 'steel', [0, 0, -.10]);
  piece(j.spine, 'exposed-right-waist-actuator', 'cylinder', [.13, .29, .14], [.19, -.015, 0], 'joint');
  piece(j.pelvis, 'exposed-right-hip-plate', 'box', [.105, .21, .25], [.23, .015, 0], 'steel');
  patch(j.chest, 'rough-chest-repair', [.115, .15], [-.12, -.13, .213], 'lining', [0, -.1, .15]);
  // Two uneven rope turns, tied off to one side.
  for (let strand = 0; strand < 2; strand++) {
    const points = Array.from({ length: 49 }, (_, i) => {
      const a = i / 48 * Math.PI * 2;
      return new THREE.Vector3(Math.sin(a) * .302, .055 + strand * .023 + .013 * Math.sin(a + .4), Math.cos(a) * .229);
    });
    attach(j.pelvis, 'twisted-rope-belt', new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points), 48, .009, 5, false), 'leather', true);
  }
  piece(j.pelvis, 'rope-knot', 'sphere', [.052, .045, .036], [-.12, .065, .223], 'leather');
  for (const sign of [-1, 1]) {
    const points = [new THREE.Vector3(-.12, .06, .23), new THREE.Vector3(-.12 + sign * .025, -.025, .245), new THREE.Vector3(-.13 + sign * .035, -.15 + sign * .02, .24)];
    attach(j.pelvis, 'rope-loose-end', new THREE.TubeGeometry(new THREE.CatmullRomCurve3(points), 10, .007, 5, false), 'leather', true);
  }
  for (const s of [-1, 1]) {
    piece(j.pelvis, 'belt-pouch', 'box', [.10, .13, .06], [s * .22, .025, .15], 'hide', [0, s * .5, 0]);
    piece(j.pelvis, 'pouch-flap', 'box', [.102, .045, .016], [s * .235, .071, .18], 'leather', [0, s * .5, 0]);
  }
  const strapVertices = [], strapIndices = [];
  for (let row = 0; row <= 8; row++) {
    const t = row / 8, y = THREE.MathUtils.lerp(.145, -.205, t);
    for (const edge of [-1, 1]) {
      const x = THREE.MathUtils.lerp(.14, -.13, t) + edge * .024;
      strapVertices.push(x, y, .225 * Math.sqrt(1 - (x / .35) ** 2) + .023);
    }
    if (row < 8) { const a = row * 2; strapIndices.push(a, a + 2, a + 1, a + 1, a + 2, a + 3); }
  }
  const strapGeometry = new THREE.BufferGeometry();
  strapGeometry.setAttribute('position', new THREE.Float32BufferAttribute(strapVertices, 3)); strapGeometry.setIndex(strapIndices); strapGeometry.computeVertexNormals();
  attach(j.chest, 'cross-chest-leather-strap', strapGeometry, 'hide', true);
  piece(j.chest, 'strap-keeper', 'box', [.057, .025, .012], [.043, .02, .253], 'leather', [0, 0, -.65]);
  for (const side of ['L', 'R']) {
    const s = side === 'L' ? -1 : 1;
    const thigh = j[side + 'thigh'], shin = j[side + 'shin'], foot = j[side + 'foot'];
    const arm = j[side + 'upperArm'], forearm = j[side + 'forearm'], hand = j[side + 'hand'];
    jointBearing(thigh, 'hip', .23);
    piece(thigh, 'canvas-trouser-leg', 'cylinder', [.24, .45, .26], [0, -.215, 0], 'lining');
    jointBearing(shin, 'knee', .245);
    piece(shin, 'wrapped-boot-shaft', 'cylinder', [.235, .42, .245], [0, -.20, .015], 'hide');
    for (let i = 0; i < 5; i++) {
      const y = -.075 - i * .064, r = .123 + i * .003;
      const wrap = attach(shin, `boot-binding-${i}`, loft([robeLoop(r + .004, r + .009, y + .022), robeLoop(r + .007, r + .012, y - .022)]), i % 2 ? 'lining' : 'leather', true);
      wrap.position.z = .015;
    }
    jointBearing(foot, 'ankle', .18);
    piece(foot, 'salvaged-foot-shell', 'box', [.345, .18, .43], [0, -.015, .075], 'steel');
    piece(foot, 'heavy-tread', 'box', [.355, .055, .47], [0, -.078, .10], 'joint');
    piece(j[side + 'toe'], 'articulated-metal-toe', 'box', [.325, .12, .24], [0, .012, .025], 'edge');
    if (side === 'R') {
      jointBearing(arm, 'exposed-right-shoulder', .245);
      piece(arm, 'right-upper-arm-actuator', 'cylinder', [.17, .28, .18], [0, -.14, 0], 'joint');
      for (const face of [-1, 1]) piece(arm, 'right-upper-arm-plating', 'box', [.19, .20, .045], [0, -.16, face * .084], 'steel', [0, 0, -.08]);
      // The sleeve has been ripped away at the seam: only unequal teeth remain.
      const seam = robeLoop(.16, .155, .045);
      const shreds = robeLoop(.17, .16, 0).map(([x, y, z], i) => [x, y - [.018, .075, .027, .11, .012, .047, .021][i % 7], z]);
      rippedShell(arm, 'ripped-right-shoulder-seam', [seam, shreds], 'canvas');
      // The animator stretches the forearm shell to the bone. Its open cuff
      // receives the separate wrist hinge; the Rhand joint has no hand mesh.
      jointBearing(forearm, 'blade-elbow', .23);
      piece(forearm, 'blade-actuator-core', 'cylinder', [.19, .27, .20], [0, -.135, 0], 'joint');
      attach(forearm, 'salvaged-blade-housing', loft([robeLoop(.125, .12, -.025), robeLoop(.145, .135, -.12), robeLoop(.135, .125, -.255)]), 'steel');
      for (const sign of [-1, 1]) {
        piece(forearm, 'housing-longitudinal-rail', 'box', [.026, .19, .042], [sign * .122, -.15, .058], 'edge');
        piece(forearm, 'housing-rivet', 'sphere', [.026, .026, .026], [sign * .09, -.215, .111], 'joint');
      }
      attach(forearm, 'blade-mount-collar', loft([robeLoop(.148, .139, -.235), robeLoop(.15, .14, -.278), robeLoop(.101, .086, -.283), robeLoop(.10, .085, -.24)]), 'edge');
      piece(forearm, 'blade-mount-dark-socket', 'box', [.19, .018, .13], [0, -.277, 0], 'joint');
    } else {
      piece(j[side + 'clavicle'], 'mantle-shoulder-bridge', 'sphere', [.48, .20, .32], [s * .13, .015, 0], 'canvas');
      const shoulder = attach(arm, 'canvas-shoulder-cap', new THREE.SphereGeometry(.5, 12, 8), 'canvas', true);
      shoulder.scale.set(.41, .30, .37); shoulder.position.set(0, .015, 0);
      attach(arm, 'loose-torn-sleeve', loft([robeLoop(.165, .16, .025), robeLoop(.17, .16, -.10), tornLoop(.18, .17, -.32)]), 'canvas', true);
      piece(forearm, 'sleeve-elbow-fold', 'sphere', [.29, .26, .28], [0, -.01, 0], 'lining');
      attach(forearm, 'left-forearm-sleeve', loft([robeLoop(.145, .14, .025), robeLoop(.14, .14, -.13), tornLoop(.13, .125, -.27)]), 'lining', true);
      for (let i = 0; i < 7; i++) bandage(forearm, `dusty-arm-bandage-${i}`, -.28 + i * .038, [.255 + i * .004, .26]);
      piece(hand, 'cloth-mitten', 'box', [.21, .22, .16], [0, -.035, -.015], 'lining');
      for (let i = 0; i < 4; i++) bandage(hand, `hand-wrap-${i}`, -.12 + i * .05, [.205, .18]);
      piece(hand, 'wrapped-thumb', 'sphere', [.09, .13, .10], [.10, -.025, .025], 'canvas');
    }
  }
  piece(j.neck, 'cowl-neck-wrap', 'cylinder', [.25, .21, .24], [0, .015, -.025], 'lining');
  piece(j.neck, 'cowl-fold', 'sphere', [.37, .18, .31], [0, -.005, -.025], 'canvas');

  const mantleLoop = (width, depth, height, hem = false) => Array.from({ length: 24 }, (_, i) => {
    const a = i * Math.PI * 2 / 24, fold = 1 + .035 * Math.cos(a * 7);
    const fray = hem ? [0, -.024, .007, -.041, -.012, .013][i % 6] : 0;
    return [Math.sin(a) * width * fold, height + .035 * Math.cos(a) + .028 * Math.sin(a) + fray, Math.cos(a) * depth * fold - .025];
  });
  // Both poncho layers share the long right-side tear, exposing the ribs below.
  rippedShell(j.chest, 'heavy-shoulder-poncho', [mantleLoop(.18, .16, .28), mantleLoop(.38, .235, .13), mantleLoop(.49, .265, -.025), mantleLoop(.47, .275, -.12, true)], 'canvas');
  rippedShell(j.chest, 'poncho-underlayer', [mantleLoop(.37, .23, .09), mantleLoop(.465, .265, -.13), mantleLoop(.45, .275, -.175, true)], 'lining');
  patch(j.chest, 'poncho-repair', [.13, .095], [-.21, -.075, .225], 'trim', [-.15, -.4, -.12]);

  // Deep, rounded drape around layered face cloth; only a narrow eye slit.
  const opening = hoodLoop(.178, .218, .115, .245);
  const outer = hoodLoop(.249, .275, .12, .185);
  attach(j.head, 'sagging-desert-hood', loft([outer, hoodLoop(.25, .264, .12, -.055), hoodLoop(.19, .23, .13, -.19), hoodLoop(.07, .08, .19, -.255)], true), 'canvas', true);
  attach(j.head, 'loose-folded-hood-mouth', loft([opening, hoodLoop(.202, .239, .112, .23), outer]), 'canvas', true);
  const cavity = new THREE.Mesh(loft([opening, hoodLoop(.173, .213, .115, .19), hoodLoop(.16, .20, .115, .01)], true), new THREE.MeshBasicMaterial({ color: palette.void, side: THREE.DoubleSide }));
  cavity.name = 'recessed-shadow-void'; j.head.add(cavity);
  const faceWrapLoop = (y, width, depth, tilt = 0) => robeLoop(width, depth, y).map(([x, h, z]) => [x, h + tilt * x, z + .052]);
  attach(j.head, 'wrapped-head-underlayer', loft([faceWrapLoop(-.045, .13, .115), faceWrapLoop(.055, .16, .142), faceWrapLoop(.158, .164, .139)]), 'lining', true);
  for (let i = 0; i < 4; i++) {
    const y = -.025 + i * .046;
    attach(j.head, `overlapping-face-scarf-${i}`, loft([faceWrapLoop(y, .158, .143, .035), faceWrapLoop(y + .031, .17, .152, -.025), faceWrapLoop(y + .051, .164, .145, .015)]), i % 2 ? 'canvas' : 'lining', true);
  }
  attach(j.head, 'brow-cloth-wrap', loft([faceWrapLoop(.187, .165, .143), faceWrapLoop(.232, .168, .14, .07), faceWrapLoop(.289, .133, .11)]), 'lining', true);
  attach(j.head, 'brow-overlapping-fold', loft([faceWrapLoop(.218, .17, .145, .05), faceWrapLoop(.254, .158, .132, .02)]), 'canvas', true);
  const lens = new THREE.Mesh(new THREE.SphereGeometry(.006, 8, 6), new THREE.MeshStandardMaterial({ color: 0x68451f, emissive: 0x9d6423, emissiveIntensity: .12, roughness: .8 }));
  lens.name = 'single-recessed-optic'; lens.position.set(.046, .178, .186); j.head.add(lens);
  patch(j.head, 'hood-repair', [.10, .12], [-.239, .16, -.035], 'trim', [0, -Math.PI / 2, .1]);

  // Keep the public hoodTail/plume attachment as the gathered scarf knot.
  // The long strip is simulated from the head in secondary.js.
  const hoodTail = new THREE.Group(); hoodTail.name = 'hood-tail'; hoodTail.position.set(-.156, .015, .17); j.head.add(hoodTail);
  attach(hoodTail, 'scarf-gathered-knot', loft([robeLoop(.046, .029, .035), tornLoop(.052, .032, -.065)]), 'lining', true);

  // Public name stays sword. Leave this group unparented for the motion code.
  // Hinge pin = origin; blade +Y; cutting edge +X; spine -X; flat faces +/-Z.
  const sword = new THREE.Group(); sword.name = 'sword';
  for (const face of [-1, 1]) {
    const plate = new THREE.Shape();
    plate.moveTo(-.058, -.06); plate.lineTo(.047, -.06); plate.lineTo(.060, -.018);
    plate.lineTo(.048, .069); plate.lineTo(.033, .10); plate.lineTo(-.044, .10);
    plate.lineTo(-.065, .043); plate.closePath();
    const geo = new THREE.ExtrudeGeometry(plate, { depth: .017, bevelEnabled: false });
    geo.translate(0, 0, face > 0 ? .027 : -.044);
    const housing = weatherShell(painted(geo, 0x454b47, .87, .62));
    housing.name = `wrist-hinge-side-plate-${face}`; sword.add(housing);
    const cap = attach(sword, `hinge-pin-cap-${face}`, new THREE.CylinderGeometry(.027, .027, .009, 10), 'steel');
    cap.rotation.x = Math.PI / 2; cap.position.z = face * .048;
    piece(sword, `hinge-pin-slot-${face}`, 'box', [.027, .005, .002], [0, 0, face * .053], 'joint', [0, 0, .35]);
  }
  const pin = attach(sword, 'wrist-hinge-through-pin', new THREE.CylinderGeometry(.020, .020, .096, 12), 'edge');
  pin.rotation.x = Math.PI / 2;
  piece(sword, 'blade-root-tongue', 'box', [.070, .10, .042], [-.004, .047, 0], 'joint');
  const bladeGeometry = new THREE.BufferGeometry();
  const vertices = [], bladeIndices = [];
  // Five-sided section: a full-thickness blunt back, two broad flats, and
  // one honed bevel on +X. The shallow rearward sweep stays under 6 cm.
  const sections = [[.10, -.045, .045, .0125], [.25, -.046, .043, .0125], [.43, -.050, .035, .0115], [.62, -.058, .020, .0105], [.80, -.066, -.002, .009], [.93, -.064, -.028, .006]];
  sections.forEach(([y, back, edge, halfThickness], row) => {
    const bevel = Math.min(.016, (edge - back) * .3);
    vertices.push(back, y, -halfThickness, back, y, halfThickness,
      edge - bevel, y, halfThickness * .78, edge, y, 0, edge - bevel, y, -halfThickness * .78);
    if (row) for (let k = 0; k < 5; k++) {
      const a = (row - 1) * 5 + k, b = (row - 1) * 5 + (k + 1) % 5;
      bladeIndices.push(a, b, b + 5, a, b + 5, a + 5);
    }
  });
  const point = vertices.length / 3; vertices.push(-.05, 1.00, 0);
  for (let k = 0; k < 5; k++) bladeIndices.push(point - 5 + k, point - 5 + (k + 1) % 5, point);
  for (let k = 1; k < 4; k++) bladeIndices.push(0, k + 1, k);
  bladeGeometry.setAttribute('position', new THREE.Float32BufferAttribute(vertices, 3));
  bladeGeometry.setIndex(bladeIndices); bladeGeometry.computeVertexNormals();
  const blade = weatherShell(painted(bladeGeometry, 0x939b98, .64, .76)); blade.name = 'mechanical-sabre-blade'; sword.add(blade);
  const bp = blade.geometry.attributes.position, bn = blade.geometry.attributes.normal, bc = blade.geometry.attributes.color;
  const wornEdge = new THREE.Color(0xd4d5c3), spineSteel = new THREE.Color(0x535e5a), dust = new THREE.Color(0xa38a62), steelColor = new THREE.Color();
  for (let i = 0; i < bp.count; i++) {
    steelColor.fromBufferAttribute(bc, i);
    // Only the cutting bevel is polished; the back remains blunt and oxidized.
    if (bn.getX(i) > .25) steelColor.copy(wornEdge).multiplyScalar(.91 + .07 * Math.sin(bp.getY(i) * 137));
    else if (bn.getX(i) < -.5) steelColor.copy(spineSteel);
    steelColor.lerp(dust, .48 * Math.max(0, 1 - (bp.getY(i) - .10) / .19));
    bc.setXYZ(i, steelColor.r, steelColor.g, steelColor.b);
  }
  for (const face of [-1, 1]) {
    for (let i = 0; i < 3; i++) {
      piece(sword, `root-segment-line-${face}-${i}`, 'box', [.047, .003, .0015], [-.009, .142 + i * .043, face * .012], 'joint', [0, 0, -.12]);
      const rivet = attach(sword, `root-flush-rivet-${face}-${i}`, new THREE.CylinderGeometry(.004, .004, .003, 6), 'edge');
      rivet.rotation.x = Math.PI / 2; rivet.position.set(-.032, .156 + i * .043, face * .0125);
    }
  }
  // A small embedded charge indicator uses the existing emissive interface.
  const rune = new THREE.Mesh(chamferedBox([.006, .065, .024]), new THREE.MeshStandardMaterial({ color: 0x655b3b, emissive: 0x694019, emissiveIntensity: .12, roughness: .7, metalness: .4 }));
  rune.name = 'blade-charge-inset'; rune.position.set(-.018, .30, 0); sword.add(rune);
  const light = new THREE.PointLight(0xff9a45, 0, 3); light.position.y = .50; sword.add(light);
  const base = new THREE.Object3D(); base.name = 'bladeBase'; base.position.set(0, .12, 0); sword.add(base);
  const tip = new THREE.Object3D(); tip.name = 'bladeTip'; tip.position.set(-.05, 1.00, 0); sword.add(tip);
  slim(j, sword);
  // Preserve the old public secondary attachment as an alias, without plume geometry.
  return { ...rig, sword, blade, rune, light, bladeBase: base, bladeTip: tip, hoodTail, plume: hoodTail };
}

// Slimmer silhouette, same proportions: shrink girth (joint-space X/Z) per body part and
// keep every length. Pieces are wrapped in a scaled group so rotated pieces scale correctly.
export const GIRTH = {
  pelvis: [.80, .84], spine: [.76, .82], chest: [.78, .82], neck: [.82, .82], head: [.92, .92],
  clavicle: [.78, .78], upperArm: [.74, .76], forearm: [.72, .74], hand: [.80, .82],
  thigh: [.72, .76], shin: [.72, .76], foot: [.82, .90], toe: [.82, .90],
};
function slim(j, sword) {
  const jointSet = new Set(Object.values(j));
  for (const [name, joint] of Object.entries(j)) {
    const part = /^[LR]/.test(name) ? name.slice(1) : name;
    const f = GIRTH[part]; if (!f) continue;
    const kids = joint.children.filter(c => !jointSet.has(c) && c !== sword && c.name !== 'Rgrip');
    if (!kids.length) continue;
    const group = new THREE.Group(); group.name = name + '-girth'; group.scale.set(f[0], 1, f[1]);
    joint.add(group); for (const c of kids) group.add(c);
  }
}

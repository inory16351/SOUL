// Exports the procedural Three.js content (models, map, move data) as JSON for the Unity port.
// Run in the page (dev server must be running):  await (await import('/tools/export-unity.js')).exportAll()
// Files land in web-proto/.export/ (server.mjs POST sink) and are copied to
// unity/Assets/SOUL/Resources/Data/ by the Unity menu "SOUL/Import Web Export".
// Everything stays in THREE space (right-handed, +Y up, character faces +Z); the Unity
// importer converts (x -> -x, quaternion (x,-y,-z,w), triangle winding flipped).
import * as THREE from 'three';

const R = v => Math.round(v * 1e5) / 1e5;
const arr = (a, n) => Array.from(a, R);
function colorOf(c) { return c ? [R(c.r), R(c.g), R(c.b)] : null; }

function serializeMaterial(m) {
  if (Array.isArray(m)) m = m[0];
  if (!m) return null;
  return {
    name: m.name || '', type: m.type, color: colorOf(m.color), emissive: colorOf(m.emissive), emissiveIntensity: m.emissiveIntensity ?? 0,
    roughness: m.roughness ?? 1, metalness: m.metalness ?? 0, opacity: m.opacity, transparent: !!m.transparent,
    blending: m.blending === THREE.AdditiveBlending ? 'add' : 'normal', side: m.side === THREE.BackSide ? 'back' : m.side === THREE.DoubleSide ? 'double' : 'front',
    vertexColors: !!m.vertexColors, flat: !!m.flatShading, toneMapped: m.toneMapped !== false, depthWrite: m.depthWrite !== false, fog: m.fog !== false,
    size: m.size, hasMap: !!m.map,
  };
}
function serializeGeometry(g, flat) {
  let geo = g;
  if (flat && geo.index) geo = geo.toNonIndexed();
  if (flat) { geo = geo === g ? geo.clone() : geo; geo.deleteAttribute('normal'); geo.computeVertexNormals(); }
  else if (!geo.attributes.normal && geo.index) { geo = geo.clone(); geo.computeVertexNormals(); }
  const out = { position: arr(geo.attributes.position.array) };
  if (geo.attributes.normal) out.normal = arr(geo.attributes.normal.array);
  if (geo.attributes.color) out.color = arr(geo.attributes.color.array.length === geo.attributes.position.count * 3 ? geo.attributes.color.array : Array.from({ length: geo.attributes.position.count * 3 }, (_, i) => geo.attributes.color.array[Math.floor(i / 3) * geo.attributes.color.itemSize + i % 3]));
  if (geo.attributes.uv) out.uv = arr(geo.attributes.uv.array);
  if (geo.index) out.index = Array.from(geo.index.array);
  const dr = geo.drawRange; if (dr && dr.count !== Infinity && !geo.index) out.drawCount = dr.count;
  return out;
}

// Walk an Object3D tree. ids: Map(Object3D -> id) for part references.
function serializeTree(root, { skip = () => false, ids = new Map(), identityRoot = true } = {}) {
  let next = 0;
  function node(o, isRoot) {
    if (skip(o)) return null;
    const id = next++; ids.set(o, id);
    const n = { id, name: o.name || '', type: o.type, visible: o.visible,
      p: isRoot && identityRoot ? [0, 0, 0] : arr(o.position.toArray()), q: isRoot && identityRoot ? [0, 0, 0, 1] : arr(o.quaternion.toArray()), s: arr(o.scale.toArray()) };
    if (o.isMesh || o.isPoints || o.isLine) {
      const mat = serializeMaterial(o.material);
      n.material = mat; n.geometry = serializeGeometry(o.geometry, !!mat?.flat && o.isMesh);
      n.castShadow = o.castShadow; n.receiveShadow = o.receiveShadow; n.renderOrder = o.renderOrder;
    }
    if (o.isLight) n.light = { kind: o.type, color: colorOf(o.color), intensity: o.intensity, distance: o.distance ?? 0, decay: o.decay ?? 2, groundColor: colorOf(o.groundColor), castShadow: !!o.castShadow };
    n.children = o.children.map(c => node(c, false)).filter(Boolean);
    return n;
  }
  return node(root, true);
}
// Replace Object3D references inside a parts structure with {$node: id}.
function partsRefs(value, ids) {
  if (value?.isObject3D) return { $node: ids.get(value) ?? -1 };
  if (Array.isArray(value)) return value.map(v => partsRefs(v, ids));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, partsRefs(v, ids)]));
  return value;
}

async function post(name, data) {
  const body = JSON.stringify(data);
  const r = await fetch('/__export/' + name, { method: 'POST', body });
  if (!r.ok) throw new Error(name + ': ' + r.status);
  return { name, kb: Math.round(body.length / 1024) };
}

export async function exportAll() {
  const { character, animator } = window.__dbg;
  const out = [];
  // ---- character: exported in its mounted state (sword on Rhand, arm meshes stretched,
  // robe torn, face screen + neon edge tubes added); the root itself is reset to identity.
  animator.play('idle'); animator.resetFeet();
  const ids = new Map();
  const tree = serializeTree(character.root, { ids, skip: o => o === character.skeleton });
  const named = { sword: character.sword, bladeBase: character.bladeBase, bladeTip: character.bladeTip, hoodTail: character.hoodTail, rune: character.rune, blade: character.blade };
  out.push(await post('character.json', { tree, refs: partsRefs(named, ids) }));

  // ---- enemies: fresh instances
  const em = await import('/src/enemy_models.js');
  for (const [name, make] of [['crawler', em.createCrawler], ['warden', em.createWarden], ['ashWarden', em.createAshWarden], ['ashMite', em.createAshMite], ['sentry', em.createSentry], ['houndAlpha', em.createHoundAlpha], ['executor', em.createExecutor]]) {
    const m = make(), eids = new Map();
    const t = serializeTree(m.root, { ids: eids });
    out.push(await post(name + '.json', { tree: t, radius: m.radius, height: m.height, parts: partsRefs(m.parts, eids) }));
  }

  // ---- map: a fresh scene (no character / enemies), dummy removed
  const sm = await import('/src/scene.js');
  const world = sm.createScene();
  if (world.dummy?.root) world.scene.remove(world.dummy.root);
  const mapTree = serializeTree(world.scene, { skip: o => o.isLight && o.type === 'DirectionalLight' ? false : false });
  out.push(await post('map.json', { tree: mapTree, background: colorOf(world.scene.background), fog: { color: colorOf(world.scene.fog.color), density: world.scene.fog.density }, bounds: world.bounds,
    sun: { offset: [-24, 12, -18], color: colorOf(world.sun.color), intensity: world.sun.intensity } }));

  // ---- breakable props: one of each kind (order barrel, barrel, crate, crate, lamp)
  const pm = await import('/src/props.js');
  const holder = new THREE.Group(), fakeVfx = { burst() {}, dust() {} };
  const b = pm.createBreakables(holder, fakeVfx, 5, 17);
  const kinds = { barrel: b.list[0], crate: b.list[2], lamp: b.list[4] };
  const props = {};
  for (const [k, o] of Object.entries(kinds)) props[k] = { tree: serializeTree(o.group), radius: o.radius, height: o.height, hp: o.maxHp ?? o.hp };
  out.push(await post('props.json', props));

  // ---- motion data
  const mv = await import('/src/moves.js');
  out.push(await post('moves.json', { neutral: mv.neutral, moves: mv.moves, BASE_BODY: mv.BASE_BODY, STANCE_FEET: mv.STANCE_FEET, IDLE_GRIP: mv.IDLE_GRIP, ARM_BLADE_BLOCKERS: mv.ARM_BLADE_BLOCKERS }));
  return out;
}

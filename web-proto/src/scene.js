import * as THREE from 'three';
import { painted } from './character.js';

function random(seed) { let n = seed; return () => { n = (n * 1664525 + 1013904223) >>> 0; return n / 4294967296; }; }
export function createScene() {
  const scene = new THREE.Scene(); scene.background = new THREE.Color(0x454b48); scene.fog = new THREE.FogExp2(0x454b48,.043);
  // Dusty overcast light: quiet amber highlights on faded canvas and teal shells.
  // The renderer already supplies PCF soft shadows and a mild .25 bloom pass.
  scene.add(new THREE.HemisphereLight(0xb6c3bf,0x514b40,1.4));
  const sun = new THREE.DirectionalLight(0xe9d4af,2.4); sun.position.set(-5,9,4); sun.castShadow=true;
  sun.shadow.mapSize.set(2048,2048); Object.assign(sun.shadow.camera,{left:-12,right:12,top:12,bottom:-12,near:.1,far:35}); sun.shadow.normalBias=.025; sun.shadow.bias=-.0002; sun.shadow.radius=3; scene.add(sun,sun.target);
  const rim = new THREE.DirectionalLight(0xb2c4bf,.95); rim.position.set(4,5,-5); scene.add(rim);
  const fill = new THREE.DirectionalLight(0xc4c5b6,.3); fill.position.set(2,2,5); scene.add(fill);
  const ground = painted(new THREE.CylinderGeometry(13,13.5,.35,64),0x626b55); ground.position.y=-.20; scene.add(ground);
  const floor = painted(new THREE.PlaneGeometry(200,200),0x454f43); floor.rotation.x=-Math.PI/2; floor.position.y=-.39; scene.add(floor);
  const ring = new THREE.Mesh(new THREE.RingGeometry(2.75,2.77,96),new THREE.MeshBasicMaterial({color:0xe2d3a0,transparent:true,opacity:.17,side:THREE.DoubleSide})); ring.rotation.x=-Math.PI/2; ring.position.set(0,-.014,.65); scene.add(ring);
  const ring2=ring.clone(); ring2.scale.setScalar(1.07); ring2.material=ring.material.clone(); ring2.material.opacity=.07; scene.add(ring2);
  const rand = random(42);
  // Broken stone facets at the edge frame the subject without becoming a level.
  for(let i=0;i<72;i++) {
    const angle=rand()*Math.PI*2, radius=4.3+rand()*8.2;
    const rock=painted(new THREE.DodecahedronGeometry(.12+rand()*.45,0),rand()>.5?0x777b69:0x8c8a73);
    rock.position.set(Math.cos(angle)*radius,-.015,Math.sin(angle)*radius); rock.scale.set(1+rand(),.4+rand()*.7,1); rock.rotation.set(rand(),rand()*5,rand()); scene.add(rock);
  }
  const grassGeo = new THREE.BufferGeometry(), vertices=[], colors=[];
  for(let i=0;i<550;i++) {
    const angle=rand()*Math.PI*2,radius=3.5+rand()*9,x=Math.cos(angle)*radius,z=Math.sin(angle)*radius;
    for(let k=0;k<3;k++) { const h=.08+rand()*.22,a=rand()*6.28,dx=Math.cos(a)*.04,dz=Math.sin(a)*.04;
      vertices.push(x-dx,-.015,z-dz,x+dx,-.015,z+dz,x+dx*2,h,z+dz*2); const c=new THREE.Color(rand()>.5?0x92955b:0x555f3b); for(let v=0;v<3;v++)colors.push(c.r,c.g,c.b);
    }
  }
  grassGeo.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3)); grassGeo.setAttribute('color',new THREE.Float32BufferAttribute(colors,3)); grassGeo.computeVertexNormals();
  scene.add(new THREE.Mesh(grassGeo,new THREE.MeshStandardMaterial({vertexColors:true,side:THREE.DoubleSide,roughness:1,flatShading:true})));
  // Subtle etched radial marks on the training circle.
  for(let i=0;i<12;i++){ const angle=i*Math.PI/6; const mark=new THREE.Mesh(new THREE.BoxGeometry(.025,.005,i%3===0?.20:.10),new THREE.MeshBasicMaterial({color:0xbcb793,transparent:true,opacity:.28})); mark.position.set(Math.sin(angle)*2.65,-.012,.65+Math.cos(angle)*2.65); mark.rotation.y=angle; scene.add(mark); }
  const dummy = createDummy(scene);
  return {scene,dummy, sun};
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

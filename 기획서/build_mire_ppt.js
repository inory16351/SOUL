// ALONE(가제) Z6 독성 늪 기획서 PPT 생성 → 기획서/20261109_ALONE_Z6독성늪기획서_v0.1_정승운.pptx
// 사용: NODE_PATH=<pptxgenjs · sharp · jszip 설치 폴더>/node_modules node 기획서/build_mire_ppt.js
// 내용 원본: 기획서/Z6_독성늪_보스_생태계.md (사용자 결정 1~3차 반영본), 기획_결정사항.md
// 스타일 · 도구는 build_boss_ppt.js · build_skill_ppt.js 와 같음(복사). 늪 색(독 녹색 · 독 보라)만 더함
// 그림: 기획서/art → 없으면 unity/Captures (Unity 실제 캡처). 없으면 회색 자리표시. 나머지는 도형 · 표
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const sharp = require('sharp');

const ROOT = __dirname;
const ART = path.join(ROOT, 'art');
const CAP = path.join(ROOT, '..', 'unity', 'Captures');
const VER = 'v0.1';
const DATE = '2026-11-09';
const OUT = path.join(ROOT, `20261109_ALONE_Z6독성늪기획서_${VER}_정승운.pptx`);
const DOC = `ALONE(가제) · Z6 독성 늪 기획서 ${VER}`;

const FONT = 'Noto Sans KR';
const C = {
  coal: '16181B', deep: '0E1012', ink: '23272C', muted: '646B73', bg: 'EEEBE6', card: 'FFFFFF', line: 'D9D4CC', tint: 'E4DED5',
  rust: 'C2562B', ash: '8A8F96', soul: '33B5C8', grudge: 'B8322A', gold: 'E0A030',
  blue: '3F8FD8', red: 'D9502F', rope: 'E0A030', ok: '2E8B6E', draft: 'D99A1E',
  // 늪 전용
  tox: '7DAA2F', toxDk: '4F6B24', sludge: '5B5A3A', mud: '8A7A5A', poison: '8A4FC0', flesh: 'A8606A', water: '4FA6B8', mach: '6E737A',
};
const W = 13.333, H = 7.5, X0 = 0.6, CW = W - 2 * X0;

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = '정승운';
pres.title = 'ALONE(가제) Z6 독성 늪 기획서 — TOXIC MIRE';
pres.theme = { headFontFace: FONT, bodyFontFace: FONT };
pres.defineSlideMaster({
  title: 'CONTENT',
  background: { color: C.bg },
  objects: [
    { rect: { x: 0, y: 0, w: 0.14, h: H, fill: { color: C.rust } } },
    { text: { text: DOC, options: { x: X0, y: 7.02, w: 7, h: 0.3, fontFace: FONT, fontSize: 9, color: '8E8880', margin: 0 } } },
    { placeholder: { options: { name: 'kicker', type: 'body', x: X0, y: 0.32, w: 8, h: 0.3, fontFace: FONT, fontSize: 12, bold: true, color: C.rust, margin: 0 }, text: '' } },
    { placeholder: { options: { name: 'title', type: 'title', x: X0, y: 0.6, w: CW, h: 0.62, fontFace: FONT, fontSize: 28, bold: true, color: C.ink, margin: 0, valign: 'middle' }, text: '' } },
    { placeholder: { options: { name: 'sub', type: 'body', x: X0, y: 1.22, w: CW, h: 0.36, fontFace: FONT, fontSize: 14, color: C.muted, margin: 0 }, text: '' } },
  ],
  slideNumber: { x: W - X0 - 0.5, y: 7.02, w: 0.5, h: 0.3, fontFace: FONT, fontSize: 9, color: '8E8880', align: 'right' },
});
pres.defineSlideMaster({ title: 'COVER', background: { color: C.deep } });
pres.defineSlideMaster({
  title: 'DARK',
  background: { color: C.coal },
  objects: [
    { rect: { x: 0, y: 0, w: 0.14, h: H, fill: { color: C.rust } } },
    { text: { text: DOC, options: { x: X0, y: 7.02, w: 7, h: 0.3, fontFace: FONT, fontSize: 9, color: '6E6962', margin: 0 } } },
    { placeholder: { options: { name: 'kicker', type: 'body', x: X0, y: 0.32, w: 8, h: 0.3, fontFace: FONT, fontSize: 12, bold: true, color: C.gold, margin: 0 }, text: '' } },
    { placeholder: { options: { name: 'title', type: 'title', x: X0, y: 0.6, w: CW, h: 0.62, fontFace: FONT, fontSize: 28, bold: true, color: 'FFFFFF', margin: 0, valign: 'middle' }, text: '' } },
    { placeholder: { options: { name: 'sub', type: 'body', x: X0, y: 1.22, w: CW, h: 0.36, fontFace: FONT, fontSize: 14, color: 'B8B2AA', margin: 0 }, text: '' } },
  ],
  slideNumber: { x: W - X0 - 0.5, y: 7.02, w: 0.5, h: 0.3, fontFace: FONT, fontSize: 9, color: '6E6962', align: 'right' },
});

// ---------- 도구 (build_skill_ppt.js 와 같음) ----------
const dims = {};
const A = (f) => (path.isAbsolute(f) ? f : fs.existsSync(path.join(ART, f)) ? path.join(ART, f) : path.join(CAP, f));
const has = (f) => fs.existsSync(A(f));
async function loadDims() {
  for (const dir of [CAP, ART]) if (fs.existsSync(dir)) for (const f of fs.readdirSync(dir)) if (/\.(png|jpe?g)$/i.test(f) && has(f)) { const m = await sharp(A(f)).metadata(); dims[f] = [m.width, m.height]; }
}
const shadow = () => ({ type: 'outer', color: '000000', opacity: 0.16, blur: 6, offset: 2, angle: 90 });

function text(s, t, x, y, w, h, o = {}) {
  s.addText(t, { x, y, w, h, fontFace: FONT, fontSize: o.size || 13, color: o.color || C.ink, bold: !!o.bold, align: o.align || 'left', valign: o.valign || 'top', margin: 0, isTextBox: true, paraSpaceAfter: o.gap || 0, lineSpacingMultiple: o.ls || 1.15, fit: 'none' });
}
function card(s, x, y, w, h, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.1, fill: { color: o.fill || C.card, transparency: o.tr || 0 }, line: { color: o.line || o.fill || C.card, width: o.lw || 0.75, dashType: o.dash ? 'dash' : 'solid' }, shadow: o.flat ? undefined : shadow() });
}
function chip(s, t, x, y, w, h, fill, color = 'FFFFFF', size = 11) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: h / 2, fill: { color: fill }, line: { color: fill } });
  text(s, t, x, y, w, h, { size, bold: true, color, align: 'center', valign: 'middle' });
}
function badge(s, n, x, y, d = 0.42, fill = C.rust) {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill }, line: { color: fill } });
  text(s, String(n), x, y, d, d, { size: d > 0.4 ? 14 : 11, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
}
function arrow(s, x1, y1, x2, y2, color = C.ash, w = 1.75, both = false) {
  s.addShape(pres.shapes.LINE, { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1) || 0.001, h: Math.abs(y2 - y1) || 0.001, flipH: x2 < x1, flipV: y2 < y1, line: { color, width: w, endArrowType: 'triangle', beginArrowType: both ? 'triangle' : undefined } });
}
function line(s, x1, y1, x2, y2, color = C.ash, w = 1, dash = false) {
  s.addShape(pres.shapes.LINE, { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1) || 0.001, h: Math.abs(y2 - y1) || 0.001, flipH: x2 < x1, flipV: y2 < y1, line: { color, width: w, dashType: dash ? 'dash' : 'solid' } });
}
function rect(s, x, y, w, h, fill, o = {}) {
  s.addShape(o.shape || pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: fill, transparency: o.tr || 0 }, line: { color: o.line || fill, width: o.lw || 0.75, transparency: o.line ? 0 : o.tr || 0, dashType: o.dash ? 'dash' : 'solid' }, rotate: o.rot });
}
function oval(s, x, y, w, h, fill, o = {}) { rect(s, x, y, w, h, fill, { ...o, shape: pres.shapes.OVAL }); }
function img(s, f, x, y, w, h, o = {}) {
  if (!has(f) || !dims[f]) {
    s.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: 'CFC9C0' }, line: { color: 'B9B2A8', dashType: 'dash' } });
    text(s, `이미지 자리: ${f}`, x, y, w, h, { size: 11, color: C.muted, align: 'center', valign: 'middle' });
    return;
  }
  const [iw, ih] = dims[f];
  const r = Math.min(w / iw, h / ih), ww = iw * r, hh = ih * r;
  s.addImage({ path: A(f), x: x + (w - ww) / 2, y: y + (h - hh) / 2, w: ww, h: hh, shadow: o.shadow ? shadow() : undefined });
}
async function coverCrop(f, w, h, focusY = 0.5) {
  if (!has(f) || !dims[f]) return null;
  const out = path.join(ART, '_crop', `${path.parse(f).name}_${Math.round(w * 100)}x${Math.round(h * 100)}.jpg`);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  const [iw, ih] = dims[f];
  const tr = w / h, ir = iw / ih;
  let cw = iw, ch = ih, left = 0, top = 0;
  if (ir > tr) { cw = Math.round(ih * tr); left = Math.round((iw - cw) / 2); } else { ch = Math.round(iw / tr); top = Math.round((ih - ch) * focusY); }
  await sharp(A(f)).extract({ left, top, width: cw, height: ch }).jpeg({ quality: 90 }).toFile(out);
  return out;
}
async function cimg(s, f, x, y, w, h, o = {}) {
  const p = await coverCrop(f, w, h, o.focusY);
  if (!p) return img(s, f, x, y, w, h);
  s.addImage({ path: p, x, y, w, h, shadow: o.shadow ? shadow() : undefined });
}
function caption(s, t, x, y, w) { text(s, t, x, y, w, 0.3, { size: 10, color: C.muted }); }
function captionDk(s, t, x, y, w) { text(s, t, x, y, w, 0.3, { size: 10, color: '8E8880' }); }
function slide(kicker, title, sub, status) {
  const s = pres.addSlide({ masterName: 'CONTENT' });
  s.addText(kicker, { placeholder: 'kicker' });
  s.addText(title, { placeholder: 'title', align: 'left' });
  if (sub) s.addText(sub, { placeholder: 'sub' });
  if (status) statusTag(s, status, W - X0, 0.28);
  return s;
}
function darkSlide(kicker, title, sub, status) {
  const s = pres.addSlide({ masterName: 'DARK' });
  s.addText(kicker, { placeholder: 'kicker' });
  s.addText(title, { placeholder: 'title', align: 'left' });
  if (sub) s.addText(sub, { placeholder: 'sub' });
  if (status) statusTag(s, status, W - X0, 0.28);
  return s;
}
const DK = { fill: '24282D', line: '363B42', flat: true };
function statusTag(s, st, right, y) {
  const T = { ok: [['결정', C.ok]], draft: [['초안 · 검토 필요', C.draft]], mix: [['결정', C.ok], ['초안 섞임', C.draft]], todo: [['정할 것', C.grudge]] }[st];
  let x = right;
  for (let i = T.length - 1; i >= 0; i--) { const [t, c] = T[i]; const w = 0.32 + t.length * 0.14; x -= w; chip(s, t, x, y, w, 0.32, c, 'FFFFFF', 10); x -= 0.1; }
}
const OK = (s, x, y, w = 0.62) => chip(s, '결정', x, y, w, 0.28, C.ok, 'FFFFFF', 9.5);
const DR = (s, x, y, w = 0.62) => chip(s, '초안', x, y, w, 0.28, C.draft, 'FFFFFF', 9.5);
function table(s, rows, x, y, w, colW, o = {}) {
  const head = rows[0].map((t) => ({ text: t, options: { bold: true, color: 'FFFFFF', fill: { color: o.head || C.coal }, align: 'center' } }));
  const body = rows.slice(1).map((r, i) => r.map((t, j) => {
    const cell = typeof t === 'object' ? t : { text: t };
    return { text: cell.text, options: { bold: j === 0 || cell.bold, color: cell.color || C.ink, fill: { color: cell.fill || (i % 2 ? 'F7F5F2' : C.card) }, align: (j === 0 && !o.leftFirst) || o.center ? 'center' : 'left' } };
  }));
  s.addTable([head, ...body], { x, y, w, colW, fontFace: FONT, fontSize: o.size || 12, valign: 'middle', border: { type: 'solid', color: C.line, pt: 0.75 }, rowH: o.rowH || 0.42, margin: [0.04, 0.1, 0.04, 0.1] });
}
const bullets = (a) => a.map((t) => '•  ' + t).join('\n');
// 세로 단면 도식용: 슬러지 층(얕음 · 깊음)
function sludge(s, x, y, w, h, deep) { rect(s, x, y, w, h, deep ? C.toxDk : C.tox, { tr: deep ? 10 : 35 }); }
// 거북 껍질 (단면 · 탑뷰 공용 아이콘)
function shell(s, x, y, w, h, fill = C.sludge, pods = true) {
  s.addShape(pres.shapes.CHORD, { x, y, w, h: h * 2, angleRange: [180, 0], fill: { color: fill }, line: { color: C.coal, width: 1 } });
  if (pods) [0.25, 0.5, 0.75].forEach((k) => oval(s, x + w * k - 0.12, y + h * (k === 0.5 ? 0.02 : 0.25) - 0.1, 0.24, 0.24, C.poison, { line: 'C9A2F0' }));
}

// ---------- 슬라이드 ----------
const COVER = has('mire_key.png') ? 'mire_key.png' : 'codex_marsh_z6.png';
async function build() {
  await loadDims();

  // 1 표지
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    await cimg(s, COVER, 0, 0, W, H, { focusY: 0.5 });
    rect(s, 0, 0, W, H, C.deep, { tr: 60 });
    rect(s, 0, 0, W, 3.4, C.deep, { tr: 25 });
    text(s, 'ZONE DESIGN DOCUMENT  ·  ZONE 6', 0, 0.45, W, 0.35, { size: 13, bold: true, color: 'A9D25A', align: 'center' });
    text(s, '독성 늪', 0, 0.8, W, 1.1, { size: 60, bold: true, color: 'FFFFFF', align: 'center' });
    text(s, 'TOXIC MIRE · 깊은 존(2단계) · 존 + 엘리트 2 + 존 보스', 0, 1.9, W, 0.35, { size: 14, color: 'D7D2CA', align: 'center' });
    text(s, '유기체가 기계를 먹는 곳 — 새 이동 능력 없이, 바깥에서 모은 파츠를 모두 써먹는다', 0, 2.3, W, 0.4, { size: 15, color: 'F0ECE6', align: 'center' });
    text(s, `Z6 존 기획서  ·  작성자 정승운  ·  작성일 ${DATE}  ·  버전 ${VER}  ·  ALONE(가제) 컨셉 기획서 v0.3 부속`, 0, 2.78, W, 0.3, { size: 11, color: 'D7D2CA', align: 'center' });
    text(s, COVER === 'codex_marsh_z6.png' ? '배경: Unity 실제 화면(늪 지형 작업 중 캡처)' : '배경: 컨셉 이미지 (ChatGPT 생성, 분위기 참고용)', X0, 6.95, 6, 0.3, { size: 9.5, color: 'A8A29A' });
  }

  // 2 존 개요
  {
    const s = slide('01  존 개요', '존 개요 — 깊은 존, 입구 둘, 열쇠 하나', '두 입구 중 어디로 와도 깰 수 있어야 한다. 내오염 코어가 있으면 쉬워질 뿐이다', 'ok');
    card(s, X0, 1.8, CW, 0.95, { fill: C.coal, flat: true });
    text(s, '죽은 숲(Z5) 너머, 기계가 반쯤 가라앉아 썩어 가는 늪. 바깥 존에서 모은 파츠를 총동원해\n가라앉은 마을을 지나 늪의 어미를 쓰러뜨리고 연구소 열쇠 하나를 얻는다.', X0 + 0.35, 1.8, CW - 0.7, 0.95, { size: 14.5, bold: true, color: 'FFFFFF', valign: 'middle', ls: 1.3 });
    table(s, [
      ['항목', '내용'],
      ['단계', '깊은 존(2단계). Z5 죽은 숲에서 들어옴'],
      ['구역', '슬러지 둑(입구 · 포드) → 가라앉은 마을(탐험) → 보스 지대'],
      ['적', '몬스터 5종 · 엘리트 2(껍질 숙주 · 거대 준설기) · 존 보스(늪의 어미)'],
      ['보상', { text: '연구소 열쇠 1/2 (다른 하나는 Z2 마천루 묘지)', bold: true }],
      ['새 이동 능력', { text: '없음 — 원래 기획. 모은 파츠를 다 써먹는 존', bold: true, color: C.grudge }],
    ], X0, 3.0, 7.0, [1.6, 5.4], { rowH: 0.56, size: 12 });
    // 입구 도식
    const dx = 7.95, dw = W - X0 - dx;
    card(s, dx, 3.0, dw, 3.36, { fill: 'F7F5F2', flat: true });
    text(s, '들어오는 길', dx + 0.2, 3.1, 2, 0.3, { size: 12, bold: true, color: C.muted });
    card(s, dx + 1.45, 3.15, 1.9, 0.5, { fill: C.mach, flat: true });
    text(s, 'Z5 죽은 숲', dx + 1.45, 3.15, 1.9, 0.5, { size: 13, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    arrow(s, dx + 2.0, 3.68, dx + 1.0, 4.25, C.ash, 2); arrow(s, dx + 2.8, 3.68, dx + 3.8, 4.25, C.ash, 2);
    const gate = (x, h, need, c) => {
      card(s, x, 4.28, 2.15, 1.0, { fill: C.card, line: c, lw: 1.5, flat: true });
      text(s, h, x + 0.12, 4.33, 1.95, 0.35, { size: 12.5, bold: true, color: c });
      text(s, need, x + 0.12, 4.68, 1.95, 0.55, { size: 10.5, color: C.ink });
    };
    gate(dx + 0.12, '입구 A · 슬러지', '내오염 코어 (Z4 보스)', C.toxDk);
    gate(dx + 2.53, '입구 B · 잔해 벽', '캐논 포 (Z3 보스)', C.rust);
    arrow(s, dx + 1.2, 5.3, dx + 2.0, 5.65, C.ash, 2); arrow(s, dx + 3.6, 5.3, dx + 2.8, 5.65, C.ash, 2);
    card(s, dx + 1.3, 5.66, 2.2, 0.5, { fill: C.toxDk, flat: true });
    text(s, 'Z6 독성 늪', dx + 1.3, 5.66, 2.2, 0.5, { size: 13, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    text(s, '설계 규칙: 내오염 코어 없이도 깰 수 있고, 있으면 쉬워진다', X0, 6.5, CW, 0.32, { size: 12.5, bold: true, color: C.rust });
  }

  // 3 뒤집힌 생태계 (어두운 판)
  {
    const s = darkSlide('02  생태계', 'Z0와 뒤집힌 생태계 — 유기체가 기계를 먹는다', '플레이어(기계)는 이 존에서 사냥당하는 쪽이라는 감각을 받는다', 'ok');
    const col = (x, tag, title, a, aC, b, bC, body, img2) => {
      card(s, x, 1.85, 5.85, 3.55, DK);
      chip(s, tag, x + 0.25, 2.05, 1.1, 0.34, tag === 'Z0' ? C.ash : C.tox, 'FFFFFF', 11);
      text(s, title, x + 1.5, 2.0, 4.2, 0.44, { size: 17, bold: true, color: 'FFFFFF', valign: 'middle' });
      // 포식 관계: 사냥꾼 → 먹이
      card(s, x + 0.35, 2.75, 2.1, 1.25, { fill: aC, flat: true });
      text(s, a, x + 0.35, 2.75, 2.1, 1.25, { size: 15, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
      arrow(s, x + 2.55, 3.38, x + 3.3, 3.38, 'E6E1D9', 3);
      text(s, '먹는다', x + 2.45, 2.95, 0.95, 0.3, { size: 10, bold: true, color: 'B8B2AA', align: 'center' });
      card(s, x + 3.4, 2.75, 2.1, 1.25, { fill: bC, flat: true });
      text(s, b, x + 3.4, 2.75, 2.1, 1.25, { size: 15, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
      text(s, body, x + 0.35, 4.2, 5.2, 1.45, { size: 12, color: 'E6E1D9', ls: 1.35 });
    };
    col(X0, 'Z0', '잿빛 폐허 — 기계의 땅', '기계', C.mach, '유기체', C.flesh, bullets(['기계가 유기체를 사냥한다', '쥐 · 새 같은 작은 생물이 숨어 산다', '플레이어는 같은 기계 편에서 출발']));
    col(X0 + 6.28, 'Z6', '독성 늪 — 유기체의 땅', '유기체', C.toxDk, '기계', C.mach, bullets(['녹슨 기계는 늪에 반쯤 잠겨 썩어 간다', '떼벌레가 기계 잔해를 갉아 혼을 빨아들인다', '플레이어(기계)도 먹잇감이다']));
    card(s, X0, 5.75, CW, 0.82, { fill: '2B3324', line: C.toxDk, flat: true });
    text(s, '공략에 쓰는 생태', X0 + 0.3, 5.75, 2.0, 0.82, { size: 13, bold: true, color: 'A9D25A', valign: 'middle' });
    text(s, '떼가 기계를 먹는 동안 지나간다 · 떼를 끌어와 준설기와 싸우게 만든다 · 준설기 흡입구에 떼를 넣어 막는다(엘리트 기믹)', X0 + 2.3, 5.75, CW - 2.6, 0.82, { size: 12, color: 'E6E1D9', valign: 'middle' });
  }

  // 4 지형 규칙
  {
    const s = slide('03  지형 규칙', '지형 규칙 — 슬러지와 수위', '얕은 슬러지는 아프고 느리다. 깊은 슬러지는 못 들어간다. 길은 둑 · 지붕 · 파이프로 잇는다', 'ok');
    // 단면 도식
    const dx = X0, dy = 1.8, dw = 7.3, dh = 2.9;
    card(s, dx, dy, dw, dh, { fill: 'F7F5F2', flat: true });
    text(s, '옆에서 본 단면 (설명용)', dx + 0.2, dy + 0.1, 3, 0.28, { size: 10, color: C.muted });
    const gy = dy + 2.05; // 수면
    sludge(s, dx + 0.15, gy, dw - 0.3, 0.7, true);
    rect(s, dx + 0.15, gy, 2.2, 0.7, C.tox, { tr: 20 });
    rect(s, dx + 0.15, gy + 0.35, 2.2, 0.35, C.mud);
    // 둑
    rect(s, dx + 0.15, gy - 0.35, 1.2, 0.7, C.mud, { shape: pres.shapes.ROUNDED_RECTANGLE });
    text(s, '둑', dx + 0.15, gy - 0.35, 1.2, 0.35, { size: 10.5, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    text(s, '얕은 슬러지', dx + 1.35, gy + 0.03, 1.0, 0.3, { size: 9.5, bold: true, color: C.ink, align: 'center' });
    text(s, '깊은 슬러지', dx + 2.6, gy + 0.2, 1.4, 0.3, { size: 10, bold: true, color: 'FFFFFF' });
    // 가라앉은 집 지붕
    s.addShape(pres.shapes.ISOSCELES_TRIANGLE, { x: dx + 4.0, y: gy - 0.75, w: 1.5, h: 0.75, fill: { color: C.mach }, line: { color: C.coal, width: 1 } });
    rect(s, dx + 4.15, gy, 1.2, 0.7, '55595F', { tr: 30 });
    text(s, '지붕', dx + 4.0, gy - 0.42, 1.5, 0.35, { size: 10.5, bold: true, color: 'FFFFFF', align: 'center' });
    // 쓰러진 파이프
    rect(s, dx + 5.6, gy - 0.22, 1.5, 0.22, C.rust, { shape: pres.shapes.ROUNDED_RECTANGLE });
    text(s, '파이프', dx + 5.6, gy - 0.52, 1.5, 0.28, { size: 10.5, bold: true, color: C.rust, align: 'center' });
    // 점프 화살표
    const hop = (x1, x2, y, lab) => { line(s, x1, y, (x1 + x2) / 2, y - 0.55, C.soul, 1.5, true); arrow(s, (x1 + x2) / 2, y - 0.55, x2, y, C.soul, 1.5); if (lab) text(s, lab, (x1 + x2) / 2 - 0.6, y - 0.85, 1.2, 0.26, { size: 9.5, bold: true, color: C.soul, align: 'center' }); };
    hop(dx + 1.1, dx + 4.2, gy - 0.4, '2단 점프');
    hop(dx + 5.3, dx + 5.9, gy - 0.3, '');
    // 거품 (크롤러 위치)
    [0, 0.18, 0.32].forEach((k, i) => oval(s, dx + 3.05 + k, gy - 0.08 - i * 0.04, 0.1, 0.1, 'C9E08A'));
    text(s, '거품 = 크롤러', dx + 2.55, gy + 0.42, 1.5, 0.26, { size: 9, color: 'E6F0C8' });
    // 규칙 표
    table(s, [
      ['', '내오염 코어 없음', '내오염 코어 있음'],
      ['얕은 슬러지', '걸을 수 있음 · 서 있는 동안 지속 피해 · 이동 느려짐', { text: '피해 없음', bold: true, color: C.ok }],
      ['깊은 슬러지', '못 들어감 · 빠지면 포드로', { text: '걸어서 건넘 → 지름길이 열림', bold: true, color: C.ok }],
    ], X0, 4.95, 7.3, [1.4, 3.75, 2.15], { rowH: 0.56, size: 11.5 });
    // 오른쪽: 수위 · 정화 전후
    const rx = 8.25, rw = W - X0 - rx;
    await cimg(s, 'water_mire.png', rx, 1.8, rw, rw * 9 / 16, { shadow: true });
    caption(s, 'Unity 실제 화면 — 늪 수면 · 지형 작업 중', rx, 1.8 + rw * 9 / 16 + 0.04, rw);
    const py = 4.75;
    text(s, '수위', rx, py, 1, 0.3, { size: 13, bold: true, color: C.rust });
    text(s, '보스 아레나에서 오르내린다(썰물 · 밀물). 캐논으로 막힌 배수로를 뚫으면 웅덩이 수위가 낮아진다', rx, py + 0.32, rw, 0.7, { size: 11 });
    text(s, '정화 전 → 후', rx, py + 1.1, 2, 0.3, { size: 13, bold: true, color: C.rust });
    chip(s, '녹색 안개 · 거품', rx, py + 1.48, 1.9, 0.34, C.toxDk, 'FFFFFF', 10.5);
    arrow(s, rx + 1.95, py + 1.65, rx + 2.3, py + 1.65, C.ash, 1.75);
    chip(s, '맑은 물 · 갈대 · 반딧불', rx + 2.35, py + 1.48, rw - 2.35, 0.34, C.water, 'FFFFFF', 10.5);
  }

  // 5 파츠를 늪에서 쓰는 곳
  {
    const s = slide('04  파츠 활용', '새 능력 대신, 모은 파츠를 늪에서 쓴다', '늪은 새 이동 능력을 주지 않는다. 바깥 존 파츠마다 늪 안에 쓸 곳이 하나씩 있다', 'ok');
    const P = [
      ['로프 팔', 'Z0 보스', '떠 있는 파이프 앵커에 매달려 깊은 슬러지를 건넌다', '숨은 둑', C.rope],
      ['제트엔진', 'Z1 보스', '무너진 지붕 사이 큰 갭을 넘는다', '마을 위쪽 루트', C.blue],
      ['캐논 포', 'Z3 보스', '입구 B 잔해 벽을 부수고, 막힌 배수로를 뚫어 웅덩이 수위를 낮춘다', '입구 B · 수위', C.rust],
      ['자석 다리', 'Z5 보스', '반쯤 잠긴 탱크 외벽을 오른다', '기록', C.mach],
      ['내오염 코어', 'Z4 보스', '입구 A로 들어오고, 깊은 슬러지를 걸어서 건넌다', '입구 A · 지름길', C.toxDk],
      ['도약 관절', 'Z0 엘리트', '둑과 지붕 사이 2단 점프. 거북 껍질 발판에서 숨은 단으로 오른다', '숨은 높은 단', C.soul],
    ];
    const rh = 0.7, y0 = 1.8;
    // 머리줄
    rect(s, X0, y0, CW, 0.42, C.coal);
    [['파츠', 0.3, 2.3], ['얻는 곳', 2.75, 1.3], ['늪에서 쓰는 곳', 4.2, 5.6], ['열리는 것', 10.0, 2.0]].forEach(([t, x, w]) => text(s, t, X0 + x, y0, w, 0.42, { size: 12, bold: true, color: 'FFFFFF', valign: 'middle' }));
    P.forEach(([n, from, use, opens, c], i) => {
      const y = y0 + 0.42 + i * rh;
      rect(s, X0, y, CW, rh, i % 2 ? 'F7F5F2' : C.card, { line: C.line });
      rect(s, X0, y, 0.12, rh, c);
      text(s, n, X0 + 0.3, y, 2.3, rh, { size: 15, bold: true, valign: 'middle' });
      chip(s, from, X0 + 2.75, y + rh / 2 - 0.16, 1.2, 0.32, i === 5 ? C.soul : C.ash, 'FFFFFF', 10);
      text(s, use, X0 + 4.2, y, 5.6, rh, { size: 12, valign: 'middle' });
      chip(s, opens, X0 + 10.0, y + rh / 2 - 0.17, 1.95, 0.34, c, 'FFFFFF', 10.5);
    });
    text(s, '입구는 내오염 코어 · 캐논 중 하나면 된다. 나머지 파츠는 숨은 길 · 지름길 · 기록을 연다', X0, 6.55, CW, 0.32, { size: 12, bold: true, color: C.rust });
  }

  // 6 몬스터 5종
  {
    const s = slide('05  몬스터', '몬스터 5종 — 영역 생태계', 'Z0와 같은 영역 생태계 시스템. 유기체 셋, 오염된 기계 둘', 'ok');
    const T = (t, c) => ({ text: t, bold: true, color: c });
    table(s, [
      ['종', '분류', '역할', '행동', '공격'],
      ['늪 개구리', T('유기체', C.toxDk), '분위기 · 먹이 · 길잡이', '둑 위를 뛰어다니고, 위험하면 슬러지로 뛰어든다', '없음'],
      ['떼벌레', T('유기체', C.toxDk), '잡몹 떼', '6~8마리가 날아다님. 수명 약 8초. 기계 잔해에 붙어 갉아 먹는다', '달려들어 물기(작음) · 맞으면 짧게 둔화'],
      ['슬러지 크롤러', T('기계 · 오염', C.mach), '매복', '슬러지 속에 잠겨 있다가 튀어나온다. 거품으로 위치가 보인다', '튀어나오며 할퀴기 · 독 침 뱉기'],
      ['포자 덩굴', T('유기체 · 고정', C.toxDk), '길목 견제', '움직이지 않는다. 포자 주머니가 부풀었다 터진다', '포물선 독 포자 · 바닥 장판 4초'],
      ['녹슨 준설기', T('기계 · 반쯤 잠김', C.mach), '중형 경비', '떼벌레에게 갉아 먹히면서 버틴다. 플레이어가 오면 같이 싸운다', '흡입(당기기) → 내려찍기'],
    ], X0, 1.8, CW, [1.75, 1.6, 2.0, 4.0, 2.78], { rowH: 0.62, size: 11.5 });
    const scenes = [
      ['떼가 기계를 먹는 장면', '떼벌레가 준설기에 붙어 갉아 먹는다. 그 틈에 지나갈 수 있고, 떼를 끌어오면 준설기와 싸우게 만들 수 있다', C.toxDk],
      ['개구리 길잡이', '개구리를 쫓아가면 숨은 둑 길이 나온다. Z0 쥐 구하기와 같은 길잡이 역할', C.soul],
    ];
    scenes.forEach(([h, b, c], i) => {
      const x = X0 + i * (CW / 2 + 0.1), w = CW / 2 - 0.1, y = 5.7;
      card(s, x, y, w, 1.12);
      rect(s, x, y, 0.12, 1.12, c);
      text(s, h, x + 0.3, y + 0.1, w - 0.5, 0.36, { size: 14, bold: true, color: c });
      text(s, b, x + 0.3, y + 0.48, w - 0.5, 0.6, { size: 11 });
    });
  }

  // 7 영역 배치
  {
    const s = slide('06  영역 배치', '영역 배치 — 둑에서 마을, 보스 지대까지', '대략의 흐름. 좌표 · 수는 레벨 작업 때 정한다', 'draft');
    const N = [
      ['슬러지 둑', '입구 · 포드', '둑 길 양옆 3곳', ['개구리', '떼벌레', '크롤러 1'], C.mud],
      ['기계 무덤', '둑 → 마을', '반쯤 잠긴 2곳', ['준설기 + 떼(먹는 장면)', '포자 덩굴', '★ 거대 준설기'], C.mach],
      ['가라앉은 마을', '탐험', '지붕 섬 4~5곳', ['크롤러 매복', '포자 덩굴 길목', '개구리 둥지'], C.toxDk],
      ['마을 가장자리', '엘리트', '껍질 숙주 웅덩이', ['★ 껍질 숙주', '둘레 떼벌레'], C.poison],
      ['파이프 다리', '보스 가는 길', '파이프 다리', ['크롤러 2', '포자 덩굴 1'], C.rust],
      ['보스 지대', '마을 광장', '수위가 오르내림', ['★ 늪의 어미'], C.coal],
    ];
    const bw = 1.88, gap = (CW - 6 * bw) / 5, y = 1.85;
    N.forEach(([h, tag, area, mobs, c], i) => {
      const x = X0 + i * (bw + gap);
      card(s, x, y, bw, 0.95, { fill: c, flat: true });
      text(s, h, x + 0.12, y + 0.08, bw - 0.24, 0.4, { size: 14, bold: true, color: 'FFFFFF' });
      text(s, tag, x + 0.12, y + 0.5, bw - 0.24, 0.3, { size: 10.5, color: 'E6E1D9' });
      if (i < 5) arrow(s, x + bw + 0.02, y + 0.47, x + bw + gap - 0.02, y + 0.47, C.ash, 2);
      card(s, x, y + 1.1, bw, 2.25);
      text(s, area, x + 0.12, y + 1.18, bw - 0.24, 0.32, { size: 11, bold: true, color: C.muted });
      mobs.forEach((m, k) => {
        const star = m.startsWith('★');
        text(s, star ? m : '•  ' + m, x + 0.12, y + 1.55 + k * 0.52, bw - 0.2, 0.5, { size: 10.5, bold: star, color: star ? C.grudge : C.ink });
      });
    });
    // 지름길 / 숨은 길 표시
    const sy = 5.45;
    card(s, X0, sy, CW, 1.35, { fill: 'F7F5F2', flat: true });
    text(s, '길 위의 갈래', X0 + 0.25, sy + 0.12, 3, 0.32, { size: 13, bold: true, color: C.rust });
    const L = [
      ['지름길', '내오염 코어 — 깊은 슬러지를 걸어서 둑 → 마을', C.toxDk],
      ['숨은 둑', '로프 팔 · 개구리 길잡이', C.rope],
      ['마을 위쪽', '제트엔진 — 지붕 사이 갭', C.blue],
      ['숨은 높은 단', '껍질 숙주 처치 → 껍질 발판 → 2단 점프', C.poison],
    ];
    L.forEach(([h, b, c], i) => {
      const x = X0 + 0.25 + i * 2.98;
      chip(s, h, x, sy + 0.55, 1.3, 0.32, c, 'FFFFFF', 10.5);
      text(s, b, x, sy + 0.92, 2.85, 0.4, { size: 10.5 });
    });
  }

  // 8 엘리트 1 — 껍질 숙주: 외형 · 패턴
  {
    const s = slide('07  엘리트 1', '껍질 숙주 — 숨어서 멀리서 압박한다', '거북 같은 진화 유기체. 군단숙주처럼 몸을 숨긴 채 독 뭉치를 계속 던진다', 'ok');
    // 탑뷰 웅덩이 도식
    const cx = 3.2, cy = 3.95, r = 1.75;
    card(s, X0, 1.8, 5.2, 4.25, { fill: 'F7F5F2', flat: true });
    text(s, '탑뷰 · 마을 가장자리 웅덩이', X0 + 0.2, 1.88, 3.5, 0.28, { size: 10, color: C.muted });
    oval(s, cx - r, cy - r, 2 * r, 2 * r, C.toxDk, { tr: 15 });
    oval(s, cx - r * 0.55, cy - r * 0.55, r * 1.1, r * 1.1, '3C5219');
    [[0, -1], [1, 0], [0, 1], [-1, 0]].forEach(([a, b]) => rect(s, cx + a * (r + 0.05) - 0.38, cy + b * (r + 0.05) - 0.22, 0.76, 0.44, C.mud, { shape: pres.shapes.ROUNDED_RECTANGLE, line: C.coal }));
    text(s, '둑 4개', X0 + 0.2, cy + r - 0.2, 1.0, 0.3, { size: 10, bold: true, color: C.mud });
    oval(s, cx - 0.65, cy - 0.5, 1.3, 1.0, C.sludge, { line: C.coal });
    [[-0.35, -0.2], [0.15, -0.3], [0.05, 0.15]].forEach(([a, b]) => oval(s, cx + a, cy + b, 0.26, 0.26, C.poison, { line: 'C9A2F0' }));
    text(s, '등껍질 + 독 주머니 3', cx - 1.1, cy + 0.55, 2.2, 0.28, { size: 10, bold: true, color: 'FFFFFF', align: 'center' });
    // 던지기 포물선 + 착탄 원
    line(s, cx + 0.3, cy - 0.4, cx + 1.3, cy - 1.6, C.poison, 1.5, true);
    oval(s, cx + 1.05, cy - 2.0, 0.7, 0.4, C.poison, { tr: 55, line: C.poison, dash: true });
    text(s, '착탄 예고', cx + 1.8, cy - 1.95, 1.1, 0.28, { size: 9.5, bold: true, color: C.poison });
    caption(s, '외형 — 껍질만 내민 거대 거북 · 독 주머니 3(보라 · 녹색 혈관) · 기계 판이 박힌 살덩어리 머리와 다리', X0, 6.1, 5.2);
    // 패턴 순환
    const px = 6.15, pw = W - X0 - px;
    const S = [
      ['잠복', '껍질만 내밀고 잠김. 껍질을 치면 튕김 — 피해 거의 없음', C.sludge],
      ['독 뭉치 던지기 × 3', '주머니가 부풀고(예고) 슬러지를 뭉쳐 포물선 투척. 원형 예고 → 큰 피해 + 독 장판 5초. 주머니 순서대로 3발 = 1세트', C.poison],
      ['부상 · 숨 쉬기 4초', '두 세트를 던지면 머리를 들고 숨을 쉰다 — 딜 타이밍', C.ok],
    ];
    S.forEach(([h, b, c], i) => {
      const y = 1.8 + i * 1.18;
      card(s, px, y, pw, 1.0);
      rect(s, px, y, 0.12, 1.0, c);
      badge(s, i + 1, px + 0.3, y + 0.14, 0.4, c);
      text(s, h, px + 0.85, y + 0.1, pw - 1.0, 0.42, { size: 14.5, bold: true, valign: 'middle' });
      text(s, b, px + 0.85, y + 0.52, pw - 1.05, 0.46, { size: 10.5, color: C.muted });
    });
    // 순환 화살표 (3 → 1)
    line(s, px + pw + 0.02, 1.8 + 2 * 1.18 + 0.5, px + pw + 0.2, 1.8 + 2 * 1.18 + 0.5, C.ash, 1.5);
    line(s, px + pw + 0.2, 1.8 + 0.5, px + pw + 0.2, 1.8 + 2 * 1.18 + 0.5, C.ash, 1.5);
    arrow(s, px + pw + 0.2, 1.8 + 0.5, px + pw + 0.02, 1.8 + 0.5, C.ash, 1.5);
    card(s, px, 5.4, pw, 0.85, { fill: C.coal, flat: true });
    chip(s, 'HP 50% 이하', px + 0.2, 5.65, 1.5, 0.34, C.grudge, 'FFFFFF', 10.5);
    text(s, '웅덩이 반대편으로 잠수해 이동. 터지면 작은 덩어리 4개로 갈라지는 큰 독 덩어리를 섞는다', px + 1.85, 5.4, pw - 2.0, 0.85, { size: 11, color: 'FFFFFF', valign: 'middle' });
  }

  // 9 엘리트 1 — 파훼 · 발판 · 보상
  {
    const s = slide('08  엘리트 1 · 파훼', '껍질 숙주 — 독 주머니를 터뜨리고, 껍질을 밟고 오른다', '부푼 주머니를 치면 독이 자기에게 쏟아진다. 죽으면 껍질이 굳어 숨은 단으로 가는 발판이 된다', 'ok');
    // 파훼 흐름
    const F = [['주머니가 부풂', '투척 예고', C.poison], ['그때 친다', '주머니가 터짐 · 독이 거북에게 쏟아짐 · 투척 취소', C.rust], ['3개 다 터뜨림', '8초 완전히 드러나 기절 · 받는 피해 2배', C.ok]];
    let fx = X0;
    F.forEach(([h, b, c], i) => {
      const w = 2.55;
      s.addShape(pres.shapes.CHEVRON, { x: fx, y: 1.8, w, h: 0.95, fill: { color: c }, line: { color: c } });
      text(s, h, fx + 0.55, 1.84, w - 0.9, 0.4, { size: 13, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, fx + 0.55, 2.22, w - 0.95, 0.5, { size: 9.5, color: 'FFFFFF' });
      fx += w + 0.04;
    });
    table(s, [
      ['주머니를 치는 법', '결과'],
      ['둑 위에서 2단 점프 (도약 관절)', '가까이서 바로'],
      ['원거리 스킬 (레이저 파동)', '둑에서 멀리'],
      ['내오염 코어', '웅덩이에 들어가 껍질 위에 올라탐'],
      [{ text: '셋 다 없음', color: C.grudge }, '부상 타이밍만 노리는 긴 싸움'],
    ], X0, 2.95, 7.7, [3.6, 4.1], { rowH: 0.46, size: 11.5, leftFirst: true });
    // 껍질 발판 단면
    const dx = X0, dy = 5.35, dw = 7.7, dh = 1.5;
    card(s, dx, dy, dw, dh, { fill: 'F7F5F2', flat: true });
    const gy = dy + 1.15;
    sludge(s, dx + 0.15, gy, dw - 0.3, 0.25, true);
    shell(s, dx + 1.6, gy - 0.5, 1.5, 0.5, C.ash, false);
    text(s, '굳은 껍질 ≈ 2.5m', dx + 0.1, gy - 0.4, 1.45, 0.28, { size: 9.5, bold: true, align: 'right' });
    rect(s, dx + 4.6, dy + 0.15, 0.7, gy - dy - 0.15, C.mach, { line: C.coal });
    rect(s, dx + 4.35, dy + 0.12, 1.2, 0.18, C.mach, { line: C.coal });
    text(s, '급수탑 꼭대기 = 숨은 높은 단', dx + 5.55, dy + 0.1, 2.1, 0.5, { size: 10, bold: true, color: C.poison });
    line(s, dx + 2.35, gy - 0.55, dx + 3.6, dy - 0.05 + 0.2, C.soul, 1.5, true);
    arrow(s, dx + 3.6, dy + 0.15, dx + 4.4, dy + 0.12, C.soul, 1.5);
    text(s, '2단 점프', dx + 3.0, dy + 0.4, 1.0, 0.26, { size: 9.5, bold: true, color: C.soul });
    text(s, '죽은 뒤', dx + 0.2, dy + 0.1, 1.2, 0.26, { size: 10, bold: true, color: C.muted });
    // 보상 카드 (어두운)
    const rx = 8.6, rw = W - X0 - rx;
    card(s, rx, 1.8, rw, 5.05, { fill: C.coal, flat: true });
    text(s, '보상', rx + 0.25, 1.92, 2, 0.3, { size: 12, bold: true, color: 'B8B2AA' });
    text(s, '새 파츠', rx + 0.25, 2.25, 2, 0.3, { size: 11, color: 'C9A2F0' });
    text(s, '독에 잠식된 코어', rx + 0.25, 2.52, rw - 0.5, 0.5, { size: 20, bold: true, color: 'FFFFFF' });
    // 보라 칼날
    s.addShape(pres.shapes.RIGHT_TRIANGLE, { x: rx + 0.35, y: 3.2, w: 2.7, h: 0.32, fill: { color: C.poison }, line: { color: 'C9A2F0', width: 1 }, flipH: true });
    rect(s, rx + 3.05, 3.16, 0.55, 0.4, C.mach, { shape: pres.shapes.ROUNDED_RECTANGLE });
    text(s, '칼날이 보라색으로', rx + 0.35, 3.6, rw - 0.5, 0.28, { size: 10, color: 'C9A2F0' });
    text(s, bullets([
      '모든 공격에 독 피해 — 맞은 적 3초 지속 피해, 다시 맞히면 갱신',
      '독 저항은 없음 (내오염 코어와 겹치지 않게)',
      '거북에게 삼켜졌다 뱉어진 기계의 코어. 숨은 단 꼭대기에 박혀 있다',
    ]), rx + 0.25, 4.0, rw - 0.45, 1.85, { size: 10.5, color: 'E6E1D9', ls: 1.3 });
    text(s, '+ 기본 부품 40 · 생존자 기록 · 붉은 조각 · 보급 상자', rx + 0.25, 6.05, rw - 0.4, 0.65, { size: 10.5, bold: true, color: 'F2C27A' });
  }

  // 10 엘리트 2 — 거대 준설기
  {
    const s = slide('09  엘리트 2', '거대 준설기 — 떼벌레로 흡입구를 막는다', '떼에게 반쯤 먹힌 기계. 유기체 vs 기계 생태를 그대로 공략에 쓴다', 'mix');
    table(s, [
      ['항목', '내용'],
      ['위치', '슬러지 둑과 마을 사이 기계 무덤 한가운데'],
      ['외형', '반쯤 잠긴 거대 준설기. 몸 곳곳이 떼에게 뚫렸고 떼벌레 둥지가 붙어 있다'],
      ['주 공격', '거대 흡입구로 플레이어를 끌어당긴다'],
      ['기믹', { text: '떼벌레를 흡입구로 끌어들이게 하면 막혀서 기절', bold: true }],
      ['뜯기 (로프 팔 이후)', '흡입구 덩어리 덮개를 뜯는다'],
    ], X0, 1.8, 6.6, [1.9, 4.7], { rowH: 0.6, size: 11.5 });
    // 흡입 도식
    const dx = 7.5, dw = W - X0 - dx;
    card(s, dx, 1.8, dw, 3.0, { fill: 'F7F5F2', flat: true });
    text(s, '탑뷰 · 흡입 기믹', dx + 0.2, 1.88, 3, 0.28, { size: 10, color: C.muted });
    rect(s, dx + 0.3, 2.55, 1.5, 1.5, C.mach, { shape: pres.shapes.ROUNDED_RECTANGLE, line: C.coal });
    s.addShape(pres.shapes.TRAPEZOID, { x: dx + 1.55, y: 2.95, w: 0.7, h: 0.7, fill: { color: '4A4F55' }, line: { color: C.coal, width: 1 }, rotate: 90 });
    text(s, '준설기', dx + 0.3, 2.55, 1.5, 1.5, { size: 12, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    rect(s, dx + 2.3, 2.4, 2.5, 1.7, C.soul, { tr: 80, line: C.soul, dash: true });
    text(s, '흡입 범위', dx + 3.7, 2.43, 1.05, 0.26, { size: 9.5, bold: true, color: C.soul, align: 'right' });
    arrow(s, dx + 4.7, 3.3, dx + 2.4, 3.3, C.soul, 1.75);
    for (let i = 0; i < 6; i++) oval(s, dx + 3.3 + (i % 3) * 0.33, 2.8 + Math.floor(i / 3) * 0.75 + (i % 2) * 0.1, 0.17, 0.17, C.toxDk);
    text(s, '떼벌레', dx + 2.4, 3.8, 1.0, 0.26, { size: 10, bold: true, color: C.toxDk });
    text(s, '플레이어가 떼를 끌고 와 흡입 범위에 세운다 → 떼가 빨려 들어가 흡입구가 막힘 → 기절', dx + 0.2, 4.2, dw - 0.4, 0.55, { size: 10.5, bold: true });
    // 보상
    card(s, X0, 5.6, 6.6, 1.22);
    rect(s, X0, 5.6, 0.12, 1.22, C.draft);
    text(s, '보상', X0 + 0.3, 5.68, 1, 0.32, { size: 13, bold: true, color: C.rust });
    DR(s, X0 + 1.0, 5.7);
    text(s, '기본 부품 40 + 원거리 · 제어 스킬 「흡입」 — 앞쪽 적을 끌어당겨 모은다. 집행자 레이저 파동과 같은 엘리트 스킬 보상', X0 + 0.3, 6.05, 6.1, 0.72, { size: 11 });
    card(s, dx, 5.0, dw, 1.82, { fill: C.coal, flat: true });
    text(s, '결정 / 초안', dx + 0.25, 5.1, 3, 0.3, { size: 12, bold: true, color: 'F2C27A' });
    OK(s, dx + 0.25, 5.5); text(s, '거대 준설기를 엘리트로 넣는다 (2026-11-09)', dx + 1.0, 5.5, dw - 1.2, 0.28, { size: 10.5, color: 'FFFFFF', valign: 'middle' });
    DR(s, dx + 0.25, 5.88); text(s, '보상 스킬 「흡입」 — 확정 전', dx + 1.0, 5.88, dw - 1.2, 0.28, { size: 10.5, color: 'FFFFFF', valign: 'middle' });
    DR(s, dx + 0.25, 6.26); text(s, '체력 · 흡입 세기 · 기절 시간 — 수치 없음', dx + 1.0, 6.26, dw - 1.2, 0.28, { size: 10.5, color: 'FFFFFF', valign: 'middle' });
  }

  // 11 존 보스 — 늪의 어미: 개요 · 페이즈
  {
    const s = slide('10  존 보스', '늪의 어미 — 썰물과 밀물', '늪 전체에 뿌리내린 거대한 진화 유기체. 껍질 숙주들이 그 새끼다. 삼킨 기계를 몸에 박아 두었다(살 + 철판)', 'ok');
    const P = [
      ['썰물', '수위 낮음 · 진흙 바닥이 드러남', ['촉수 3개가 땅에서 솟아 휩쓸고 내려찍는다', '촉수 뿌리의 발광 혹을 치면 촉수가 끊긴다'], C.mud, false],
      ['밀물', '수위 높음 · 진흙이 잠김', ['지붕 · 파이프 · 떠오르는 알 껍질 위로 피한다', '본체가 떠올라 독 덩어리를 흩뿌리고 독 숨을 쏜다', '약점 노출: 입 안 응축된 혼'], C.toxDk, true],
      ['HP 50% 이하', '주기가 빨라짐', ['밀물 · 썰물이 더 자주 바뀐다', '알 껍질이 가라앉는 시간이 짧아진다', '쓰러진 거북 껍질이 가장자리 발판으로 떠오른다'], C.grudge, true],
    ];
    const bw = (CW - 0.4) / 3;
    P.forEach(([h, sub, b, c, high], i) => {
      const x = X0 + i * (bw + 0.2), y = 1.8;
      card(s, x, y, bw, 4.95);
      rect(s, x, y, bw, 0.62, c, { shape: pres.shapes.ROUNDED_RECTANGLE });
      rect(s, x, y + 0.4, bw, 0.22, c);
      text(s, `${i + 1}  ${h}`, x + 0.2, y, bw - 0.4, 0.62, { size: 17, bold: true, color: 'FFFFFF', valign: 'middle' });
      // 미니 단면: 수위
      const mx = x + 0.2, my = y + 0.8, mw = bw - 0.4, mh = 2.1;
      rect(s, mx, my, mw, mh, 'F7F5F2');
      const lvl = high ? 0.85 : 1.65;
      rect(s, mx, my + mh - 0.3, mw, 0.3, C.mud);
      sludge(s, mx, my + lvl, mw, mh - lvl - (high ? 0 : 0.3), true);
      s.addShape(pres.shapes.ISOSCELES_TRIANGLE, { x: mx + 0.15, y: my + 0.15, w: 0.8, h: 0.4, fill: { color: C.mach }, line: { color: C.coal, width: 0.75 } });
      rect(s, mx + 0.25, my + 0.55, 0.6, mh - 0.55, '7A7E84', { tr: 30 });
      if (!high) {
        [0.45, 0.62, 0.8].forEach((k) => { line(s, mx + mw * k, my + mh - 0.3, mx + mw * k + 0.1, my + 0.35, C.flesh, 4); oval(s, mx + mw * k - 0.09, my + mh - 0.42, 0.2, 0.2, 'F2C27A'); });
        text(s, '촉수 · 발광 혹', mx + mw * 0.4, my + 0.05, 1.6, 0.26, { size: 9, bold: true, color: C.flesh });
      } else {
        shell(s, mx + mw * 0.42, my + lvl - 0.18, 0.5, 0.18, 'E8E0C8', false);
        text(s, '알 껍질', mx + mw * 0.36, my + lvl - 0.45, 0.8, 0.24, { size: 9, bold: true, color: C.ink });
        const bx = mx + mw * (i === 2 ? 0.58 : 0.68);
        oval(s, bx, my + lvl - 0.35, 0.75, 0.6, C.flesh, { line: C.coal });
        oval(s, bx + 0.27, my + lvl - 0.15, 0.2, 0.2, C.soul);
        if (i === 2) { shell(s, mx + mw * 0.84, my + lvl - 0.16, 0.5, 0.16, C.ash, false); text(s, '거북 껍질', mx + mw * 0.78, my + lvl - 0.45, 0.8, 0.24, { size: 9, bold: true, color: C.ink }); text(s, '⇅ 빨라짐', mx + mw * 0.05, my + 1.4, 1.2, 0.3, { size: 11, bold: true, color: 'FFFFFF' }); }
        text(s, '본체 · 입 안 혼', bx - mw * 0.13, my + lvl + 0.3, 1.5, 0.24, { size: 9, bold: true, color: 'FFFFFF' });
      }
      text(s, sub, x + 0.2, y + 3.05, bw - 0.4, 0.3, { size: 11.5, bold: true, color: c });
      text(s, bullets(b), x + 0.2, y + 3.45, bw - 0.4, 1.45, { size: 11.5, ls: 1.35 });
    });
    caption(s, '아레나: 가라앉은 마을 광장 — 지붕 · 둑 · 쓰러진 파이프가 섞인 원형 공간. 수위가 오르내린다', X0, 6.8, CW);
  }

  // 12 보스 공략 수단 · 원혼 파츠 없음 (어두운 판)
  {
    const s = darkSlide('11  존 보스 · 공략', '파츠 없이도 깬다. 파츠가 있으면 쉬워진다', '모은 파츠를 총동원하는 시험. 보상은 연구소 열쇠와 정화 — 원혼 파츠는 없다', 'ok');
    card(s, X0, 1.85, 5.6, 2.1, DK);
    chip(s, '파츠 없이', X0 + 0.25, 2.05, 1.4, 0.34, C.ash, 'FFFFFF', 11);
    text(s, '지붕 동선만으로 밀물을 버틴다.\n썰물에 촉수를 끊어 본체를 끌어낸다.', X0 + 0.25, 2.55, 5.1, 1.2, { size: 14, color: 'FFFFFF', ls: 1.4 });
    const Q = [['내오염 코어', '밀물에도 얕은 곳을 걸어 다닌다', C.toxDk], ['캐논 포', '떠오른 본체의 약점을 멀리서 친다', C.rust], ['로프 팔', '촉수 혹을 뜯어 던진다 (뜯기 기믹)', C.rope]];
    const qx = X0 + 5.85, qw = CW - 5.85;
    text(s, '파츠가 있으면', qx, 1.85, 3, 0.34, { size: 14, bold: true, color: 'F2C27A' });
    Q.forEach(([h, b, c], i) => {
      const y = 2.3 + i * 0.56;
      chip(s, h, qx, y, 1.6, 0.4, c, 'FFFFFF', 11);
      text(s, b, qx + 1.8, y, qw - 1.8, 0.4, { size: 12.5, color: 'E6E1D9', valign: 'middle' });
    });
    // 원혼 파츠 규칙
    card(s, X0, 4.25, CW, 2.5, { fill: '2E2224', line: C.grudge, flat: true });
    text(s, '규칙 — 유기체 보스는 원혼 파츠가 없다', X0 + 0.3, 4.38, CW - 0.6, 0.45, { size: 18, bold: true, color: 'FFFFFF' });
    text(s, bullets([
      '원혼은 기계에 깃든 원한이다. 유기체 보스에게는 생기지 않는다 (사용자 결정)',
      '그래서 원혼 파츠는 최대 8개가 아니라 기계 보스 수만큼이다',
      '유기체 보스가 몇 체인지는 다른 존을 기획할 때 정한다',
    ]), X0 + 0.3, 4.95, 7.4, 1.7, { size: 12.5, color: 'E6E1D9', ls: 1.45 });
    const R = [['연구소 열쇠 1/2', C.gold], ['정화 · 표정 +1', C.soul], ['원혼 파츠', '5A5F66']];
    R.forEach(([t, c], i) => {
      chip(s, t, X0 + 8.1, 4.98 + i * 0.55, 3.75, 0.42, c, 'FFFFFF', 12);
    });
    line(s, X0 + 9.2, 5.08 + 2 * 0.55 + 0.11, X0 + 10.75, 5.08 + 2 * 0.55 + 0.11, 'FFFFFF', 2);
  }

  // 13 정화 연출
  {
    const s = slide('12  정화', '정화 연출 — 늪이 맑은 물로 돌아온다', '어미가 쓰러지면 슬러지가 걷히고, 알 껍질에서 작은 거북들이 깨어나 물속으로 사라진다', 'ok');
    const SB = [
      ['어미가 쓰러짐', '본체가 가라앉고 녹색 안개가 옅어진다', 'sludge'],
      ['슬러지가 걷힘', '독 거품이 꺼지고 맑은 물이 차오른다', 'clear'],
      ['갈대 · 반딧불', '물가에 갈대가 서고 반딧불이 뜬다', 'reed'],
      ['작은 거북', '알 껍질이 깨지고 새끼 거북들이 물속으로 사라진다', 'turtle'],
    ];
    const cw = (CW - 0.6) / 4, ih = 2.5;
    SB.forEach(([h, b, k], i) => {
      const x = X0 + i * (cw + 0.2), y = 1.8;
      card(s, x, y, cw, ih + 1.5);
      const fx = x + 0.12, fy = y + 0.12, fw = cw - 0.24, fh = ih - 0.14;
      const sky = { sludge: '3B4A2A', clear: '4F6E6A', reed: '2E4F5E', turtle: '2E4F5E' }[k];
      rect(s, fx, fy, fw, fh, sky);
      const wy = fy + fh * 0.58;
      rect(s, fx, wy, fw, fh - (wy - fy), k === 'sludge' ? C.toxDk : k === 'clear' ? '5E8F7A' : C.water);
      if (k === 'sludge') { rect(s, fx, fy, fw, fh * 0.55, C.tox, { tr: 70 }); oval(s, fx + fw * 0.3, wy - 0.25, fw * 0.4, 0.5, C.flesh, { line: C.coal }); [0.2, 0.7, 0.85].forEach((q) => oval(s, fx + fw * q, wy + 0.2, 0.12, 0.12, 'C9E08A')); }
      if (k === 'clear') { rect(s, fx, wy, fw * 0.45, fh - (wy - fy), C.toxDk, { tr: 30 }); arrow(s, fx + fw * 0.5, wy + 0.4, fx + fw * 0.15, wy + 0.4, 'FFFFFF', 1.5); }
      if (k === 'reed') { for (let r = 0; r < 7; r++) line(s, fx + 0.2 + r * 0.12, wy + 0.1, fx + 0.25 + r * 0.12, wy - 0.55 - (r % 3) * 0.12, '9DBF6A', 2); [[0.55, 0.25], [0.7, 0.4], [0.8, 0.15], [0.62, 0.55], [0.9, 0.35]].forEach(([a, b2]) => oval(s, fx + fw * a, fy + fh * b2, 0.09, 0.09, 'F7E27A')); }
      if (k === 'turtle') { shell(s, fx + fw * 0.15, wy - 0.15, 0.7, 0.25, 'E8E0C8', false); [[0.5, 0.2], [0.65, 0.45], [0.8, 0.3]].forEach(([a, b2]) => oval(s, fx + fw * a, wy + b2, 0.26, 0.16, '6E8A4A', { line: C.coal })); }
      badge(s, i + 1, x + 0.18, y + ih + 0.12, 0.42, i === 3 ? C.water : C.rust);
      text(s, h, x + 0.72, y + ih + 0.12, cw - 0.85, 0.42, { size: 15, bold: true, valign: 'middle' });
      text(s, b, x + 0.18, y + ih + 0.65, cw - 0.36, 1.0, { size: 11.5, color: C.muted });
      if (i < 3) arrow(s, x + cw + 0.02, y + 1.2, x + cw + 0.18, y + 1.2, C.ash, 2);
    });
    caption(s, '도형 스토리보드(설명용). 컨셉 이미지 · Unity 캡처는 아직 없음', X0, 6.0, CW);
  }

  // 14 기록
  {
    const s = slide('13  기록', '기록 — Z0 생존자를 늪까지 이어간다', 'Z0 규칙 그대로 6~8개. 어떤 루트로 와도 이야기가 이어진다', 'mix');
    const colW = (CW - 0.4) / 3;
    const G = [
      ['자기 존 단서', C.toxDk, ['"물이 빠질 때 뿌리를 끊어라"', '"알 껍질은 잠깐만 떠 있다"'], '보스 공략을 문장으로 흘린다'],
      ['이웃 존 단서', C.mach, ['Z5 죽은 숲', 'Z4 공장 — 내오염 코어', '연구소 열쇠'], '다음에 갈 곳 · 반대 입구를 가리킨다'],
      ['Z0 생존자', C.rust, ['하윤이 민재를 찾아 늪까지 왔다', '서 중사 분대의 마지막 무전', '우진이 나르던 약의 행방'], 'Z0 인물들이 늪까지 온 마지막 기록'],
    ];
    G.forEach(([h, c, items, foot], i) => {
      const x = X0 + i * (colW + 0.2), y = 1.8;
      card(s, x, y, colW, 4.0);
      rect(s, x, y, colW, 0.12, c);
      text(s, h, x + 0.25, y + 0.25, colW - 0.5, 0.42, { size: 17, bold: true, color: c });
      items.forEach((t, k) => {
        card(s, x + 0.25, y + 0.85 + k * 0.78, colW - 0.5, 0.64, { fill: 'F7F5F2', flat: true });
        text(s, t, x + 0.4, y + 0.85 + k * 0.78, colW - 0.8, 0.64, { size: 12, bold: true, valign: 'middle' });
      });
      text(s, foot, x + 0.25, y + 3.35, colW - 0.5, 0.55, { size: 11, color: C.muted });
    });
    card(s, X0, 6.0, CW, 0.8, { fill: 'F7F5F2', flat: true });
    OK(s, X0 + 0.25, 6.26); text(s, 'Z0 생존자를 이어간다 (사용자 결정)', X0 + 1.0, 6.0, 4.5, 0.8, { size: 11.5, valign: 'middle' });
    DR(s, X0 + 5.6, 6.26); text(s, '위 문구는 예시. 기록 문구 초안은 Claude가 쓰고 사용자가 고친다', X0 + 6.35, 6.0, 5.7, 0.8, { size: 11.5, valign: 'middle' });
  }

  // 15 남은 것
  {
    const s = slide('14  남은 것', '남은 것 · 구현 순서', '결정이 남은 것과 구현 순서(제안)', 'todo');
    const Q = [
      ['준설기 스킬 「흡입」 확정', '원거리 · 제어 엘리트 스킬. 이름 · 효과 확정 필요', '사용자 결정'],
      ['늪 기록 문구', '초안(Claude) → 사용자 수정. 6~8개', '초안 작성'],
      ['유기체 보스 수', '원혼 파츠 개수(= 기계 보스 수)가 여기서 정해진다', '다른 존 기획 때'],
      ['수치', '엘리트 · 어미 체력, 수위 주기, 알 껍질 유지 시간', '플레이 검토'],
    ];
    Q.forEach(([h, b, tag], i) => {
      const y = 1.8 + i * 0.8;
      card(s, X0, y, CW, 0.68);
      badge(s, i + 1, X0 + 0.22, y + 0.13, 0.42);
      text(s, h, X0 + 0.85, y, 3.6, 0.68, { size: 14.5, bold: true, valign: 'middle' });
      text(s, b, X0 + 4.5, y, CW - 6.4, 0.68, { size: 11.5, color: C.muted, valign: 'middle' });
      chip(s, tag, X0 + CW - 1.75, y + 0.18, 1.55, 0.32, i === 0 ? C.rust : C.grudge, 'FFFFFF', 10);
    });
    text(s, '구현 순서 (제안)', X0, 5.1, 4, 0.34, { size: 14, bold: true, color: C.rust });
    const O = [['지형', '슬러지 · 수위'], ['몬스터 5종', '영역 생태계'], ['엘리트 2', '껍질 숙주 · 준설기'], ['늪의 어미', '썰물 · 밀물'], ['기록', '6~8개'], ['모델', 'Codex']];
    const ow = CW / 6;
    O.forEach(([h, b], i) => {
      const x = X0 + i * ow, c = [C.mud, C.toxDk, C.poison, C.flesh, C.rust, C.mach][i];
      s.addShape(pres.shapes.CHEVRON, { x, y: 5.55, w: ow - 0.03, h: 1.0, fill: { color: c }, line: { color: c } });
      text(s, h, x + 0.55, 5.6, ow - 0.85, 0.48, { size: 13.5, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, x + 0.55, 6.05, ow - 0.85, 0.4, { size: 10, color: 'FFFFFF' });
    });
  }

  await pres.writeFile({ fileName: OUT });
  await koreanWrap(OUT);
  console.log('saved', OUT);
}

// 한글이 글자 중간에서 잘리지 않게: 글자 언어 ko-KR + 문단 eaLnBrk="0" (어절 단위 줄바꿈)
async function koreanWrap(file) {
  const JSZip = require('jszip');
  const zip = await JSZip.loadAsync(fs.readFileSync(file));
  for (const name of Object.keys(zip.files).filter((n) => /^ppt\/(slides|slideLayouts|slideMasters)\/[^/]+\.xml$/.test(n))) {
    let s = await zip.file(name).async('string');
    s = s.replace(/<a:pPr(?![^>]*eaLnBrk)/g, '<a:pPr eaLnBrk="0"').replace(/<a:p>(?!<a:pPr)/g, '<a:p><a:pPr eaLnBrk="0"/>')
      .replaceAll('lang="en-US"', 'lang="ko-KR" altLang="en-US"');
    zip.file(name, s);
  }
  fs.writeFileSync(file, await zip.generateAsync({ type: 'nodebuffer', compression: 'DEFLATE' }));
}

build().catch((e) => { console.error(e); process.exit(1); });

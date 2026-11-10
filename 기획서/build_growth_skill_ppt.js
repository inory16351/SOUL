// ALONE(가제) 성장 · 스킬 시스템 기획서 PPT 생성 → 기획서/20261110_ALONE_성장스킬시스템기획서_v0.1_정승운.pptx
// 사용: NODE_PATH=<pptxgenjs · sharp · jszip 설치 폴더>/node_modules node 기획서/build_growth_skill_ppt.js
// 내용 원본: 기획_결정사항.md '부위 슬롯 + 공용 노드 + 모듈 업그레이드 (2026-11-10 결정)',
//           기획서/스킬_슬롯_카탈로그.md (스킬 60 · 노드 · 존 배치 · 상인 표는 이 md의 표를 그대로 읽는다)
// 스타일 · 도구는 build_skill_ppt.js 와 같음(복사). 그림: 기획서/art (ChatGPT, prompts.md) → 없으면 자리 표시
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const sharp = require('sharp');

const ROOT = __dirname;
const ART = path.join(ROOT, 'art');
const CAP = path.join(ROOT, '..', 'unity', 'Captures');
const UI = path.join(ROOT, '..', 'unity', 'Assets', 'SOUL', 'Resources', 'UI');
const MD = path.join(ROOT, '스킬_슬롯_카탈로그.md');
const VER = 'v0.1';
const DATE = '2026-11-10';
const OUT = path.join(ROOT, `20261110_ALONE_성장스킬시스템기획서_${VER}_정승운.pptx`);
const DOC = `ALONE(가제) · 성장 · 스킬 시스템 기획서 ${VER}`;

const FONT = 'Noto Sans KR';
const C = {
  coal: '16181B', deep: '0E1012', ink: '23272C', muted: '646B73', bg: 'EEEBE6', card: 'FFFFFF', line: 'D9D4CC', tint: 'E4DED5',
  rust: 'C2562B', ash: '8A8F96', soul: '33B5C8', grudge: 'B8322A', gold: 'E0A030',
  blue: '3F8FD8', red: 'D9502F', rope: 'E0A030', ok: '2E8B6E', draft: 'D99A1E',
};
// 부위 색 (성장 트리 갈래 색과 같은 계열)
const PART = {
  머리: { c: '7B5FC2', en: 'HEAD' }, 몸통: { c: 'D9502F', en: 'CORE' }, 무기: { c: 'C99A2E', en: 'BLADE' }, 팔: { c: '3F8FD8', en: 'ARM' }, 다리: { c: '3E9E6E', en: 'LEGS' },
};
// 획득처 색
const SRC = { 엘리트: 'B8322A', 기둥: '33B5C8', 상인: 'E0A030', 기록: '6B7A8F', 숨은: '2E8B6E', 희귀: '8E6AD8' };
const srcKind = (t) => (t.startsWith('엘리트') ? '엘리트' : t.startsWith('공명') ? '기둥' : t.startsWith('상인') ? '상인' : t.startsWith('생존자') ? '기록' : t.startsWith('숨은') ? '숨은' : t.startsWith('희귀') ? '희귀' : '');
const W = 13.333, H = 7.5, X0 = 0.6, CW = W - 2 * X0;

// ---------- md 표 읽기 ----------
const mdText = fs.readFileSync(MD, 'utf8');
const clean = (t) => t.replace(/\*\*/g, '').replace(/`/g, '').trim();
/// heading 다음에 나오는 첫 표 (머리줄 포함, 구분줄 제외)
function mdTable(heading, nth = 0) {
  const i = mdText.indexOf(heading);
  if (i < 0) throw new Error('md heading not found: ' + heading);
  const lines = mdText.slice(i).split(/\r?\n/);
  const tables = []; let cur = null;
  for (const l of lines.slice(1)) {
    if (/^#{2,3} /.test(l) && tables.length + (cur ? 1 : 0) > nth) break;
    if (/^\s*\|/.test(l)) { if (!cur) cur = []; if (!/^\s*\|\s*-/.test(l)) cur.push(l.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(clean)); }
    else if (cur) { tables.push(cur); cur = null; if (tables.length > nth) break; }
  }
  if (cur) tables.push(cur);
  if (!tables[nth]) throw new Error('md table not found: ' + heading);
  return tables[nth];
}
const CAT = {
  무기: mdTable('### 8-1.'), 팔: mdTable('### 8-2.'), 다리: mdTable('### 8-3.'), 몸통: mdTable('### 8-4.'), 머리: mdTable('### 8-5.'),
};
const NODES = mdTable('## 4. 공용 노드');
const ZONES = mdTable('## 9. 존별 배치');
const ELITES = mdTable('### 엘리트 목록');
const MERCH = mdTable('## 10. 상인');
const MODS = mdTable('## 5. 모듈 업그레이드');
const SRCS = mdTable('## 7. 획득처 규칙', 0);
const ZSRC = mdTable('## 7. 획득처 규칙', 1);
const ALL = Object.entries(CAT).flatMap(([p, t]) => t.slice(1).map((r) => ({ part: p, id: r[0], name: r[1], kind: r[2], eff: r[3], lv3: r[4], lv5: r[5], src: r[6], zone: r[7] })));
if (ALL.length !== 60) throw new Error('skills: ' + ALL.length);

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = '정승운';
pres.title = 'ALONE(가제) 성장 · 스킬 시스템 기획서 — 부위 슬롯 · 공용 노드 · 모듈 업그레이드';
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
  for (const dir of [CAP, ART]) if (fs.existsSync(dir)) for (const f of fs.readdirSync(dir)) if (/\.(png|jpe?g)$/i.test(f) && has(f)) { try { const m = await sharp(A(f)).metadata(); dims[f] = [m.width, m.height]; } catch (e) { /* 깨진 · 빈 파일은 건너뜀 */ } }
}
async function addDim(f) { if (fs.existsSync(f)) { const m = await sharp(f).metadata(); dims[path.basename(f)] = [m.width, m.height]; } }
const shadow = () => ({ type: 'outer', color: '000000', opacity: 0.16, blur: 6, offset: 2, angle: 90 });

function text(s, t, x, y, w, h, o = {}) {
  s.addText(t, { x, y, w, h, fontFace: FONT, fontSize: o.size || 13, color: o.color || C.ink, bold: !!o.bold, align: o.align || 'left', valign: o.valign || 'top', margin: 0, isTextBox: true, paraSpaceAfter: o.gap || 0, lineSpacingMultiple: o.ls || 1.15, fit: 'none' });
}
function card(s, x, y, w, h, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.1, fill: { color: o.fill || C.card }, line: { color: o.line || o.fill || C.card, width: o.lw || 0.75, dashType: o.dash ? 'dash' : 'solid' }, shadow: o.flat ? undefined : shadow() });
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
function img(s, f, x, y, w, h, o = {}) {
  if (!has(f) || !dims[path.basename(f)]) {
    s.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: o.dark ? '24282D' : 'CFC9C0' }, line: { color: o.dark ? '4A4F56' : 'B9B2A8', dashType: 'dash' } });
    text(s, `이미지 자리: ${path.basename(f)}`, x, y, w, h, { size: 11, color: o.dark ? '8E8880' : C.muted, align: 'center', valign: 'middle' });
    return null;
  }
  const [iw, ih] = dims[path.basename(f)];
  const r = Math.min(w / iw, h / ih), ww = iw * r, hh = ih * r;
  s.addImage({ path: A(f), x: x + (w - ww) / 2, y: y + (h - hh) / 2, w: ww, h: hh, shadow: o.shadow ? shadow() : undefined });
  return { x: x + (w - ww) / 2, y: y + (h - hh) / 2, w: ww, h: hh };
}
async function coverCrop(f, w, h, focusY = 0.5) {
  if (!has(f) || !dims[path.basename(f)]) return null;
  const out = path.join(ART, '_crop', `${path.parse(f).name}_${Math.round(w * 100)}x${Math.round(h * 100)}.jpg`);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  const [iw, ih] = dims[path.basename(f)];
  const tr = w / h, ir = iw / ih;
  let cw = iw, ch = ih, left = 0, top = 0;
  if (ir > tr) { cw = Math.round(ih * tr); left = Math.round((iw - cw) / 2); } else { ch = Math.round(iw / tr); top = Math.round((ih - ch) * focusY); }
  await sharp(A(f)).extract({ left, top, width: cw, height: ch }).jpeg({ quality: 90 }).toFile(out);
  return out;
}
async function cimg(s, f, x, y, w, h, o = {}) {
  const p = await coverCrop(f, w, h, o.focusY);
  if (!p) return img(s, f, x, y, w, h, o);
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
  const T = { ok: [['결정', C.ok]], draft: [['초안 · 검토 필요', C.draft]], mix: [['결정', C.ok], ['초안 섞임', C.draft]], todo: [['정할 것', C.grudge]], impl: [['구현 현황', C.coal]] }[st];
  let x = right;
  for (let i = T.length - 1; i >= 0; i--) { const [t, c] = T[i]; const w = 0.32 + t.length * 0.14; x -= w; chip(s, t, x, y, w, 0.32, c, 'FFFFFF', 10); x -= 0.1; }
}
const OK = (s, x, y, w = 0.62) => chip(s, '결정', x, y, w, 0.28, C.ok, 'FFFFFF', 9.5);
const DR = (s, x, y, w = 0.62) => chip(s, '초안', x, y, w, 0.28, C.draft, 'FFFFFF', 9.5);
function table(s, rows, x, y, w, colW, o = {}) {
  const head = rows[0].map((t) => ({ text: t, options: { bold: true, color: 'FFFFFF', fill: { color: o.headFill || C.coal }, align: 'center' } }));
  const body = rows.slice(1).map((r, i) => r.map((t, j) => {
    const cell = typeof t === 'object' ? t : { text: t };
    return { text: cell.text, options: { bold: (j === 0 && !o.plainFirst) || cell.bold, color: cell.color || C.ink, fill: { color: cell.fill || (i % 2 ? 'F7F5F2' : C.card) }, align: cell.align || ((j === 0 && !o.leftFirst) || o.center ? 'center' : 'left') } };
  }));
  s.addTable([head, ...body], { x, y, w, colW, fontFace: FONT, fontSize: o.size || 12, valign: 'middle', border: { type: 'solid', color: C.line, pt: 0.75 }, rowH: o.rowH || 0.42, margin: o.margin || [0.04, 0.1, 0.04, 0.1] });
}
const bullets = (a) => a.map((t) => '•  ' + t).join('\n');
function key(s, t, x, y, w = 0.5, h = 0.5, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.06, fill: { color: o.fill || '2A2E33' }, line: { color: o.line || '444A51', width: o.lw || 0.75, dashType: o.dash ? 'dash' : 'solid' } });
  text(s, t, x, y, w, h, { size: o.size || 11, bold: true, color: o.color || 'D7D2CA', align: 'center', valign: 'middle' });
}
// 슬롯 칸 (filled = 스킬 이름, 빈 칸 = 점선)
function slotBox(s, x, y, w, h, label, col, o = {}) {
  const filled = !!label;
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.06, fill: { color: filled ? (o.passive ? 'FFFFFF' : col) : (o.dark ? '22262B' : 'F4F1EC') }, line: { color: filled ? col : (o.dark ? '5A5F66' : 'B9B2A8'), width: filled ? 1.25 : 0.9, dashType: filled ? 'solid' : 'dash' } });
  if (filled) text(s, label, x + 0.04, y, w - 0.08, h, { size: o.size || 9.5, bold: true, color: o.passive ? col : 'FFFFFF', align: 'center', valign: 'middle', ls: 1.0 });
  else if (o.ph) text(s, o.ph, x, y, w, h, { size: o.size || 9, color: o.dark ? '6E6962' : '9A948C', align: 'center', valign: 'middle' });
}
// 원혼 소켓 (슬롯 밖)
function grudgeSocket(s, x, y, d, label, o = {}) {
  s.addShape(pres.shapes.HEXAGON, { x, y, w: d * 1.15, h: d, fill: { color: label ? C.grudge : (o.dark ? '2A1F1F' : 'F3E3E0') }, line: { color: C.grudge, width: 1.25, dashType: label ? 'solid' : 'dash' } });
  if (label) text(s, label, x - 0.6, y + d + 0.02, d * 1.15 + 1.2, 0.26, { size: 8.5, bold: true, color: C.grudge, align: 'center' });
}

// ---------- 슬라이드 ----------
async function build() {
  await loadDims();
  await addDim(path.join(UI, 'growth_tree_bg.png'));

  // 1 표지
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    const cov = has('growth_cover.png') ? 'growth_cover.png' : 'key_art.png';
    await cimg(s, cov, 0, 0, W, H, { focusY: 0.5 });
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: 3.3, fill: { color: C.deep, transparency: 35 }, line: { color: C.deep, transparency: 100 } });
    text(s, 'SYSTEM DESIGN DOCUMENT  ·  GROWTH & SKILL', 0, 0.45, W, 0.35, { size: 13, bold: true, color: C.soul, align: 'center' });
    text(s, '골라 끼우는 몸', 0, 0.8, W, 1.1, { size: 60, bold: true, color: 'FFFFFF', align: 'center' });
    text(s, '성장 · 스킬 시스템 기획서 — 부위 슬롯 · 공용 노드 · 모듈 업그레이드 · 스킬 60', 0, 1.9, W, 0.35, { size: 15, color: 'D7D2CA', align: 'center' });
    text(s, '기계와 진화 유기체에게서 뜯어 온 부품을 다섯 부위에 골라 끼운다 — 무엇을 달지가 곧 빌드다', 0, 2.3, W, 0.4, { size: 15, color: 'F0ECE6', align: 'center' });
    text(s, `성장 · 스킬 시스템 기획서  ·  작성자 정승운  ·  작성일 ${DATE}  ·  버전 ${VER}  ·  스킬 시스템 기획서 v0.1(2026-11-08) 대체`, 0, 2.78, W, 0.3, { size: 11, color: 'D7D2CA', align: 'center' });
  }

  // 2 목차
  {
    const s = slide('CONTENTS', '목차', '결정(2026-11-10)을 먼저, 그 위에 올린 초안(스킬 60 · 노드 · 배치)을 뒤에', null);
    const T = [
      ['01', '개편 의도', '무엇이 바뀌었나'], ['02', '시스템 한눈에 보기', '다섯 층'], ['03', '재화와 루프', '경험치 · 기본 부품'], ['04', '부위 슬롯', '5부위 × 1 ~ 3칸'],
      ['05', '장착 예시', '액티브 6 + 패시브'], ['06', '공용 노드', '트리 그림 · 슬롯 확장'], ['07', '공용 노드 표', '36노드 · 81포인트'], ['08', '모듈 업그레이드', '이동 능력 · 늘 켜짐'],
      ['09', '원혼 파츠', '슬롯 밖 영구 소켓'], ['10', '스킬 카탈로그', '개요 + 부위별 5장'], ['16', '획득처와 존 배치', '규칙 · 지도 · 표 · 엘리트'], ['20', '상인', '존마다 2 ~ 3개'],
      ['21', 'Z0 첫 시간', '슬롯이 차는 순서'], ['22', 'UI 화면 안', '장착 · 모듈 · 상인 · 강화'], ['24', '열린 질문', '사용자 결정 필요'],
    ];
    const cw = (CW - 0.5) / 3;
    T.forEach(([n, h, b], i) => {
      const col = Math.floor(i / 5), row = i % 5;
      const x = X0 + col * (cw + 0.25), y = 1.8 + row * 0.98;
      card(s, x, y, cw, 0.82);
      text(s, n, x + 0.2, y, 0.7, 0.82, { size: 22, bold: true, color: C.rust, valign: 'middle' });
      text(s, h, x + 0.95, y + 0.1, cw - 1.1, 0.36, { size: 15, bold: true });
      text(s, b, x + 0.95, y + 0.45, cw - 1.1, 0.3, { size: 11, color: C.muted });
    });
  }

  // 3 개편 의도
  {
    const s = slide('01  개편 의도', '무제한 스킬에서, 골라 끼우는 몸으로', '2026-11-08 "스킬 = 파츠"(얻는 대로 키 지정, 슬롯 없음)를 2026-11-10 결정으로 바꾼다', 'ok');
    table(s, [
      ['항목', '이전 (2026-11-08)', '지금 (2026-11-10)'],
      ['스킬 수', '제한 없음. 얻을 때마다 새 키', '부위 슬롯 5부위 × 1 ~ 3칸, 액티브 최대 6'],
      ['성장 노드', '스탯 + 반격 · 로프 강화 + 파츠 조건', '간단한 스탯 + 그 부위 슬롯 +1 만'],
      ['경험치', '주운 기본 부품의 누적', '처치로 얻음. 죽어도 안 잃음'],
      ['화폐', '기본 부품 + 원혼이 깃든 부품', '기본 부품 하나 (원혼이 깃든 부품 삭제)'],
      ['이동 능력', '파츠 스킬처럼 키 지정', '모듈 업그레이드 — 슬롯 밖, 늘 켜짐'],
      ['원혼 파츠', '스킬형 · 각인형 (새 키)', '그 부위에 슬롯 밖 소켓으로 영구 부착'],
      ['일반 몹', '포드 휴식 때 부활', '시간이 지나면 다시 나타남. 파밍 무제한'],
    ], X0, 1.8, 8.1, [1.45, 3.05, 3.6], { rowH: 0.5, size: 11 });
    const G = [
      ['고르는 재미', '스킬 60개 중 15칸만 낀다. 무엇을 빼고 무엇을 올릴지가 빌드', C.rust],
      ['키 부담 줄이기', '스킬이 늘어도 누르는 키는 6개 + 모듈 고정 키', C.blue],
      ['파밍이 곧 성장', '리스폰 몹 → 경험치는 노드, 부품은 상인 · 강화로', C.ok],
    ];
    const gx = 9.0, gw = W - X0 - gx;
    G.forEach(([h, b, c], i) => {
      const y = 1.8 + i * 1.38;
      card(s, gx, y, gw, 1.22);
      s.addShape(pres.shapes.RECTANGLE, { x: gx, y, w: 0.1, h: 1.22, fill: { color: c }, line: { color: c } });
      text(s, h, gx + 0.3, y + 0.12, gw - 0.45, 0.4, { size: 15, bold: true, color: c });
      text(s, b, gx + 0.3, y + 0.55, gw - 0.45, 0.62, { size: 11, color: C.muted });
    });
    text(s, '기준 문서: 기획_결정사항.md "부위 슬롯 + 공용 노드 + 모듈 업그레이드 (2026-11-10 결정)". 이전 "스킬 = 파츠" · "성장 노드 트리" 절보다 우선', X0, 6.05, CW, 0.5, { size: 10.5, color: C.muted });
    text(s, '이 문서의 표시', X0, 6.5, 1.5, 0.28, { size: 10.5, bold: true, color: C.muted, valign: 'middle' });
    OK(s, X0 + 1.45, 6.5); text(s, '사용자 결정', X0 + 2.15, 6.5, 2.0, 0.28, { size: 10.5, valign: 'middle' });
    DR(s, X0 + 3.6, 6.5); text(s, '작업 에이전트 초안 · 검토 필요 (스킬 60 · 노드 수치 · 배치 · 가격)', X0 + 4.3, 6.5, 7, 0.28, { size: 10.5, valign: 'middle' });
  }

  // 4 한눈에 보기
  {
    const s = darkSlide('02  시스템 한눈에 보기', '성장은 다섯 층으로 나뉜다', '처치가 경험치와 부품을 주고, 경험치는 몸의 틀(칸)을, 부품은 칸에 낀 스킬을 키운다', 'ok');
    const L = [
      ['경험치', 'XP', '처치 (일반 · 엘리트 · 보스)', '공용 노드 → 스탯 · 슬롯 +1', '잃지 않음', C.soul],
      ['기본 부품', 'PARTS', '처치 드롭 · 보급 상자 · 원혼 정화', '상인 · 스킬 강화 · 수리 키트', '그 자리에 떨어뜨림', C.gold],
      ['슬롯 스킬', '60', '엘리트 · 숨은 곳 · 기둥 · 상인 · 기록 · 희귀', '부위 슬롯 (최대 15칸, 액티브 6)', '잃지 않음', C.rust],
      ['모듈', 'MODULE', '존 보스 · Z0 엘리트', '늘 켜짐 · 슬롯 밖 · 길을 연다', '잃지 않음', C.ok],
      ['원혼 파츠', 'RED', '기계 보스 정화 뒤 붉은 부품', '장착하면 그 부위에 영구 소켓', '잃지 않음 · 뗄 수 없음', C.grudge],
    ];
    const cw = (CW - 4 * 0.2) / 5;
    L.forEach(([h, en, get, use, die, c], i) => {
      const x = X0 + i * (cw + 0.2), y = 1.85;
      card(s, x, y, cw, 4.55, DK);
      s.addShape(pres.shapes.RECTANGLE, { x, y, w: cw, h: 0.1, fill: { color: c }, line: { color: c } });
      text(s, en, x + 0.2, y + 0.25, cw - 0.4, 0.3, { size: 11, bold: true, color: c });
      text(s, h, x + 0.2, y + 0.55, cw - 0.4, 0.45, { size: 19, bold: true, color: 'FFFFFF' });
      const R = [['얻는 곳', get], ['쓰는 곳', use], ['죽으면', die]];
      R.forEach(([k, v], j) => {
        const yy = y + 1.25 + j * 1.05;
        text(s, k, x + 0.2, yy, cw - 0.4, 0.28, { size: 10, bold: true, color: '8E8880' });
        text(s, v, x + 0.2, yy + 0.28, cw - 0.4, 0.72, { size: 12, color: 'E6E1D9' });
      });
    });
    captionDk(s, '재화는 경험치와 기본 부품 둘뿐. 일반 몹은 시간이 지나면 다시 나타나고, 엘리트 · 보스는 영구 처치', X0, 6.55, CW);
  }

  // 5 재화와 루프
  {
    const s = slide('03  재화와 루프', '처치 하나가 두 갈래로 흐른다', '경험치는 몸의 틀을, 기본 부품은 스킬과 생존을 키운다. 몹이 다시 나타나므로 파밍 제한은 없다', 'mix');
    const box = (t, b, x, y, w, c, h = 0.9) => { card(s, x, y, w, h, { fill: c, flat: true }); text(s, t, x + 0.15, y + 0.08, w - 0.3, 0.36, { size: 13.5, bold: true, color: 'FFFFFF' }); if (b) text(s, b, x + 0.15, y + 0.45, w - 0.3, h - 0.5, { size: 10, color: 'FFFFFF' }); };
    box('처치', '일반 몹 · 엘리트 · 보스', X0, 3.05, 1.7, C.coal, 1.0);
    box('경험치', '죽어도 안 잃음', 2.75, 1.85, 1.8, C.soul);
    box('포인트', '누적 20 · 43 · 69 … (+3씩)', 4.95, 1.85, 1.8, '2A8FA0');
    box('공용 노드', '스탯 · 슬롯 +1', 7.15, 1.85, 1.8, '22707D');
    box('기본 부품', '지갑 · 죽으면 떨어뜨림', 2.75, 4.25, 1.8, C.gold);
    box('상인', '한 번 사면 끝', 4.95, 3.6, 1.8, 'B98222', 0.8);
    box('스킬 강화', 'Lv2 ~ 5', 4.95, 4.5, 1.8, 'B98222', 0.8);
    box('수리 키트', '포드에서 제작', 4.95, 5.4, 1.8, 'B98222', 0.8);
    arrow(s, X0 + 1.7, 3.35, 2.75, 2.3, C.soul, 2); arrow(s, X0 + 1.7, 3.75, 2.75, 4.7, C.gold, 2);
    arrow(s, 4.55, 2.3, 4.95, 2.3, C.soul, 2); arrow(s, 6.75, 2.3, 7.15, 2.3, C.soul, 2);
    arrow(s, 4.55, 4.5, 4.95, 4.0, C.gold, 1.75); arrow(s, 4.55, 4.7, 4.95, 4.9, C.gold, 1.75); arrow(s, 4.55, 4.9, 4.95, 5.8, C.gold, 1.75);
    // 사망 · 리스폰
    card(s, 7.15, 3.6, 1.8, 1.25, { fill: 'F7F5F2', line: C.grudge, lw: 1.25, flat: true });
    text(s, '사망', 7.3, 3.68, 1.5, 0.32, { size: 13, bold: true, color: C.grudge });
    text(s, '부품만 그 자리에. 근처 기계가 주워 감', 7.3, 4.0, 1.55, 0.8, { size: 10, color: C.ink });
    card(s, 7.15, 5.0, 1.8, 1.2, { fill: 'F7F5F2', line: C.ok, lw: 1.25, flat: true });
    text(s, '리스폰', 7.3, 5.08, 1.5, 0.32, { size: 13, bold: true, color: C.ok });
    text(s, '일반 몹은 시간이 지나면 다시. 다시 처치로', 7.3, 5.4, 1.55, 0.75, { size: 10, color: C.ink });
    line(s, 8.05, 6.2, 8.05, 6.3, C.ok, 1.25, true); line(s, 8.05, 6.3, X0 + 0.85, 6.3, C.ok, 1.25, true); arrow(s, X0 + 0.85, 6.3, X0 + 0.85, 4.07, C.ok, 1.25);
    // 오른쪽 표
    const rx = 9.4, rw = W - X0 - rx;
    text(s, '기본 부품 쓰는 곳 (초안)', rx, 1.8, rw, 0.32, { size: 13, bold: true, color: C.rust });
    table(s, [
      ['쓰는 곳', '가격'],
      ['상인 · 바깥 존', '약 80'],
      ['상인 · 깊은 존', '약 200'],
      ['상인 · 연구소', '약 350'],
      ['강화 Lv2 / 3 / 4 / 5', '30 / 60 / 100 / 150'],
      ['수리 키트 1개', '25 (소지 3)'],
    ], rx, 2.18, rw, [1.95, rw - 1.95], { rowH: 0.4, size: 10.5, leftFirst: true });
    text(s, '경험치 (초안)', rx, 4.75, rw, 0.32, { size: 13, bold: true, color: C.soul });
    text(s, bullets(['일반 2 ~ 6 · 엘리트 60 · 보스 200', '깊은 존 2배 · 연구소 3배', '노드 36개 = 81포인트 ≈ 경험치 11,300']), rx, 5.1, rw, 1.1, { size: 10.5, ls: 1.3 });
    OK(s, X0, 6.5); text(s, '두 재화 · 처치 경험치 · 리스폰 · 강화 비용 · 상인 가격대', X0 + 0.7, 6.5, 6, 0.28, { size: 10.5, valign: 'middle' });
    DR(s, 9.4, 6.5); text(s, '경험치 수치 · 키트 가격', 10.1, 6.5, 4, 0.28, { size: 10.5, valign: 'middle' });
  }

  // 6 부위 슬롯
  {
    const s = slide('04  부위 슬롯', '다섯 부위, 부위마다 1 ~ 3칸', '시작 5칸 → 공용 노드로 최대 15칸. 스킬은 들어갈 부위가 정해져 있다', 'ok');
    const fig = path.join(ART, '_crop', 'character_front.png');
    if (has('character.png')) { fs.mkdirSync(path.dirname(fig), { recursive: true }); await sharp(A('character.png')).extract({ left: 30, top: 160, width: 430, height: 650 }).png().toFile(fig); dims['character_front.png'] = [430, 650]; }
    const fx = 5.3, fy = 1.75, fw = 2.72, fh = 4.12;
    s.addShape(pres.shapes.OVAL, { x: fx - 0.2, y: fy + fh - 0.35, w: fw + 0.4, h: 0.5, fill: { color: 'D9D4CC' }, line: { color: 'D9D4CC' } });
    img(s, fig, fx, fy, fw, fh);
    const P = (px, py) => [fx + (px - 30) / 430 * fw, fy + (py - 160) / 650 * fh];
    caption(s, '정면 · 화면 왼쪽 칼날이 로봇의 오른팔', fx - 0.3, fy + fh + 0.12, fw + 0.6);
    const LW = 4.4, RX = 8.33, RW = W - X0 - RX;
    const parts = [
      ['머리', '감지 · 표식 · 약점', '패시브 많음 (A 4 · P 8)', P(150, 250), X0, 1.75, LW],
      ['무기', '오른팔 칼날 · 피해 · 범위', '액티브 많음 (A 8 · P 4)', P(110, 520), X0, 3.3, LW],
      ['몸통', '가슴 코어 · 생존 · 독 · 불', 'A 5 · P 7', P(255, 420), RX, 1.75, RW],
      ['팔', '왼팔 도구 · 제어 · 반격 · 설치', 'A 7 · P 5', P(395, 470), RX, 3.3, RW],
      ['다리', '돌진 · 회피 · 착지', 'A 6 · P 6', P(300, 730), RX, 4.85, RW],
    ];
    for (const [h, b, n, [ax, ay], kx, ky, w] of parts) {
      const c = PART[h].c, hh = 1.38;
      card(s, kx, ky, w, hh);
      s.addShape(pres.shapes.RECTANGLE, { x: kx, y: ky, w: 0.1, h: hh, fill: { color: c }, line: { color: c } });
      text(s, h, kx + 0.25, ky + 0.1, 1.0, 0.36, { size: 15, bold: true, color: c });
      text(s, PART[h].en, kx + 1.05, ky + 0.14, 1.0, 0.3, { size: 9.5, bold: true, color: C.ash });
      text(s, n, kx + w - 2.05, ky + 0.14, 1.9, 0.3, { size: 9.5, color: C.muted, align: 'right' });
      text(s, b, kx + 0.25, ky + 0.45, w - 0.4, 0.3, { size: 10.5, color: C.muted });
      const sw = (w - 0.5 - 0.2) / 3;
      for (let k = 0; k < 3; k++) slotBox(s, kx + 0.25 + k * (sw + 0.1), ky + 0.83, sw, 0.4, null, c, { ph: k === 0 ? '시작 1칸' : k === 1 ? '노드 확장 I' : '노드 확장 II', size: 8.5 });
      const ex = kx < fx ? kx + w : kx;
      line(s, ex, ky + 0.3, ax, ay, c, 1, true);
      s.addShape(pres.shapes.OVAL, { x: ax - 0.07, y: ay - 0.07, w: 0.14, h: 0.14, fill: { color: c }, line: { color: 'FFFFFF', width: 1 } });
    }
    card(s, X0, 4.85, LW, 1.8, { fill: C.coal, flat: true });
    text(s, '규칙', X0 + 0.2, 4.93, 2, 0.3, { size: 12.5, bold: true, color: C.gold });
    text(s, bullets(['무기 스킬은 무기 칸에만 (부위 고정)', '액티브 · 패시브 모두 칸을 차지', '교체는 충전 포드에서만 · 강화 단계는 스킬에 남음', '원혼 파츠와 모듈은 칸 밖']), X0 + 0.2, 5.25, LW - 0.35, 1.38, { size: 10.5, color: 'E6E1D9', ls: 1.28 });
    caption(s, '부위별 A/P 수는 카탈로그 초안 기준', RX, 6.3, RW);
  }

  // 7 장착 예시
  {
    const s = darkSlide('05  장착 예시', '액티브는 6개까지, 남는 칸은 패시브', 'Z0 · Z1 · Z3을 돈 중반 예시: 슬롯 11칸 열림. 키는 장착한 액티브에만 붙는다', 'mix');
    const rows = [
      ['무기', 3, [['회오리', 0], ['레이저 파동', 0], ['진동 칼날', 1]]],
      ['팔', 2, [['반격', 0], ['되받기 장갑', 1]]],
      ['다리', 2, [['피스톤 돌진', 0], ['거미 발 고정', 0]]],
      ['몸통', 2, [['두꺼운 장갑판', 1], null], '뒤틀린 에너지 코어'],
      ['머리', 2, [['섬광 렌즈', 0], ['위험 감지', 1]]],
    ];
    const rx = X0, ry = 1.85;
    rows.forEach(([p, n, list, gr], i) => {
      const y = ry + i * 0.82, c = PART[p].c;
      text(s, p, rx, y, 0.9, 0.62, { size: 14, bold: true, color: c, valign: 'middle' });
      for (let k = 0; k < 3; k++) {
        const it = list[k];
        if (k >= n) { slotBox(s, rx + 0.95 + k * 1.75, y + 0.06, 1.62, 0.5, null, c, { dark: true, ph: '잠김', size: 9 }); continue; }
        slotBox(s, rx + 0.95 + k * 1.75, y + 0.06, 1.62, 0.5, it ? it[0] : null, c, { dark: true, passive: it && it[1], ph: '빈 칸', size: 10 });
      }
      if (gr) { s.addShape(pres.shapes.HEXAGON, { x: rx + 6.3, y: y + 0.08, w: 0.48, h: 0.42, fill: { color: C.grudge }, line: { color: "FF6B5E", width: 1.5 } }); text(s, gr + ' → 코어 과부하', rx + 6.85, y + 0.02, 2.4, 0.6, { size: 9.5, bold: true, color: 'E88A80', valign: 'middle' }); }
    });
    text(s, '채운 칸 = 액티브 · 흰 칸 = 패시브 · 붉은 육각 = 원혼 소켓(슬롯 밖)', rx, ry + 5 * 0.82 + 0.02, 7, 0.28, { size: 10, color: '8E8880' });
    // 키 바
    const kx = 9.35, kw = W - X0 - kx;
    card(s, kx, 1.85, kw, 4.4, DK);
    text(s, '액티브 키 바', kx + 0.25, 1.98, kw - 0.5, 0.32, { size: 13, bold: true, color: C.gold });
    const K = [['Q', '회오리', '무기'], ['W', '레이저 파동', '무기'], ['E', '반격', '팔'], ['R', '피스톤 돌진', '다리'], ['+1', '거미 발 고정', '다리'], ['+2', '섬광 렌즈', '머리']];
    K.forEach(([k, n, p], i) => {
      const y = 2.42 + i * 0.5;
      key(s, k, kx + 0.3, y, 0.55, 0.4, { fill: PART[p].c, color: 'FFFFFF', line: PART[p].c, size: 11 });
      text(s, n, kx + 1.0, y, kw - 1.3, 0.4, { size: 11.5, color: 'E6E1D9', valign: 'middle' });
    });
    key(s, '원혼', kx + 0.3, 5.47, 0.55, 0.4, { fill: C.grudge, color: 'FFFFFF', line: C.grudge, size: 9.5 });
    text(s, '코어 과부하 (6개 밖)', kx + 1.0, 5.47, kw - 1.3, 0.4, { size: 11.5, color: 'E88A80', valign: 'middle' });
    captionDk(s, '+1 · +2는 플레이어가 고르는 보조 키', kx + 0.25, 5.9, kw - 0.4);
    captionDk(s, '모듈(로프 · 제트 · 캐논)은 고정 키를 따로 받는다 — 6개에 넣지 않음', X0, 6.55, CW);
  }

  // 8 공용 노드 그림
  {
    const s = slide('06  공용 노드', '성장 트리 그림은 그대로, 노드는 스탯과 칸', '5갈래 · 36소켓 위치 유지. 갈래마다 슬롯 확장 2개(I = 2칸째, II = 3칸째)', 'mix');
    const bg = path.join(UI, 'growth_tree_bg.png');
    const r = img(s, bg, X0, 1.75, 8.1, 4.56, { shadow: true });
    const pos = {
      c_slot1: [1449, 355], c_slot2: [1519, 195], r_slot1: [320, 310], r_slot2: [147, 562], l_slot1: [1422, 499], l_slot2: [1536, 563],
      g_slot1: [897, 842], g_slot2: [318, 826], h_slot1: [940, 80], h_slot2: [325, 91],
    };
    if (r) for (const [id, [px, py]] of Object.entries(pos)) {
      const x = r.x + px / 1672 * r.w, y = r.y + py / 941 * r.h, d = 0.3;
      s.addShape(pres.shapes.OVAL, { x: x - d / 2, y: y - d / 2, w: d, h: d, fill: { color: C.gold, transparency: 15 }, line: { color: 'FFFFFF', width: 1.5 } });
      text(s, id.endsWith('1') ? 'I' : 'II', x - d / 2, y - d / 2, d, d, { size: 8.5, bold: true, color: C.coal, align: 'center', valign: 'middle' });
    }
    caption(s, '배경: Resources/UI/growth_tree_bg.png (게임 화면 그림). 노란 원 = 슬롯 확장 노드 위치 (GrowthTree.cs 좌표)', X0, 6.36, 8.1);
    const rx = 9.0, rw = W - X0 - rx;
    card(s, rx, 1.75, rw, 2.35);
    text(s, '노드가 하는 일', rx + 0.22, 1.85, rw - 0.4, 0.32, { size: 13.5, bold: true });
    text(s, bullets(['간단한 스탯: 체력 · 받는 피해 · 평타 · 쿨다운 · 이동 · 대쉬 · 지도', '그 갈래 부위의 슬롯 +1', '액티브 스킬은 넣지 않음', '특정 스킬 · 이동 파츠 조건 노드는 일반 스탯으로 교체']), rx + 0.22, 2.22, rw - 0.4, 1.85, { size: 10.5, ls: 1.25 });
    table(s, [
      ['갈래 → 부위', '포인트'],
      ['코어 → 몸통', '18'], ['오른팔 → 무기', '16'], ['왼팔 → 팔', '15'], ['다리 → 다리', '16'], ['머리 → 머리', '16'], ['합계 36노드', '81'],
    ], rx, 4.3, rw, [rw - 1.1, 1.1], { rowH: 0.3, size: 10, leftFirst: true });
  }

  // 9 공용 노드 표
  {
    const s = slide('07  공용 노드 표', '노드 36개 — 슬롯 확장 10 · 스탯 26', '굵은 금색 = 슬롯 확장 · 주황 이름 = 이번에 바뀐 노드(특정 스킬 · 파츠 조건 → 스탯). id · 위치는 GrowthTree.cs와 같음', 'draft');
    const rows = NODES.slice(1).map((r) => {
      const [br, id, name, eff, cost, pre, chg] = r;
      const slot = chg === '슬롯', changed = chg && !slot;
      const f = slot ? 'FBEFD3' : undefined;
      return [{ text: br.replace(/\(.*\)/, ''), fill: f }, { text: name, bold: slot, color: changed ? C.rust : C.ink, fill: f }, { text: eff, fill: f }, { text: cost, align: 'center', fill: f }, { text: pre, fill: f }];
    });
    const head = ['갈래', '노드', '효과', '비용', '선행'];
    const cw = [0.62, 1.55, 2.38, 0.45, 1.0];
    const tw = cw.reduce((a, b) => a + b, 0);
    table(s, [head, ...rows.slice(0, 18)], X0, 1.72, tw, cw, { rowH: 0.262, size: 8.5, margin: [0.02, 0.06, 0.02, 0.06] });
    table(s, [head, ...rows.slice(18)], X0 + tw + 0.13, 1.72, tw, cw, { rowH: 0.262, size: 8.5, margin: [0.02, 0.06, 0.02, 0.06] });
  }

  // 10 모듈 업그레이드
  {
    const s = darkSlide('08  모듈 업그레이드', '이동 능력은 칸을 쓰지 않는 소프트웨어', '존 관문 파츠와 도약 관절. 얻으면 설치되어 늘 켜진다 — 슬롯과 따로, 모듈 화면에서 관리', 'mix');
    const rows = MODS.slice(1).map((r) => [r[0], r[1], r[2], r[3]]);
    table(s, [['모듈', '하는 일', '얻는 곳', '여는 게이트'], ...rows], X0, 1.85, 7.6, [1.45, 2.55, 2.0, 1.6], { rowH: 0.48, size: 11, headFill: '2E3338' });
    const rx = 8.55, rw = W - X0 - rx;
    const P = [
      ['슬롯 밖 · 늘 켜짐', '칸도, 6개 액티브 제한도 쓰지 않는다. 스토리상 몸에 달린 것으로 친다', C.ok],
      ['설치 연출', '포드에서 진행 막대 + 얼굴 LED 깜빡임 → "설치 완료". 업데이트를 받는 기계 느낌', C.soul],
      ['고정 키', '로프 · 제트 · 캐논처럼 누르는 것은 고정 키. 내오염 코어 · 자석 다리 · 도약 관절은 키 없음', C.gold],
    ];
    P.forEach(([h, b, c], i) => {
      const y = 1.85 + i * 1.38;
      card(s, rx, y, rw, 1.22, DK);
      s.addShape(pres.shapes.RECTANGLE, { x: rx, y, w: 0.1, h: 1.22, fill: { color: c }, line: { color: c } });
      text(s, h, rx + 0.3, y + 0.12, rw - 0.45, 0.36, { size: 14, bold: true, color: c });
      text(s, b, rx + 0.3, y + 0.52, rw - 0.45, 0.66, { size: 10.5, color: 'D7D2CA' });
    });
    captionDk(s, '연구소 열쇠(Z2 · Z6 보스)는 모듈이 아니라 진행 물건. 모듈 단계 업그레이드(로프 사거리 등)는 열린 질문', X0, 5.35, 7.6);
    captionDk(s, '보스 = 모듈 확정 보상, 엘리트 = 자기 몸 부위(슬롯 스킬). Z0 우두머리만 예외로 모듈(도약 관절)', X0, 5.7, 7.6);
  }

  // 11 원혼 파츠
  {
    const s = darkSlide('09  원혼 파츠', '붉은 부품은 칸 밖에, 영원히 붙는다', '기계 보스를 정화하면 남는 붉은 부품. 포드에 제출하거나, 그 부위에 영구 부착한다', 'ok');
    const parts = ['머리', '몸통', '무기', '팔', '다리'];
    const gr = { 몸통: '뒤틀린 에너지 코어 (Z0)' };
    parts.forEach((p, i) => {
      const y = 1.9 + i * 0.85, c = PART[p].c;
      text(s, p, X0, y, 0.85, 0.55, { size: 14, bold: true, color: c, valign: 'middle' });
      for (let k = 0; k < 3; k++) slotBox(s, X0 + 0.9 + k * 1.0, y + 0.05, 0.9, 0.45, null, c, { dark: true, ph: '칸', size: 9 });
      line(s, X0 + 3.95, y + 0.05, X0 + 3.95, y + 0.5, '5A5F66', 1, true);
      grudgeSocket(s, X0 + 4.2, y + 0.06, 0.42, null, { dark: true });
      if (gr[p]) { s.addShape(pres.shapes.HEXAGON, { x: X0 + 4.2, y: y + 0.06, w: 0.48, h: 0.42, fill: { color: C.grudge }, line: { color: 'FF6B5E', width: 1.5 } }); text(s, gr[p] + ' → 코어 과부하', X0 + 4.85, y, 3.0, 0.55, { size: 10.5, bold: true, color: 'E88A80', valign: 'middle' }); }
    });
    text(s, '부위 칸 1 ~ 3                원혼 소켓 (슬롯 밖)', X0 + 0.9, 6.2, 5, 0.3, { size: 10, color: '8E8880' });
    const rx = 8.2, rw = W - X0 - rx;
    const R = [
      ['슬롯을 차지하지 않는다', '장착하면 그 보스 부품이 생긴 부위에 하나 더 붙는다. 빌드 칸은 그대로', C.ok],
      ['영구 · 뗄 수 없음', '한 번 달면 끝. 장착 수가 얼굴 붉어짐과 엔딩 4루트를 정한다(기존 결정)', C.grudge],
      ['코어 과부하는 원혼 스킬', '슬롯 스킬 60개에 들지 않는다. 지금 구현은 액티브 6개 제한 밖, 키 하나 따로', C.gold],
    ];
    R.forEach(([h, b, c], i) => {
      const y = 1.9 + i * 1.42;
      card(s, rx, y, rw, 1.26, DK);
      s.addShape(pres.shapes.RECTANGLE, { x: rx, y, w: 0.1, h: 1.26, fill: { color: c }, line: { color: c } });
      text(s, h, rx + 0.3, y + 0.12, rw - 0.45, 0.36, { size: 14, bold: true, color: 'FFFFFF' });
      text(s, b, rx + 0.3, y + 0.52, rw - 0.45, 0.7, { size: 10.5, color: 'D7D2CA' });
    });
    captionDk(s, '게임 안에서는 "원한 · 원혼"이라고 쓰지 않는다 — "붉게 빛나는 부품"과 부품 이름만', rx, 6.2, rw);
  }

  // 12 카탈로그 개요
  {
    const s = slide('10  스킬 카탈로그', '슬롯 스킬 60 — 부위마다 12', '기계와 진화 유기체에게서 뜯어 온 부품이 테마. 액티브 30 · 패시브 30, 존마다 7 ~ 8개', 'draft');
    const parts = ['무기', '팔', '다리', '몸통', '머리'];
    const kinds = ['엘리트', '기둥', '상인', '기록', '숨은', '희귀'];
    const rows = parts.map((p) => {
      const L = ALL.filter((k) => k.part === p);
      return [{ text: p, color: PART[p].c }, String(L.filter((k) => k.kind.startsWith('A')).length), String(L.filter((k) => k.kind.startsWith('P')).length), ...kinds.map((kd) => String(L.filter((k) => srcKind(k.src) === kd).length)), String(L.length)];
    });
    rows.push([{ text: '합계' }, '30', '30', ...kinds.map((kd) => String(ALL.filter((k) => srcKind(k.src) === kd).length)), '60']);
    table(s, [['부위', '액티브', '패시브', '엘리트', '공명 기둥', '상인', '생존자 기록', '숨은 장소', '희귀 드롭', '합계'], ...rows], X0, 1.8, 8.3, [0.85, 0.75, 0.75, 0.78, 0.92, 0.7, 1.08, 0.92, 0.9, 0.65], { rowH: 0.4, size: 10.5, center: true });
    const rx = 9.2, rw = W - X0 - rx;
    card(s, rx, 1.8, rw, 2.75);
    text(s, '카탈로그 읽는 법', rx + 0.22, 1.9, rw - 0.4, 0.32, { size: 13.5, bold: true });
    text(s, bullets(['A = 액티브(쿨다운) · P = 패시브', 'Lv1 효과 + Lv3 · Lv5 추가 효과', '액티브는 레벨당 피해 +12% · 쿨다운 −8% (지금 코드)', '패시브는 레벨마다 주 수치가 오름(괄호 안 Lv5 값)', '굵은 이름 = 지금 코드에 있는 스킬']), rx + 0.22, 2.27, rw - 0.4, 2.25, { size: 10.5, ls: 1.25 });
    text(s, '지금 스킬은 부위를 유지', X0, 5.0, 8.3, 0.32, { size: 13, bold: true, color: C.rust });
    const keep = [['반격', '팔'], ['흡입', '팔'], ['회오리', '무기'], ['칼날 폭풍', '무기'], ['레이저 파동', '무기'], ['독에 잠식된 코어', '몸통']];
    keep.forEach(([n, p], i) => { const x = X0 + (i % 3) * 2.8, y = 5.4 + Math.floor(i / 3) * 0.46; chip(s, `${n} → ${p}`, x, y, 2.65, 0.36, PART[p].c, 'FFFFFF', 10.5); });
    card(s, rx, 4.75, rw, 1.75, { fill: C.coal, flat: true });
    text(s, '테마', rx + 0.22, 4.85, rw - 0.4, 0.3, { size: 12.5, bold: true, color: C.gold });
    text(s, '엘리트는 자기 몸 부위를 떨어뜨린다(크레인 다리, 탐조등, 포자낭…). 생존자 기록은 사람이 쓰던 실용 장비(그물, 방패, 연막), 상인은 고친 고물', rx + 0.22, 5.18, rw - 0.4, 1.3, { size: 10.5, color: 'E6E1D9' });
    caption(s, '코어 과부하(원혼)와 모듈(도약 관절 등)은 60개에 넣지 않음', X0, 6.4, 8.3);
  }

  // 13 ~ 17 부위별 카탈로그
  const order = [['무기', '오른팔 칼날 — 피해와 범위'], ['팔', '왼팔 도구 — 제어 · 반격 · 설치'], ['다리', '돌진 · 회피 · 착지 (이동 능력은 모듈)'], ['몸통', '가슴 코어 — 생존 · 에너지 · 독 · 불'], ['머리', '얼굴 화면 · 센서 — 감지 · 표식 · 약점']];
  order.forEach(([p, sub], pi) => {
    const L = ALL.filter((k) => k.part === p);
    const na = L.filter((k) => k.kind.startsWith('A')).length;
    const s = slide(`${11 + pi}  스킬 카탈로그 · ${p}`, `${p} (${PART[p].en}) — 액티브 ${na} · 패시브 ${12 - na}`, sub, 'draft');
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: 0.14, h: H, fill: { color: PART[p].c }, line: { color: PART[p].c } });
    const rows = L.map((k) => {
      const kd = srcKind(k.src), isA = k.kind.startsWith('A');
      const bold = /^(회오리|칼날 폭풍|레이저 파동|반격|흡입|독에 잠식된 코어)$/.test(k.name);
      return [k.id, { text: k.name, bold: true, color: bold ? PART[p].c : C.ink }, { text: k.kind, color: isA ? 'FFFFFF' : PART[p].c, fill: isA ? PART[p].c : undefined, bold: true, align: 'center' }, k.eff, k.lv3, k.lv5, { text: k.src, color: SRC[kd] || C.ink, bold: true }, { text: k.zone, align: 'center', bold: true }];
    });
    table(s, [['#', '이름', '종류', '효과 (Lv1)', 'Lv3', 'Lv5', '출처', '존'], ...rows], X0, 1.7, CW, [0.5, 1.45, 0.62, 4.13, 1.78, 1.78, 1.37, 0.5], { rowH: 0.385, size: 8.8, margin: [0.02, 0.06, 0.02, 0.06], headFill: C.coal });
  });

  // 18 획득처 규칙
  {
    const s = slide('16  획득처', '여섯 곳에서, 8개 존에 고르게', '엘리트 15 · 공명 기둥 7 · 상인 19 · 생존자 기록 8 · 숨은 장소 8 · 희귀 드롭 3', 'mix');
    const rows = SRCS.slice(1).map((r) => { const kd = srcKind(r[0]) || (r[0].startsWith('일반') ? '희귀' : r[0].startsWith('공명') ? '기둥' : ''); return [{ text: r[0], color: SRC[kd] || C.ink }, { text: r[1], align: 'center' }, r[2]]; });
    table(s, [['획득처', '수', '규칙'], ...rows], X0, 1.75, 7.0, [1.55, 0.5, 4.95], { rowH: 0.5, size: 10, leftFirst: true });
    const rx = 7.85, rw = W - X0 - rx;
    const zr = ZSRC.slice(1).map((r) => [{ text: r[0].split(' ')[0] }, ...r.slice(1)]);
    table(s, [['존', '엘리트', '기둥', '상인', '기록', '숨은', '희귀', '합'], ...zr], rx, 1.75, rw, [0.55, 0.7, 0.6, 0.6, 0.6, 0.6, 0.6, rw - 4.25], { rowH: 0.4, size: 10, center: true });
    OK(s, rx, 5.95); text(s, '획득처 종류 · 상인 2 ~ 3개 · Z0 엘리트 보상', rx + 0.7, 5.95, rw - 0.7, 0.28, { size: 10, valign: 'middle' });
    DR(s, rx, 6.32); text(s, '개수 배분 · 희귀 드롭 확률(약 0.5%)', rx + 0.7, 6.32, rw - 0.7, 0.28, { size: 10, valign: 'middle' });
  }

  // 19 존 배치 지도 (보드)
  {
    const s = darkSlide('17  존 배치 지도', '어느 존에서 무엇을 얻나', '존 연결도와 같은 배치(ZoneGraph.cs). 점 색 = 획득처', 'draft');
    const gx = X0, gy = 1.75, gw = CW, gh = 4.75, cols = 4, rowsN = 3, cg = 0.12;
    const cw = (gw - cg * 3) / cols, ch = (gh - cg * 2) / rowsN;
    const Z = { 1: [0, 1, 0, 0], 3: [2, 2, 0, 0], 4: [3, 3, 0, 0], 2: [0, 0, 1, 2], 0: [1, 2, 1, 1], 5: [3, 3, 1, 1], 7: [1, 2, 2, 2], 6: [3, 3, 2, 2] };
    const zname = { 0: '잿빛 폐허', 1: '무너진 도심', 2: '마천루 묘지', 3: '고철 협곡', 4: '전쟁 기계 공장', 5: '죽은 숲', 6: '독성 늪', 7: '연구소' };
    const tier = { 0: '바깥 · 허브', 1: '바깥', 3: '바깥', 5: '바깥', 2: '깊은 곳', 4: '깊은 곳', 6: '깊은 곳', 7: '최심부' };
    for (const [z, [c0, c1, r0, r1]] of Object.entries(Z)) {
      const x = gx + c0 * (cw + cg), y = gy + r0 * (ch + cg), w = (c1 - c0 + 1) * cw + (c1 - c0) * cg, h = (r1 - r0 + 1) * ch + (r1 - r0) * cg;
      const deep = tier[z] !== '바깥' && tier[z] !== '바깥 · 허브';
      card(s, x, y, w, h, { fill: deep ? '2A2629' : '24282D', line: deep ? '4A3A3A' : '3A3F45', flat: true });
      text(s, `Z${z}  ${zname[z]}`, x + 0.14, y + 0.08, w - 1.2, 0.3, { size: 11.5, bold: true, color: 'FFFFFF' });
      text(s, tier[z], x + w - 1.1, y + 0.1, 0.98, 0.26, { size: 8.5, color: '8E8880', align: 'right' });
      const L = ALL.filter((k) => k.zone === 'Z' + z);
      const ncol = w > cw * 1.5 ? 4 : 2, per = Math.ceil(L.length / ncol), iw = (w - 0.2) / ncol;
      L.forEach((k, i) => {
        const cc = Math.floor(i / per), rr = i % per;
        const xx = x + 0.14 + cc * iw, yy = y + 0.4 + rr * 0.25;
        const col = SRC[srcKind(k.src)];
        s.addShape(pres.shapes.OVAL, { x: xx, y: yy + 0.07, w: 0.12, h: 0.12, fill: { color: col }, line: { color: col } });
        text(s, k.name, xx + 0.17, yy, iw - 0.2, 0.26, { size: 8.5, color: 'E6E1D9', valign: 'middle' });
      });
      if (z === '0') text(s, '+ 모듈 도약 관절 (우두머리)', x + 0.14, y + h - 0.3, w - 0.3, 0.24, { size: 8.5, color: '8E8880' });
    }
    let lx = X0;
    for (const [k, c] of Object.entries(SRC)) {
      s.addShape(pres.shapes.OVAL, { x: lx, y: 6.66, w: 0.14, h: 0.14, fill: { color: c }, line: { color: c } });
      const lbl = { 엘리트: '엘리트', 기둥: '공명 기둥', 상인: '상인', 기록: '생존자 기록', 숨은: '숨은 장소', 희귀: '희귀 드롭' }[k];
      text(s, lbl, lx + 0.2, 6.58, 1.3, 0.3, { size: 9.5, color: 'D7D2CA', valign: 'middle' });
      lx += 1.5;
    }
  }

  // 20 존별 표
  {
    const s = slide('18  존별 배치 표', '존마다 7 ~ 8개, 숨은 장소는 다른 존의 모듈로', '숨은 장소 8곳 중 7곳이 다른 존 보스의 모듈로 열린다 — 바깥 존을 다시 찾아갈 이유', 'draft');
    const rows = ZONES.slice(1).map((r) => [r[0].replace(' ', '\n'), r[1], r[2], r[3], r[4], r[5], r[6]]);
    table(s, [['존', '엘리트', '공명 기둥', '상인 (가격)', '생존자 기록', '숨은 장소 (필요 모듈)', '희귀 드롭'], ...rows], X0, 1.72, CW, [1.35, 2.05, 1.0, 2.95, 1.05, 2.48, 1.25], { rowH: 0.56, size: 9.3, margin: [0.03, 0.07, 0.03, 0.07] });
  }

  // 21 엘리트 목록
  {
    const s = slide('19  엘리트', '엘리트는 자기 몸 부위를 떨어뜨린다', 'Z0 · Z6은 결정. Z1 ~ Z5 · Z7 엘리트 이름은 존 기획 때 다시 정할 자리표시', 'mix');
    const rows = ELITES.slice(1).map((r) => {
      const fixed = r[0] === 'Z0' || r[0] === 'Z6';
      const org = r[2].startsWith('유기체');
      return [r[0], { text: r[1], bold: true }, { text: r[2], color: org ? C.ok : C.ink }, r[3], { text: fixed ? '결정' : '자리표시', color: fixed ? C.ok : C.draft, bold: true, align: 'center' }];
    });
    const half = 8;
    const cw = [0.45, 1.45, 1.28, 2.05, 0.82];
    table(s, [['존', '엘리트', '종류', '부위 → 스킬', '상태'], ...rows.slice(0, half)], X0, 1.75, 6.1, cw, { rowH: 0.5, size: 9.5 });
    table(s, [['존', '엘리트', '종류', '부위 → 스킬', '상태'], ...rows.slice(half)], X0 + 6.03, 1.75, 6.1, cw, { rowH: 0.5, size: 9.5 });
    caption(s, '초록 = 진화 유기체 (유기체 보스는 원혼 파츠가 없지만, 유기체 엘리트는 슬롯 스킬을 준다). Z0 우두머리만 모듈(도약 관절)', X0, 6.42, CW);
  }

  // 22 상인
  {
    const s = slide('20  상인', '숨어 사는 온화한 기계 상인', '존마다 고정 상품 2 ~ 3개, 한 번 사면 끝. 바깥 존 약 80 · 깊은 존 약 200 · 연구소 약 350', 'mix');
    img(s, 'merchant.png', X0, 1.75, 4.6, 2.6, { shadow: true });
    caption(s, 'ChatGPT 컨셉 — Z0 고물 수집가와 상품 3개', X0, 4.38, 4.6);
    card(s, X0, 4.75, 4.6, 1.85, { fill: C.coal, flat: true });
    text(s, '규칙', X0 + 0.2, 4.85, 2, 0.3, { size: 12.5, bold: true, color: C.gold });
    text(s, bullets(['찾으면 지도에 표시 (숨은 상인)', '같은 상품은 다른 존에 없음', '전 상품 합계 3,040 부품 — 리스폰 파밍으로 채우는 양', '스킬 60 전부 Lv5 = 20,400 부품']), X0 + 0.2, 5.18, 4.25, 1.4, { size: 10.5, color: 'E6E1D9', ls: 1.25 });
    const rx = X0 + 4.85, rw = W - X0 - rx;
    const rows = MERCH.slice(1).map((r) => [r[0], { text: r[1], bold: true }, r[2], r[3]]);
    table(s, [['존', '상인 (자리표시)', '위치 (초안)', '상품 (가격)'], ...rows], rx, 1.75, rw, [0.5, 1.55, 1.75, rw - 3.8], { rowH: 0.54, size: 9.5 });
  }

  // 23 Z0 첫 시간
  {
    const s = slide('21  Z0 첫 시간', 'Z0을 떠날 때 액티브 2 ~ 3, 패시브 1 ~ 2', '빈 슬롯으로 시작해 기둥 → 상인 → 엘리트 → 보스 순으로 몸이 찬다 (순서는 자유)', 'draft');
    const S = [
      ['시작', '평타 · 대쉬 · 점프. 슬롯 5칸 비어 있음', C.ash, null],
      ['공명 기둥', '반격 — 팔 칸 첫 액티브. 뜯기 기믹의 열쇠', SRC.기둥, ['팔', '반격']],
      ['처치 · 상인', '경험치로 첫 노드들, 부품 80으로 상인 1개 (되받기 장갑 등)', SRC.상인, ['팔', '되받기 장갑']],
      ['엘리트 2체', '집행자 → 레이저 파동(무기) · 우두머리 → 도약 관절(모듈)', SRC.엘리트, ['무기', '레이저 파동']],
      ['보스', '로프 팔(모듈) + 뒤틀린 에너지 코어(원혼 · 몸통 소켓)', C.grudge, null],
      ['로프 턱', '숨은 장소 → 부품 자석 (팔 패시브)', SRC.숨은, ['팔', '부품 자석']],
    ];
    const bw = (CW - 5 * 0.18) / 6;
    S.forEach(([h, b, c, sl], i) => {
      const x = X0 + i * (bw + 0.18), y = 1.85;
      card(s, x, y, bw, 3.0);
      s.addShape(pres.shapes.RECTANGLE, { x, y, w: bw, h: 0.1, fill: { color: c }, line: { color: c } });
      badge(s, i + 1, x + 0.15, y + 0.25, 0.4, c);
      text(s, h, x + 0.65, y + 0.25, bw - 0.75, 0.4, { size: 13.5, bold: true, valign: 'middle' });
      text(s, b, x + 0.15, y + 0.8, bw - 0.3, 1.4, { size: 10.5, color: C.muted });
      if (sl) slotBox(s, x + 0.15, y + 2.3, bw - 0.3, 0.48, sl[1], PART[sl[0]].c, { passive: /장갑|자석/.test(sl[1]), size: 10 });
      if (i < 5) arrow(s, x + bw + 0.01, y + 1.5, x + bw + 0.17, y + 1.5, C.ash, 1.5);
    });
    card(s, X0, 5.1, CW, 1.45, { fill: C.coal, flat: true });
    text(s, 'Z0 안에서 더 얻을 수 있는 것', X0 + 0.25, 5.2, 5, 0.3, { size: 12.5, bold: true, color: C.gold });
    text(s, bullets(['상인 나머지 2개 (위험 감지 · 두꺼운 장갑판)', '생존자 기록을 다 모으면 수리 집게 (팔 패시브)', '스크랩 하운드 희귀 드롭 가벼운 프레임 (다리 패시브) — 파밍']), X0 + 0.25, 5.55, 6.5, 0.95, { size: 10.5, color: 'E6E1D9', ls: 1.25 });
    text(s, '슬롯은 노드로 5 → 6 ~ 7칸. 팔에 몰리므로 "팔 슬롯 확장 I"이 첫 목표가 되기 쉽다', X0 + 7.0, 5.55, CW - 7.25, 0.9, { size: 10.5, color: 'E6E1D9' });
  }

  // 24 UI — 부위 장착 화면
  {
    const s = darkSlide('22  UI 화면 안', '부위 장착 화면 (포드)', '가운데 몸, 부위마다 칸. 칸을 고르면 그 부위 스킬만 보인다. 위쪽은 액티브 6칸 키 바', 'draft');
    img(s, 'ui_slots.png', X0, 1.75, 7.9, 4.45, { shadow: true, dark: true });
    captionDk(s, 'ChatGPT 목업 (분위기 참고용). 그림의 칸 4개 줄은 기획상 최대 3칸, 글자는 실제 화면에서 넣음', X0, 6.25, 7.9);
    const rx = 8.8, rw = W - X0 - rx;
    const N = [
      ['몸 그림 + 5부위', '부위마다 칸 1 ~ 3. 잠긴 칸은 자물쇠 + "노드 확장"'],
      ['부위 스킬 목록', '얻은 것만, 레벨 · A/P · 쿨다운. 끼우기 / 빼기'],
      ['액티브 키 바', 'Q W E R + 2. 끌어다 놓아 키 지정. 6개가 차면 액티브는 못 끼움'],
      ['원혼 소켓', '부위 옆 붉은 육각. 장착된 것만 보이고 뺄 수 없음'],
      ['강화', '스킬을 고르면 다음 레벨 비용 · Lv3 · Lv5 효과 미리보기'],
    ];
    N.forEach(([h, b], i) => {
      const y = 1.75 + i * 0.9;
      badge(s, i + 1, rx, y + 0.05, 0.38, C.rust);
      text(s, h, rx + 0.5, y, rw - 0.5, 0.32, { size: 12.5, bold: true, color: 'FFFFFF' });
      text(s, b, rx + 0.5, y + 0.33, rw - 0.5, 0.55, { size: 10, color: 'B8B2AA' });
    });
  }

  // 25 UI — 모듈 · 상인 · 공용 노드
  {
    const s = darkSlide('23  UI 화면 안', '모듈 화면 · 상인 화면 · 공용 노드 화면', '모듈은 설치 목록(켜고 끄기 없음), 상인은 2 ~ 3칸, 공용 노드는 지금 성장 트리 화면 그대로', 'draft');
    img(s, 'ui_modules.png', X0, 1.75, 6.0, 3.38, { shadow: true, dark: true });
    captionDk(s, '모듈 화면 — ChatGPT 목업', X0, 5.15, 6.0);
    const bx = X0 + 6.25, bw = W - X0 - bx;
    // 상인 화면 목업 (도형)
    card(s, bx, 1.75, bw, 2.3, { fill: C.deep, line: '3A3F45', flat: true });
    text(s, '상인 — 고물 수집가 (Z0)', bx + 0.2, 1.85, bw - 0.4, 0.3, { size: 11.5, bold: true, color: C.gold });
    const goods = [['되받기 장갑', '팔 · P', '80'], ['위험 감지', '머리 · P', '80'], ['두꺼운 장갑판', '몸통 · P', '팔림']];
    goods.forEach(([n, p, pr], i) => {
      const x = bx + 0.2 + i * ((bw - 0.4) / 3), w = (bw - 0.4) / 3 - 0.12, sold = pr === '팔림';
      const c = PART[p.split(' ')[0]].c;
      s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 2.25, w, h: 1.6, rectRadius: 0.06, fill: { color: sold ? '1A1C1F' : '24282D' }, line: { color: sold ? '3A3F45' : c, width: 1 } });
      text(s, n, x + 0.08, 2.35, w - 0.16, 0.5, { size: 10.5, bold: true, color: sold ? '5A5F66' : 'FFFFFF', align: 'center', valign: 'middle' });
      text(s, p, x + 0.08, 2.85, w - 0.16, 0.3, { size: 9, color: sold ? '5A5F66' : c, align: 'center' });
      chip(s, sold ? '팔림' : `부품 ${pr}`, x + 0.15, 3.35, w - 0.3, 0.32, sold ? '3A3F45' : C.gold, sold ? '8E8880' : C.coal, 9.5);
    });
    // 공용 노드
    const r = img(s, path.join(UI, 'growth_tree_bg.png'), bx, 4.25, bw, 1.85, { dark: true });
    captionDk(s, '공용 노드 — 지금 화면 그대로 (슬롯 노드만 모양 구분)', bx, 6.12, bw);
    card(s, X0, 5.5, 6.0, 1.05, DK);
    text(s, '모듈 화면: 아이콘 · 이름 · 버전 · 설명 · 여는 게이트. 새 모듈은 포드에서 설치 진행 막대 뒤 목록에 들어온다', X0 + 0.2, 5.55, 5.6, 0.95, { size: 10.5, color: 'D7D2CA', valign: 'middle' });
  }

  // 26 열린 질문
  {
    const s = slide('24  열린 질문', '열린 질문', '사용자 결정이 필요한 항목. 위쪽일수록 구현을 막는다', 'todo');
    const Q = [
      ['원혼 액티브와 6개 제한', '지금 구현은 제한 밖(키 따로). 원혼 파츠를 많이 달면 액티브가 7 ~ 9개가 된다. 유지할지', '구현 막음'],
      ['슬롯 확장 II에 모듈 조건', '예: 몸통 3칸 = 내오염 코어. 진행 속도를 맞출지, 포인트만으로 둘지(초안)', ''],
      ['희귀 드롭 · 경험치 · 키트 수치', '희귀 약 0.5%, 일반 2 ~ 6 · 엘리트 60 · 보스 200, 수리 키트 25', ''],
      ['생존자 기록 보상 기준', '존 기록 전부를 모았을 때(초안) / 절반일 때', ''],
      ['모듈 단계 업그레이드', '로프 사거리 같은 모듈 강화를 둘지 (초안: 없음, 로프 와인더 노드 삭제)', ''],
      ['자리표시 확정 · 겹침 상한', 'Z1 ~ Z5 · Z7 엘리트 · 상인 이름, 받는 피해 감소 같은 패시브 합산 상한', ''],
    ];
    Q.forEach(([h, b, tag], i) => {
      const y = 1.75 + i * 0.84;
      card(s, X0, y, CW, 0.72);
      badge(s, i + 1, X0 + 0.22, y + 0.15, 0.42, C.grudge);
      text(s, h, X0 + 0.85, y, 3.6, 0.72, { size: 13, bold: true, valign: 'middle' });
      text(s, b, X0 + 4.5, y, CW - 6.0, 0.72, { size: 10.5, color: C.muted, valign: 'middle' });
      if (tag) chip(s, tag, X0 + CW - 1.35, y + 0.2, 1.15, 0.32, C.rust, 'FFFFFF', 10);
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

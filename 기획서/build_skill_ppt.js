// ALONE(가제) 스킬 시스템 기획서 PPT 생성 → 기획서/20261108_ALONE_스킬시스템기획서_v0.1_정승운.pptx
// 사용: NODE_PATH=<pptxgenjs · sharp · jszip 설치 폴더>/node_modules node 기획서/build_skill_ppt.js
// 내용 원본: 기획_결정사항.md('스킬 = 파츠' · '원혼 파츠' · '이동 파츠' · '파츠 장착 / 성장' · '점점 늘어나는 표정'),
//           기획서/Z0_보스_재의감시자.md, ALONE(가제) 컨셉 기획.md(성장). 스타일 · 도구는 build_boss_ppt.js 와 같음(복사)
// 그림: 기획서/art → 없으면 unity/Captures. 얼굴 단계 그림은 art/face_*.png 를 붉게 물들여 art/_crop 에 만든다
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const sharp = require('sharp');

const ROOT = __dirname;
const ART = path.join(ROOT, 'art');
const CAP = path.join(ROOT, '..', 'unity', 'Captures');
const VER = 'v0.1';
const DATE = '2026-11-08';
const OUT = path.join(ROOT, `20261108_ALONE_스킬시스템기획서_${VER}_정승운.pptx`);
const DOC = `ALONE(가제) · 스킬 시스템 기획서 ${VER}`;

const FONT = 'Noto Sans KR';
const C = {
  coal: '16181B', deep: '0E1012', ink: '23272C', muted: '646B73', bg: 'EEEBE6', card: 'FFFFFF', line: 'D9D4CC', tint: 'E4DED5',
  rust: 'C2562B', ash: '8A8F96', soul: '33B5C8', grudge: 'B8322A', gold: 'E0A030',
  blue: '3F8FD8', red: 'D9502F', rope: 'E0A030', ok: '2E8B6E', draft: 'D99A1E',
};
const W = 13.333, H = 7.5, X0 = 0.6, CW = W - 2 * X0;

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = '정승운';
pres.title = 'ALONE(가제) 스킬 시스템 기획서 — 스킬 = 파츠';
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
// 어두운 판: 그림이 주인공인 장(같은 팔레트 · 폰트, 바탕만 반전)
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

// ---------- 도구 (build_boss_ppt.js 와 같음) ----------
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
    s.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: 'CFC9C0' }, line: { color: 'B9B2A8', dashType: 'dash' } });
    text(s, `이미지 자리: ${path.basename(f)}`, x, y, w, h, { size: 11, color: C.muted, align: 'center', valign: 'middle' });
    return;
  }
  const [iw, ih] = dims[path.basename(f)];
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
// status: 'ok' = 결정, 'draft' = 초안 · 검토 필요, 'mix' = 결정 + 초안 섞임. 제목 줄 오른쪽 위에 붙는다
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
const DK = { fill: '24282D', line: '363B42', flat: true }; // 어두운 판 위 카드
function statusTag(s, st, right, y) {
  const T = { ok: [['결정', C.ok]], draft: [['초안 · 검토 필요', C.draft]], mix: [['결정', C.ok], ['초안 섞임', C.draft]], todo: [['정할 것', C.grudge]], impl: [['구현 현황', C.coal]] }[st];
  let x = right;
  for (let i = T.length - 1; i >= 0; i--) { const [t, c] = T[i]; const w = 0.32 + t.length * 0.14; x -= w; chip(s, t, x, y, w, 0.32, c, 'FFFFFF', 10); x -= 0.1; }
}
// 작은 상태 칩 (본문 안)
const OK = (s, x, y, w = 0.62) => chip(s, '결정', x, y, w, 0.28, C.ok, 'FFFFFF', 9.5);
const DR = (s, x, y, w = 0.62) => chip(s, '초안', x, y, w, 0.28, C.draft, 'FFFFFF', 9.5);
const TODO = (s, x, y, w = 0.85) => chip(s, '정할 것', x, y, w, 0.28, C.grudge, 'FFFFFF', 9.5);
function table(s, rows, x, y, w, colW, o = {}) {
  const head = rows[0].map((t) => ({ text: t, options: { bold: true, color: 'FFFFFF', fill: { color: C.coal }, align: 'center' } }));
  const body = rows.slice(1).map((r, i) => r.map((t, j) => {
    const cell = typeof t === 'object' ? t : { text: t };
    return { text: cell.text, options: { bold: j === 0 || cell.bold, color: cell.color || C.ink, fill: { color: cell.fill || (i % 2 ? 'F7F5F2' : C.card) }, align: (j === 0 && !o.leftFirst) || o.center ? 'center' : 'left' } };
  }));
  s.addTable([head, ...body], { x, y, w, colW, fontFace: FONT, fontSize: o.size || 12, valign: 'middle', border: { type: 'solid', color: C.line, pt: 0.75 }, rowH: o.rowH || 0.42, margin: [0.04, 0.1, 0.04, 0.1] });
}
const bullets = (a) => a.map((t) => '•  ' + t).join('\n');
// 키캡
function key(s, t, x, y, w = 0.5, h = 0.5, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.06, fill: { color: o.fill || '2A2E33' }, line: { color: o.line || '444A51', width: o.lw || 0.75, dashType: o.dash ? 'dash' : 'solid' } });
  text(s, t, x, y, w, h, { size: o.size || 11, bold: true, color: o.color || 'D7D2CA', align: 'center', valign: 'middle' });
}
// 붉게 물든 얼굴 (t = 0 원래 색 ~ 1 빨강, dim = 밝기)
async function redFace(f, t, dim = 1) {
  const out = path.join(ART, '_crop', `${path.parse(f).name}_red${Math.round(t * 100)}_${Math.round(dim * 100)}.png`);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  const R = [[0, 0.4 * dim, 0.6 * dim], [0.1, 0.12, 0], [0.1, 0, 0.1]];
  const I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]];
  const M = I.map((row, i) => row.map((v, j) => v * (1 - t) + R[i][j] * t));
  await sharp(path.join(ART, f)).removeAlpha().recomb(M).png().toFile(out);
  const m = await sharp(out).metadata(); dims[path.basename(out)] = [m.width, m.height];
  return out;
}
async function hudCrop() {
  const src = path.join(CAP, 'hud_game.png');
  if (!fs.existsSync(src)) return null;
  const out = path.join(ART, '_crop', 'hud_skillbar.png');
  fs.mkdirSync(path.dirname(out), { recursive: true });
  await sharp(src).extract({ left: 700, top: 995, width: 520, height: 83 }).png().toFile(out);
  dims['hud_skillbar.png'] = [520, 83];
  return out;
}

// ---------- 슬라이드 ----------
async function build() {
  await loadDims();

  // 1 표지
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    await cimg(s, 'key_art.png', 0, 0, W, H, { focusY: 0.5 });
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: 3.3, fill: { color: C.deep, transparency: 40 }, line: { color: C.deep, transparency: 100 } });
    text(s, 'SYSTEM DESIGN DOCUMENT  ·  SKILL', 0, 0.45, W, 0.35, { size: 13, bold: true, color: C.soul, align: 'center' });
    text(s, '스킬 = 파츠', 0, 0.8, W, 1.1, { size: 60, bold: true, color: 'FFFFFF', align: 'center' });
    text(s, '스킬 시스템 기획서', 0, 1.9, W, 0.35, { size: 15, color: 'D7D2CA', align: 'center' });
    text(s, '칼 하나로 깨어난 방랑자 — 부품을 달 때마다 몸이 자라고, 할 수 있는 일이 늘어난다', 0, 2.3, W, 0.4, { size: 15, color: 'F0ECE6', align: 'center' });
    text(s, `스킬 시스템 기획서  ·  작성자 정승운  ·  작성일 ${DATE}  ·  버전 ${VER}  ·  ALONE(가제) 컨셉 기획서 v0.3 부속`, 0, 2.78, W, 0.3, { size: 11, color: 'D7D2CA', align: 'center' });
  }

  // 2 설계 목표 (어두운 판 · 큰 캐릭터 그림)
  {
    const s = darkSlide('01  설계 목표', '처음엔 칼 하나, 파츠로 몸이 자란다', '스킬은 레벨로 해금되지 않는다 — 세계에서 부품을 찾아 몸에 달 때 생긴다', 'ok');
    await cimg(s, 'character.png', X0, 1.8, 7.4, 4.16, { shadow: true });
    captionDk(s, '컨셉 3면도 + 이동 파츠 5종 (ChatGPT 컨셉, 분위기 참고용). 처음엔 칼날 팔뿐, 오른쪽 파츠가 하나씩 몸에 붙는다', X0, 6.02, 7.4);
    const G = [
      ['칼 하나로 시작', '평타 · 대쉬 · 점프만 있다. 첫 존은 칼과 위치 선정만으로 버틴다', C.ash],
      ['파츠 = 몸 = 스킬', '로켓 팔을 달면 "로켓 팔" 스킬이 생긴다. 몸의 변화가 곧 성장이다', C.rust],
      ['길이 열린다', '이동 파츠의 스킬이 막힌 길을 연다. 메트로배니아 진행과 맞물린다', C.rope],
    ];
    const gx = 8.35, gw = W - X0 - gx;
    G.forEach(([h, b, c], i) => {
      const y = 1.8 + i * 1.42;
      card(s, gx, y, gw, 1.26, DK);
      s.addShape(pres.shapes.RECTANGLE, { x: gx, y, w: 0.1, h: 1.26, fill: { color: c }, line: { color: c } });
      badge(s, i + 1, gx + 0.3, y + 0.17, 0.4, c);
      text(s, h, gx + 0.85, y + 0.15, gw - 1.0, 0.42, { size: 16, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, gx + 0.3, y + 0.66, gw - 0.5, 0.56, { size: 11, color: 'B8B2AA' });
    });
    text(s, '이 문서의 표시', X0, 6.5, 1.5, 0.28, { size: 10.5, bold: true, color: 'B8B2AA', valign: 'middle' });
    OK(s, X0 + 1.45, 6.5); text(s, '사용자 결정 (기획_결정사항.md)', X0 + 2.15, 6.5, 2.8, 0.28, { size: 10.5, color: 'E6E1D9', valign: 'middle' });
    DR(s, X0 + 4.85, 6.5); text(s, '작업 에이전트 초안 · 사용자 검토 필요', X0 + 5.55, 6.5, 3.2, 0.28, { size: 10.5, color: 'E6E1D9', valign: 'middle' });
    TODO(s, X0 + 8.75, 6.5); text(s, '아직 안 정함', X0 + 9.7, 6.5, 2, 0.28, { size: 10.5, color: 'E6E1D9', valign: 'middle' });
  }

  // 3 시작 상태
  {
    const s = slide('02  시작 상태', '시작할 때는 평타 · 대쉬 · 점프뿐', '지금 프로토의 반격(E) · 회오리 · 레이저 파동 · 칼날 폭풍은 시작 기술에서 빠지고 파츠로 옮긴다', 'ok');
    const K = [['A', '평타', '칼날 3연타. 오른팔 칼날은 기본 제공 무기', 1.0], ['SPACE', '대쉬', '짧은 무적 회피. 쿨다운 1.1초 (지금 값)', 1.9], ['F', '점프', '1.1m 턱을 오른다. 쿨다운 0.8초 (지금 값)', 1.0]];
    K.forEach(([k, h, b, kw], i) => {
      const y = 1.85 + i * 1.18;
      card(s, X0, y, 5.6, 1.0);
      key(s, k, X0 + 0.25, y + 0.2, kw, 0.6, { fill: C.coal, color: 'FFFFFF', size: 13 });
      text(s, h, X0 + 2.35, y + 0.1, 3.1, 0.4, { size: 16, bold: true });
      text(s, b, X0 + 2.35, y + 0.5, 3.15, 0.45, { size: 11, color: C.muted });
    });
    text(s, '이동은 지금처럼 탑뷰 + AOS식(마우스)으로 유지', X0, 5.48, 5.6, 0.3, { size: 11, color: C.muted });
    // 빈 스킬 칸
    const bx = 6.75, bw = W - X0 - bx;
    card(s, bx, 1.85, bw, 4.4, { fill: C.coal, flat: true });
    text(s, '스킬 — 처음엔 비어 있음', bx + 0.3, 2.0, bw - 0.6, 0.4, { size: 15, bold: true, color: 'FFFFFF' });
    text(s, '칸 수가 정해져 있지 않다. 파츠를 얻을 때마다 하나씩 생기고, 플레이어가 키를 붙인다', bx + 0.3, 2.42, bw - 0.6, 0.5, { size: 11, color: 'B8B2AA' });
    for (let r = 0; r < 2; r++) for (let c = 0; c < 5; c++) {
      const x = bx + 0.4 + c * 1.12, y = 3.15 + r * 1.2;
      s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w: 0.9, h: 0.9, rectRadius: 0.08, fill: { color: '22262B' }, line: { color: '5A5F66', width: 1, dashType: 'dash' } });
      text(s, r === 1 && c === 4 ? '…' : '+', x, y, 0.9, 0.9, { size: 22, color: '5A5F66', align: 'center', valign: 'middle' });
    }
    text(s, '정해진 QWER 네 칸에 끼우는 방식이 아님', bx + 0.3, 5.6, bw - 0.6, 0.35, { size: 12, bold: true, color: C.gold });
    text(s, '빈 칸 그림은 설명용. 실제 HUD 모양은 정할 것', bx, 6.35, bw, 0.3, { size: 10, color: C.muted, align: 'right' });
    card(s, X0, 5.85, 5.6, 0.8, { fill: 'F7F5F2', flat: true });
    text(s, '반격(E)도 시작 기술이 아니다 → 초안: 왼팔 방어 장갑 파츠로 얻음 (06 참고)', X0 + 0.2, 5.85, 5.2, 0.8, { size: 11, color: C.ink, valign: 'middle' });
  }

  // 4 스킬 = 파츠 흐름
  {
    const s = slide('03  스킬 = 파츠', '파츠를 얻으면 스킬이 생긴다', '획득 → 키 지정 → 사용 → 레벨업. 슬롯 제한 없음', 'ok');
    const F = [['파츠 획득', '보스 · 공명 기둥 · 엘리트 · 상인', C.ash], ['스킬 생김', '파츠마다 스킬 1개\n이동 파츠도 스킬을 줌', C.rust], ['키 지정', '빈 키를 플레이어가 직접 누름\n(키 설정 화면)', C.coal], ['사용', '쿨다운 기반. 스태미나 없음', C.blue], ['레벨업', '기본 부품 → 포인트로\n스킬 레벨을 올림', C.ok]];
    const bw = 2.15, gx = (CW - 5 * bw) / 4;
    F.forEach(([h, b, c], i) => {
      const x = X0 + i * (bw + gx), y = 1.85;
      card(s, x, y, bw, 1.6, { fill: c, flat: true });
      s.addShape(pres.shapes.OVAL, { x: x + 0.15, y: y + 0.15, w: 0.38, h: 0.38, fill: { color: 'FFFFFF' }, line: { color: 'FFFFFF' } });
      text(s, String(i + 1), x + 0.15, y + 0.15, 0.38, 0.38, { size: 12, bold: true, color: c, align: 'center', valign: 'middle' });
      text(s, h, x + 0.62, y + 0.12, bw - 0.7, 0.45, { size: 16, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, x + 0.18, y + 0.7, bw - 0.3, 0.85, { size: 11, color: 'FFFFFF' });
      if (i < 4) arrow(s, x + bw + 0.03, y + 0.8, x + bw + gx - 0.03, y + 0.8, C.ash, 2);
    });
    // 레벨업 → 사용 되돌이
    line(s, X0 + 4 * (bw + gx) + bw / 2, 3.45, X0 + 4 * (bw + gx) + bw / 2, 3.7, C.ok, 1.5);
    line(s, X0 + 3 * (bw + gx) + bw / 2, 3.7, X0 + 4 * (bw + gx) + bw / 2, 3.7, C.ok, 1.5);
    arrow(s, X0 + 3 * (bw + gx) + bw / 2, 3.7, X0 + 3 * (bw + gx) + bw / 2, 3.47, C.ok, 1.5);
    // 예시
    card(s, X0, 4.0, CW, 1.05);
    text(s, '예시', X0 + 0.25, 4.0, 0.8, 1.05, { size: 13, bold: true, color: C.rust, valign: 'middle' });
    const ex = [['로켓 팔 획득', 1.7], ['"로켓 팔" 스킬', 1.7], ['Q 키를 누름', 1.6], ['Q로 발사', 1.4]];
    let ex0 = X0 + 1.1;
    ex.forEach(([t, w], i) => {
      chip(s, t, ex0, 4.33, w, 0.4, i === 2 ? C.coal : C.tint, i === 2 ? 'FFFFFF' : C.ink, 11.5);
      ex0 += w;
      if (i < ex.length - 1) { arrow(s, ex0 + 0.08, 4.53, ex0 + 0.42, 4.53, C.ash, 1.5); ex0 += 0.5; }
    });
    text(s, '로켓 팔은 사용자가 든 예시 이름', ex0 + 0.3, 4.0, X0 + CW - ex0 - 0.45, 1.05, { size: 10, color: C.muted, valign: 'middle' });
    // 규칙
    const R = [['슬롯 제한 없음', '얻은 스킬은 전부 쓸 수 있다. 무엇을 뺄지 고르는 고민 대신, 어떤 키에 둘지와 무엇을 올릴지를 고민한다'], ['키는 플레이어가', '스킬을 얻는 순간 키 지정 화면이 뜬다. 나중에 설정에서 바꿀 수 있다'], ['파츠가 보인다', '얻은 파츠는 몸 모델에 붙는다(장착 위치 고정). 화면 속 몸이 지금의 기술 목록이다']];
    R.forEach(([h, b], i) => {
      const w = (CW - 0.5) / 3, x = X0 + i * (w + 0.25);
      card(s, x, 5.3, w, 1.4);
      text(s, h, x + 0.22, 5.4, w - 0.4, 0.36, { size: 14, bold: true });
      text(s, b, x + 0.22, 5.78, w - 0.44, 0.9, { size: 11, color: C.muted });
    });
    DR(s, X0 + CW - 0.85, 5.43);
  }

  // 5 파츠 종류 · 몸 부위 (실제 캐릭터 정면 그림 + 부위 카드)
  {
    const s = slide('04  파츠 종류', '파츠 종류와 몸 부위', '파츠마다 붙는 부위가 정해져 있다. 몸 다섯 부위 — 코어 · 오른팔 · 왼팔 · 다리 · 머리', 'mix');
    // character.png 정면(왼쪽 첫 그림)만 잘라 가운데에
    const fig = path.join(ART, '_crop', 'character_front.png');
    if (has('character.png')) { fs.mkdirSync(path.dirname(fig), { recursive: true }); await sharp(A('character.png')).extract({ left: 30, top: 160, width: 430, height: 650 }).png().toFile(fig); dims['character_front.png'] = [430, 650]; }
    const fx = 5.3, fy = 1.8, fw = 2.72, fh = 4.12;
    s.addShape(pres.shapes.OVAL, { x: fx - 0.2, y: fy + fh - 0.35, w: fw + 0.4, h: 0.5, fill: { color: 'D9D4CC' }, line: { color: 'D9D4CC' } });
    img(s, fig, fx, fy, fw, fh);
    const P = (px, py) => [fx + (px - 30) / 430 * fw, fy + (py - 160) / 650 * fh];
    text(s, '정면 · 화면 왼쪽 칼날이 로봇의 오른팔', fx - 0.4, fy + fh + 0.18, fw + 0.8, 0.25, { size: 9.5, color: C.muted, align: 'center' });
    const M = C.rope, F = C.blue, G = C.grudge;
    const LW = 4.25, RX = 8.45, RW = W - X0 - RX;
    // [이름, 파츠 목록, 앵커, 카드 x, 카드 y, 카드 w]
    const parts = [
      ['머리', [['아직 없음', null]], P(150, 250), X0, 1.8, LW],
      ['오른팔 (칼날)', [['칼날 (기본)', C.ash], ['캐논 포', M], ['회전 관절 · 고속 구동부 *', F]], P(110, 520), X0, 2.95, LW],
      ['코어', [['내오염 코어', M], ['코어 방출기 *', F], ['뒤틀린 에너지 코어', G]], P(255, 420), RX, 1.8, RW],
      ['왼팔 (도구)', [['로프 팔', M], ['방어 장갑 *', F]], P(395, 470), RX, 3.6, RW],
      ['다리 · 등', [['제트엔진', M], ['자석 다리', M]], P(300, 730), RX, 5.05, RW],
    ];
    for (const [h, list, [ax, ay], kx, ky, w] of parts) {
      const hh = 0.5 + list.length * 0.36;
      card(s, kx, ky, w, hh);
      text(s, h, kx + 0.2, ky + 0.08, w - 0.4, 0.34, { size: 13, bold: true });
      list.forEach(([t, c], i) => {
        const y = ky + 0.47 + i * 0.36;
        if (c) s.addShape(pres.shapes.OVAL, { x: kx + 0.24, y: y + 0.08, w: 0.16, h: 0.16, fill: { color: c }, line: { color: c } });
        text(s, t, kx + 0.5, y, w - 0.7, 0.32, { size: 11, color: c ? C.ink : C.muted, valign: 'middle' });
      });
      const ex = kx < fx ? kx + w : kx;
      line(s, ex, ky + 0.25, ax, ay, C.rust, 1, true);
      s.addShape(pres.shapes.OVAL, { x: ax - 0.07, y: ay - 0.07, w: 0.14, h: 0.14, fill: { color: C.rust }, line: { color: 'FFFFFF', width: 1 } });
    }
    const L = [['이동 파츠', M, '길을 여는 확정 보스 보상'], ['전투 파츠', F, '전투 스킬'], ['원혼 파츠', G, '보스가 남기는 붉은 부품 · 선택']];
    L.forEach(([h, c, b], i) => {
      const y = 4.85 + i * 0.36;
      s.addShape(pres.shapes.OVAL, { x: X0 + 0.05, y: y + 0.07, w: 0.18, h: 0.18, fill: { color: c }, line: { color: c } });
      text(s, h, X0 + 0.35, y, 1.2, 0.32, { size: 11, bold: true, valign: 'middle' });
      text(s, b, X0 + 1.5, y, 2.8, 0.32, { size: 10.5, color: C.muted, valign: 'middle' });
    });
    text(s, '* = 출처 초안(검토 필요). 이동 파츠와 부위, 원혼 파츠(뒤틀린 에너지 코어)는 결정', X0, 6.62, CW, 0.3, { size: 10, color: C.muted });
  }

  // 6 지금 스킬의 출처 (초안)
  {
    const s = slide('05  스킬 출처', '지금 스킬은 어떤 파츠에서 오는가', '프로토에 있던 전투 스킬 4개를 파츠로 옮긴다. 이동 파츠의 스킬 형태도 함께 정리', 'draft');
    text(s, '전투 스킬 — 프로토에서 옮김', X0, 1.75, 6, 0.36, { size: 14, bold: true, color: C.blue });
    table(s, [
      ['스킬', '지금 키 · 쿨다운', '출처 파츠', '부위', '상태'],
      ['반격', 'E · 5초', '방어 장갑', '왼팔', { text: '초안', color: C.draft, bold: true }],
      ['회오리', 'Q · 4초', '칼날 관절 (회전 관절)', '오른팔', { text: '초안', color: C.draft, bold: true }],
      ['칼날 폭풍', 'R · 9초', '칼날 관절 (고속 구동부)', '오른팔', { text: '초안', color: C.draft, bold: true }],
      ['레이저 파동', 'W · 3초', '코어 방출기', '코어', { text: '초안', color: C.draft, bold: true }],
    ], X0, 2.15, 7.0, [1.35, 1.45, 2.3, 0.9, 1.0], { rowH: 0.5, size: 11.5 });
    text(s, '기획_결정사항.md는 회오리 · 칼날 폭풍을 "칼날 관절 파츠" 하나로 묶었다. 괄호 안 이름은 메인 세션의 구현 초안(Skills.cs)으로, 둘로 나눌지 정해야 한다', X0, 4.75, 7.0, 0.65, { size: 10.5, color: C.muted });
    text(s, '이동 파츠 스킬', 8.0, 1.75, 4.7, 0.36, { size: 14, bold: true, color: C.rope });
    table(s, [
      ['파츠', '스킬 (여는 길)'],
      ['로프 팔', '앵커 당겨 오르기 · 스윙'],
      ['제트엔진', '공중 대시 · 이단 점프'],
      ['캐논 포', '포격 (금간 벽 파괴)'],
      ['자석 다리', '금속 벽 · 천장 걷기'],
      ['내오염 코어', '오염 지대 통과'],
    ], 8.0, 2.15, 4.73, [1.5, 3.23], { rowH: 0.47, size: 11.5 });
    text(s, '파츠와 여는 길은 결정. 각각을 "키를 누르는 스킬"로 둘지, 늘 켜진 능력(내오염 코어 · 자석 다리)으로 둘지는 정할 것', 8.0, 5.05, 4.73, 0.75, { size: 10.5, color: C.muted });
    card(s, X0, 5.6, 7.0, 1.05, { fill: C.coal, flat: true });
    text(s, '뜯어 쓰는 몸과의 관계', X0 + 0.25, 5.68, 4, 0.34, { size: 12.5, bold: true, color: C.gold });
    text(s, '반격이 파츠가 되면 "관절이 빛나는 공격을 반격해 부위를 뜯는" 기믹은 방어 장갑을 얻은 뒤부터 쓸 수 있다. Z0는 원래 뜯기 없이 싸우므로 맞물린다', X0 + 0.25, 6.0, 6.5, 0.62, { size: 10.5, color: 'E6E1D9' });
    OK(s, 8.0, 5.95); text(s, '이동 파츠 · 여는 길', 8.7, 5.95, 2.3, 0.28, { size: 10.5, valign: 'middle' });
    TODO(s, 8.0, 6.32); text(s, '눌러 쓰기 / 늘 켜짐', 8.95, 6.32, 2.3, 0.28, { size: 10.5, valign: 'middle' });
  }

  // 7 획득처
  {
    const s = slide('06  획득처', '파츠는 어디서 얻는가', '보스 보상은 결정. 나머지 획득처는 후보(초안)', 'mix');
    const S = [
      ['보스 · 이동 파츠', '존 보스를 쓰러뜨리면 확정으로 이동 파츠. Z0 → 로프 팔', 'ok', C.rope],
      ['보스 · 원혼 파츠', '정화 뒤 붉게 빛나는 부품이 떨어짐. 정화할지 장착할지 고르는 선택 보상', 'ok', C.grudge],
      ['공명 기둥', '존마다 있는 기둥. 전투 스킬 파츠를 주는 자리로 쓰는 안 (전망 지점도 겸함)', 'draft', C.soul],
      ['엘리트 처치', '존마다 엘리트 1~2종. 뜯어 쓰는 몸 기믹과 함께 파츠를 떨어뜨리는 안', 'draft', C.rust],
      ['숨은 상인', '존마다 숨은 온화한 기계 상인. "원혼이 깃든 부품" 화폐로 파츠를 사는 안', 'draft', C.ash],
    ];
    S.forEach(([h, b, st, c], i) => {
      const y = 1.8 + i * 0.86;
      card(s, X0, y, 8.2, 0.74);
      s.addShape(pres.shapes.RECTANGLE, { x: X0, y, w: 0.12, h: 0.74, fill: { color: c }, line: { color: c } });
      text(s, h, X0 + 0.35, y, 2.3, 0.74, { size: 14, bold: true, valign: 'middle' });
      text(s, b, X0 + 2.7, y, 4.55, 0.74, { size: 11, color: C.muted, valign: 'middle' });
      (st === 'ok' ? OK : DR)(s, X0 + 7.4, y + 0.23);
    });
    // 오른쪽: 원칙
    const rx = 9.1, rw = W - X0 - rx;
    card(s, rx, 1.8, rw, 4.2, { fill: C.coal, flat: true });
    text(s, '획득 원칙', rx + 0.25, 1.95, rw - 0.5, 0.36, { size: 14, bold: true, color: C.gold });
    text(s, bullets([
      '진행 순서가 자유 → 어떤 파츠 없이도 보스를 깰 수 있어야 하고, 있으면 쉬워진다',
      '이동 파츠는 길(깊은 곳 입구)을 연다. 전투 파츠는 길을 막지 않는다',
      '다시 찾아갈 이유: 나중 파츠로 열리는 작은 보상 지점(상인 · 부품 · 음성 기록)',
    ]), rx + 0.25, 2.4, rw - 0.45, 3.5, { size: 11, color: 'FFFFFF', ls: 1.35, gap: 6 });
    TODO(s, X0, 6.2); text(s, 'Z0에서 첫 전투 스킬을 얻는 시점 · 존마다 몇 개를 얻는지 · 파츠별 스킬 목록과 순서', X0 + 0.95, 6.2, 11, 0.28, { size: 11, valign: 'middle' });
  }

  // 8 키 지정 화면 목업
  {
    const s = slide('07  키 지정 UX', '키 지정 화면 — 키보드 목업', '스킬을 얻는 순간 뜬다. 쓰고 싶은 키를 누르면 끝. 게임 안 문구는 중립적으로', 'draft');
    // 목업 패널
    const mx = X0, my = 1.75, mw = 8.0, mh = 5.0;
    s.addShape(pres.shapes.RECTANGLE, { x: mx, y: my, w: mw, h: mh, fill: { color: C.deep }, line: { color: '3A3F45', width: 1 }, shadow: shadow() });
    text(s, '새 부품', mx + 0.35, my + 0.25, 3, 0.3, { size: 11, bold: true, color: C.soul });
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: mx + 0.35, y: my + 0.62, w: 0.95, h: 0.95, rectRadius: 0.1, fill: { color: '2A2E33' }, line: { color: C.soul, width: 1.5 } });
    s.addShape(pres.shapes.RIGHT_ARROW, { x: mx + 0.5, y: my + 0.92, w: 0.65, h: 0.35, fill: { color: C.rust }, line: { color: C.rust } });
    text(s, '로켓 팔', mx + 1.5, my + 0.6, 4, 0.5, { size: 22, bold: true, color: 'FFFFFF' });
    text(s, '오른팔 · 칼날을 접고 팔을 쏘아 보낸다', mx + 1.5, my + 1.1, 5, 0.3, { size: 11, color: 'B8B2AA' });
    text(s, '사용할 키를 누르세요', mx, my + 1.85, mw, 0.4, { size: 16, bold: true, color: C.gold, align: 'center' });
    // 키보드
    const rows = ['QWERTYUIOP', 'ASDFGHJKL', 'ZXCVBNM'];
    const kw = 0.5, kg = 0.08, k0 = mx + 1.1;
    const used = { A: 'base', F: 'base', W: 'own', E: 'own', R: 'own', M: 'res', G: 'res', T: 'res', V: 'res', C: 'res', P: 'res', H: 'res', K: 'res', Q: 'new' };
    rows.forEach((r, ri) => {
      [...r].forEach((ch, ci) => {
        const x = k0 + ri * 0.3 + ci * (kw + kg), y = my + 2.35 + ri * (kw + kg);
        const u = used[ch];
        const o = u === 'base' ? { fill: '4A4F56', color: 'FFFFFF' } : u === 'own' ? { fill: C.blue, color: 'FFFFFF', line: C.blue } : u === 'res' ? { fill: '1E2125', color: '5A5F66', dash: true, line: '5A5F66' } : u === 'new' ? { fill: C.gold, color: C.coal, line: 'FFFFFF', lw: 2 } : {};
        key(s, ch, x, y, kw, kw, o);
      });
    });
    key(s, 'SPACE', k0 + 0.9, my + 2.35 + 3 * (kw + kg), 3.2, 0.4, { fill: '4A4F56', color: 'FFFFFF', size: 10 });
    // 범례
    const LG = [['4A4F56', '기본 동작'], [C.blue, '이미 지정한 스킬'], [C.gold, '지금 누른 키'], ['1E2125', '예약 키']];
    LG.forEach(([c, t], i) => {
      const x = mx + 0.4 + i * 1.9, y = my + 4.6;
      s.addShape(pres.shapes.RECTANGLE, { x, y: y + 0.05, w: 0.22, h: 0.22, fill: { color: c }, line: { color: '5A5F66', width: 0.75, dashType: i === 3 ? 'dash' : 'solid' } });
      text(s, t, x + 0.3, y, 1.55, 0.32, { size: 10, color: 'D7D2CA', valign: 'middle' });
    });
    // 오른쪽 규칙
    const rx = 8.95, rw = W - X0 - rx;
    text(s, '규칙 (초안)', rx, 1.75, rw, 0.36, { size: 14, bold: true, color: C.rust });
    text(s, bullets([
      '빈 키를 누르면 바로 지정',
      '이미 쓰는 키를 누르면 "바꿀까요?" 확인 → 밀려난 스킬은 키 없음 상태',
      '기본 동작(A · Space · F)과 예약 키(메뉴 · 지도 · 디버그)는 고를 수 없음',
      '나중에 설정 화면에서 언제든 다시 지정',
      '숫자 키 · 마우스 버튼을 쓸지는 정할 것',
    ]), rx, 2.15, rw, 3.2, { size: 11.5, ls: 1.35, gap: 4 });
    card(s, rx, 5.35, rw, 1.4, { fill: 'F7F5F2', flat: true });
    TODO(s, rx + 0.2, 5.48);
    text(s, '패드 — 버튼 수가 적어 "키 하나에 스킬 하나"가 안 된다. 스킬 휠 · 보조 버튼 조합 등 별도 설계 필요', rx + 0.2, 5.82, rw - 0.4, 0.9, { size: 11 });
    caption(s, '예시: 로켓 팔(사용자 예시 이름) · 지금 프로토의 예약 키 기준', mx, my + mh + 0.05, mw);
  }

  // 9 스킬 레벨업
  {
    const s = slide('08  스킬 레벨', '기본 부품으로 스킬 레벨을 올린다', '스킬은 파츠로 생기고, 레벨로 깊어진다. 몸 능력치(최대 체력 등)는 성장 노드로 오른다', 'mix');
    const F = [['적 처치 · 줍기', C.ash], ['기본 부품 (경험치)', C.rust], ['포인트', C.gold], ['스킬 레벨', C.ok]];
    const fw = 2.55, fg = (CW - 4 * fw) / 3;
    F.forEach(([t, c], i) => {
      const x = X0 + i * (fw + fg);
      chip(s, t, x, 1.8, fw, 0.55, c, 'FFFFFF', 14);
      if (i < 3) arrow(s, x + fw + 0.05, 2.07, x + fw + fg - 0.05, 2.07, C.ash, 2);
    });
    // 레벨업 축
    text(s, '레벨이 오를 때 바뀌는 것', X0, 2.7, 6, 0.36, { size: 14, bold: true });
    const AX = [['피해', '한 번에 주는 피해'], ['쿨다운', '다시 쓰기까지 시간'], ['범위', '판정 크기 · 사거리'], ['추가 효과', '경직 · 연타 수 · 둔화 등']];
    AX.forEach(([h, b], i) => {
      const x = X0 + i * 1.75;
      card(s, x, 3.1, 1.6, 1.15);
      text(s, h, x + 0.15, 3.18, 1.3, 0.38, { size: 14, bold: true, color: C.ok });
      text(s, b, x + 0.15, 3.58, 1.35, 0.62, { size: 10.5, color: C.muted });
    });
    // 예시 레벨 표
    text(s, '예시 — 회오리 (숫자는 형태만 보여 주는 가짜 값)', X0, 4.45, 7, 0.32, { size: 12, bold: true, color: C.draft });
    table(s, [
      ['레벨', '1', '2', '3', '4', '5'],
      ['피해', '100%', '115%', '130%', '130%', '150%'],
      ['쿨다운', '4초', '4초', '3.5초', '3.5초', '3초'],
      ['추가', '—', '—', '범위 +20%', '끌어당김', '2회전'],
    ], X0, 4.8, 6.85, [1.1, 1.15, 1.15, 1.15, 1.15, 1.15], { rowH: 0.4, size: 11, center: true });
    // 오른쪽: 몸 능력치
    const rx = 7.85, rw = W - X0 - rx;
    card(s, rx, 2.7, rw, 1.9);
    s.addShape(pres.shapes.RECTANGLE, { x: rx, y: 2.7, w: 0.12, h: 1.9, fill: { color: C.rust }, line: { color: C.rust } });
    text(s, '몸 능력치 = 성장 노드', rx + 0.35, 2.8, rw - 0.5, 0.36, { size: 14, bold: true });
    text(s, '최대 체력 등은 몸 부위별 성장 노드로 오른다(코어 · 오른팔 · 왼팔 · 다리 · 머리). 회복 스킬은 최대 체력의 %로 계산해 노드가 올라도 가치가 유지된다', rx + 0.35, 3.2, rw - 0.55, 1.35, { size: 11, color: C.muted });
    card(s, rx, 4.8, rw, 1.9, { fill: C.coal, flat: true });
    text(s, '정할 것', rx + 0.25, 4.9, 2, 0.34, { size: 13, bold: true, color: C.gold });
    text(s, bullets([
      '레벨 상한 (구현 초안은 5) · 레벨당 포인트',
      '스킬 레벨과 성장 노드가 같은 포인트를 나눠 쓰는지',
      '죽으면 떨어지는 기본 부품 — 포인트로 바꾼 뒤엔 안전한지',
      '포인트를 되돌려 받는(리셋) 수단',
    ]), rx + 0.25, 5.25, rw - 0.45, 1.4, { size: 10.5, color: 'FFFFFF', ls: 1.3 });
  }

  // 10 원혼 파츠 개요 (어두운 판 · 큰 실제 화면)
  {
    const s = darkSlide('09  원혼 파츠', '원혼 파츠 — 정화할까, 장착할까', '보스를 정화해도 남은 것이 한 부위에 모여 붉게 빛나는 부품으로 떨어진다. 부위마다 따로 고른다', 'ok');
    await cimg(s, 'boss_purify_reward.png', X0, 1.8, 7.0, 3.94, { shadow: true });
    chip(s, '붉게 빛나는 부품', X0 + 0.25, 1.98, 1.95, 0.36, C.grudge, 'FFFFFF', 11);
    captionDk(s, 'Unity 실제 화면 (Z0 정화 직후) — 로프 팔(이동 파츠)과 붉은 부품(원혼 파츠)이 함께 떨어진다', X0, 5.78, 7.0);
    const bx = 7.95, bw = W - X0 - bx;
    const B = [
      ['정화 — 포드에 제출', C.soul, ['플레이 시간 약 15분 (탐험하는 동안 진행)', '그 존이 한 단계 더 자연화', '보상: 기본 부품(경험치)']],
      ['장착 — 위험을 감수', C.grudge, ['보스의 능력을 흡수 · 영구 (뗄 수 없음)', '얼굴이 붉어지고 표정을 잃음', '엔딩 4루트 판정에 들어감']],
    ];
    B.forEach(([h, c, l], i) => {
      const y = 1.8 + i * 2.05;
      card(s, bx, y, bw, 1.89, DK);
      s.addShape(pres.shapes.RECTANGLE, { x: bx, y, w: 0.1, h: 1.89, fill: { color: c }, line: { color: c } });
      text(s, h, bx + 0.3, y + 0.12, bw - 0.4, 0.4, { size: 15, bold: true, color: c });
      text(s, bullets(l), bx + 0.3, y + 0.6, bw - 0.45, 1.2, { size: 11.5, color: 'E6E1D9', ls: 1.35 });
    });
    const T = [['각인형', '이미 가진 스킬 하나에 특수 능력. 키 지정 없음', '예시 필요 (아직 없음)', '5A5F66'], ['스킬형', '보스의 능력을 새 스킬로. 새 키를 지정', 'Z0 뒤틀린 에너지 코어 → 코어 과부하', C.grudge]];
    text(s, '장착 효과는 파츠마다 하나로 고정', X0, 6.18, 3.2, 0.5, { size: 12, bold: true, color: 'FFFFFF', valign: 'middle' });
    T.forEach(([h, b, e, c], i) => {
      const x = 3.95 + i * 4.42, w = 4.3;
      card(s, x, 6.08, w, 0.72, DK);
      chip(s, h, x + 0.15, 6.27, 0.95, 0.34, c, 'FFFFFF', 11);
      text(s, b, x + 1.2, 6.1, w - 1.3, 0.34, { size: 10, color: 'B8B2AA', valign: 'middle' });
      text(s, e, x + 1.2, 6.42, w - 1.3, 0.34, { size: 10.5, bold: true, color: i ? 'F08A6E' : '8E8880', valign: 'middle' });
    });
  }

  // 11 Z0 예시 — 코어 과부하
  {
    const s = slide('10  Z0 예시', '뒤틀린 에너지 코어 → 코어 과부하', '에너지 기둥에서 착안한 스킬형. 코어를 고속으로 돌려 내부 에너지를 끌어낸다', 'mix');
    // 아이콘: 금 간 코어 + 붉은 오라
    const cx = X0 + 1.9, cy = 3.2;
    s.addShape(pres.shapes.OVAL, { x: cx - 1.35, y: cy - 1.35, w: 2.7, h: 2.7, fill: { color: C.grudge, transparency: 85 }, line: { color: C.grudge, width: 1, dashType: 'dash' } });
    s.addShape(pres.shapes.OVAL, { x: cx - 0.95, y: cy - 0.95, w: 1.9, h: 1.9, fill: { color: C.grudge, transparency: 65 }, line: { color: C.grudge, transparency: 100 } });
    s.addShape(pres.shapes.OVAL, { x: cx - 0.55, y: cy - 0.55, w: 1.1, h: 1.1, fill: { color: 'E8452F' }, line: { color: '7A1D16', width: 2 } });
    line(s, cx - 0.3, cy - 0.35, cx + 0.05, cy + 0.05, '7A1D16', 2.5); line(s, cx + 0.05, cy + 0.05, cx - 0.1, cy + 0.42, '7A1D16', 2.5); line(s, cx + 0.05, cy + 0.05, cx + 0.38, cy - 0.12, '7A1D16', 2);
    text(s, '붉은 오라 · 금 간 코어 (도식)', X0, 4.7, 3.8, 0.28, { size: 10, color: C.muted, align: 'center' });
    // 효과 / 대가
    const ex = 4.6, ew = 3.95;
    card(s, ex, 1.8, ew, 2.1);
    text(s, '효과', ex + 0.25, 1.9, 2, 0.36, { size: 15, bold: true, color: C.ok });
    text(s, bullets(['지속 시간 동안 체력이 계속 회복 (최대 체력의 %)', '이동 속도 증가']), ex + 0.25, 2.32, ew - 0.45, 1.5, { size: 12, ls: 1.35 });
    card(s, ex + ew + 0.2, 1.8, ew, 2.1);
    text(s, '대가', ex + ew + 0.45, 1.9, 2, 0.36, { size: 15, bold: true, color: C.grudge });
    text(s, bullets(['캐릭터 주변에 붉은 오라', '적의 공격에 더 큰 피해', '장착 자체의 대가: 얼굴 · 엔딩 (11 참고)']), ex + ew + 0.45, 2.32, ew - 0.45, 1.5, { size: 12, ls: 1.35 });
    // 수치 표
    text(s, '초안 값 (검토 필요)', ex, 4.1, 4, 0.34, { size: 13, bold: true, color: C.draft });
    table(s, [
      ['지속', '회복', '이동 속도', '받는 피해', '쿨다운'],
      ['8초', '초당 최대 체력 4% (총 32%)', '+30%', '+50%', '45초'],
    ], ex, 4.48, 8.1, [1.0, 3.0, 1.3, 1.4, 1.4], { rowH: 0.45, size: 12, center: true });
    // 타임라인: 8초 동안 HP
    const tx = ex, ty = 5.65, tw = 8.1;
    text(s, '쓰는 8초 동안', X0, 5.6, 3.8, 0.3, { size: 12, bold: true, align: 'right' });
    text(s, '회복은 쌓이고, 맞으면 1.5배로 깎인다 → 안전할 때 켜야 이득', X0, 5.9, 3.8, 0.55, { size: 10.5, color: C.muted, align: 'right' });
    s.addShape(pres.shapes.RECTANGLE, { x: tx, y: ty, w: tw, h: 0.5, fill: { color: C.grudge, transparency: 80 }, line: { color: C.grudge, width: 1 } });
    for (let i = 0; i <= 8; i++) {
      const x = tx + (tw * i) / 8;
      if (i > 0 && i < 8) line(s, x, ty, x, ty + 0.5, C.grudge, 0.5, true);
      text(s, `${i}s`, x - 0.3, ty + 0.55, 0.6, 0.22, { size: 9, color: C.muted, align: 'center' });
      if (i > 0) text(s, `+${i * 4}%`, x - (tw / 8), ty, tw / 8, 0.5, { size: 10, bold: true, color: C.grudge, align: 'center', valign: 'middle' });
    }
    text(s, '게임 안 표기: 부품 이름과 스킬 이름만. 붉은 빛으로 정체를 짐작하게 한다', ex, 6.6, tw, 0.28, { size: 10, color: C.muted });
  }

  // 12 대가와 엔딩 (어두운 판 · 얼굴 크게)
  {
    const s = darkSlide('11  대가 · 엔딩', '장착할수록 얼굴이 붉어지고, 엔딩이 갈린다', '원혼 파츠는 보스 8명분, 최대 8개. 얼굴 변화는 루트 경계에 맞춘다 — 되찾는 표정과 정면으로 부딪힌다', 'mix');
    const faces = [await redFace('face_happy.png', 0), await redFace('face_focus.png', 0.4), await redFace('face_idle.png', 0.85, 0.85), await redFace('face_idle.png', 1, 1.1)];
    const R = [['0~1', '루트 1', '원래 색 · 해금한 표정 전부'], ['2~3', '루트 2', '붉은 기가 돎'], ['4~5', '루트 3', '진한 빨강 · 표정이 가끔 무표정으로 끊김'], ['6 이상', '루트 4', '빨강 · 무표정만 출력']];
    const RC = [C.soul, C.rust, C.grudge, '7A1D16'];
    const cw = (CW - 0.45) / 4, fh = cw * 2 / 3;
    R.forEach(([n, r, f], i) => {
      const x = X0 + i * (cw + 0.15), y = 1.85;
      img(s, faces[i], x, y, cw, fh);
      s.addShape(pres.shapes.RECTANGLE, { x, y: y + fh, w: cw, h: 0.08, fill: { color: RC[i] }, line: { color: RC[i] } });
      text(s, `장착 ${n}`, x, y + fh + 0.22, cw - 1.1, 0.42, { size: 17, bold: true, color: 'FFFFFF' });
      chip(s, r, x + cw - 1.0, y + fh + 0.27, 0.95, 0.32, RC[i], 'FFFFFF', 10.5);
      text(s, f, x, y + fh + 0.7, cw, 0.6, { size: 11, color: 'B8B2AA' });
    });
    arrow(s, X0, 5.3, X0 + CW, 5.3, C.grudge, 2);
    text(s, '장착 수 → (영구라서 엔딩은 장착한 수로 판정)', X0, 5.36, CW, 0.28, { size: 10.5, bold: true, color: 'F08A6E', align: 'right' });
    card(s, X0, 5.8, CW, 0.7, DK);
    OK(s, X0 + 0.2, 6.01); text(s, '얼굴이 붉어지고 표정을 잃음 · 엔딩 4루트 · 장착은 영구', X0 + 0.95, 5.8, 6.0, 0.7, { size: 10.5, color: 'E6E1D9', valign: 'middle' });
    DR(s, X0 + 7.05, 6.01); TODO(s, X0 + 7.75, 6.01);
    text(s, '경계 숫자 · 루트별 엔딩 시나리오 · 얼굴 외 능력치 단점 여부', X0 + 8.7, 5.8, 3.3, 0.7, { size: 10.5, color: 'E6E1D9', valign: 'middle' });
    captionDk(s, '얼굴 그림은 PixelFace 규칙 렌더(art/face_*.png)를 색만 바꾼 설명용', X0, 6.6, CW);
  }

  // 13 서사 원칙 (어두운 판 · 그림 카드)
  {
    const s = darkSlide('12  서사 원칙', '게임 안에서는 이름을 붙이지 않는다', '"원한 · 원혼"은 기획 문서 안의 용어. 플레이어는 붉은 빛 · 얼굴 · 엔딩으로 스스로 짐작한다', 'ok');
    const cw = (CW - 0.3) / 2;
    const D = [
      ['게임 안에서 쓰는 것', C.ok, ['"붉게 빛나는 부품"', '부품 이름 — 뒤틀린 에너지 코어', '중립적인 스킬 이름 — 코어 과부하', '효과는 있는 그대로 — 체력 회복 · 받는 피해 증가']],
      ['게임 안에서 쓰지 않는 것', C.grudge, ['"원한", "원혼" — UI · 자막 · 부품 설명 · 스킬 이름 · 음성 기록 전부', '정체를 설명하는 문장 ("인간의 증오가 깃든…")', '"장착하면 나쁜 엔딩" 같은 직접 경고']],
    ];
    D.forEach(([h, c, l], i) => {
      const x = X0 + i * (cw + 0.3);
      card(s, x, 1.8, cw, 2.0, DK);
      s.addShape(pres.shapes.RECTANGLE, { x, y: 1.8, w: cw, h: 0.08, fill: { color: c }, line: { color: c } });
      text(s, h, x + 0.25, 1.98, cw - 0.5, 0.36, { size: 14, bold: true, color: i ? 'F08A6E' : '5CC9A4' });
      text(s, bullets(l), x + 0.25, 2.4, cw - 0.5, 1.35, { size: 11, color: 'E6E1D9', ls: 1.3 });
    });
    text(s, '짐작하게 만드는 세 가지', X0, 4.0, 6, 0.34, { size: 13, bold: true, color: 'FFFFFF' });
    const red = await redFace('face_idle.png', 1, 1.1);
    const Q = [['붉은 빛', '부품 · 오라 · 뜯긴 몸에서 새는 붉은 빛. 처치 때 붉은 혼이 푸르게 바뀌는 연출과 대비', 'hook.png', C.grudge], ['얼굴', '장착할수록 LED가 붉어지고 되찾은 표정이 끊긴다. 말 대신 얼굴이 보여 준다', red, C.rust], ['엔딩', '장착 수에 따라 4루트. 끝에 가서야 무엇을 몸에 달았는지 드러난다', 'key_art.png', C.gold]];
    const qw = (CW - 0.5) / 3, ih = 1.25;
    for (let i = 0; i < Q.length; i++) {
      const [h, b, f, c] = Q[i], x = X0 + i * (qw + 0.25), y = 4.4;
      card(s, x, y, qw, 2.4, DK);
      if (path.isAbsolute(f)) { s.addShape(pres.shapes.RECTANGLE, { x, y, w: qw, h: ih, fill: { color: '0B0C0E' }, line: { color: '0B0C0E' } }); img(s, f, x, y, qw, ih); }
      else await cimg(s, f, x, y, qw, ih, { focusY: 0.45 });
      badge(s, i + 1, x + 0.18, y + ih + 0.14, 0.36, c);
      text(s, h, x + 0.65, y + ih + 0.12, qw - 0.8, 0.4, { size: 14, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, x + 0.18, y + ih + 0.56, qw - 0.36, 0.6, { size: 10, color: 'B8B2AA' });
    }
  }

  // 14 정할 것
  {
    const s = slide('13  정할 것', '정할 것', '사용자 결정이 필요한 항목. 위쪽일수록 구현을 막는다', 'todo');
    const Q = [
      ['파츠별 스킬 목록과 얻는 순서', '존마다 몇 개, 어떤 파츠가 어떤 스킬을 주는지. Z0에서 첫 스킬을 얻는 시점', '구현 막음'],
      ['스킬 레벨 상한 · 포인트 수치', '레벨당 포인트, 성장 노드와 같은 포인트를 쓰는지, 리셋 수단', '구현 막음'],
      ['이동 파츠: 눌러 쓰기 / 늘 켜짐', '내오염 코어 · 자석 다리처럼 키가 필요 없는 능력을 키 목록에 넣을지', ''],
      ['키 설정 — 패드', '버튼이 모자람. 스킬 휠 · 보조 버튼 조합 · 자주 쓰는 것만 바로 쓰기 등', ''],
      ['스킬이 많아질 때 조작 부담', '후반 10개 이상일 때 키 기억 부담. 묶음(이동 / 전투) · 쿨다운 HUD 배치', ''],
      ['원혼 파츠 각인형 예시 · 경계 숫자 · 엔딩 시나리오', '첫 각인형 파츠, 장착 수 경계(0~1 / 2~3 / 4~5 / 6+), 루트 4개 내용', ''],
    ];
    Q.forEach(([h, b, tag], i) => {
      const y = 1.75 + i * 0.84;
      card(s, X0, y, CW, 0.72);
      badge(s, i + 1, X0 + 0.22, y + 0.15, 0.42, C.grudge);
      text(s, h, X0 + 0.85, y, 4.6, 0.72, { size: 13.5, bold: true, valign: 'middle' });
      text(s, b, X0 + 5.5, y, CW - 7.0, 0.72, { size: 11, color: C.muted, valign: 'middle' });
      if (tag) chip(s, tag, X0 + CW - 1.35, y + 0.2, 1.15, 0.32, C.rust, 'FFFFFF', 10);
    });
  }

  // 15 구현 현황
  {
    const s = slide('14  구현 현황', '구현 현황', '지금 프로토는 QWER 고정 칸. 새 구조(스킬 = 파츠)는 메인 세션이 구현 중', 'impl');
    const hud = await hudCrop();
    text(s, '지금 — 고정 7칸 (2026-11-07 캡처)', X0, 1.75, 6, 0.32, { size: 13, bold: true });
    if (hud) img(s, hud, X0, 2.12, 5.6, 1.08, { shadow: true }); else img(s, 'hud_game.png', X0, 2.12, 5.6, 1.08);
    table(s, [
      ['키', 'A', 'Q', 'W', 'E', 'R', 'SPACE', 'F'],
      ['동작', '연속 베기', '회오리', '레이저 파동', '반격', '칼날 폭풍', '대쉬', '점프'],
      ['쿨다운', '—', '4초', '3초', '5초', '9초', '1.1초', '0.8초'],
    ], X0, 3.4, 5.6, [0.75, 0.7, 0.6, 0.85, 0.55, 0.75, 0.75, 0.65], { rowH: 0.4, size: 9.5, center: true });
    text(s, '출처: unity/Assets/SOUL/Runtime/Game/SoulGame.cs (COOLDOWN · 스킬바)', X0, 4.7, 5.6, 0.3, { size: 10, color: C.muted });
    const rx = 6.65, rw = W - X0 - rx;
    text(s, '새 구조 — 구현 예정 · 진행 중', rx, 1.75, rw, 0.32, { size: 13, bold: true, color: C.rust });
    table(s, [
      ['항목', '상태'],
      ['스킬 정의 · 출처 파츠 · 보유 목록 · 레벨 (World/Skills.cs)', '메인 세션 작업 중'],
      ['시작 = 평타 · 대쉬 · 점프만', '구현 예정'],
      ['획득 시 키 지정 화면 · 예약 키', '구현 예정'],
      ['기본 부품 → 포인트 → 스킬 레벨', '구현 예정'],
      ['원혼 파츠 드롭 · 포드 정화 / 장착 선택 (SoulGame.Grudge.cs)', '있음 (Z0)'],
      ['코어 과부하 스킬 효과 · 붉은 오라', '메인 세션 작업 중'],
      ['얼굴 붉어짐 · 엔딩 판정', '구현 예정'],
    ], rx, 2.12, rw, [4.4, rw - 4.4], { rowH: 0.48, size: 10.5, leftFirst: true });
    card(s, X0, 5.15, 5.6, 1.0, { fill: 'F7F5F2', flat: true });
    text(s, bullets(['상태는 2026-11-08 작성 시점 기준', 'Skills.cs는 아직 커밋 전 초안(레벨 상한 5, 존별 공명 기둥 스킬 표 등). 확정은 메인 세션 결과로 갱신']), X0 + 0.2, 5.25, 5.25, 0.85, { size: 10.5, color: C.ink, ls: 1.3 });
    text(s, '경로는 unity/Assets/SOUL/Runtime/ 아래', rx, 6.1, rw, 0.3, { size: 10, color: C.muted });
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

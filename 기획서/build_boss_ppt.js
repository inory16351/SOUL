// ALONE(가제) Z0 보스 기획서 PPT 생성 → 기획서/20261107_ALONE_Z0보스기획서_v0.1_정승운.pptx
// 사용: NODE_PATH=<pptxgenjs · sharp · jszip 설치 폴더>/node_modules node 기획서/build_boss_ppt.js
// 내용 원본: 기획서/Z0_보스_재의감시자.md. 스타일 · 도구는 build_concept_ppt.js 와 같음(복사)
// 그림: 기획서/art (ChatGPT 컨셉) → 없으면 unity/Captures (Unity 실제 캡처). 둘 다 없으면 회색 자리표시
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const sharp = require('sharp');

const ROOT = __dirname;
const ART = path.join(ROOT, 'art');
const CAP = path.join(ROOT, '..', 'unity', 'Captures');
const VER = 'v0.1';
const DATE = '2026-11-07';
const OUT = path.join(ROOT, `20261107_ALONE_Z0보스기획서_${VER}_정승운.pptx`);
const DOC = `ALONE(가제) · Z0 보스 기획서 ${VER}`;

const FONT = 'Noto Sans KR';
const C = {
  coal: '16181B', deep: '0E1012', ink: '23272C', muted: '646B73', bg: 'EEEBE6', card: 'FFFFFF', line: 'D9D4CC', tint: 'E4DED5',
  rust: 'C2562B', ash: '8A8F96', soul: '33B5C8', grudge: 'B8322A', gold: 'E0A030',
  blue: '3F8FD8', red: 'D9502F', rope: 'E0A030',
};
const W = 13.333, H = 7.5, X0 = 0.6, CW = W - 2 * X0;

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = '정승운';
pres.title = 'ALONE(가제) Z0 보스 기획서 — 재의 감시자';
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

// ---------- 도구 (build_concept_ppt.js 와 같음 + 캡처 폴더 탐색) ----------
const dims = {};
const A = (f) => (fs.existsSync(path.join(ART, f)) ? path.join(ART, f) : path.join(CAP, f));
const has = (f) => fs.existsSync(A(f));
async function loadDims() {
  for (const dir of [CAP, ART]) if (fs.existsSync(dir)) for (const f of fs.readdirSync(dir)) if (/\.(png|jpe?g)$/i.test(f) && has(f)) { const m = await sharp(A(f)).metadata(); dims[f] = [m.width, m.height]; }
}
const shadow = () => ({ type: 'outer', color: '000000', opacity: 0.16, blur: 6, offset: 2, angle: 90 });

function text(s, t, x, y, w, h, o = {}) {
  s.addText(t, { x, y, w, h, fontFace: FONT, fontSize: o.size || 13, color: o.color || C.ink, bold: !!o.bold, align: o.align || 'left', valign: o.valign || 'top', margin: 0, isTextBox: true, paraSpaceAfter: o.gap || 0, lineSpacingMultiple: o.ls || 1.15, fit: 'none' });
}
function card(s, x, y, w, h, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.1, fill: { color: o.fill || C.card }, line: { color: o.line || o.fill || C.card, width: 0.75, dashType: o.dash ? 'dash' : 'solid' }, shadow: o.flat ? undefined : shadow() });
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
// 그림: 틀 안에 비율 유지(contain). 없으면 자리표시
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
// cover 용: 비율에 맞춰 가운데 자른 사본을 art/_crop 에 만든다
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
function slide(kicker, title, sub) {
  const s = pres.addSlide({ masterName: 'CONTENT' });
  s.addText(kicker, { placeholder: 'kicker' });
  s.addText(title, { placeholder: 'title', align: 'left' });
  if (sub) s.addText(sub, { placeholder: 'sub' });
  return s;
}
// 표: rows[0] = 머리줄
function table(s, rows, x, y, w, colW, o = {}) {
  const head = rows[0].map((t) => ({ text: t, options: { bold: true, color: 'FFFFFF', fill: { color: C.coal }, align: 'center' } }));
  const body = rows.slice(1).map((r, i) => r.map((t, j) => {
    const cell = typeof t === 'object' ? t : { text: t };
    return { text: cell.text, options: { bold: j === 0 || cell.bold, color: cell.color || C.ink, fill: { color: cell.fill || (i % 2 ? 'F7F5F2' : C.card) }, align: (j === 0 && !o.leftFirst) || o.center ? 'center' : 'left' } };
  }));
  s.addTable([head, ...body], { x, y, w, colW, fontFace: FONT, fontSize: o.size || 12, valign: 'middle', border: { type: 'solid', color: C.line, pt: 0.75 }, rowH: o.rowH || 0.42, margin: [0.04, 0.1, 0.04, 0.1] });
}
const bullets = (a) => a.map((t) => '•  ' + t).join('\n');
// 에너지 기둥 아이콘: 받침 + 몸통 + 끝. n = 순서 번호(없으면 생략)
function pillar(s, x, y, color, n, lit = true) {
  s.addShape(pres.shapes.RECTANGLE, { x: x - 0.28, y: y + 0.9, w: 0.56, h: 0.14, fill: { color: '8E8880' }, line: { color: '8E8880' } });
  s.addShape(pres.shapes.RECTANGLE, { x: x - 0.17, y: y + 0.2, w: 0.34, h: 0.72, fill: { color: C.coal }, line: { color: lit ? color : C.coal, width: 1.5 } });
  s.addShape(pres.shapes.ISOSCELES_TRIANGLE, { x: x - 0.17, y: y - 0.06, w: 0.34, h: 0.28, fill: { color: lit ? color : '5A5F66' }, line: { color: lit ? color : '5A5F66' } });
  if (n) badge(s, n, x - 0.17, y + 0.38, 0.34, color);
}

// ---------- 슬라이드 ----------
const CONCEPT = 'boss_scrapbeast.png';
async function build() {
  await loadDims();
  const coverArt = has(CONCEPT) ? CONCEPT : 'boss_wake.png';

  // 1 표지
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    await cimg(s, coverArt, 0, 0, W, H, { focusY: 0.5 });
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: 3.3, fill: { color: C.deep, transparency: 40 }, line: { color: C.deep, transparency: 100 } });
    text(s, 'BOSS DESIGN DOCUMENT  ·  ZONE 0', 0, 0.45, W, 0.35, { size: 13, bold: true, color: C.soul, align: 'center' });
    text(s, '재의 감시자', 0, 0.8, W, 1.1, { size: 60, bold: true, color: 'FFFFFF', align: 'center' });
    text(s, 'Z0 잿빛 폐허 보스 · 이름은 검토 필요', 0, 1.9, W, 0.35, { size: 14, color: 'D7D2CA', align: 'center' });
    text(s, '에너지 기둥에서 힘을 받아 깨어나는 거대한 고철 짐승 — 기둥을 순서대로 부숴 회복을 막는다', 0, 2.3, W, 0.4, { size: 15, color: 'F0ECE6', align: 'center' });
    text(s, `Z0 보스 기획서  ·  작성자 정승운  ·  작성일 ${DATE}  ·  버전 ${VER}  ·  ALONE(가제) 컨셉 기획서 v0.3 부속`, 0, 2.78, W, 0.3, { size: 11, color: 'D7D2CA', align: 'center' });
  }

  // 2 한 줄 요약 · 역할
  {
    const s = slide('01  개요', '한 줄 요약 · 역할', '첫 존의 보스 — 로프 팔 없이 칼과 위치 선정만으로 공략할 수 있어야 한다');
    card(s, X0, 1.8, CW, 1.05, { fill: C.coal, flat: true });
    text(s, '폐허 도심 동쪽 보스 지대에 잠들어 있던 거대한 고철 짐승. 에너지 기둥에서 힘을 받아 깨어나고,\n기둥을 정해진 순서대로 부숴야만 회복을 막을 수 있다.', X0 + 0.35, 1.8, CW - 0.7, 1.05, { size: 15, bold: true, color: 'FFFFFF', valign: 'middle', ls: 1.3 });
    table(s, [
      ['항목', '내용'],
      ['존', 'Z0 잿빛 폐허 — 시작 · 중앙 허브'],
      ['보스 지대', '동쪽 원형 콘크리트 벽 · 중심 (600, 0) · 반지름 42m'],
      ['출입', '서쪽 정문 · 북쪽 로프 지름길(한 방향)'],
      ['공략 수단', '칼과 위치 선정만 (첫 존이라 로프 팔 없음)'],
      ['빠진 기믹', '"뜯기"(관절 공격 반격 → 부위 뜯기)는 로프 팔을 얻은 뒤의 보스로 옮김. Z0에서는 쓰지 않음'],
    ], X0, 3.1, 7.4, [1.6, 5.8], { rowH: 0.56, size: 12 });
    const R = [['정화', '안개가 걷히고 색과 풀이 돌아오고 생물이 돌아옴', C.soul], ['로프 팔', '첫 핵심 이동 파츠 획득', C.rope], ['첫 표정', '표정 성장의 첫 해금', C.rust]];
    text(s, '처치 보상', 8.35, 3.1, 4.3, 0.36, { size: 15, bold: true, color: C.rust });
    R.forEach(([h, b, c], i) => {
      const y = 3.55 + i * 0.95;
      card(s, 8.35, y, 4.38, 0.8);
      s.addShape(pres.shapes.RECTANGLE, { x: 8.35, y, w: 0.12, h: 0.8, fill: { color: c }, line: { color: c } });
      text(s, h, 8.65, y + 0.08, 3.9, 0.34, { size: 14, bold: true });
      text(s, b, 8.65, y + 0.42, 3.95, 0.32, { size: 11, color: C.muted });
    });
  }

  // 3 외형 콘셉트
  {
    const s = slide('02  외형', '외형 콘셉트 — 고철 기계 짐승', '증오한 인간의 영혼이 고철에 깃들었다. 개의 얼굴, 도마뱀의 몸, 다리 셋, 등 위의 거대한 팔');
    const hasArt = has(CONCEPT);
    await cimg(s, hasArt ? CONCEPT : 'boss_wake.png', X0, 1.75, 6.3, 3.55, { shadow: true, focusY: 0.5 });
    caption(s, hasArt ? '컨셉 이미지 (ChatGPT 생성, 분위기 참고용) — 다리 수 · 기둥 수는 오른쪽 기획 기준' : 'Unity 실제 화면 — 컨셉 이미지는 검토 필요', X0, 5.36, 6.3);
    text(s, '팔은 빅토르의 팔 같은 느낌을 참고만 하고 디자인은 오리지널. 기어 · 엔진 블록 · 배기관 · 자동차 패널 · 타이어 · 철근 같은 부품이 무작위로 섞인다', X0, 5.72, 6.3, 0.8, { size: 11, color: C.ink });
    const P = [
      ['머리', '개과(늑대 · 자칼) 얼굴. 용접한 판의 긴 두개골, 경첩으로 열리는 턱과 들쭉날쭉한 고철 이빨, 빛나는 영혼의 눈'],
      ['몸', '날렵하고 낮게 깔린 도마뱀형. 갈비뼈 · 피스톤 · 파이프 · 판, 좌우로 휘는 긴 척추와 사슬 꼬리'],
      ['다리 3개', '앞 왼쪽 · 앞 오른쪽 · 뒤 왼쪽. 칼날 발톱을 옆으로 벌리고 도마뱀처럼 기어다님'],
      ['매달린 팔', '네 번째 다리(뒤 오른쪽)가 뒤틀리고 거대해져 등 위로 솟아 매달림. 끝에 손가락 3개 집게'],
      ['코어', '가슴 안의 금 간 용광로 코어가 빛남'],
    ];
    P.forEach(([h, b], i) => {
      const y = 1.75 + i * 0.86;
      chip(s, h, 7.2, y + 0.04, 1.3, 0.36, i === 3 ? C.rust : C.coal, 'FFFFFF', 11);
      text(s, b, 8.65, y, 4.08, 0.8, { size: 11.5 });
    });
    const y = 1.75 + 5 * 0.86 + 0.05;
    text(s, '페이즈별 발광', 7.2, y, 1.6, 0.36, { size: 12, bold: true, valign: 'middle' });
    chip(s, '1P 파랑 · 눈 · 코어 · 기둥', 8.65, y, 2.0, 0.36, C.blue, 'FFFFFF', 10);
    chip(s, '2P 빨강 · 강하게', 10.75, y, 1.98, 0.36, C.red, 'FFFFFF', 10);
  }

  // 4 크기 · 실루엣
  {
    const s = slide('03  크기 · 실루엣', '크기 · 실루엣', '낮게 기는 긴 몸 위로 팔이 높이 솟는다 — 멀리서도 팔 실루엣으로 보스를 알아본다');
    const m = 0.6, gy = 6.25, bx = 1.2; // 1m = 0.6in, 바닥선 y
    card(s, X0, 1.72, 8.25, 4.95, { fill: 'F7F5F2', flat: true });
    // 눈금(1m)
    for (let i = 0; i <= 7; i++) { line(s, bx - 0.25, gy - i * m, bx - 0.15, gy - i * m, C.ash, 0.75); text(s, `${i}`, bx - 0.55, gy - i * m - 0.1, 0.25, 0.2, { size: 8, color: C.muted, align: 'right' }); }
    text(s, 'm', bx - 0.55, gy - 7 * m - 0.35, 0.3, 0.2, { size: 8, color: C.muted, align: 'right' });
    line(s, X0 + 0.15, gy, X0 + 8.1, gy, C.ink, 1.5);
    // 꼬리 3m (사슬)
    const tx0 = bx, tx1 = bx + 3 * m;
    for (let i = 0; i < 6; i++) s.addShape(pres.shapes.OVAL, { x: tx0 + i * (3 * m / 6), y: gy - 0.55 - i * 0.05, w: 3 * m / 6, h: 0.18, fill: { color: 'FFFFFF', transparency: 100 }, line: { color: C.ash, width: 1.5 } });
    // 몸 7m · 어깨 2.2m
    const b0 = tx1, b1 = tx1 + 7 * m, sh = 2.2 * m;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: b0, y: gy - sh, w: 7 * m - 0.6, h: sh * 0.62, rectRadius: 0.2, fill: { color: '6B6259' }, line: { color: C.coal, width: 1 } });
    s.addShape(pres.shapes.TRAPEZOID, { x: b1 - 0.95, y: gy - sh + 0.05, w: 0.95, h: 0.55, fill: { color: '5A524A' }, line: { color: C.coal, width: 1 }, rotate: 90 });
    s.addShape(pres.shapes.OVAL, { x: b1 - 0.55, y: gy - sh + 0.18, w: 0.12, h: 0.12, fill: { color: C.blue }, line: { color: C.blue } });
    s.addShape(pres.shapes.OVAL, { x: b0 + 7 * m * 0.62, y: gy - sh + 0.28, w: 0.32, h: 0.32, fill: { color: C.blue }, line: { color: C.blue } });
    [[b0 + 0.6, 'L'], [b1 - 1.6, 'L'], [b1 - 1.1, 'R']].forEach(([x]) => { line(s, x, gy - sh * 0.45, x - 0.25, gy - 0.35, C.coal, 3); line(s, x - 0.25, gy - 0.35, x + 0.05, gy, C.coal, 3); });
    // 매달린 팔: 엉덩이에서 위로 솟아 앞쪽으로 아치
    const hx = b0 + 0.9, hy = gy - sh + 0.1, ex = hx + 0.55, ey = gy - 5.6 * m, wx = hx + 2.2, wy = gy - 6.5 * m;
    line(s, hx, hy, ex, ey, C.rust, 9); line(s, ex, ey, wx, wy, C.rust, 7);
    [-0.25, 0, 0.25].forEach((d) => line(s, wx, wy, wx + 0.4, wy + 0.35 + d, C.rust, 3));
    // 치수
    arrow(s, b0, gy + 0.22, b1, gy + 0.22, C.ink, 1, true); text(s, '몸길이 약 7m', b0, gy + 0.26, 7 * m, 0.25, { size: 10.5, bold: true, align: 'center' });
    arrow(s, tx0, gy + 0.22, tx1, gy + 0.22, C.ash, 1, true); text(s, '꼬리 3m (별도)', tx0, gy + 0.26, 3 * m, 0.25, { size: 10, color: C.muted, align: 'center' });
    line(s, b1 - 0.4, gy - sh, b1 + 0.25, gy - sh, C.ink, 0.75, true);
    arrow(s, b1 + 0.15, gy, b1 + 0.15, gy - sh, C.ink, 1, true); text(s, '어깨 약 2.2m (기어갈 때)', b1 + 0.22, gy - sh - 0.5, 1.2, 0.45, { size: 10, bold: true });
    line(s, wx - 0.1, wy, b1 + 1.4, wy, C.rust, 0.75, true);
    arrow(s, b1 + 1.3, gy, b1 + 1.3, wy, C.rust, 1, true); text(s, '팔을 들면 6~7m', b1 + 0.25, wy + 0.1, 1.0, 0.45, { size: 10, bold: true, color: C.rust, align: 'right' });
    // 플레이어 (크기 어림)
    const px = b1 + 0.75, ph = 0.75;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: px - 0.13, y: gy - ph, w: 0.26, h: ph, rectRadius: 0.08, fill: { color: 'C9B48A' }, line: { color: C.ink, width: 0.75 } });
    s.addShape(pres.shapes.OVAL, { x: px - 0.06, y: gy - ph + 0.06, w: 0.12, h: 0.08, fill: { color: C.soul }, line: { color: C.soul } });
    text(s, '플레이어', px - 0.5, gy - ph - 0.3, 1.0, 0.25, { size: 9.5, bold: true, align: 'center' });
    text(s, '옆에서 본 도식 · 눈금 1m · 플레이어 키는 어림', X0 + 3.2, 1.82, 4.9, 0.28, { size: 10, color: C.muted, align: 'right' });
    table(s, [
      ['항목', '값'],
      ['몸길이', '약 7m (꼬리 3m 별도)'],
      ['어깨 높이', '약 2.2m (기어갈 때)'],
      ['팔을 든 높이', '6~7m'],
      ['모델 아치 높이', '약 6.4m (Codex 12 결과)'],
      ['모델 판정', '반지름 2.4 · 높이 3.2 (Codex 12 결과)'],
      ['플레이어와 비율', { text: '검토 필요', color: C.grudge, bold: true }],
    ], 9.1, 1.72, 3.63, [1.5, 2.13], { rowH: 0.5, size: 11 });
    text(s, '기둥 사이 간격은 범위기(회오리 · 칼날 폭풍, 반경 약 4m)가 두 개를 한꺼번에 치지 못할 만큼 넓게. 보스 지대 반지름 42m', 9.1, 5.4, 3.63, 1.2, { size: 11, color: C.ink });
  }

  // 5 전투 흐름
  {
    const s = slide('04  전투 흐름', '전투 흐름', '쓰러진 채 기다리다 기둥의 힘으로 깨어나고, 한 번 쓰러진 뒤 붉게 폭주해 다시 일어난다');
    const F = [
      ['진입', '보스 지대에 들어섬', C.ash], ['깨어남', '안개 벽 · 기둥이 솟고 파랗게 빛남', C.soul], ['1페이즈', 'HP 900 · 파란 기둥 3개', C.blue], ['쓰러짐', '1페이즈 HP 0', C.coal],
      ['폭주', '기둥 · 눈이 빨갛게 강하게 빛남', C.grudge], ['2페이즈', 'HP 540 (60%) · 빨간 기둥 4개', C.red], ['처치', '2페이즈 HP 0', C.coal], ['정화', '안개 · 색 · 풀 · 생물 회복', C.soul],
    ];
    const bw = 2.7, bh = 1.3, gx = (CW - 4 * bw) / 3;
    F.forEach(([h, b, c], i) => {
      const r = Math.floor(i / 4), k = i % 4, col = r === 0 ? k : 3 - k, x = X0 + col * (bw + gx), y = 1.95 + r * 2.05;
      card(s, x, y, bw, bh, { fill: c, flat: true });
      text(s, `${i + 1}  ${h}`, x + 0.2, y + 0.14, bw - 0.4, 0.42, { size: 17, bold: true, color: 'FFFFFF' });
      text(s, b, x + 0.2, y + 0.62, bw - 0.4, 0.6, { size: 11.5, color: 'FFFFFF' });
      if (i === 3) arrow(s, x + bw / 2, y + bh + 0.05, x + bw / 2, y + 2.0, C.ash, 2.25);
      else if (i < 7) { const dir = r === 0 ? 1 : -1, ax = r === 0 ? x + bw + 0.05 : x - 0.05; arrow(s, ax, y + bh / 2, ax + dir * (gx - 0.1), y + bh / 2, C.ash, 2.25); }
    });
    card(s, X0, 5.6, CW, 1.1, { fill: 'F7F5F2' });
    text(s, bullets([
      '1페이즈 · 2페이즈 모두: 일반 공격 5초 간격 + 에너지 기둥 기믹 30초마다. 기믹 중에도 보스는 계속 공격',
      '처치 뒤 정화 · 로프 팔 · 첫 표정 해금이 이어짐. 원래 기획의 "뜯기"는 Z0에서 쓰지 않음',
    ]), X0 + 0.3, 5.72, CW - 0.6, 0.9, { size: 12, ls: 1.35 });
  }

  // 6 깨어남 연출
  {
    const s = slide('05  깨어남 연출', '깨어남 — 등장 연출 스토리보드', '처음엔 기능이 멈춘 채 쓰러져 있다. 들어서는 순간 문이 닫히고, 기둥의 빛이 보스를 깨운다');
    const SB = [
      ['쓰러져 있음', '공격하지 않고 HP 바도 없음', 'boss_dormant.png'],
      ['안개 벽', '안으로 들어서면 정문이 영혼 안개 벽으로 닫힘', null],
      ['기둥이 솟음', '기둥이 땅에서 솟아 하나씩 순서대로 파랗게 빛남. 빛이 보스로 흘러듦', 'boss_wake.png'],
      ['일어남', '몸을 일으키고 눈이 켜짐. 보스 이름 + 화면 아래 HP 바', 'boss_awake.png'],
    ];
    const cw = (CW - 0.6) / 4, ih = 2.75;
    for (let i = 0; i < SB.length; i++) {
      const [h, b, f] = SB[i], x = X0 + i * (cw + 0.2), y = 1.8;
      card(s, x, y, cw, ih + 1.65);
      if (f && has(f)) await cimg(s, f, x + 0.12, y + 0.12, cw - 0.24, ih - 0.14, { focusY: 0.5 });
      else {
        s.addShape(pres.shapes.RECTANGLE, { x: x + 0.12, y: y + 0.12, w: cw - 0.24, h: ih - 0.14, fill: { color: 'CFC9C0' }, line: { color: 'B9B2A8', dashType: 'dash' } });
        text(s, f ? `이미지 자리: ${f}` : '캡처 없음 · 연출 검토 필요', x + 0.12, y + 0.12, cw - 0.24, ih - 0.14, { size: 10.5, color: C.muted, align: 'center', valign: 'middle' });
      }
      badge(s, i + 1, x + 0.18, y + ih + 0.12, 0.42, i === 3 ? C.blue : C.rust);
      text(s, h, x + 0.72, y + ih + 0.12, cw - 0.85, 0.42, { size: 15, bold: true, valign: 'middle' });
      text(s, b, x + 0.18, y + ih + 0.65, cw - 0.36, 1.3, { size: 11.5, color: C.muted });
      if (i < 3) arrow(s, x + cw + 0.02, y + 2.3, x + cw + 0.18, y + 2.3, C.ash, 2);
    }
    caption(s, '1 · 3 · 4 = Unity 실제 캡처(세션 boss-pillars, 2026-11-07). 2 안개 벽은 캡처 없음', X0, 6.4, CW);
  }

  // 7 메인 기믹 — 에너지 기둥
  {
    const s = slide('06  메인 기믹', '메인 기믹 — 에너지 기둥 (30초마다)', '기둥이 하나씩 켜지며 순서를 보여 준다. 1페이즈는 켜진 순서대로, 2페이즈는 반대로 부순다');
    table(s, [
      ['', '1페이즈', '2페이즈'],
      ['기둥 수', '3', '4'],
      ['색', { text: '파랑', color: C.blue, bold: true }, { text: '빨강', color: C.red, bold: true }],
      ['부수는 순서', { text: '켜진 순서대로', bold: true }, { text: '켜진 순서의 반대로', bold: true }],
      ['내구도', '3타', '3타'],
      ['주기 · 제한', '30초마다 · 12초', '30초마다 · 12초'],
    ], X0, 1.75, 4.6, [1.5, 1.55, 1.55], { rowH: 0.52, size: 12, center: true });
    text(s, bullets(['기둥은 땅에 고정', '생성 → 하나씩 불이 켜짐 → 다 보여 준 뒤부터 12초', '기믹 중에도 보스는 계속 공격']), X0, 5.0, 4.6, 1.2, { size: 11.5, ls: 1.35 });
    // 순서 도식
    const dx = 5.55, dw = W - X0 - dx;
    const row = (y, color, n, label, rev) => {
      card(s, dx, y, dw, 2.35, { fill: 'F7F5F2', flat: true });
      chip(s, label, dx + 0.2, y + 0.15, 1.5, 0.34, color, 'FFFFFF', 11);
      text(s, '켜지는 순서', dx + 0.2, y + 0.75, 1.4, 0.3, { size: 11, bold: true, color: C.muted });
      text(s, '부수는 순서', dx + 0.2, y + 1.6, 1.4, 0.3, { size: 11, bold: true, color: C.ink });
      const span = (dw - 2.2) / n;
      for (let i = 0; i < n; i++) {
        const cx = dx + 1.95 + span * (i + 0.5);
        pillar(s, cx, y + 0.15, color, i + 1);
        const k = rev ? n - i : i + 1;
        badge(s, k, cx - 0.21, y + 1.5, 0.42, C.coal);
        if (i < n - 1) arrow(s, cx + 0.35, y + 0.65, cx + span - 0.35, y + 0.65, color, 1.5);
      }
      text(s, rev ? '마지막에 켜진 기둥부터 ← 거꾸로' : '처음 켜진 기둥부터 → 그대로', dx + 1.95, y + 2.0, dw - 2.1, 0.28, { size: 10.5, bold: true, color, align: 'center' });
    };
    row(1.75, C.blue, 3, '1페이즈', false);
    row(4.25, C.red, 4, '2페이즈', true);
    text(s, '기둥 위 숫자 = 켜지는 순서(설명용). 실제 화면에는 숫자 · 글자 UI 없이 빛으로만 보여 줌', dx, 6.65, dw, 0.3, { size: 10, color: C.muted });
  }

  // 8 성공 · 실패 규칙
  {
    const s = slide('07  기둥 규칙', '성공 · 실패 규칙', '공격을 피하면서 순서대로 부수는 것이 핵심 긴장. 실패하면 남은 기둥만큼 보스가 회복한다');
    // 타임라인
    const T = [['생성', '기둥이 빛나며 땅에서 솟음', 2.6, C.ash], ['순서 보여 주기', '하나씩 불이 켜짐', 3.0, C.coal], ['12초 제한', '정해진 순서로 3타씩 부숨', 6.13, C.rust]];
    let tx = X0;
    T.forEach(([h, b, w, c], i) => {
      s.addShape(pres.shapes.CHEVRON, { x: tx, y: 1.8, w, h: 0.75, fill: { color: c }, line: { color: c } });
      text(s, h, tx + 0.5, 1.8, w - 0.8, 0.42, { size: 14, bold: true, color: 'FFFFFF', valign: 'middle' });
      text(s, b, tx + 0.5, 2.17, w - 0.8, 0.32, { size: 10.5, color: 'FFFFFF' });
      tx += w + 0.05;
    });
    // 성공 / 실패
    card(s, X0, 2.85, 5.9, 1.55);
    s.addShape(pres.shapes.RECTANGLE, { x: X0, y: 2.85, w: 0.12, h: 1.55, fill: { color: C.soul }, line: { color: C.soul } });
    text(s, '성공', X0 + 0.35, 2.95, 2, 0.4, { size: 16, bold: true, color: C.soul });
    text(s, bullets(['순서대로 모두 부숨', '보스가 회복하지 않음', '보상: 보스가 잠깐 경직']), X0 + 0.35, 3.38, 5.4, 0.95, { size: 12, ls: 1.3 });
    card(s, X0 + 6.23, 2.85, 5.9, 1.55);
    s.addShape(pres.shapes.RECTANGLE, { x: X0 + 6.23, y: 2.85, w: 0.12, h: 1.55, fill: { color: C.grudge }, line: { color: C.grudge } });
    text(s, '실패', X0 + 6.58, 2.95, 2, 0.4, { size: 16, bold: true, color: C.grudge });
    text(s, bullets(['틀린 기둥을 치면 즉시 실패 · 시간이 다 되면 그때 실패', '남은 기둥 1개당 보스 최대 HP의 8% 회복', '남은 기둥에서 보스로 에너지가 빨려 드는 연출']), X0 + 6.58, 3.38, 5.4, 0.95, { size: 12, ls: 1.3 });
    // 회복 예시 계산
    text(s, '실패 시 회복량 예시 — 최대 HP 900 기준, 기둥 1개 = 8% = 72', X0, 4.62, CW, 0.34, { size: 13, bold: true, color: C.rust });
    table(s, [
      ['남은 기둥', '0개', '1개', '2개', '3개', '4개'],
      ['1페이즈 (3개 중)', '0', '8% · 72', '16% · 144', { text: '24% · 216', bold: true }, '—'],
      ['2페이즈 (4개 중)', '0', '8% · 72', '16% · 144', '24% · 216', { text: '32% · 288', bold: true }],
    ], X0, 5.0, CW, [2.53, 1.92, 1.92, 1.92, 1.92, 1.92], { rowH: 0.46, size: 12, center: true });
    text(s, '※ 2페이즈 시작 HP는 540(60%). 2페이즈에서 4개가 다 남아 실패하면 288 회복 — 2페이즈 체력의 절반 이상. 회복 상한(최대 HP 넘김 여부)은 검토 필요', X0, 6.42, CW, 0.5, { size: 11, bold: true, color: C.grudge });
  }

  // 9 일반 공격 AI
  {
    const s = slide('08  일반 공격 AI', '일반 공격 AI — 거리로 고른다', '5초에 한 번(전역). 모든 공격은 바닥의 빨간 예고 범위가 먼저 차오르고, 다 차는 순간 판정');
    // 결정 트리
    card(s, X0 + 2.6, 1.8, 2.6, 0.7, { fill: C.coal, flat: true });
    text(s, '5초마다 공격 선택', X0 + 2.6, 1.8, 2.6, 0.7, { size: 14, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    s.addShape(pres.shapes.DIAMOND, { x: X0 + 2.75, y: 2.75, w: 2.3, h: 1.0, fill: { color: C.card }, line: { color: C.ink, width: 1.25 } });
    text(s, '플레이어와\n거리', X0 + 2.75, 2.75, 2.3, 1.0, { size: 12, bold: true, align: 'center', valign: 'middle' });
    arrow(s, X0 + 3.9, 2.5, X0 + 3.9, 2.75, C.ash, 2);
    // 근접 (왼쪽)
    line(s, X0 + 2.75, 3.25, X0 + 1.35, 3.25, C.rust, 2); arrow(s, X0 + 1.35, 3.25, X0 + 1.35, 4.05, C.rust, 2);
    chip(s, '8m 이내', X0 + 1.45, 2.9, 1.2, 0.32, C.rust, 'FFFFFF', 10.5);
    card(s, X0, 4.05, 2.7, 2.55);
    text(s, '근접 — 거리에 맞게', X0 + 0.18, 4.15, 2.4, 0.36, { size: 13, bold: true, color: C.rust });
    [['발톱 할퀴기', '앞 부채꼴'], ['물기', '짧은 부채꼴'], ['몸통 · 꼬리 휩쓸기', '넓은 부채꼴']].forEach(([n, d], i) => {
      text(s, n, X0 + 0.18, 4.6 + i * 0.62, 2.4, 0.3, { size: 12, bold: true });
      text(s, d, X0 + 0.18, 4.88 + i * 0.62, 2.4, 0.26, { size: 10.5, color: C.muted });
    });
    // 원거리 (오른쪽)
    line(s, X0 + 5.05, 3.25, X0 + 6.6, 3.25, C.blue, 2); arrow(s, X0 + 6.6, 3.25, X0 + 6.6, 3.55, C.blue, 2);
    chip(s, '8m 초과', X0 + 5.2, 2.9, 1.2, 0.32, C.blue, 'FFFFFF', 10.5);
    s.addShape(pres.shapes.OVAL, { x: X0 + 6.15, y: 3.55, w: 0.9, h: 0.5, fill: { color: C.card }, line: { color: C.ink, width: 1 } });
    text(s, '50 : 50', X0 + 6.15, 3.55, 0.9, 0.5, { size: 11, bold: true, align: 'center', valign: 'middle' });
    arrow(s, X0 + 6.3, 4.02, X0 + 4.75, 4.4, C.ash, 1.75); arrow(s, X0 + 6.9, 4.02, X0 + 7.75, 4.4, C.ash, 1.75);
    card(s, X0 + 3.1, 4.4, 3.05, 2.2);
    text(s, '원거리 — 고철 던지기', X0 + 3.28, 4.5, 2.8, 0.36, { size: 13, bold: true, color: C.blue });
    text(s, '매달린 거대 팔로 근처의 고철 덩어리를 집어 플레이어 쪽으로 던짐. 착탄 지점에 원형 예고, 날아가는 고철이 보임', X0 + 3.28, 4.92, 2.75, 1.6, { size: 11 });
    card(s, X0 + 6.35, 4.4, 3.05, 2.2);
    text(s, '돌진 — 굴러서 돌진', X0 + 6.53, 4.5, 2.8, 0.36, { size: 13, bold: true, color: C.blue });
    text(s, '몸을 말아 바퀴처럼 굴러 직선으로 돌진. 긴 직선 예고가 먼저 뜸', X0 + 6.53, 4.92, 2.75, 1.6, { size: 11 });
    // 거리 원 (탑뷰)
    const cx = 11.45, cy = 3.45, r8 = 0.8;
    s.addShape(pres.shapes.OVAL, { x: cx - r8 * 1.6, y: cy - r8 * 1.6, w: r8 * 3.2, h: r8 * 3.2, fill: { color: C.blue, transparency: 88 }, line: { color: C.blue, width: 1, dashType: 'dash' } });
    s.addShape(pres.shapes.OVAL, { x: cx - r8, y: cy - r8, w: 2 * r8, h: 2 * r8, fill: { color: C.rust, transparency: 80 }, line: { color: C.rust, width: 1.5 } });
    s.addShape(pres.shapes.OVAL, { x: cx - 0.18, y: cy - 0.18, w: 0.36, h: 0.36, fill: { color: C.coal }, line: { color: C.coal } });
    arrow(s, cx, cy, cx + r8, cy, C.rust, 1.25); text(s, '8m', cx + 0.2, cy - 0.3, 0.6, 0.25, { size: 10.5, bold: true, color: C.rust });
    text(s, '근접', cx - 0.5, cy + 0.3, 1.0, 0.25, { size: 10, bold: true, color: C.rust, align: 'center' });
    text(s, '원거리 / 돌진', cx - 0.8, cy + r8 + 0.06, 1.6, 0.25, { size: 10, bold: true, color: C.blue, align: 'center' });
    text(s, '탑뷰 · 보스 중심', cx - 1.0, 4.82, 2.0, 0.25, { size: 10, color: C.muted, align: 'center' });
    text(s, '기준 거리 8m는 상수 — 플레이 검토 후 조정', 10.15, 5.2, 2.6, 0.6, { size: 11, bold: true, color: C.grudge });
    text(s, '근접 3종의 고르는 규칙(거리별 우선순위)은 검토 필요', 10.15, 5.85, 2.6, 0.6, { size: 11, color: C.muted });
  }

  // 10 수치 요약
  {
    const s = slide('09  수치', '수치 요약 — 첫 구현 값', '숫자는 첫 구현 값. 플레이 검토 후 조정한다');
    table(s, [
      ['항목', '값', '비고'],
      ['최대 HP', '900', '1페이즈 시작'],
      ['2페이즈 시작 HP', '60% (540)', '쓰러진 뒤 붉게 폭주하며 다시 일어남'],
      ['일반 공격 간격', '5초', '전역 · 기믹 중에도 유지'],
      ['근접 / 원거리 기준 거리', '8m', '상수 · 조정 예정'],
      ['원거리 : 돌진 (멀 때)', '50 : 50', '고철 던지기 : 굴러서 돌진'],
      ['기둥 기믹 주기', '30초', '1 · 2페이즈 공통'],
      ['기둥 수 (1P / 2P)', '3 / 4', '파랑 · 켜진 순서 / 빨강 · 반대 순서'],
      ['기둥 내구도', '3타', ''],
      ['순서 제한 시간', '12초', '순서를 다 보여 준 뒤부터'],
      ['실패 시 회복', '남은 기둥당 최대 HP의 8%', '1P 최대 24% (216) · 2P 최대 32% (288)'],
    ], X0, 1.6, 8.3, [2.6, 2.6, 3.1], { rowH: 0.46, size: 12 });
    card(s, 9.2, 1.6, 3.53, 5.06, { fill: C.coal, flat: true });
    text(s, '정해지지 않은 값', 9.45, 1.75, 3.1, 0.36, { size: 14, bold: true, color: 'F2C27A' });
    text(s, bullets([
      '보스 공격 피해량',
      '예고 범위가 차오르는 시간 (특히 고철 던지기 착탄)',
      '근접 3종의 사거리 · 각도',
      '성공 보상 경직 시간',
      '회복이 최대 HP를 넘을 수 있는지',
      '기둥 배치 좌표 · 간격',
    ]), 9.45, 2.2, 3.1, 3.4, { size: 11.5, color: 'FFFFFF', ls: 1.4 });
    chip(s, '검토 필요', 9.45, 6.05, 1.3, 0.34, C.grudge, 'FFFFFF', 10.5);
  }

  // 11 플레이 검토 포인트
  {
    const s = slide('10  플레이 검토', '플레이 검토 때 볼 것', '숫자보다 "읽히는가 · 피할 수 있는가"를 먼저 본다');
    const Q = [
      ['공격 템포', '5초 간격이 느리게 느껴지는지(소울라이크 보스치고 여유가 많음). 기둥 기믹과 겹칠 때 압박은 적당한지'],
      ['기둥 시간', '12초 안에 3타 × 3~4개를 부술 수 있는지. 보스 지대(반지름 42m) 안의 기둥 사이 이동 거리와 함께'],
      ['반대 순서 가독성', '2페이즈 "반대 순서"가 화면에서 바로 읽히는지. 빨간 기둥의 켜지는 순서를 숫자 · 글자 UI 없이 보여 줘야 함'],
      ['회피', '굴러서 돌진을 대쉬 무적으로 피할 수 있는지. 고철 던지기 착탄 예고 시간'],
      ['이름', '"재의 감시자"가 새 외형(고철 짐승)에 맞는지. 이름을 바꿀지는 사용자 결정'],
    ];
    Q.forEach(([h, b], i) => {
      const y = 1.75 + i * 0.98;
      card(s, X0, y, CW, 0.84);
      badge(s, i + 1, X0 + 0.25, y + 0.21, 0.42);
      text(s, h, X0 + 0.9, y, 2.6, 0.84, { size: 15, bold: true, valign: 'middle' });
      text(s, b, X0 + 3.5, y, CW - 5.2, 0.84, { size: 12, color: C.muted, valign: 'middle' });
      chip(s, i === 4 ? '사용자 결정' : '검토 필요', X0 + CW - 1.55, y + 0.25, 1.35, 0.34, i === 4 ? C.rust : C.grudge, 'FFFFFF', 10.5);
    });
  }

  // 12 현재 구현 · 구현 위치
  {
    const s = slide('11  구현', '현재 구현 · 구현 위치', 'Unity 6 프로토 실제 화면(2026-11-07) — 보스 로직 · 연출은 세션 boss-pillars에서 진행 중');
    const iw = 2.4, ih = iw * 9 / 16;
    const caps = [['boss_dormant.png', '쓰러짐'], ['boss_wake.png', '기둥이 켜지며 깨어남'], ['boss_awake.png', '일어남']];
    for (let i = 0; i < caps.length; i++) {
      const y = 1.72 + i * (ih + 0.36);
      await cimg(s, caps[i][0], X0, y, iw, ih, { shadow: true });
      caption(s, `${caps[i][1]} · ${caps[i][0]}`, X0, y + ih + 0.04, iw + 0.3);
    }    table(s, [
      ['내용', '파일', '상태'],
      ['보스 타입 · AI 상태 (쓰러짐 · 깨어남 · 페이즈 · 공격 선택)', 'Runtime/Combat/Enemies.cs', '진행 중'],
      ['전투 흐름 (안개 벽 · HP 바 · 페이즈 전환 · 정화)', 'Runtime/Game/SoulGame.Zone.cs', '진행 중'],
      ['기둥 기믹', 'Runtime/Game/BossPillars.cs\n임시 모델 World/BossPillarProps.cs', '진행 중 · 기둥은 임시 모델'],
      ['보스 지대 · 기둥 배치 표', 'Runtime/World/ZoneState.cs', '진행 중'],
      ['보스 모델 (원본: 웹)', 'web-proto/src/enemy_models.js createAshWarden()\n→ SOUL/Import Web Export → Prefabs/AshWarden', '모델 완료(Codex 12) · 내보내기 검토 필요'],
      ['Codex 명세', 'unity/codex/11_boss_pillar.md (기둥)\nunity/codex/12_scrap_beast.md (보스)', '12 결과 있음 · 11 결과 검토 필요'],
    ], 3.35, 1.72, 9.38, [2.9, 4.0, 2.48], { rowH: 0.62, size: 10.5, leftFirst: true });
    text(s, '캡처는 unity/Captures/, Runtime 경로는 unity/Assets/SOUL/ 아래. 상태는 2026-11-07 작성 시점 기준이며, 확정은 boss-pillars 세션 결과로 갱신', 3.35, 6.5, 9.38, 0.4, { size: 10, color: C.muted });
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

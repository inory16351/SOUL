// ALONE(가제) 컨셉 기획서 PPT 생성 → 기획서/20261106_ALONE_컨셉기획서_v0.1_정승운.pptx
// 사용: NODE_PATH=<pptxgenjs · sharp · jszip 설치 폴더>/node_modules node 기획서/build_concept_ppt.js
// 그림: 기획서/art (ChatGPT 생성 컨셉 이미지 + Unity 실제 화면 캡처). 없는 그림은 회색 자리표시로 들어감
const fs = require('fs');
const path = require('path');
const pptxgen = require('pptxgenjs');
const sharp = require('sharp');

const ROOT = __dirname;
const ART = path.join(ROOT, 'art');
const VER = 'v0.1';
const DATE = '2026-11-06';
const OUT = path.join(ROOT, `20261106_ALONE_컨셉기획서_${VER}_정승운.pptx`);
const DOC = `ALONE(가제) · 컨셉 기획서 ${VER}`;

const FONT = 'Noto Sans KR';
const C = {
  coal: '16181B', deep: '0E1012', ink: '23272C', muted: '646B73', bg: 'EEEBE6', card: 'FFFFFF', line: 'D9D4CC', tint: 'E4DED5',
  rust: 'C2562B', ash: '8A8F96', soul: '33B5C8', grudge: 'B8322A', gold: 'E0A030',
  rope: 'E0A030', jet: '3F8FD8', cannon: 'D9502F', magnet: '8E5BC8', toxic: '4CAF50', base: '8A8F96',
};
const W = 13.333, H = 7.5, X0 = 0.6, CW = W - 2 * X0;

const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.author = '정승운';
pres.title = 'ALONE(가제) 컨셉 기획서';
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

// ---------- 도구 ----------
const dims = {};
const A = (f) => path.join(ART, f);
const has = (f) => fs.existsSync(A(f));
async function loadDims() { for (const f of fs.readdirSync(ART)) if (/\.(png|jpe?g)$/i.test(f)) { const m = await sharp(A(f)).metadata(); dims[f] = [m.width, m.height]; } }
const shadow = () => ({ type: 'outer', color: '000000', opacity: 0.16, blur: 6, offset: 2, angle: 90 });

function text(s, t, x, y, w, h, o = {}) {
  s.addText(t, { x, y, w, h, fontFace: FONT, fontSize: o.size || 13, color: o.color || C.ink, bold: !!o.bold, align: o.align || 'left', valign: o.valign || 'top', margin: 0, isTextBox: true, paraSpaceAfter: o.gap || 0, lineSpacingMultiple: o.ls || 1.15, fit: 'none' });
}
function card(s, x, y, w, h, o = {}) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: 0.1, fill: { color: o.fill || C.card }, line: { color: o.line || o.fill || C.card, width: 0.75 }, shadow: o.flat ? undefined : shadow() });
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
// 그림: 틀 안에 비율 유지(contain). 없으면 자리표시
function img(s, f, x, y, w, h, o = {}) {
  if (!has(f) || !dims[f]) {
    s.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: 'CFC9C0' }, line: { color: 'B9B2A8', dashType: 'dash' } });
    text(s, `이미지 자리: ${f}`, x, y, w, h, { size: 11, color: C.muted, align: 'center', valign: 'middle' });
    return;
  }
  const [iw, ih] = dims[f];
  if (o.cover) { // 틀을 꽉 채우도록 미리 자른 사본 사용
    s.addImage({ path: o.coverPath, x, y, w, h, shadow: o.shadow ? shadow() : undefined });
    return;
  }
  const r = Math.min(w / iw, h / ih), ww = iw * r, hh = ih * r;
  s.addImage({ path: A(f), x: x + (w - ww) / 2, y: y + (h - hh) / 2, w: ww, h: hh, shadow: o.shadow ? shadow() : undefined });
}
// cover 용: 비율에 맞춰 가운데 자른 사본을 art/_crop 에 만든다
async function coverCrop(f, w, h, focusY = 0.5) {
  if (!has(f)) return null;
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
    return { text: cell.text, options: { bold: j === 0 || cell.bold, color: cell.color || C.ink, fill: { color: cell.fill || (i % 2 ? 'F7F5F2' : C.card) }, align: j === 0 && !o.leftFirst ? 'center' : 'left' } };
  }));
  s.addTable([head, ...body], { x, y, w, colW, fontFace: FONT, fontSize: o.size || 12, valign: 'middle', border: { type: 'solid', color: C.line, pt: 0.75 }, rowH: o.rowH || 0.42, margin: [0.04, 0.1, 0.04, 0.1] });
}

// ---------- 슬라이드 ----------
async function build() {
  await loadDims();

  // 1 표지
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    await cimg(s, 'key_art.png', 0, 0, W, H, { focusY: 0.5 });
    // 하늘 쪽(위)에 제목. 로봇이 있는 아래쪽은 그림 그대로
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: 3.3, fill: { color: C.deep, transparency: 45 }, line: { color: C.deep, transparency: 100 } });
    text(s, 'CONCEPT DOCUMENT', 0, 0.45, W, 0.35, { size: 13, bold: true, color: C.soul, align: 'center' });
    text(s, 'ALONE', 0, 0.75, W, 1.2, { size: 72, bold: true, color: 'FFFFFF', align: 'center' });
    text(s, '(가제)', 0, 1.9, W, 0.35, { size: 14, color: 'D7D2CA', align: 'center' });
    text(s, '폐허가 된 지구에서 기억을 잃고 깨어난 로봇이 기계에 깃든 원한을 정화하는 탑뷰 소울라이크 탐험 액션', 0, 2.3, W, 0.4, { size: 15, color: 'F0ECE6', align: 'center' });
    text(s, `컨셉 기획서  ·  작성자 정승운  ·  작성일 ${DATE}  ·  버전 ${VER}`, 0, 2.78, W, 0.3, { size: 11, color: 'D7D2CA', align: 'center' });
  }

  // 2 목차
  {
    const s = slide('CONTENTS', '목차');
    const items = [
      ['기획 의도', '목표 · 레퍼런스 · 차별점'], ['원페이지 기획서', '장르 · 핵심 재미 · 한 줄 요약'], ['세계관 · 스토리', '멸망한 지구 · 원한 · 과학자'],
      ['캐릭터', '기억을 잃은 AGI 로봇'], ['비주얼 가이드', '도형 폴리곤 · 폐허 색감 · 정화 연출'], ['조작 · 전투', '로스트아크식 조작 · 텔레그래프'],
      ['게임 루프', '탐험 · 정화 · 충전 포드 · 새 길'], ['월드 구조', '존 8개 · 3갈래 · 존 연결도 초안'], ['이동 파츠', '화살표 색 = 게이트 종류'],
      ['성장 · 재화', '부위별 트리 · 재화 2종 · 상인'], ['소울 루프', '부품 드롭 · 회수 · 체크포인트'], ['적 · 보스 · 생물', '파훼 + 회피 · 바이오메카닉'],
      ['제작 파이프라인', '웹 프로토 → Unity 6'], ['다음 단계', '연결도 확정 · 로프 팔 · 첫 보스'],
    ];
    const cols = 2, rowsN = 7, cw = (CW - 0.4) / cols, rh = 0.72;
    items.forEach(([h, d], i) => {
      const c = Math.floor(i / rowsN), r = i % rowsN, x = X0 + c * (cw + 0.4), y = 1.55 + r * rh;
      text(s, String(i + 1).padStart(2, '0'), x, y, 0.7, 0.5, { size: 22, bold: true, color: C.rust, valign: 'middle' });
      text(s, h, x + 0.8, y + 0.02, 2.6, 0.5, { size: 16, bold: true, valign: 'middle' });
      text(s, d, x + 3.2, y + 0.02, cw - 3.2, 0.5, { size: 12, color: C.muted, valign: 'middle' });
      s.addShape(pres.shapes.LINE, { x, y: y + rh - 0.08, w: cw, h: 0, line: { color: C.line, width: 0.75 } });
    });
  }

  // 3 기획 의도
  {
    const s = slide('01  기획 의도', '기획 의도', '귀여운 로봇 한 대와 증오로 뭉친 기계 덩어리의 대비 — 혼자 남은 존재가 세상을 정화해 나가는 여정');
    const pts = [
      ['감성', '도형 폴리곤의 간결하고 귀여운 3D로 AI 티를 줄이고, 폐허의 쓸쓸함 속 작은 로봇의 동심을 전달'],
      ['탐험', '엘든링식 청크와 보스 지대 + 점프로 고저차를 만든 탑뷰 오픈월드. 갈 수 없던 곳이 파츠로 열리는 메트로베니아 감각'],
      ['전투', '로스트아크식 조작의 손맛 + 패턴을 읽고 피하는 소울라이크 긴장감. 보스는 이동 스킬 조합으로 기믹 파훼'],
      ['성장', '쓰러뜨린 기계의 부품으로 몸을 바꾼다. 새 팔·다리·코어가 곧 새 이동 방식이자 새 스킬'],
    ];
    pts.forEach(([h, b], i) => {
      const y = 1.8 + i * 1.18;
      badge(s, i + 1, X0, y, 0.46);
      text(s, h, X0 + 0.65, y - 0.02, 3, 0.4, { size: 16, bold: true });
      text(s, b, X0 + 0.65, y + 0.38, 5.6, 0.7, { size: 12, color: C.muted });
    });
    await cimg(s, 'game_1.png', 7.15, 1.8, 5.58, 3.14, { shadow: true, focusY: 0.45 });
    caption(s, 'Unity 6 프로토 실제 화면 — 잿빛 폐허', 7.15, 5.0, 5.5);
    text(s, '레퍼런스: 엘든링(오픈월드·보스 지대) · 오리와 눈먼 숲 / 할로우 나이트(능력 잠금 탐험) · 산나비(로프 팔) · 로스트아크(조작·파훼) · 나인(분위기)', 7.15, 5.45, 5.58, 0.9, { size: 11, color: C.ink });
  }

  // 4 원페이지 기획서
  {
    const s = slide('02  원페이지 기획서', '원페이지 기획서');
    table(s, [
      ['항목', '내용'],
      ['장르', '탑뷰 소울라이크 액션 + 메트로베니아식 오픈월드 탐험'],
      ['플랫폼 / 엔진', 'PC (Windows) / 웹 프로토(Three.js) → Unity 6 URP 본편'],
      ['시점 / 조작', '탑뷰(약 55°) · 로스트아크식: 우클릭 이동, A 공격, QWER 스킬, Space 회피, F 점프'],
      ['핵심 재미', '패턴을 읽고 피하는 묵직한 근접 전투 + 파츠로 길을 여는 탐험'],
      ['주인공', '전쟁으로 모든 걸 잃은 천재 과학자가 남긴 AGI 로봇. 오른팔 전완이 곧 칼날'],
      ['목표', '증오에 물든 전쟁 기계를 정화하며 8개 존을 지나 과학자의 연구소에 닿기'],
      ['성장', '기본 부품 = 경험치로 부위별 트리 노드 해금, 핵심 구동부 파츠로 이동 스킬 해금'],
      ['차별점', '도형 폴리곤 + 바이오메카닉 보스, 몸 부위가 곧 스킬트리, 붉은 혼이 푸르게 흩어지는 정화 연출'],
    ], X0, 1.45, CW, [2.3, CW - 2.3], { rowH: 0.5, size: 13 });
    card(s, X0, 6.2, CW, 0.62, { fill: C.coal, flat: true });
    text(s, '한 줄 요약 — 혼자 남은 작은 로봇이 쓰러뜨린 기계의 부품으로 몸을 바꿔 가며, 원한으로 뒤덮인 지구를 정화하는 여정', X0 + 0.3, 6.2, CW - 0.6, 0.62, { size: 14, bold: true, color: 'FFFFFF', valign: 'middle' });
  }

  // 5 세계관
  {
    const s = slide('03  세계관 · 스토리', '세계관 · 스토리', '인류의 욕심과 서로에 대한 증오가 지구를 태웠다. 남은 것은 원한이 깃든 전쟁 기계뿐');
    await cimg(s, 'world.png', X0, 1.75, 6.3, 3.55, { shadow: true });
    caption(s, '컨셉 이미지 — 원한이 깃든 전쟁 기계와 폐허 (ChatGPT 생성)', X0, 5.36, 6.3);
    const rows = [
      ['멸망', '인류의 욕심과 증오로 지구가 초토화. 문명은 폐허로'],
      ['원한', '남은 전쟁 기계에 인류의 원한이 뭉침. 에너지를 흡수하려고 살아남은 유기체를 모조리 학살 중'],
      ['과학자', '전쟁으로 모든 걸 잃은 천재 과학자가 평화와 동심을 그리며 비밀리에 AGI 로봇을 개발'],
      ['로봇', '과학자가 쏟아 넣은 영혼 때문에 기계들이 로봇을 유기체로 인식하고 추적·공격'],
      ['목적', '기계에 깃든 원한과 증오를 정화한다. 기억의 조각은 충전 포드의 과학자 음성 기록으로'],
    ];
    rows.forEach(([h, b], i) => {
      const y = 1.75 + i * 0.78;
      chip(s, h, 7.2, y + 0.04, 1.1, 0.36, i === 4 ? C.soul : C.coal, 'FFFFFF', 11);
      text(s, b, 8.45, y, 4.3, 0.72, { size: 12 });
    });
    text(s, '세계관 레퍼런스: 나인(애니메이션) · 공각기동대 · 아톰 — 분위기만 참고, 디자인은 오리지널', 7.2, 5.75, 5.5, 0.5, { size: 11, color: C.muted });
  }

  // 6 캐릭터
  {
    const s = slide('04  캐릭터', '캐릭터 — 기억을 잃은 방랑자 로봇', '프로토 코드명 Morrow-07. 폐허에서 깨어나 존재 이유를 찾아 떠돈다');
    img(s, 'character.png', X0, 1.75, 7.0, 3.94, { shadow: true });
    caption(s, '컨셉 이미지 — 캐릭터 시트 · 이동 파츠 5종 (ChatGPT 생성)', X0, 5.75, 7);
    text(s, '프로토 화면의 실제 모델은 같은 실루엣을 도형 폴리곤으로 코드 생성(Codex). 컨셉 이미지는 색 · 비율 · 파츠 형태 기준', X0, 6.1, 7, 0.6, { size: 11, color: C.ink });
    table(s, [
      ['항목', '내용'],
      ['외형', '듄 느낌의 허름한 사막 후드 로브. 오른쪽이 찢어져 기계 팔이 드러남'],
      ['얼굴', '검은 화면에 픽셀 LED 얼굴(0_0). 표정이 픽셀로 바뀜'],
      ['무기', '오른팔 전완이 곧 칼날(기본 제공). 손목 고정, 팔과 칼날이 일직선'],
      ['모션', '몸통이 이끄는 수평 베기, 팔을 뻗은 채 휘두름. 칼이 몸을 통과하지 않음'],
      ['성장 부위', '코어 / 오른팔(칼날) / 왼팔(도구) / 다리 / 머리 — 부위마다 트리'],
      ['장착 규칙', '이동 파츠는 왼팔·다리·등에 상시, 전투 파츠는 오른팔 칼날을 수납하고 전개'],
    ], 7.85, 1.75, 4.88, [1.15, 3.73], { rowH: 0.7, size: 11.5 });
  }

  // 7 비주얼 가이드
  {
    const s = slide('05  비주얼 가이드', '비주얼 가이드');
    await cimg(s, 'world.png', X0, 1.45, 4.0, 3.0, { shadow: true, focusY: 0.6 });
    await cimg(s, 'game_1.png', X0 + 4.15, 1.45, 4.0, 3.0, { shadow: true, focusY: 0.45 });
    await cimg(s, 'pod.png', X0 + 8.3, 1.45, 3.83, 3.0, { shadow: true });
    caption(s, '컨셉 — 폐허 분위기', X0, 4.5, 4); caption(s, '프로토 — 탑뷰 실제 화면', X0 + 4.15, 4.5, 4); caption(s, '컨셉 — 과학자의 충전 포드', X0 + 8.3, 4.5, 3.8);
    const pts = [
      ['1', '도형 폴리곤 구현', '알비온 · 어비스리움 · 마이오아시스는 "구현 방식" 레퍼런스. 박스·원기둥 위주의 간결한 모델로 귀여움 + 낮은 제작비'],
      ['2', '폐허 색감', '그을음 · 녹 · 회색 콘크리트 · 재. 강조색은 로봇의 시안(얼굴·칼날)과 원한의 붉은빛 두 가지만'],
      ['3', '정화 연출', '적을 쓰러뜨리면 붉은 혼이 푸른빛으로 바뀌며 흩어지고 부품이 떨어짐. 세계관 목표를 매 처치마다 보여줌'],
    ];
    pts.forEach(([n, h, b], i) => {
      const x = X0 + i * 4.15;
      badge(s, n, x, 4.95, 0.42);
      text(s, h, x + 0.55, 4.95, 3.4, 0.42, { size: 15, bold: true, valign: 'middle' });
      text(s, b, x, 5.47, 3.9, 1.0, { size: 12, color: C.muted });
    });
    text(s, '탑뷰 컷어웨이: 플레이어 머리 위 건물은 디더링으로 지워 폐건물 안에서도 캐릭터가 보이게 함', X0, 6.5, CW, 0.35, { size: 11, color: C.ink });
  }

  // 8 조작 · 전투
  {
    const s = slide('06  조작 · 전투', '조작 · 전투', '로스트아크식 조작 + 소울라이크 회피. 스태미나 없이 쿨다운이 긴장감을 만든다');
    const keys = [
      ['우클릭', '이동', '누르고 있으면 따라감'], ['A', '평타 3연타', '커서 방향, 몸이 이끄는 베기'], ['Q', '회오리', '주변 원형 베기'], ['W', '레이저 파동', '직선 관통'],
      ['E', '반격', '맞는 순간 패링 → 적 경직'], ['R', '칼날 폭풍', '무적 다단 히트'], ['SPACE', '대시', '무적 + 잔상'], ['F', '점프', '고저차 · 턱 오르기'],
    ];
    keys.forEach(([k, n, d], i) => {
      const c = i % 4, r = Math.floor(i / 4), x = X0 + c * 3.05, y = 1.8 + r * 1.45;
      card(s, x, y, 2.85, 1.25);
      chip(s, k, x + 0.2, y + 0.2, k.length > 2 ? 1.0 : 0.6, 0.42, C.coal, 'FFFFFF', 12);
      text(s, n, x + (k.length > 2 ? 1.35 : 0.95), y + 0.2, 1.8, 0.42, { size: 15, bold: true, valign: 'middle' });
      text(s, d, x + 0.2, y + 0.75, 2.5, 0.4, { size: 11, color: C.muted });
    });
    card(s, X0, 4.85, CW, 1.85, { fill: 'F7F5F2' });
    text(s, '전투 규칙', X0 + 0.3, 5.0, 3, 0.4, { size: 15, bold: true, color: C.rust });
    text(s, [
      '적 공격은 바닥의 빨간 예고 범위(텔레그래프)로 보여주고, 다 차는 순간 범위 안이면 피격 — 보고 피하는 소울라이크',
      '공격 판정은 눈에 보이는 이펙트보다 넉넉하게(쾌적함 우선). 대시·점프 공중·칼날 폭풍은 무적',
      'QWER은 추후 파츠 장착으로 확장 — 오른팔 전투 파츠(캐논 등)는 칼날을 수납한 뒤 전개되는 스킬형',
    ].map((t) => '•  ' + t).join('\n'), X0 + 0.3, 5.42, CW - 0.6, 1.2, { size: 12, ls: 1.35 });
  }

  // 9 게임 루프
  {
    const s = slide('07  게임 루프', '기본 게임 루프', '기계들을 처치하며 부품을 모으고 몸체를 강화해 갈 수 있는 범위를 넓힌다');
    const steps = [['탐험', '존 청크 · 고저차 · 숨은 길'], ['전투', '기계 · 원한 · 보스'], ['정화', '붉은 혼 → 푸른빛, 부품 드롭'], ['충전 포드', '휴식 · 저장 · 성장 · 빠른 이동'], ['파츠 해금', '보스 처치 → 핵심 구동부'], ['새 길', '화살표 색 게이트 돌파']];
    const cx = W / 2, cy = 4.25, rx = 4.3, ry = 1.95;
    const pos = steps.map((_, i) => { const a = -Math.PI / 2 + (i * 2 * Math.PI) / steps.length; return [cx + rx * Math.cos(a), cy + ry * Math.sin(a)]; });
    pos.forEach(([x, y], i) => {
      const [x2, y2] = pos[(i + 1) % pos.length];
      const dx = x2 - x, dy = y2 - y, L = Math.hypot(dx, dy), ux = dx / L, uy = dy / L;
      arrow(s, x + ux * 1.45, y + uy * 0.55, x2 - ux * 1.45, y2 - uy * 0.55, C.ash, 2);
    });
    pos.forEach(([x, y], i) => {
      card(s, x - 1.35, y - 0.5, 2.7, 1.0, { fill: i === 2 ? C.soul : i === 4 ? C.rust : C.card });
      const dark = i === 2 || i === 4;
      text(s, steps[i][0], x - 1.35, y - 0.42, 2.7, 0.45, { size: 16, bold: true, align: 'center', color: dark ? 'FFFFFF' : C.ink });
      text(s, steps[i][1], x - 1.35, y + 0.02, 2.7, 0.4, { size: 11, align: 'center', color: dark ? 'FFFFFF' : C.muted });
    });
    text(s, '↻', cx - 0.5, cy - 0.45, 1, 0.9, { size: 40, bold: true, color: C.rust, align: 'center', valign: 'middle' });
  }

  // 10 월드 구조 — 존 연결도 초안
  {
    const s = slide('08  월드 구조', '월드 구조 — 존 연결도 (초안)', '존 8개 · 존마다 보스 1 · 중앙에서 3갈래. 데바데 보드게임 맵처럼 덩어리를 잇고 화살표 색 = 필요한 이동 파츠');
    const Z = {
      0: [5.15, 3.85, '잿빛 폐허', '시작 · 첫 포드', '보스 → 로프 팔'],
      1: [2.1, 2.2, '무너진 도심', 'A 갈래 · 고저차', '보스 → 제트엔진'],
      2: [0.75, 4.2, '마천루 묘지', 'A 깊은 곳', '보스 → 연구소 열쇠'],
      3: [5.15, 1.75, '고철 협곡', 'B 갈래 · 잔해', '보스 → 캐논 포'],
      4: [8.0, 1.75, '전쟁 기계 공장', 'B 깊은 곳', '보스 → 내오염 코어'],
      5: [8.15, 3.85, '죽은 숲', 'C 갈래 · 생물', '보스 → 자석 다리'],
      6: [8.15, 5.65, '독성 늪', 'C 깊은 곳', '보스 → 연구소 열쇠'],
      7: [3.4, 5.65, '과학자의 연구소', '최심부', '최종 보스'],
    };
    const bw = 2.2, bh = 1.05;
    const mid = (k) => [Z[k][0] + bw / 2, Z[k][1] + bh / 2];
    const edge = (a, b, color, both = false) => {
      const [x1, y1] = mid(a), [x2, y2] = mid(b);
      const dx = x2 - x1, dy = y2 - y1;
      const t = Math.min(Math.abs(dx) > 0.001 ? (bw / 2 + 0.05) / Math.abs(dx) : 9, Math.abs(dy) > 0.001 ? (bh / 2 + 0.05) / Math.abs(dy) : 9);
      arrow(s, x1 + dx * t, y1 + dy * t, x2 - dx * t, y2 - dy * t, color, 2.75, both);
    };
    edge(0, 1, C.base, true); edge(0, 3, C.base, true); edge(0, 5, C.base, true);
    edge(1, 2, C.magnet); edge(3, 4, C.jet); edge(5, 6, C.toxic);
    edge(1, 3, C.cannon, true); edge(3, 5, C.rope, true); edge(4, 5, C.rope, true);
    edge(2, 7, C.rust); edge(6, 7, C.rust);
    Object.entries(Z).forEach(([k, [x, y, n, d, r]]) => {
      const hub = k === '0', last = k === '7';
      card(s, x, y, bw, bh, { fill: hub ? C.coal : last ? C.grudge : C.card, line: hub || last ? undefined : C.line });
      const fg = hub || last ? 'FFFFFF' : C.ink;
      text(s, `Z${k}  ${n}`, x + 0.12, y + 0.08, bw - 0.24, 0.34, { size: 12.5, bold: true, color: fg });
      text(s, d, x + 0.12, y + 0.42, bw - 0.24, 0.26, { size: 9.5, color: hub || last ? 'D7D2CA' : C.muted });
      text(s, r, x + 0.12, y + 0.7, bw - 0.24, 0.26, { size: 9.5, bold: true, color: hub || last ? 'F2C27A' : C.rust });
    });
    // 범례
    const lx = 10.75; let ly = 1.75;
    text(s, '화살표 색', lx, ly, 2, 0.3, { size: 12, bold: true }); ly += 0.38;
    [[C.base, '기본(걷기 · 점프)'], [C.rope, '로프 팔'], [C.jet, '제트엔진'], [C.cannon, '캐논 포'], [C.magnet, '자석 다리'], [C.toxic, '내오염 코어'], [C.rust, '연구소 열쇠 2개']].forEach(([c, t]) => {
      s.addShape(pres.shapes.LINE, { x: lx, y: ly + 0.14, w: 0.5, h: 0, line: { color: c, width: 3, endArrowType: 'triangle' } });
      text(s, t, lx + 0.62, ly, 1.9, 0.3, { size: 11, valign: 'middle' }); ly += 0.36;
    });
    text(s, '진행 규칙\n3갈래 중 어디든 먼저 갈 수 있음. 갈래 깊은 곳은 다른 갈래 보스의 파츠가 있어야 열림 (엘든링 + 할로우 나이트)', lx, ly + 0.15, 2.0, 2.2, { size: 10, color: C.muted, ls: 1.25 });
    text(s, '※ 에이전트 초안 — 존 이름 · 배치 · 게이트는 정승운 검토 후 확정', X0, 6.75, 8, 0.3, { size: 10, bold: true, color: C.grudge });
  }

  // 11 이동 파츠
  {
    const s = slide('09  이동 파츠', '이동 파츠 — 몸을 바꾸면 길이 열린다', '이동 스킬은 핵심 구동부 파츠를 얻으면 해금. 맵의 화살표 색이 곧 필요한 파츠');
    const P = [
      [C.base, '기본', '—', '걷기 턱 0.45m · F 점프 1.1m', '일반 연결'],
      [C.rope, '로프 팔', '왼팔', '산나비식. 앵커를 당겨 오르기 · 스윙으로 갭 건너기', '첫 핵심 이동 파츠'],
      [C.jet, '제트엔진', '등 · 다리', '공중 대시 · 이단 점프', '넓은 갭, 더 높은 턱'],
      [C.cannon, '캐논 포', '오른팔', '칼날을 수납한 뒤 전개하는 전투 겸 파괴 스킬', '금 간 벽 · 잔해 바리케이드'],
      [C.magnet, '자석 다리', '다리', '녹슨 금속 벽 · 천장을 따라 걷기', '수직 루트'],
      [C.toxic, '내오염 코어', '코어', '독성 슬러지 · 방사능 지대 내성', '오염 지대 통과'],
    ];
    const cw = (CW - 0.5) / 3, ch = 2.35;
    const icons = [null, 'part_rope.png', 'part_jet.png', 'part_cannon.png', 'part_magnet.png', 'part_core.png'];
    P.forEach(([c, n, slot, how, opens], i) => {
      const x = X0 + (i % 3) * (cw + 0.25), y = 1.75 + Math.floor(i / 3) * (ch + 0.25);
      card(s, x, y, cw, ch);
      s.addShape(pres.shapes.RECTANGLE, { x, y, w: cw, h: 0.12, fill: { color: c }, line: { color: c } });
      const ic = icons[i] && has(icons[i]) ? icons[i] : null;
      if (ic) { s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: x + 0.22, y: y + 0.32, w: 1.25, h: 1.25, rectRadius: 0.1, fill: { color: 'CCC3BD' }, line: { color: 'CCC3BD' } }); img(s, ic, x + 0.27, y + 0.37, 1.15, 1.15); }
      const tx = ic ? x + 1.65 : x + 0.25, tw = cw - (tx - x) - 0.2;
      text(s, n, tx, y + 0.3, tw, 0.42, { size: 17, bold: true });
      chip(s, slot, tx, y + 0.78, 1.05, 0.32, c, 'FFFFFF', 10.5);
      text(s, how, tx, y + 1.2, tw, 0.65, { size: 11.5, color: C.muted });
      text(s, '여는 것  ' + opens, x + 0.25, y + 1.85, cw - 0.5, 0.4, { size: 12, bold: true, color: C.ink });
    });
  }

  // 12 성장 · 재화
  {
    const s = slide('10  성장 · 재화', '성장 — 몸체 부위별 트리', '기본 부품 = 경험치로 노드를 열고, 핵심 구동부 파츠를 얻으면 해당 가지와 이동 스킬이 해금');
    const T = [['코어', '체력 · 에너지 · 내오염', C.toxic], ['오른팔(칼날)', '평타 · 전투 파츠 · 캐논', C.cannon], ['왼팔(도구)', '로프 · 반격 · 도구', C.rope], ['다리', '대시 · 제트 · 자석', C.jet], ['머리', '탐지 · 숨은 길 · 기록', C.magnet]];
    const cx = 3.45, cy = 4.15;
    const at = T.map((_, i) => { const a = -Math.PI / 2 + (i * 2 * Math.PI) / T.length; return [cx + 2.25 * Math.cos(a), cy + 1.95 * Math.sin(a)]; });
    at.forEach(([x, y], i) => s.addShape(pres.shapes.LINE, { x: Math.min(cx, x), y: Math.min(cy, y), w: Math.abs(x - cx) || 0.001, h: Math.abs(y - cy) || 0.001, flipH: x < cx, flipV: y < cy, line: { color: T[i][2], width: 2.5 } }));
    s.addShape(pres.shapes.OVAL, { x: cx - 0.85, y: cy - 0.85, w: 1.7, h: 1.7, fill: { color: C.coal }, line: { color: C.coal } });
    text(s, '로봇\n몸체', cx - 0.85, cy - 0.85, 1.7, 1.7, { size: 15, bold: true, color: 'FFFFFF', align: 'center', valign: 'middle' });
    T.forEach(([n, d, c], i) => {
      const [x, y] = at[i];
      card(s, x - 1.05, y - 0.38, 2.1, 0.76, { fill: c, flat: true });
      text(s, n, x - 1.05, y - 0.34, 2.1, 0.36, { size: 13, bold: true, color: 'FFFFFF', align: 'center' });
      text(s, d, x - 1.05, y + 0.02, 2.1, 0.3, { size: 9.5, color: 'FFFFFF', align: 'center' });
    });
    table(s, [
      ['재화', '얻는 곳', '쓰는 곳'],
      ['기본 부품', '모든 적 · 오브젝트', '성장 노드 해금(경험치). 사망 시 그 자리에 드롭'],
      ['원혼이 깃든 부품', '강한 적 · 보스 · 탐험', '수리 키트 제작 · 숨은 상인 거래(화폐)'],
      ['핵심 구동부 파츠', '존 보스', '이동 스킬 · 트리 가지 해금. 잃지 않음'],
    ], 6.95, 1.75, 5.78, [1.6, 1.6, 2.58], { rowH: 0.62, size: 11.5 });
    card(s, 6.95, 4.55, 5.78, 1.95, { fill: 'F7F5F2' });
    text(s, '회복 · 상인', 7.2, 4.7, 3, 0.35, { size: 14, bold: true, color: C.rust });
    text(s, [
      '회복은 수리 키트: 맵에서 가끔 떨어지고, 충전 포드에서 원혼 부품으로 제작',
      '숨어 사는 온화한 기계 상인이 존마다 1명 — 존마다 다른 업그레이드 파츠, 찾으면 지도에 표시',
      '스태미나 없음: QWER · 대시 쿨다운이 긴장감 담당',
    ].map((t) => '•  ' + t).join('\n'), 7.2, 5.1, 5.35, 1.35, { size: 11.5, ls: 1.3 });
  }

  // 13 소울 루프
  {
    const s = slide('11  소울 루프', '소울 루프 — 충전 포드와 부품 회수');
    await cimg(s, 'pod.png', X0, 1.45, 5.4, 3.6, { shadow: true });
    caption(s, '컨셉 이미지 — 과학자의 충전 포드 (ChatGPT 생성)', X0, 5.1, 5.4);
    const flow = [['사망', C.grudge], ['기본 부품 드롭', C.ash], ['포드 재기동', C.coal], ['드롭 지점 회수', C.soul]];
    flow.forEach(([t, c], i) => {
      const x = 6.4 + i * 1.62;
      chip(s, t, x, 1.55, 1.45, 0.62, c, 'FFFFFF', 10.5);
      if (i < flow.length - 1) arrow(s, x + 1.47, 1.86, x + 1.6, 1.86, C.ash, 1.5);
    });
    text(s, '회수 전에 또 죽으면 소멸. 핵심 파츠는 절대 잃지 않음', 6.4, 2.3, 6.3, 0.35, { size: 12, bold: true, color: C.grudge });
    table(s, [
      ['포드 기능', '내용'],
      ['휴식', '수리(체력 회복) · 저장 · 적 리스폰'],
      ['성장', '기본 부품으로 부위별 트리 노드 해금'],
      ['제작', '원혼 부품으로 수리 키트 제작'],
      ['빠른 이동', '발견한 포드끼리 처음부터 이동 가능'],
      ['기억', '가끔 과학자의 음성 기록 재생 — 스토리 조각'],
    ], 6.4, 2.85, 6.33, [1.6, 4.73], { rowH: 0.5, size: 12 });
    text(s, '체크포인트 = 과학자가 남긴 충전 포드. 세계관과 시스템이 한 오브젝트에서 만남', X0, 5.6, 5.4, 0.7, { size: 12, color: C.ink });
  }

  // 14 적 · 생물 · 오브젝트
  {
    const s = slide('12  적 · 생물 · 오브젝트', '적 · 생물 · 오브젝트', '고철로 만든 기계에 인간의 영혼과 증오가 깃든 존재. 모든 공격은 바닥 예고로 보여준다');
    table(s, [
      ['적(프로토)', '체력', '패턴'],
      ['스크랩 하운드', '60', '물기 = 부채꼴 · 돌진 = 직선 5m. 빠르게 붙는 견제형'],
      ['할로우 워든', '240', '휩쓸기 = 넓은 부채꼴 · 내려찍기 = 플레이어 위치 원 · 찌르기 = 긴 직선. 강인도 60'],
    ], X0, 1.75, 7.2, [1.7, 0.8, 4.7], { rowH: 0.6, size: 12 });
    card(s, X0, 3.85, 3.5, 2.8);
    text(s, '생물', X0 + 0.25, 4.0, 3, 0.35, { size: 15, bold: true, color: C.soul });
    text(s, ['분위기 · 기계에게 사냥당하는 대상', '길잡이: 숨은 길 · 아이템 쪽으로 도망', '일부는 공격', '숨어 다니는 강력한 진화 유기체 → 유기체형 보스로'].map((t) => '•  ' + t).join('\n'), X0 + 0.25, 4.42, 3.05, 2.1, { size: 11.5, ls: 1.3 });
    card(s, X0 + 3.7, 3.85, 3.5, 2.8);
    text(s, '오브젝트', X0 + 3.95, 4.0, 3, 0.35, { size: 15, bold: true, color: C.rust });
    text(s, ['파괴된 자동차 · 버려진 비행기 · 파괴된 탱크', '오를 수 있는 폐건물(층 · 계단 · 경사로)', '부서지는 드럼통 · 상자 · 신호등', '파츠별 조각을 조합해 랜덤성 부여 검토'].map((t) => '•  ' + t).join('\n'), X0 + 3.95, 4.42, 3.05, 2.1, { size: 11.5, ls: 1.3 });
    await cimg(s, 'enemies.png', 8.1, 1.75, 4.63, 4.9, { shadow: true });
    caption(s, '컨셉 이미지 — 원한이 깃든 기계 · 폐허의 생물 (ChatGPT 생성)', 8.1, 6.68, 4.63);
  }

  // 15 보스
  {
    const s = slide('13  보스 기획', '보스 — 파훼하고, 읽고, 피한다', '존마다 1체(총 8체). 이동 스킬 조합으로 기믹 파훼(원신 · 로아) + 패턴 회피(소울)');
    await cimg(s, 'boss.png', X0, 1.75, 6.0, 4.0, { shadow: true });
    caption(s, '컨셉 이미지 — 바이오메카닉 보스 (ChatGPT 생성)', X0, 5.8, 6);
    text(s, '파훼 힌트는 모션과 텔레그래프 UI 형태로 제공. 보스마다 새 패턴 + 직전에 얻은 파츠를 쓰는 기믹', X0, 6.2, 6, 0.6, { size: 11.5, color: C.ink });
    text(s, '기괴한 바이오메카닉 표현 — 도형 모델의 한계를 4요소 조합으로 보완', 6.95, 1.75, 5.8, 0.4, { size: 14, bold: true, color: C.rust });
    const E = [['발광 혈관 · 맥동', '셰이더로 빛과 부풀림. 약점 위치 표시와 겸함'], ['따로 노는 덩어리', '조각이 제멋대로 덜컹거리는 조립체 — 리깅 없이 기괴함'], ['기름 · 연기 · 혼 파티클', '붉은 원한이 새어 나오고, 정화 시 푸르게 바뀜'], ['살 덩어리 + 철판 대비', '부드러운 구체 덩어리 vs 각진 철판 · 볼트']];
    E.forEach(([h, b], i) => {
      const y = 2.3 + i * 0.95;
      badge(s, i + 1, 6.95, y, 0.42, C.grudge);
      text(s, h, 7.55, y - 0.02, 5.2, 0.36, { size: 13.5, bold: true });
      text(s, b, 7.55, y + 0.36, 5.2, 0.5, { size: 11.5, color: C.muted });
    });
    card(s, 6.95, 6.05, 5.78, 0.7, { fill: C.coal, flat: true });
    text(s, '유기체형 보스도 존재 — 폐허에서 기괴하게 진화해 기계도 쉽게 못 건드리는 강력한 생물', 7.15, 6.05, 5.4, 0.7, { size: 11.5, color: 'FFFFFF', valign: 'middle' });
  }

  // 16 제작 파이프라인
  {
    const s = slide('14  제작 파이프라인', '제작 파이프라인');
    const P = [['1', '웹 프로토', 'Three.js. 모션 · 암블레이드 솔버 · 전투를 코드 표로 확정'], ['2', 'Unity 이식', 'Unity 6 URP. sim space(웹 좌표계 그대로)로 수식 1:1 이식'], ['3', '수치 검증', 'SoulGame.Audit() vs 웹 __proto.audit() — 15개 모션 1~3% 이내 일치']];
    P.forEach(([n, h, b], i) => {
      const x = X0 + i * 4.15;
      card(s, x, 1.55, 3.85, 1.75);
      badge(s, n, x + 0.25, 1.75, 0.46);
      text(s, h, x + 0.85, 1.75, 2.8, 0.46, { size: 17, bold: true, valign: 'middle' });
      text(s, b, x + 0.25, 2.38, 3.35, 0.85, { size: 12, color: C.muted });
      if (i < 2) arrow(s, x + 3.88, 2.42, x + 4.12, 2.42, C.rust, 2);
    });
    table(s, [
      ['작업', '방식', '담당'],
      ['캐릭터 · 적 · 맵 모델링', '코드로 생성하는 도형 폴리곤 모델', 'Codex'],
      ['모션 · 전투 · AI', '키 포즈 데이터 + 솔버, 텔레그래프 상태기계', 'Claude'],
      ['컨셉 이미지', '분위기 · 캐릭터 · 보스 시안', 'ChatGPT 이미지'],
      ['월드', '존 그래프는 수작업, 존 안 장식만 절차 생성(스트리밍 스캐터)', '기획 + 에이전트'],
      ['기획 확정', '컨셉 기획서 → 질의응답 결정사항 문서 → 구현', '정승운'],
    ], X0, 3.65, CW, [3.2, 6.73, 2.2], { rowH: 0.5, size: 12 });
  }

  // 17 다음 단계
  {
    const s = slide('15  다음 단계', '다음 단계');
    const N = [
      ['존 연결도 확정', '8존 초안(이 문서 08) 검토 → 존 이름 · 게이트 · 보스 · 포드 · 상인 위치 확정'],
      ['존 그래프 데이터화', '현재 4지역 절차 월드를 존 단위로 교체. 게이트를 파츠 id로(1 로프 · 2 제트 · 3 캐논 · 4 자석 · 5 오염)'],
      ['로프 팔', '첫 핵심 파츠. 앵커 오브젝트, 당기기 · 스윙, 왼팔 모델 · 모션'],
      ['소울 루프', '충전 포드, 부품 드롭 · 회수, 수리 키트, 빠른 이동'],
      ['첫 존 보스', 'Z0 보스 — 로프 팔 보상. 파훼 기믹 + 텔레그래프 패턴 3종'],
    ];
    N.forEach(([h, b], i) => {
      const y = 1.5 + i * 1.0;
      card(s, X0, y, CW, 0.82);
      badge(s, i + 1, X0 + 0.25, y + 0.19, 0.44);
      text(s, h, X0 + 0.9, y, 2.8, 0.82, { size: 15, bold: true, valign: 'middle' });
      text(s, b, X0 + 3.7, y, CW - 4, 0.82, { size: 12.5, color: C.muted, valign: 'middle' });
    });
    text(s, '미해결: 폐허 안개 가독성 조정 · 적 길찾기(NavMesh) · 폐건물 디테일 보강', X0, 6.6, CW, 0.3, { size: 11, color: C.grudge });
  }

  // 18 마무리
  {
    const s = pres.addSlide({ masterName: 'COVER' });
    await cimg(s, 'key_art.png', 0, 0, W, H, { focusY: 0.5 });
    s.addShape(pres.shapes.RECTANGLE, { x: 0, y: 0, w: W, h: H, fill: { color: C.deep, transparency: 30 }, line: { color: C.deep, transparency: 100 } });
    text(s, '목표', 0, 0.9, W, 0.5, { size: 16, bold: true, color: C.soul, align: 'center' });
    text(s, '마지막 원한을 정화한 뒤,\n혼자 남은 로봇이 처음으로 쉬는 순간', 0, 1.45, W, 1.4, { size: 30, bold: true, color: 'FFFFFF', align: 'center', ls: 1.25 });
    [['비주얼', '도형 폴리곤 + 폐허'], ['탐험', '파츠로 여는 8개 존'], ['전투', '파훼 + 회피']].forEach(([h, b], i) => {
      const x = W / 2 - 4.5 + i * 3.1;
      chip(s, h, x + 0.6, 3.15, 1.6, 0.42, C.rust, 'FFFFFF', 12);
      text(s, b, x, 3.65, 2.8, 0.4, { size: 13, color: 'F0ECE6', align: 'center' });
    });
    text(s, DOC, 0, 6.7, W, 0.3, { size: 10, color: 'C9C4BC', align: 'center' });
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

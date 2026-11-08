# UI 아틀라스: 성장 트리 · 설정 창 · 스킬 강화 (2026-11-08)

ChatGPT 이미지로 만들었습니다. 기존 `icons.png`, `ui_kit.png`와 같은 대화("Create Game Key Art")에서 이어서 생성했습니다.
모든 좌표는 **픽셀이고, 원점은 이미지 왼쪽 위**입니다. `x, y, w, h` 순서입니다.
Unity `Texture2D`/`Sprite.Create`의 Rect는 원점이 왼쪽 아래이므로 `y_unity = 이미지높이 - y - h`로 바꿔 쓰세요.
모든 이미지에 글자가 없습니다. 텍스트는 게임이 그립니다.

| 파일 | 크기 | 알파 | 용도 |
|---|---|---|---|
| `growth_tree_bg.png` | 1672×941 (16:9 근사) | 없음(RGB) | 성장 트리 화면 배경 |
| `ui_nodes.png` | 1254×1254 | 투명 | 성장 트리 노드 15종 (가지 5 × 상태 3) |
| `ui_panel.png` | 1536×1024 | 투명 | 9-slice 패널, 버튼 3상태, 탭 2, 슬라이더, 키캡 4 |
| `ui_skill_icons.png` | 1774×887 | 투명(후처리) | 스킬 아이콘 8칸 |

---

## 1. `growth_tree_bg.png`

가운데에 홀로그램 로봇(정면) 실루엣이 있고, 몸에서 파란 회로선이 가지처럼 뻗어 나가 빈 원형 소켓에서 끝납니다.
그림에서 칼날 팔은 **화면 왼쪽**에 있습니다. 로봇이 정면을 보고 있어서, 로봇 기준 오른팔이 화면 왼쪽에 옵니다.

**몸의 뿌리 지점**(회로선이 시작하는 밝은 점, 눈으로 잡은 근사값)
- 머리 `(835, 228)` · 코어(가슴 보석) `(850, 430)` · 칼날 팔 손목 `(637, 535)` · 왼팔(케이블 팔) 손목 `(1023, 519)` · 다리 `(767, 673)`

**빈 소켓 중심**(Hough 원 검출, 반지름 약 28px → 노드는 지름 56~64px로 그리면 소켓에 맞습니다). 모두 38개입니다.
그룹 이름은 화면 위치입니다. 어느 그룹을 어느 가지에 줄지는 성장 트리 세션이 정합니다.
선이 이어지는 모양을 보면 머리 → top, 칼날 팔 → mid_left, 왼팔 → mid_right, 다리 → bottom이 자연스럽고, upper_left/upper_right는 어깨·목 쪽에서 나옵니다(코어 가지로 쓰기 좋음).

| 그룹 | 개수 | 중심 (x, y) |
|---|---|---|
| top | 3 | (736,78) (835,47) (940,80) |
| upper_left | 4 | (204,187) (325,91) (366,214) (474,129) |
| mid_left | 8 | (147,562) (224,356) (256,500) (274,605) (320,310) (348,455) (416,535) (449,379) |
| upper_right | 4 | (1245,128) (1352,212) (1415,89) (1519,195) |
| mid_right | 8 | (1228,377) (1254,538) (1325,454) (1357,310) (1403,605) (1422,499) (1449,355) (1536,563) |
| bottom | 11 | (264,717) (318,826) (425,692) (517,826) (649,839) (769,861) (897,842) (1131,823) (1244,691) (1346,827) (1409,719) |

- 화면 비율이 다르면 이 좌표를 `화면크기 / (1672, 941)`로 비례해서 바꿉니다. 배경을 cover로 늘리면 잘리는 부분을 고려해야 합니다.
- 노드 수가 소켓보다 적으면 남는 소켓은 비워 둬도 그림상 자연스럽습니다(빈 소켓 그림).

## 2. `ui_nodes.png` (5열 × 3행)

열(가지): 1 코어(육각 보석) · 2 오른팔(칼날) · 3 왼팔(케이블 집게) · 4 다리(부츠+분사) · 5 머리(후드+얼굴 화면)
행(상태): 1 잠김(회색, 빛 없음) · 2 열 수 있음(파란 테두리 약하게) · 3 열림(밝은 파랑, 블룸)

균일 격자로 자르려면 **셀 245×245**, 셀 왼쪽 위 = `(16 + 245*col, 211 + 289*row)`를 쓰면 모든 노드가 들어갑니다(행 간격 289, 열 간격 245).
타이트 사각형(알파 > 40):

| 행\열 | 코어 | 오른팔 | 왼팔 | 다리 | 머리 |
|---|---|---|---|---|---|
| 잠김 | 23,219,232,236 | 267,219,231,236 | 511,218,232,236 | 756,218,232,236 | 1000,219,233,235 |
| 열 수 있음 | 22,507,233,237 | 267,507,230,236 | 511,507,231,237 | 756,507,232,236 | 1001,506,232,237 |
| 열림 | 23,796,232,244 | 267,796,231,245 | 512,796,231,244 | 756,796,231,244 | 1001,797,232,243 |

노드 바깥 테두리는 10각형 금속 링(볼트 6개)이고, 지름은 약 232px입니다. "열림" 행은 바깥 글로우 때문에 아래로 8px 더 큽니다.

## 3. `ui_panel.png`

| 조각 | x, y, w, h | 9-slice 테두리 (L, R, T, B) 권장 |
|---|---|---|
| 큰 창 패널(안쪽 어두운 금속 채움 포함) | 21, 96, 893, 676 | 120, 120, 120, 120 |
| 버튼 기본 | 938, 109, 571, 147 | 60, 60, 45, 45 |
| 버튼 강조(파란 테두리) | 937, 272, 574, 149 | 60, 60, 45, 45 |
| 버튼 비활성(회색) | 939, 437, 572, 146 | 60, 60, 45, 45 |
| 탭 비활성 | 950, 639, 275, 96 | 60, 60, 30, 20 |
| 탭 활성(위쪽 파란 줄) | 1229, 639, 279, 96 | 80, 80, 30, 20 |
| 슬라이더 막대(왼쪽 절반 파랑 채움 포함) | 35, 844, 569, 82 | 60, 60, 0, 0 |
| 슬라이더 손잡이(원형) | 629, 831, 105, 105 | — |
| 키캡 기본 | 796, 818, 129, 132 | 35, 35, 35, 35 |
| 키캡 눌림(파란 테두리) | 947, 819, 131, 131 | 35, 35, 35, 35 |
| 키캡 넓음(스페이스·Shift 등) | 1100, 819, 261, 131 | 35, 35, 35, 35 |
| 키캡 작음 | 1383, 821, 122, 127 | 30, 30, 30, 30 |

- 큰 패널: 왼쪽 위와 오른쪽 아래 모서리에 천 조각이 감겨 있습니다. 왼쪽 위 천은 위 변을 따라 x≈300(이미지 좌표)까지 뻗어 있어서, 테두리 120으로 자르면 위쪽 가장자리 일부에 천이 늘어나 보일 수 있습니다. 거슬리면 T를 230 정도로 키우거나, 그냥 그대로 씁니다(천이 늘어나도 크게 티 나지 않음).
- 위·아래 변 가운데와 좌우 변 가운데에 파란 네온 줄이 있습니다. 9-slice로 늘리면 이 줄도 같이 늘어납니다.
- 슬라이더: 막대 안쪽 홈은 대략 x 95~550, y 868~900입니다. 채움(파랑)은 그림에 박혀 있으므로 실제 값 표시는 홈 위에 단색 파랑을 덧그리거나, 막대를 채움 없는 부분(x≥340)만 늘려 쓰는 식으로 처리합니다.
- 기존 `ui_kit.png`(HUD)와 금속·녹 질감이 같습니다.
- **사용처**: 설정 창 `Runtime/Game/SettingsArt.cs`가 이 표의 사각형과 테두리를 `Piece` 상수로 그대로 씁니다. 사각형을 바꾸면 그 상수도 함께 고쳐야 합니다. 임포트 설정은 `ui_kit`과 같습니다(밉맵 없음, NPOT None, 무압축, clamp).

## 4. `ui_skill_icons.png` (4열 × 2행)

균일 격자: **셀 443×443**, 셀 왼쪽 위 = `(443.5*col, 443.5*row)` 반올림. 아이콘이 셀 안에 들어갑니다.

| 칸 | 스킬 | 타이트 사각형(알파 > 60) |
|---|---|---|
| (0,0) | 반격(칼날로 막고 스파크 + 반격 호) | 22, 50, 385, 355 |
| (1,0) | 회오리 | 468, 26, 389, 401 |
| (2,0) | 레이저 파동 | 904, 83, 426, 323 |
| (3,0) | 칼날 폭풍 | 1330, 52, 429, 350 |
| (0,1) | 코어 과부하(붉은 빛, 유일한 빨강) | 19, 477, 386, 360 |
| (1,1) | 로프 팔(주황 케이블 집게) | 452, 476, 409, 351 |
| (2,1) | 예비: 칼날 돌진 | 906, 571, 404, 219 |
| (3,1) | 예비: 내려찍기 충격파 | 1350, 514, 406, 305 |

- 정사각 아이콘 칸에 넣을 때는 타이트 사각형보다 **셀 단위**로 자르는 편이 크기가 고르게 보입니다.
- 후처리: ChatGPT 원본은 "투명" 부분에 옅은 흰 안개(알파 5~90)가 깔려 있었습니다. 채도가 낮고 밝은 반투명 픽셀을 지웠습니다. 원본은 `unity/Captures/ui_src/ui_skill_icons_raw.png`에 있습니다. 어두운 배경 위에서 가장자리에 아주 옅은 회색 테가 남은 곳이 있습니다.
- 그림체는 `icons.png` 1~2행(스킬)과 같은 로봇입니다. 다만 `icons.png`는 어두운 단색 배경이고, 이 시트는 투명입니다.

---

## 프롬프트 기록 (ChatGPT 이미지, 2026-11-08, 대화 "Create Game Key Art")

순서대로 같은 대화에서 보냈습니다. 각각 한 번에 채택했습니다(재생성 없음).

1. `growth_tree_bg.png`
> Generate a new image: a full-screen BACKGROUND for the game's skill / growth tree menu, same world and palette as the earlier images in this chat (ash grey, rust orange, charcoal, cyan neon glow, dust and ash particles, low saturation), painterly but clean, NOT photoreal. Wide 16:9. Composition: in the center, a large faint ghostly blueprint-like silhouette of the small hooded wanderer robot standing front-facing (hood, black face screen, glowing chest core, right forearm is a long straight blade, left arm a mechanical cable arm, two legs), drawn as dim cyan hologram lines over a dark rusted metal backdrop. From five body regions (chest core, right blade arm, left arm, legs, head) thin glowing cyan circuit traces and old cables branch outward across the whole screen like tree roots / branches, splitting several times, ending in many EMPTY small circular sockets (about 30 dark empty round sockets with faint rim) where UI nodes will later be placed. Keep the sockets empty and evenly spread, keep the center area and branches uncluttered. Dark vignette edges, subtle rusted panel seams and scratches in the background, drifting ash. No text, no letters, no numbers, no symbols, no UI frames. Original design, not resembling any existing game.

2. `ui_nodes.png`
> Generate a new image: a GAME UI NODE SPRITE SHEET for the growth tree, matching the robot hologram background you just made and the earlier icon sheet (rusted metal, charcoal, cyan neon glow, low-poly flat-shaded, NOT photoreal). TRANSPARENT BACKGROUND (PNG with alpha), nothing behind the nodes. Square 1:1 image. A precise grid of 5 columns x 3 rows = 15 equal cells, each node centered in its own cell with generous empty space between nodes, all nodes the same size, perfectly aligned. Each node = a round hexagon-rimmed rusted metal socket frame with a simple bold symbol inside. Columns (body branch symbol): 1 CORE = a glowing hexagonal reactor core gem; 2 RIGHT ARM = a long straight forearm blade; 3 LEFT ARM = a mechanical grappling claw on a cable; 4 LEGS = a pair of robot boots with a small thrust; 5 HEAD = a hood with a face screen showing two small dots. Rows (state): row 1 LOCKED = dark dim grey, symbol barely visible, cold dead metal, no glow, a little rust and dust; row 2 AVAILABLE = rim with a soft pulsing cyan outline and faint glow, symbol in pale cyan; row 3 UNLOCKED = bright saturated cyan glowing rim and symbol, light bloom, polished metal. No text, no letters, no numbers, no labels, no grid lines. Original design, not resembling any existing game.

3. `ui_panel.png`
> Generate a new image: a GAME UI KIT SPRITE SHEET for the settings menu and skill upgrade screen, same style as the HUD frames and node sheet in this chat (rusted dark metal frames, worn edges, small bolts, torn sand-colored cloth wraps only on the big panel, thin cyan neon light strips, charcoal fill, ash dust, low saturation, low-poly flat-shaded, NOT photoreal). TRANSPARENT BACKGROUND (PNG with alpha). Wide 3:2 image. Lay out the pieces separated by clear empty space, axis-aligned, flat front view, no perspective: TOP-LEFT a large empty rectangular window panel frame with a dark semi-opaque charcoal metal fill inside, plain straight edges between the four decorated corners so it can be 9-sliced. TOP-RIGHT a column of three wide rectangular buttons of identical size, empty: 1 normal (dark metal, dim), 2 highlighted (bright cyan rim glow), 3 disabled (greyed, desaturated, scratched). MIDDLE-RIGHT two tab shapes side by side (one inactive dark, one active with cyan top strip). BOTTOM-LEFT a long horizontal slider track (thin dark groove with cyan fill on the left half) and next to it a separate small round slider knob. BOTTOM-RIGHT a row of four empty square keyboard keycaps made of worn metal (normal, pressed/cyan glowing, wide spacebar-like keycap, small square). All shapes empty inside: no text, no letters, no numbers, no symbols, no icons. Original design, not resembling any existing game.

4. `ui_skill_icons.png`
> Generate a new image: a SKILL ICON SHEET in exactly the same low-poly geometric style as the 4x4 GAME UI ICON SHEET earlier in this chat (same small hooded sand-robed robot with black face screen and cyan pixel eyes, right forearm a long straight blade, flat-shaded simple primitives, ash grey, rust orange, charcoal, glowing accents, NOT photoreal). TRANSPARENT BACKGROUND (PNG with alpha), no background color. Wide 2:1 image. A precise grid of 4 columns x 2 rows = 8 equal square cells, each icon centered in its own cell with clear empty margins, bold readable silhouettes at 48 px, no frames or borders. Row 1: 1 COUNTER = the robot braces and deflects with its blade, a bright cyan-white spark burst and a curved counter-slash arc; 2 WHIRLWIND = the robot spinning inside a tall cyan tornado of blade trails; 3 LASER WAVE = a straight cyan laser beam fired forward from the blade with concentric pulse rings; 4 BLADE STORM = a ring of many cyan energy blades orbiting the robot. Row 2: 5 CORE OVERLOAD = the robot's chest core overheating, glowing hot RED-orange with red cracks and a red energy aura around the robot (the only red icon); 6 ROPE ARM = the robot's left arm firing a mechanical grappling claw on a long orange cable; 7 spare: a blade dash thrust leaving a straight cyan streak; 8 spare: a ground slam shockwave from the blade, rocks lifting, cyan. No text, no letters, no numbers, no labels, no grid lines. Original design, not resembling any existing game.

저장 방법: 페이지에서 이미지 blob을 dataURL로 읽고, 최상위 form POST로 로컬 수신기(127.0.0.1:5199, `name`·`data` 필드)에 보냈습니다. 소켓 좌표는 OpenCV `HoughCircles`(반지름 18~34, minDist 45)로 찾고, 로봇 몸 위의 오검출을 뺐습니다.

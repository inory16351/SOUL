# SOUL — Unity 포트 핸드오프

웹 프로토(`../web-proto`, Three.js)를 Unity 6(6000.3.19f1, URP 17.3)로 옮긴 프로젝트입니다.
다른 세션/에이전트가 이어서 작업할 때 이 문서부터 읽으세요. 웹 쪽 컨셉·모션 선호·조작은 `../HANDOFF.md`에 있습니다.

## 0. 다음 에이전트 시작점 (2026-11-06 마지막 갱신)

**게임 기획 문서 (사용자 작성 + 결정사항)** — 시스템을 설계하기 전에 반드시 읽으세요.
- `../ALONE(가제) 컨셉 기획.md`: 사용자 원본 기획서(세계관, 루프, 성장, 보스, 오브젝트)
- `../메트로베니아 구현 방안.md`: 데바데 보드맵식 존 연결도
- `../기획_결정사항.md`: 질의응답으로 확정한 결정 전부입니다.
  - 존 8개, 중앙에서 3갈래, 유기적 연결, 존마다 보스
  - 이동 파츠 5종: 로프 팔 → 제트 / 캐논 / 자석 다리 / 내오염 코어
  - 부위별 성장 트리, 재화 2종, 수리 키트, 숨은 상인
  - 충전 포드(빠른 이동 거점), 부품 드롭·회수, 스태미나 없음
  - 보스 표현 방식

**지금까지 된 것**
- 전투·모션 포트(웹과 수치 동일)
- 오픈월드 마일스톤 1: 넓은 필드, 4지역, 스트리밍, 지역 분위기·배너
- 고저차: 걷는 면, 층별 벽, 계단·경사로, 점프 마운트, 낙하, 적의 층 구분
- 올라갈 수 있는 폐건물 구조물(Codex 모델링) + 디테일 패스(회색 콘크리트, 창틀, 철근, 그을음, 잔해 덩어리)
- 탑뷰 컷어웨이(플레이어 머리 위 지오메트리를 디더링으로 지움)

**다음 할 일** — 순서는 `기획_결정사항.md` 맨 아래에 있습니다.
1. **존 연결도 초안**(데바데식, 존 8개 + 파츠 색 화살표)을 만들어 **사용자 검토**를 받습니다. 사용자가 "초안은 에이전트, 수정은 사용자"로 정했습니다.
2. 확정된 연결도로 지금의 "4지역 섹터 + 랜덤 배치"를 **존 그래프 기반**으로 바꿉니다. 장식만 절차 생성합니다. `PropSurface.gate`는 파츠 id로 확장합니다(1 로프, 2 제트, 3 캐논, 4 자석, 5 오염).
3. 로프 팔 → 소울 루프(포드, 드롭·회수, 수리 키트, 빠른 이동) → 첫 보스

**확인된 미해결 사항**
- 화면이 안개 때문에 뿌옇습니다. 폐허 존의 가독성 조정이 필요합니다(`Regions.cs` 안개 농도·색, 앰비언트).
- 디테일 패스 뒤 폐건물은 스크린샷 1장으로만 확인했습니다. 전 종류를 육안 검토하지 않았습니다.
- 적은 길찾기 없이 직진합니다. 계단 옆으로 떨어지기도 합니다(NavMesh 등이 필요).
- 텔레그래프의 바닥 판정은 수평만 봅니다. 공격 발동과 피격은 높이차 1.4~1.6m 이내로 막아 두었습니다.
- **커밋 안 됨**: `unity/` 전체, 웹 변경, 기획 결정 문서.

## 1. 지금 상태 (2026-11-06)

- `Assets/SOUL/Scenes/Main.unity`에서 Play하면 웹과 같은 게임이 돌아갑니다: 캐릭터, 15개 모션, 팔-칼날 솔버, 적 2종(하운드/워든)과 텔레그래프, 부서지는 소품, VFX(칼 궤적·불꽃·링·W 파동·대쉬 잔상), 픽셀 LED 얼굴, 천 시뮬레이션, 재, HUD.
- **모션은 웹과 수치상 동일합니다.** `SoulGame.Audit()`을 웹의 `__proto.audit()`과 비교하면 15개 모션 전부 1~3% 이내로 같습니다(float32와 float64 차이). 예: light1 `limit LupperArm` 4.654 / 4.656, roll `elbow R flex` 24.38 / 24.44.
- Windows 빌드: `Builds/Windows/SOUL.exe` (MCP `manage_build`로 생성; `Builds/`는 git 제외)
- 렌더링: URP, Linear, HDR, ACES + Bloom(`Assets/SOUL/Settings/SoulPostFX.asset`)

## 2. 열기 / 실행

1. Unity Hub → `C:\Project\SOUL\unity` 열기 (Unity 6000.3.19f1)
2. `Assets/SOUL/Scenes/Main.unity` → Play
3. 조작: 우클릭 이동 · A 콤보 · Q 회오리 · W 레이저 파동 · E 반격 · R 칼날 폭풍 · Space 대쉬 · F 점프 · 휠 줌 · 1/2 하운드/워든 소환(커서 위치) · 0 전부 제거 · P 허수아비 모드 · C 일시정지 · `.` 한 프레임 진행 · V 측면 카메라 · H 피격 테스트

## 3. 핵심 설계: "sim space"

시뮬레이션 전체(애니메이션, 충돌, 전투, 적, VFX 위치)는 **Three.js 좌표계 그대로** 돌아갑니다(오른손 좌표계, +Y 위, yaw 0이면 +Z를 바라봄). Unity에 쓸 때만 변환합니다.

```
위치      (x, y, z)     -> (-x, y, z)
쿼터니언  (x, y, z, w)  -> (x, -y, -z, w)
메시      정점 x 반전 + 삼각형 감기 순서 반전 (임포터가 처리)
```

- 반사 켤레 변환이라 합성이 보존됩니다. 노드별로 변환한 계층은 원본과 똑같이 렌더링되므로, 웹 수식을 그대로 옮겨도 결과가 같습니다.
- `Sim.cs`에 three.js와 같은 수학이 있습니다: `EulerXYZ`, `ToEulerXYZ`, `FromTo`(setFromUnitVectors), `Slerp`, `FromBasis`, `Hermite`, `ClosestSegments` 등.
- **sim 데이터에는 Unity의 `Quaternion.Euler`, `LookRotation`, `AngleAxis`를 쓰지 마세요.** 규약이 달라서 결과가 틀어집니다.
- `TNode`는 sim 공간의 THREE.Object3D 대응입니다. `CharacterRig`이 프리팹의 관절·sword·bladeTip·hoodTail을 TNode 트리로 미러링하고, `Sync()`가 Unity Transform에 씁니다.
- 마우스 피킹은 Unity Ray와 y=0 평면의 교점을 `Sim.FromUnity`로 변환합니다(`FollowCam.Pick`).

## 4. 파일 지도 (`Assets/SOUL`)

| 경로 | 내용 (웹 원본) |
|---|---|
| `Runtime/Core/Sim.cs` | 좌표 변환 + three.js 수학 |
| `Runtime/Core/TNode.cs` | sim 공간 트랜스폼 노드 |
| `Runtime/Core/Mats.cs` | `SOUL/Lit`, `SOUL/Unlit` 머티리얼 팩토리. 색은 **선형**이고 `SetVector`로 씁니다(감마 변환 방지). 방사형 텍스처 생성도 여기 있습니다 |
| `Runtime/Core/SoulModel.cs` | 임포트된 프리팹의 파트 참조(`Part("legs/0/hip")`, `Num(..)`) |
| `Runtime/Anim/MoveData.cs` | moves.js 로더 (`Resources/Data/moves.json`) |
| `Runtime/Anim/SoulAnimator.cs` | anim.js 1:1 포트: 키 블렌드, dynamics, 다리 IK, 팔-칼날 솔버(정확한 임계감쇠 스프링 11/17Hz, whipHz), 충돌 후보 탐색, 힌지, 엣지 롤, 감사 |
| `Runtime/Anim/Body.cs` | body.js 캡슐 콜라이더 + limits.js 관절 제한 |
| `Runtime/Anim/CharacterRig.cs` | 캐릭터 프리팹 생성 + TNode 리그 |
| `Runtime/Game/SoulGame.cs` | main.js: 입력, 전투(DMG/AREA/HIT 표), 무적 프레임, 패리, 소환, 일시정지, HUD(OnGUI), `Audit()`/`Simulate()`/`Do()` |
| `Runtime/Game/FollowCam.cs` | camera.js |
| `Runtime/Combat/Enemies.cs` | enemies.js: Telegraph, Enemy, EnemyType.TYPES, EnemyManager, 혼(wisp), HP 바(`DrawGUI`) |
| `Runtime/Combat/Breakables.cs` | props.js |
| `Runtime/Fx/Vfx.cs` | vfx.js: 궤적 메시, 엣지 불꽃, flash/dust/ring, W 파동, 잔상 |
| `Runtime/Fx/LaserBlade.cs`, `PixelFace.cs` | neon.js: 칼날 네온 엣지, 픽셀 LED 얼굴(0_0, 피격 시 > <) |
| `Runtime/World/Secondary.cs`, `Ash.cs` | secondary.js 천(Verlet) + 스카프 + hoodTail 스프링, 떠다니는 재 |
| `Shaders/SoulLit.shader`, `SoulUnlit.shader` | 버텍스 컬러 PBR(그림자 포함) / 언릿(블렌드 설정 가능, HDR 네온·VFX용) |
| `Editor/SoulImporter.cs` | JSON → 메시/머티리얼(`Generated/*.asset`) → 프리팹(`Resources/Prefabs/*.prefab`) |
| `Editor/SoulSetup.cs` | URP 에셋, Linear, PostFX 프로필, 셰이더 Always Included |
| `Data/Web/*.json` | 웹에서 export한 원본(재빌드용, 버전 관리) |

## 5. 모델/모션 수정 흐름

모델과 모션 데이터의 **원본은 웹 프로토**입니다(코덱스가 모델링한 절차적 지오메트리).

1. `web-proto/src/*.js`를 수정합니다(모델: character.js / enemy_models.js / scene.js / props.js, 모션: moves.js).
2. 웹 서버를 띄웁니다(`node web-proto/server.mjs` 또는 launch.json의 "web-proto"). 그다음 브라우저 콘솔에서 실행합니다.
   `await (await import('/tools/export-unity.js')).exportAll()`
   결과는 `web-proto/.export/*.json`에 저장됩니다(서버의 localhost 전용 POST 엔드포인트 사용).
3. Unity 메뉴 **SOUL → Import Web Export (copy + build prefabs)**를 실행합니다. JSON을 복사하고 메시·프리팹을 다시 만들며, `moves.json`도 갱신합니다.
   - 모션만 바꿨다면 `Assets/SOUL/Resources/Data/moves.json`을 직접 고쳐도 됩니다(키 형식은 `../HANDOFF.md`의 "move format" 참고).
4. 검증할 때는 Play 상태에서 MCP `execute_code`로 웹 `__proto.audit()`과 비교합니다.
   `return UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>().AuditText();`

Unity 쪽 C#(애니메이터 등)을 고쳤다면 웹과 갈라집니다. 앞으로 어느 쪽을 기준으로 할지 정해야 합니다(아래 7번 참고).

## 6. MCP로 작업하기 (다른 에이전트용)

- MCP for Unity(`com.coplaydev.unity-mcp`)가 패키지로 들어 있습니다. 에디터를 열면 브리지가 뜹니다.
- 여러 Unity가 떠 있으면 먼저 `set_active_instance`를 호출하세요. 이 프로젝트는 `unity@7efb5b1b`이고 이름은 "unity", 경로는 `C:/Project/SOUL/unity/Assets`입니다.
- 스크립트를 수정한 뒤에는 `refresh_unity(compile=request, wait_for_ready)` → `read_console(types=error)` 순서로 확인합니다.
- 실행 테스트(Play 모드에서 `execute_code`):
  ```csharp
  var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
  g.timeScale = 0;                 // 실시간 진행 멈춤(스크린샷용)
  g.enemies.passive = true;
  g.Do("summon:warden"); g.Simulate(1f);
  g.Do("attack"); g.Simulate(.29f);  // 1타 임팩트 직후
  ```
  그다음 `manage_camera(action=screenshot, include_image=true)`로 확인합니다. `Do()`에는 `"attack"`, `"Q"`, `"W"`, `"E"`, `"R"`, `"Space"`, `"F"`, 모션 이름, `"summon:crawler|warden"`을 넘길 수 있습니다.
- `execute_code`는 CodeDom(C# 6)으로 컴파일됩니다. `Object`는 `UnityEngine.Object`로 명시하고, `string.Join`에는 배열을 넘기세요.
- 씬 구성(Map 프리팹, Sun, Main Camera, Global Volume, Game[SoulGame])은 MCP `manage_gameobject`로 만들었습니다. Sun·카메라·포그·앰비언트 값은 `execute_code`로 웹과 맞췄습니다(FogExp2 0.026, 반구광 trilight, sun #ffa078).

## 6.5 오픈월드 (마일스톤 1: 넓은 필드 + 지역, 2026-11-06)

방향: 엘든링 느낌의 오픈월드, **탑뷰 유지**(조작·모션 그대로). 마일스톤 순서(사용자와 합의): ① 넓은 필드 + 지역 → ② 소울 루프(체크포인트·사망 시 재 드롭/회수·스태미나) → ③ 적 거점 + 필드 보스 → ④ 탐색 보상(아이템·지도·숨은 길).

- **월드**: 반지름 262m 걷기 가능(`Regions.WORLD_RADIUS`), 274m에 절벽 링. 원점의 기존 웹 아레나(±30m 지면)는 그대로 두고 그 주변을 생성합니다.
- **지역** (`World/Regions.cs`): 노이즈로 경계를 흔든 120° 섹터와 중심 원으로 나눕니다. 지역마다 이름·안개·앰비언트·태양·지면 팔레트·스캐터 목록이 있습니다.
  - 0 잿빛 폐허(중심, r<~95): 무너진 건물, 슬래브, 잔해, 불탄 차, 가로등 / 랜드마크 RadioTower (-45, 55)
  - 1 재의 사막(북, +Z): 모래 언덕, 암석 첨탑, 쓰러진 송전탑, 기계 갈비뼈 / 랜드마크 GiantRemains (10, 178)
  - 2 고철 협곡(동·남동): 고철 더미, 협곡 벽, 컨테이너, 파이프 / 랜드마크 CraneWreck (150, -88)
  - 3 죽은 숲(서·남서): 탄 나무, 그루터기, 쓰러진 통나무, 뿌리 / 랜드마크 GreatDeadTree (-150, -88)
- **스트리밍** (`World/WorldStreamer.cs`): 32m 청크, 플레이어 주변 반경 3청크 로드 / 4청크 밖 언로드, 프레임당 2청크 생성. 청크 좌표 해시로 결정적이라 같은 청크는 항상 똑같이 생성됩니다. 프롭 메시는 (타입, 변형) 단위로 캐시합니다.
- **소품 모델** (`World/RegionProps.cs`): Codex가 `MeshBuilder` API로 작성했습니다. sim 공간에서 저폴리·플랫셰이딩·버텍스 컬러로 만들고, 프롭마다 바닥 충돌(원/회전 사각형)을 갖습니다.
- **충돌** (`World/WorldCollision.cs`): XZ 평면 원·OBB 밀어내기, 청크 단위로 등록합니다. 플레이어(반지름 0.38)와 적에게 적용됩니다. 길찾기는 아직 없어서 우클릭 이동은 직선 이동 + 미끄러짐입니다.
- **분위기** (`World/RegionAtmosphere.cs`): 지역이 0.6초 이상 유지되면 전환합니다. 안개·앰비언트·태양을 부드럽게 보간하고, 진입할 때 엘든링식 지역명 배너를 띄웁니다.
- 기존 맵의 배경용 `ruins skyline`(r 39~67 상자 빌딩)은 걸어서 통과되므로 런타임에 끕니다.

### 고저차 (올라갈 수 있는 폐건물)
- **데이터**: 프롭은 벽(`PropCollider`)과 **걷는 면**(`PropSurface`: Box 평평한 바닥 / Ramp 계단·경사로 / Mound 언덕)을 가집니다.
  - 벽은 `.Between(y0, y1)`로 층 높이 구간을 지정합니다. 위층 벽은 1층을 막지 않고, 1층 벽은 위층을 막지 않습니다.
  - 면에는 `gate`가 있습니다. `gate=1`은 **나중에 파츠 업그레이드(메트로배니아식 이동 스킬)로만 갈 수 있는 곳**입니다. 지금은 표시만 하고, 업그레이드는 사용자 기획 md를 받은 뒤 구현합니다.
- **규칙** (`WorldCollision`): 걸어서 오를 수 있는 단차는 0.45m(`STEP`), F 점프로 오를 수 있는 턱은 1.1m(`JUMP`)까지입니다. 머리 위 2m(`HEADROOM`)보다 높은 면은 천장으로 취급해서 그 아래를 걸어 다닐 수 있습니다.
  - `Height(x,z)`: 그 지점의 가장 높은 면
  - `Floor(x,z,footY,climb)`: 지금 발 높이에서 올라설 수 있는 바닥
  - `Move(from,to,r,climb)`: 벽을 밀어내고, 못 오르는 턱은 벽처럼 막으며 축 방향으로 미끄러뜨림
- **플레이어** (`SoulGame.Vertical`): 계단·경사로는 부드럽게 오르내립니다.
  - 점프 아크 중에 턱 위로 들어가면 루트를 올립니다. 이때 애니메이터 `AirLift`가 착지 시점까지 보정을 풀어서 화면에서 튀지 않습니다.
  - 바닥보다 높이 있으면 중력 22로 낙하하고(`Falling` 중에는 다리 IK 정지), 세게 떨어지면 착지 먼지와 흔들림이 납니다.
- **애니메이터**: `groundAt(x,z)`(= `Floor`)로 발 IK·발 고정·칼날 지면 판정이 실제 바닥 높이를 따릅니다. 계단에서는 두 발 높이가 달라집니다.
- **적**: 점프를 못 합니다. 단차와 경사로만 넘고, 턱 위는 안전지대가 됩니다. 소환할 때는 커서가 있는 층의 바닥에 생성됩니다.
- **큰 구조물**: 48m 플롯 격자(`WorldStreamer.PlotAt`)에 배치합니다. 플롯은 셀 좌표만으로 정해지는 함수라서, 이웃 청크도 그 자리를 비워 둡니다. 종류는 RuinStructures(Codex 모델링)의 ApartmentRuin / ShopRow / ParkingDeck / Overpass / StairRuin이고, 랜드마크로 CollapsedTower (58, -58)가 있습니다.
- 높이가 있는 프롭은 스케일을 1로 고정합니다. 키우거나 줄이면 1.1m 점프 턱이 깨지기 때문입니다.
- 한계:
  - 텔레그래프 판정은 높이를 보지 않습니다(옥상에 있어도 아래층 공격에 맞음).
  - 천 바닥은 루트 높이 근사입니다.
  - 길찾기가 없습니다.

## 7. 남은 일 / 알려진 차이

- **색감**: Unity 쪽이 약간 더 밝고 채도가 높습니다. 조정 지점은 Sun 2.2, 앰비언트 trilight, Bloom(threshold .9 / intensity .9)입니다. 칼 궤적이 웹보다 하얗게 보이는데, `Vfx.cs`의 TRAIL 색과 블룸으로 조정하면 됩니다.
- 포인트 라이트 강도 변환은 대략값입니다(`SoulLights.PointScale` 1.2).
- 미포팅 항목: 천의 바느질 선(렌더 전용), `slashArc`(웹에서도 미사용), 카메라 `setViewOffset`(화면 좌측 오프셋), 웹 UI의 Move Library 패널·재생 속도 슬라이더(`SoulGame.timeScale` 필드로 대체).
- 웹의 알려진 이슈가 그대로 남아 있습니다: roll 중 몸이 지면 아래로 약 9cm 내려감, counter 팔꿈치가 몸통에 약 1~2cm 파고듦, light3/heavy의 비틀림 속도가 다소 높음.
- HUD는 IMGUI(OnGUI)입니다. 정식 UI(UGUI/UI Toolkit)로 바꾸는 건 후속 작업입니다.
- 입력은 레거시 Input Manager입니다(`activeInputHandler: 0`). Input System으로 옮기는 건 선택 사항입니다.
- 모션 원본을 어디에 둘지 결정이 필요합니다. ① 웹에서 모션을 만들고 Unity로 export(현재 방식) ② Unity C#/JSON을 원본으로 전환.
- 사용자 선호(모션은 크고 사실적으로, 가감속 있게, 1타는 채찍/돌팔매처럼, 팔이 몸을 뚫지 않게, 타격 판정은 이펙트보다 크게, 모델링은 코덱스로)는 `../HANDOFF.md`에 정리돼 있습니다.

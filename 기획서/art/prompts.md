# 컨셉 이미지 프롬프트 (ChatGPT 이미지, 2026-11-06)

한 대화(“Create Game Key Art”)에서 순서대로 생성해서 스타일을 맞췄습니다. 결과는 1672×941 PNG입니다.

| 파일 | 슬라이드 | 요지 |
|---|---|---|
| `key_art.png` | 표지 · 마무리 | 폐허 노을 속 후드 로봇. 폐건물, 파괴된 차·비행기·탱크, 멀리 붉은 혼을 내뿜는 거대 고철 기계 |
| `character.png` | 04 캐릭터 | 정면/측면/후면 3면도 + 이동 파츠 5종(로프 팔, 제트, 캐논, 자석 다리, 내오염 코어) |
| `part_*.png` | 09 이동 파츠 | `character.png` 오른쪽 열에서 잘라낸 아이콘 |
| `world.png` | 03 세계관 · 05 비주얼 | 탑뷰 3/4 폐허 도시. 고저차, 고철 워커·하운드, 도망치는 작은 생물, 독성 슬러지 |
| `enemies.png` | 12 적 · 생물 | `world.png`에서 기계 2종 부분만 잘라냄 |
| `boss.png` | 13 보스 | 살 덩어리와 녹슨 철판·궤도가 붙은 바이오메카닉 보스, 바닥 부채꼴 텔레그래프, 대시로 피하는 로봇 |
| `pod.png` | 05 비주얼 · 11 소울 루프 | 반쯤 무너진 건물 안 과학자의 충전 포드, 녹음기·아이 그림·곰인형 |
| `game_1.png` | 01 기획 의도 · 07 비주얼 | Unity 6 프로토 실제 화면(잿빛 폐허), MCP 캡처 |
| `hook.png` | 09 전투 기믹 (v0.3) | 로봇이 왼팔 케이블로 뜯어낸 거대한 팔을 끌고 휘두름. 팔을 잃은 워커의 어깨에서 붉은 원한이 샘 |
| `thief.png` | 14 소울 루프 (v0.2) | 로봇의 부품과 로브 조각을 달고 가는 하운드, 멀리 지붕에서 지켜보는 로봇 |
| `restored.png` | 03 되살아나는 세계 (v0.3) | `world.png`와 같은 구도의 정화 후 모습. 맑은 하늘, 풀과 나무, 맑은 물, 쓰러진 워커, 돌아온 생물, 멀리 남은 하운드 |
| `face_*.png` | 04 표정 성장 (v0.3) | 이미지 생성 아님. `PixelFace.cs`와 같은 24×16 LED 규칙으로 렌더링. curious · sad · angry · sleep은 신규 초안 |

슬라이드 번호는 v0.3 기준입니다. 위쪽 표의 이전 항목 번호는 v0.1 기준이므로 v0.3에서는 세계관 05, 캐릭터 06, 비주얼 07, 이동 파츠 12, 소울 루프 14, 적 · 생물 15, 보스 16으로 읽으면 됩니다.

공통 스타일 문구: `clean low-poly 3D made of simple geometric primitives (boxes, cylinders, spheres, flat-shaded) … cute and minimal, NOT photoreal, no text`, 팔레트 `ash grey, rust orange, charcoal, cyan accents`.

캐릭터 고정 문구: `ragged hooded sand-colored desert robe, right side torn exposing a slim dark mechanical right arm whose forearm IS a long straight blade (no hand), black face screen with cyan pixel LED face 0_0`.

using UnityEngine;

namespace Soul
{
    // 생존자 기록 · 메인 스토리 (기획서/Z0_퀘스트_동선.md, 사용자 결정 2026-11-09)
    //  - 기록 = 죽은 인간 생존자들이 남긴 홀로그램(소리 + 자막). 줍는 곳에서 재생, Tab 성장 창 '기록' 탭에서 다시 보기.
    //  - 보상 없음(서사·단서만). 메인 보스 위치는 지도 표시 없이 기록만으로 추리. 존마다 6~8개, 일부는 파츠 뒤.
    //  - 한 존의 기록은 그 존 보스 단서 + 이웃 존(길·게이트·보스) 단서: 어느 순서로 가도 다음 힌트가 생긴다.
    //  - 메인 스토리 = 정화한 존 수(1..8)마다 한 조각, 정화 연출 직후. 과학자의 남긴 메시지.
    // TEXT IS A DRAFT (Claude) for the user to rewrite. Keep ids stable (collected state keys on them).
    public static class Lore
    {
        public class Survivor { public string id, name, role; public Color tint; public float height = 1.7f; public int build; }   // build: 0 slim, 1 stocky, 2 child
        public class Record
        {
            public string id, who; public int zone; public Vector2 pos; public string[] lines;
            public string clue;        // design note: what it points to (not shown in game)
            public int gate;           // part needed to reach it (0 = walkable; 7 = 도약 관절 mesa)
            public bool collected;
        }

        public static readonly Survivor[] People =
        {
            new Survivor { id = "hayun",  name = "하윤", role = "피난민, 두 아이의 엄마", tint = new Color(.55f, 1.5f, 2.0f), build = 0 },
            new Survivor { id = "minjae", name = "민재", role = "라디오를 든 아이",     tint = new Color(.6f, 1.7f, 1.9f), height = 1.25f, build = 2 },
            new Survivor { id = "seo",    name = "서 중사", role = "철수 부대 분대장",   tint = new Color(.5f, 1.4f, 2.1f), height = 1.8f, build = 1 },
            new Survivor { id = "dokyung", name = "도경", role = "정비공",              tint = new Color(.6f, 1.5f, 1.8f), build = 1 },
            new Survivor { id = "woojin", name = "우진", role = "약을 나르던 배달꾼",   tint = new Color(.5f, 1.6f, 2.0f), build = 0 },
        };
        public static Survivor Person(string id) { foreach (var p in People) if (p.id == id) return p; return People[0]; }

        public static readonly Record[] Records =
        {
            new Record { id = "z0_hayun_1", who = "hayun", zone = 0, pos = new Vector2(16, -44), clue = "보스: 동쪽 철문, 잠든 짐승",
                lines = new[] { "애들 데리고 광장까지 왔어. 여기 충전기 같은 게 아직 살아 있어.",
                                "동쪽 철문 쪽에선 밤마다 쇠 긁는 소리가 나.",
                                "사람들이 그러더라. 거긴 잠든 짐승이 문을 지킨다고. 절대 동쪽으론 가지 마." } },
            new Record { id = "z0_minjae_1", who = "minjae", zone = 0, pos = new Vector2(-38, 48), clue = "엘리트: 서쪽 우두머리 / 보스: 재가 날리는 방향",
                lines = new[] { "치직… 여기는 탑 꼭대기, 민재예요. 라디오 잡히는 사람?",
                                "서쪽 무너진 동네에서 개들이 울어요. 대장 개는 집채만 해요.",
                                "바람에 날리는 재는 늘 해 뜨는 쪽으로 가요. 엄마가 그쪽엔 가지 말랬어요." } },
            new Record { id = "z0_seo_1", who = "seo", zone = 0, pos = new Vector2(64, -68), clue = "이웃: 남쪽 협곡(Z3) 길 / 보스: 기둥 셋",
                lines = new[] { "부대는 남쪽 협곡 길로 철수한다. 길은 열려 있다.",
                                "동쪽 감시 구역은 포기한다. 놈은 쓰러진 척한다.",
                                "기둥 셋이 빛나면 일어난다. 반복한다, 기둥 셋." } },
            new Record { id = "z0_dokyung_1", who = "dokyung", zone = 0, pos = new Vector2(-262, 32), clue = "보스: 동쪽 원형 담장, 서쪽 정문, 매달린 팔",
                lines = new[] { "개들이 기계 시체를 뜯어 가서 지들 몸에 붙이더라.",
                                "큰 놈 하나는 다리 하나가 팔처럼 자라서 등 위에 매달려 있어. 고철을 집어 던진다고.",
                                "그 짐승, 동쪽 둥근 담장 안에 있어. 문은 서쪽을 보고 있고." } },
            new Record { id = "z0_woojin_1", who = "woojin", zone = 0, pos = new Vector2(-498, 178), clue = "이웃: 서쪽 절벽 너머 마천루 묘지(Z2, 로프 턱) / 엘리트 기둥",
                lines = new[] { "배달 가방엔 아직 약이 있는데… 다리가 안 움직여.",
                                "서쪽 끝 절벽 너머가 마천루 묘지야. 줄만 있으면 올라갈 수 있는 턱이 있었어.",
                                "우두머리가… 돌아온다. 기둥 사이로 숨어—" } },
            new Record { id = "z0_seo_2", who = "seo", zone = 0, pos = new Vector2(352, 44), clue = "보스: 기둥 순서, 2페이즈 역순",
                lines = new[] { "감시 기록. 짐승은 기둥에서 힘을 받는다. 켜지는 순서대로 부숴라.",
                                "두 번째로 일어나면 순서를 거꾸로 해야 한다.",
                                "이건 우리가 피로 배운 거다." } },
            new Record { id = "z0_hayun_2", who = "hayun", zone = 0, pos = new Vector2(128.5f, 98), gate = 7, clue = "이웃: 북쪽 연구소(Z7) 봉인 문, 열쇠 둘(Z2·Z6)",
                lines = new[] { "높은 데 올라오니 북쪽이 보여. 산 밑에 하얀 연구소 문.",
                                "열쇠가 두 개 있어야 열린대.",
                                "하나는 마천루 묘지 쪽, 하나는 남쪽 늪에서 본 사람이 있대." } },
            new Record { id = "z0_minjae_2", who = "minjae", zone = 0, pos = new Vector2(-335.5f, 207), gate = 7, clue = "이웃: 남서 도심(Z1), 동쪽 숲(Z5)",
                lines = new[] { "치직… 탑 말고 여기가 더 잘 잡혀요.",
                                "남서쪽 도심엔 아직 불 켜진 빌딩이 있대요. 동쪽 숲은 나무가 기계를 삼켰대요.",
                                "엄마, 나 동쪽 안 가요. 약속해요." } },
            // ---- Z6 독성 늪 (기획서/Z6_독성늪_보스_생태계.md): the Z0 survivors' last trail ends here ----
            new Record { id = "z6_hayun_3", who = "hayun", zone = 6, pos = new Vector2(1338, 548), clue = "이웃: 숲(Z5)에서 슬러지 직진은 공장(Z4) 코어 / 민재 행방",
                lines = new[] { "민재가 라디오 하나 들고 사라졌어. 동쪽으로 가지 말라고 했는데.",
                                "숲 끝에서 늪으로 오는 길은 둘이야. 슬러지를 건너거나, 무너진 벽을 부수거나.",
                                "공장 사람들은 독을 견디는 심장을 만들었대. 그게 있었으면…" } },
            new Record { id = "z6_seo_3", who = "seo", zone = 6, pos = new Vector2(1184, 676), clue = "엘리트: 준설기 흡입구에 벌레를 먹여라",
                lines = new[] { "분대 기록. 늪에선 벌레가 기계를 먹는다. 우리 장비도 먹혔다.",
                                "큰 준설기는 아직 숨을 쉰다. 앞에 서면 빨려 들어간다.",
                                "놈이 빨아들일 때 벌레 떼를 같이 끌고 가라. 목이 막히면 멈춘다." } },
            new Record { id = "z6_dokyung_2", who = "dokyung", zone = 6, pos = new Vector2(1028, 742), clue = "엘리트: 껍질 숙주의 주머니, 위나 멀리서",
                lines = new[] { "웅덩이 한가운데 바위인 줄 알았는데, 등껍질이었어.",
                                "껍질 위 주머니가 부풀면 독을 뭉쳐서 던져. 껍질은 칼이 안 들어가.",
                                "주머니는 위에서 치거나 멀리서 쏴야 터져. 터지면 제 독을 뒤집어쓰더라." } },
            new Record { id = "z6_woojin_2", who = "woojin", zone = 6, pos = MirePools.Ledge + new Vector2(1.4f, -.8f), gate = 7, clue = "보스: 물이 빠질 때 뿌리, 차오를 때 본체",
                lines = new[] { "급수탑 위까지 올라왔어. 약 가방은 여기 숨겨 둘게.",
                                "남쪽 광장엔 늪 전체에 뿌리 내린 어미가 있어. 물이 빠지면 뿌리가 올라와.",
                                "물이 차오를 때만 어미가 얼굴을 내밀어. 그때 지붕 위에 있어야 해." } },
            new Record { id = "z6_minjae_3", who = "minjae", zone = 6, pos = new Vector2(1082, 918), clue = "보스: 남쪽 가라앉은 광장, 밀물과 썰물",
                lines = new[] { "치직… 엄마? 늪에서도 라디오가 잡혀요.",
                                "남쪽 광장 물이 숨을 쉬어요. 들어왔다, 나갔다.",
                                "거기 큰 게 자고 있어요. 나 안 갈게요. 진짜로." } },
            new Record { id = "z6_hayun_4", who = "hayun", zone = 6, pos = new Vector2(1268, 884), clue = "보스: 알 껍질 발판은 잠깐 / 연구소 열쇠",
                lines = new[] { "광장 앞까지 왔어. 민재 라디오가 여기 떨어져 있었어.",
                                "물이 차면 하얀 알 껍질이 떠올라. 잠깐만 떠 있다가 가라앉아.",
                                "사람들이 그러더라. 어미 몸속에 연구소 열쇠 하나가 있다고." } },
            new Record { id = "z6_seo_4", who = "seo", zone = 6, pos = new Vector2(1404, 846), clue = "이웃: 숲(Z5) 자석 다리 / 연구소 열쇠 둘",
                lines = new[] { "마지막 무전. 파이프 다리 위에서 버틴다.",
                                "숲 쪽 녹슨 외벽은 자석 다리가 있어야 탄다. 거기 공장으로 가는 길이 있다.",
                                "연구소 문은 열쇠 둘. 하나는 여기, 하나는 서쪽 묘지. 이상." } },
        };

        /// main story: one beat per purified zone count (1..8), played right after the purification
        public static readonly string Scientist = "과학자";
        public static readonly string[][] Beats =
        {
            new[] { "…기동 확인. 네가 처음으로 무언가를 되돌려 놓았구나.", "그 얼굴, 아파 보여도 괜찮아. 느낀다는 뜻이니까." },
            new[] { "놀랐니? 세상은 아직 살아 있어.", "원한은 기계에 깃들었지만, 땅은 기억하고 있단다." },
            new[] { "연구소 문을 열려면 열쇠 두 개가 필요해.", "내가 일부러 그렇게 만들었다. 다시는 아무도 쉽게 들어오지 못하게." },
            new[] { "궁금하지? 왜 내가 너를 만들었는지.", "평화를 기억하는 누군가가 하나쯤은 남아 있어야 했어." },
            new[] { "웃는구나.", "그게 내가 마지막으로 보고 싶었던 얼굴이야." },
            new[] { "미안하다. 기계들이 너를 쫓는 건 내 영혼의 냄새 때문이야.", "너에게 너무 많은 걸 담았어." },
            new[] { "화가 나도 괜찮아. 그 화를 기계처럼 품지만 않으면 돼.", "붉게 빛나는 것들을 조심해." },
            new[] { "이제 쉬어도 돼.", "연구소 안쪽에서, 내가 기다리고 있을게." },
        };
        public static readonly bool[] beatSeen = new bool[8];
    }
}

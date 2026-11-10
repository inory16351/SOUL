using UnityEngine;

namespace Soul
{
    // 경로 유도 (2026-11-09). User: no explicit route ("너무 대놓고 길 알려주진 않게"), only a feel for where the
    // zone's boss lies, different per zone ("맵마다 특징 살려서"). So:
    //  A 탐험 카메라: outside combat, walking eases the view forward along the move direction and out 30 %.
    //  D 보스 쪽 기척, per zone (Cues):
    //    - drift: the ambient ash / motes lean toward the boss (Ash.wind)
    //    - flock: now and then a zone creature group rises beside the player and drifts ~22 m toward the boss,
    //      then fades. It does not wait or escort (GuideFlock: visuals, Codex 15_guide_flock)
    //    - sound: a faint zone sound from the boss direction every 40-75 s (louder nearer)
    //    The boss spot is the zone's BossArena; zones without one yet use the zone centre as a stand-in.
    //    Nothing once the zone is purified (the boss is gone).
    //  F 지도 핀: right-click the map to set / clear a pin; an arrow at the screen edge points to it (direction
    //    only); it clears itself when you reach it.
    public partial class SoulGame
    {
        struct BossCue { public string sound; public float pitch, vol, wind, speed; public string note; }
        static readonly BossCue[] Cues =
        {
            new BossCue { sound = "slam",     pitch = .55f, vol = .55f, wind = .35f, speed = 3.0f, note = "Z0 잿빛 폐허: 영혼 불티, 쇠 신음" },
            new BossCue { sound = "warn",     pitch = .62f, vol = .35f, wind = .30f, speed = 2.6f, note = "Z1 무너진 도심: 나방·종잇조각, 먼 경보음" },
            new BossCue { sound = "slam",     pitch = .42f, vol = .6f,  wind = .25f, speed = 4.2f, note = "Z2 마천루: 까마귀, 무너지는 소리" },
            new BossCue { sound = "parry",    pitch = .5f,  vol = .3f,  wind = .40f, speed = 3.4f, note = "Z3 고철 협곡: 튀는 쇳불, 쇠 부딪힘" },
            new BossCue { sound = "pillarRise", pitch = .6f, vol = .45f, wind = .45f, speed = 2.8f, note = "Z4 전쟁 주조소: 불씨·연기, 용광로 울림" },
            new BossCue { sound = "roar",     pitch = .5f,  vol = .25f, wind = .35f, speed = 3.0f, note = "Z5 죽은 숲: 낙엽, 삐걱임" },
            new BossCue { sound = "stepWater", pitch = .45f, vol = .5f, wind = .20f, speed = 2.2f, note = "Z6 독성 늪: 독 반딧불, 거품" },
            new BossCue { sound = "warn",     pitch = 1.35f, vol = .25f, wind = .25f, speed = 3.6f, note = "Z7 연구소: 전기 불꽃, 경고음" },
        };
        const float FLIGHT = 22;
        /// start a flock flight from 'from' toward 'to' over 'dist' m regardless of the boss pull (rescued creatures)
        public void FlockToward(Vector3 from, Vector3 to, float dist)
        {
            if (flock == null) return;
            flying = true; flockFree = true; flockRestart = true; flockZone = ZoneWorld.At(from.x, from.z);
            var d = Sim.FlatY(to - from); flockDir = d.sqrMagnitude > .01f ? d.normalized : Vector3.forward; flockLeft = Mathf.Max(8, dist); flockLead = from;
        }
        bool flockFree;
        GuideFlock flock; float calmT, flockCd = 25, soundCd = 30, exploreAmt, flockA, flockLeft; bool flying, flockRestart;
        Vector3 lastGuidePos, leadDir, flockLead, flockDir; int flockZone;
        System.Random guideRng = new System.Random(4127);
        /// map pin (sim XZ), null = none
        public Vector2? mapPin;

        /// where the zone's boss waits (sim), or null when there is none to pull toward (purified)
        static Vector3? BossSpot(int zone)
        {
            if (zone < 0 || ZoneState.Purified(zone)) return null;
            foreach (var a in ZoneAreas.Arenas) if (a.zone == zone) return a.center;
            var c = ZoneWorld.Center(zone); return new Vector3(c.x, 0, c.y);   // stand-in until the zone gets its arena
        }

        void GuideUpdate(float dt)
        {
            var p = ch.root.position;
            bool calm = VistaCalm() && cam.vista <= .01f && rs == RopeState.None;
            // ---- A: explore camera ----
            var vel = Sim.FlatY(p - lastGuidePos) / Mathf.Max(dt, 1e-4f); lastGuidePos = p;
            bool walking = vel.magnitude > .8f && vel.magnitude < 12 && !anim.busy;
            // WASD (direct control): a shorter, snappier lead; click-move keeps the slow 5.5 m look-ahead
            bool direct = KeyBinds.Wasd; cam.leadRate = direct ? 6 : 1.6f; cam.targetRate = direct ? 14 : 8; cam.posRate = direct ? 12 : 7;
            if (walking) leadDir = Vector3.Lerp(leadDir, vel.normalized, 1 - Mathf.Exp(-dt * (direct ? 7 : 2.5f)));
            float goal = calm && walking ? 1 : calm ? exploreAmt * Mathf.Exp(-dt * .8f) : 0;
            exploreAmt = Mathf.MoveTowards(exploreAmt, goal, dt * (calm ? .9f : 2.5f));
            cam.explore = exploreAmt; cam.exploreLead = leadDir * (direct ? 2.5f : 5.5f);
            // ---- F: pin reached ----
            if (mapPin.HasValue && (mapPin.Value - new Vector2(p.x, p.z)).magnitude < 8) { mapPin = null; SoulAudio.Play("pickup", p); }
            // ---- D: the boss's pull ----
            if (flock == null) flock = new GuideFlock(transform);
            int zone = Regions.At(p); var boss = BossSpot(zone);
            Vector3 toBoss = Vector3.zero; float dist = 0;
            if (boss.HasValue) { var d = Sim.FlatY(boss.Value - p); dist = d.magnitude; if (dist > 1) toBoss = d / dist; }
            var cue = Cues[Mathf.Clamp(zone, 0, Cues.Length - 1)];
            bool pull = boss.HasValue && dist > 45 && calm && fightArena == null;
            ash.wind = pull ? toBoss * cue.wind : Vector3.zero;
            calmT = calm ? calmT + dt : 0; flockCd -= dt; soundCd -= dt;
            if (pull && !flying && flockCd <= 0 && calmT > 10)
            {
                flockFree = false; flying = true; flockRestart = true; flockZone = zone; flockDir = toBoss; flockLeft = FLIGHT;
                var side = new Vector3(toBoss.z, 0, -toBoss.x) * (guideRng.NextDouble() < .5 ? -2.5f : 2.5f);
                flockLead = p + side + toBoss * 1.5f;
                flockCd = 60 + (float)guideRng.NextDouble() * 40;
            }
            if (flying)
            {
                float step = cue.speed * dt; flockLead += flockDir * step; flockLeft -= step;
                if (flockLeft <= 0 || (!flockFree && (!pull || zone != flockZone))) flying = false;   // arrived / threat / boss gone / left the zone
            }
            flockA = Mathf.MoveTowards(flockA, flying && flockLeft > 4 ? 1 : 0, dt * (flying && flockLeft > 4 ? 1.2f : .5f));
            flock.Update(dt, flockZone, flockLead, flockDir, flockA, flockRestart); flockRestart = false;
            if (pull && soundCd <= 0)
            {   // a faint zone sound from the boss side, louder when nearer
                float near = 1 - Mathf.InverseLerp(60, 700, dist);
                SoulAudio.Play(cue.sound, p + toBoss * 30, cue.vol * Mathf.Lerp(.35f, 1, near), cue.pitch, .08f);
                soundCd = 40 + (float)guideRng.NextDouble() * 35;
            }
        }

        /// map right-click: set a pin, or clear it when clicking near the current one
        void MapPinClick(Rect r, Vector2 mp)
        {
            var world = ZoneWorld.FromMapUV(new Vector2((mp.x - r.x) / r.width, (mp.y - r.y) / r.height));
            if (PinHovered(r, mp)) { mapPin = null; return; }
            mapPin = new Vector2(world.x, world.z);
        }
        /// screen point of the pin's tip on the map rect
        Vector2 PinOnMap(Rect r) { var uv = ZoneWorld.MapUV(new Vector3(mapPin.Value.x, 0, mapPin.Value.y)); return new Vector2(r.x + uv.x * r.width, r.y + uv.y * r.height); }
        /// the mouse is over the pin (its head or tip): hovering highlights it, clicking it (either button) removes it
        bool PinHovered(Rect r, Vector2 mp)
        {
            if (!mapPin.HasValue) return false;
            var c = PinOnMap(r); return (mp - (c + new Vector2(0, -16))).magnitude < 14 || (mp - c).magnitude < 10;
        }
        void DrawMapPin(Rect r)
        {
            if (!mapPin.HasValue) return;
            var mp = Event.current != null ? Event.current.mousePosition : new Vector2(-999, -999);
            bool hover = PinHovered(r, mp);
            DrawPinShape(PinOnMap(r), hover ? 1.3f : 1, 1, hover);
            if (hover)
            {   // remove hint: a small x beside the head
                var c = PinOnMap(r) + new Vector2(13, -26);
                GUI.color = new Color(0, 0, 0, .75f); GUI.DrawTexture(new Rect(c.x - 7, c.y - 7, 14, 14), white);
                GUI.color = new Color(1, .45f, .35f, 1); GUI.Label(new Rect(c.x - 7, c.y - 9, 14, 16), "×", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold });
                GUI.color = Color.white;
            }
        }
        /// a teardrop pin (tip at c)
        void DrawPinShape(Vector2 c, float scale, float a, bool hot = false)
        {
            GUI.color = new Color(0, 0, 0, .7f * a); GUI.DrawTexture(new Rect(c.x - 7 * scale, c.y - 23 * scale, 14 * scale, 14 * scale), white);
            GUI.color = hot ? new Color(1f, .5f, .35f, a) : new Color(1f, .78f, .3f, a); GUI.DrawTexture(new Rect(c.x - 5.5f * scale, c.y - 21.5f * scale, 11 * scale, 11 * scale), white);
            for (int i = 0; i < 4; i++) { float w = (4 - i) * 2 * scale; GUI.DrawTexture(new Rect(c.x - w / 2, c.y - (10 - i * 2.5f) * scale, w, 2.5f * scale), white); }
            GUI.color = Color.white;
        }
        /// screen position of the pin, or the edge arrow when it is off screen (returned for tests)
        Vector2 PinScreen(float W, float H, out bool onScreen, out Vector2 dir)
        {
            var world = new Vector3(mapPin.Value.x, 0, mapPin.Value.y); world.y = Terrain.Height(world.x, world.z) + 1.5f;
            var sp = cam.camera.WorldToScreenPoint(Sim.ToUnity(world)); bool front = sp.z > 0;
            var s = new Vector2(sp.x, H - sp.y); const float M = 46; dir = Vector2.zero;
            onScreen = front && s.x > M && s.x < W - M && s.y > M && s.y < H - M;
            if (onScreen) return s;
            var me = cam.camera.WorldToScreenPoint(Sim.ToUnity(ch.root.position)); var c = new Vector2(me.x, H - me.y);
            var d = s - c; if (!front) d = -d; if (d.sqrMagnitude < 1) d = Vector2.up; d.Normalize(); dir = d;
            float k = Mathf.Min((d.x > 0 ? W - M - c.x : c.x - M) / Mathf.Max(1e-3f, Mathf.Abs(d.x)), (d.y > 0 ? H - M - c.y : c.y - M) / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
            return c + d * k;
        }
        void PinGUI(float W, float H)
        {
            if (!mapPin.HasValue || showMap) return;
            var e = PinScreen(W, H, out bool on, out var d);
            if (on) { DrawPinShape(e, .9f, .9f); return; }
            var n = new Vector2(-d.y, d.x); float pulse = .75f + .25f * Mathf.Sin(realTime * 4);
            for (int i = 0; i < 6; i++)
                foreach (float sg in new[] { 1f, -1f })
                {   // chevron of small squares pointing along d
                    var q = e - d * (i * 4) + n * sg * i * 3.2f;
                    GUI.color = new Color(0, 0, 0, .55f); GUI.DrawTexture(new Rect(q.x - 3.5f, q.y - 3.5f, 7, 7), white);
                    GUI.color = new Color(1f, .78f, .3f, pulse); GUI.DrawTexture(new Rect(q.x - 2.5f, q.y - 2.5f, 5, 5), white);
                }
            GUI.color = Color.white;
        }

        /// MCP tests: force a flock flight now and step it; returns where it went relative to the boss
        public string GuideTest(float seconds)
        {
            calmT = 11; flockCd = 0; var p = ch.root.position; var sb = new System.Text.StringBuilder();
            var boss = BossSpot(Regions.At(p));
            sb.Append(boss.HasValue ? $"boss ({boss.Value.x:F0},{boss.Value.z:F0}) " : "no boss ");
            for (float t = 0; t < seconds; t += 1 / 30f) GuideUpdate(1 / 30f);
            sb.Append($"flying {flying} lead ({flockLead.x:F0},{flockLead.z:F0}) alpha {flockA:F2} ash wind {ash.wind}");
            if (boss.HasValue) { var a = Sim.FlatY(boss.Value - p).normalized; var b2 = Sim.FlatY(flockLead - p).normalized; sb.Append($" | flock vs boss dir cos {Vector3.Dot(a, b2):F2}"); }
            return sb.ToString();
        }
        public string PinTest(Vector2 pin)
        {
            mapPin = pin; var s = PinScreen(Screen.width, Screen.height, out bool on, out var d);
            return $"screen ({s.x:F0},{s.y:F0}) on {on} dir ({d.x:F2},{d.y:F2}) size {Screen.width}x{Screen.height}";
        }
    }
}

// M map capture. Run twice in play mode: once with debug off (fog), once with debug on (F1, everything). Edit DEBUG/NAME.
bool DEBUG = false; string NAME = "map_world_ingame";
var g = UnityEngine.Object.FindFirstObjectByType<Soul.SoulGame>();
typeof(Soul.SoulGame).GetField("showMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, true);
Soul.SoulGame.debugHud = DEBUG;
// a walked strip through Z0 into Z1 so the fogged map shows something
var a = Soul.ZoneWorld.Center(0); var b = Soul.ZoneWorld.Center(1);
for (int k = 0; k <= 30; k++) { var p = UnityEngine.Vector2.Lerp(a, b, k / 30f); Soul.MapFog.Reveal(new UnityEngine.Vector3(p.x, 0, p.y), 46); }
var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Captures/" + NAME + ".png");
UnityEngine.ScreenCapture.CaptureScreenshot(path);
return "queued " + path + " screen " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;

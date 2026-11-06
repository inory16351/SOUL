using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Soul.EditorTools
{
    // Builds Unity prefabs from the JSON exported by web-proto/tools/export-unity.js.
    //   web-proto/.export/*.json  ->  Assets/SOUL/Data/Web/*.json (copied, versioned)
    //                             ->  Assets/SOUL/Generated/<model>.asset (meshes + materials)
    //                             ->  Assets/SOUL/Resources/Prefabs/<Model>.prefab
    //   moves.json                ->  Assets/SOUL/Resources/Data/moves.json (runtime TextAsset)
    // Geometry is converted from three.js space: x -> -x, quaternion (x,-y,-z,w), winding flipped.
    public static class SoulImporter
    {
        const string DataDir = "Assets/SOUL/Data/Web", GenDir = "Assets/SOUL/Generated", PrefabDir = "Assets/SOUL/Resources/Prefabs", RuntimeData = "Assets/SOUL/Resources/Data";
        static readonly string[] Joints = { "root", "pelvis", "spine", "chest", "neck", "head",
            "Lclavicle", "LupperArm", "Lforearm", "Lhand", "Lthigh", "Lshin", "Lfoot", "Ltoe",
            "Rclavicle", "RupperArm", "Rforearm", "Rhand", "Rthigh", "Rshin", "Rfoot", "Rtoe" };

        [MenuItem("SOUL/Import Web Export (copy + build prefabs)")]
        public static void ImportAll()
        {
            CopyFromWeb();
            BuildAll();
        }
        [MenuItem("SOUL/Rebuild Prefabs From Assets/SOUL/Data/Web")]
        public static void BuildAll()
        {
            foreach (var d in new[] { DataDir, GenDir, PrefabDir, RuntimeData }) Directory.CreateDirectory(d);
            Build("character", "Character");
            Build("crawler", "Crawler");
            Build("warden", "Warden");
            Build("map", "Map");
            BuildProps();
            File.Copy(Path.Combine(DataDir, "moves.json"), Path.Combine(RuntimeData, "moves.json"), true);
            AssetDatabase.Refresh();
            Debug.Log("[SOUL] import done");
        }
        public static void CopyFromWeb()
        {
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../web-proto/.export"));
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException("Run the web export first: " + src);
            Directory.CreateDirectory(DataDir);
            foreach (var f in Directory.GetFiles(src, "*.json")) File.Copy(f, Path.Combine(DataDir, Path.GetFileName(f)), true);
        }

        // ---------------------------------------------------------------------------------
        class Ctx
        {
            public SoulMeshStore store; public string name;
            public Dictionary<int, Transform> byId = new Dictionary<int, Transform>();
            public Dictionary<string, Material> mats = new Dictionary<string, Material>();
            public int meshCount;
        }

        static JObject Load(string file) => JObject.Parse(File.ReadAllText(Path.Combine(DataDir, file)));

        static void Build(string file, string prefabName)
        {
            var path = Path.Combine(DataDir, file + ".json");
            if (!File.Exists(path)) { Debug.LogWarning("[SOUL] missing " + path); return; }
            var json = Load(file + ".json");
            var ctx = NewCtx(prefabName);
            var go = BuildNode((JObject)json["tree"], null, ctx);
            go.name = prefabName;
            var model = go.AddComponent<SoulModel>(); model.kind = file;
            model.radius = json.Value<float?>("radius") ?? 0; model.height = json.Value<float?>("height") ?? 0;
            if (json["parts"] != null) Flatten(json["parts"], "", model, ctx);
            if (json["refs"] != null) Flatten(json["refs"], "", model, ctx);
            if (file == "character")
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (Joints.Contains(t.name) && !model.keys.Contains("joint/" + t.name)) { model.keys.Add("joint/" + t.name); model.nodes.Add(t); }
            Save(go, ctx, prefabName);
        }
        static void BuildProps()
        {
            var json = Load("props.json");
            foreach (var kv in json)
            {
                var o = (JObject)kv.Value; var name = char.ToUpper(kv.Key[0]) + kv.Key.Substring(1);
                var ctx = NewCtx(name);
                var go = BuildNode((JObject)o["tree"], null, ctx); go.name = name;
                var model = go.AddComponent<SoulModel>(); model.kind = kv.Key;
                model.radius = o.Value<float>("radius"); model.height = o.Value<float>("height"); model.hp = o.Value<float>("hp");
                Save(go, ctx, name);
            }
        }
        static Ctx NewCtx(string name)
        {
            var storePath = $"{GenDir}/{name}.asset";
            AssetDatabase.DeleteAsset(storePath);
            var store = ScriptableObject.CreateInstance<SoulMeshStore>(); store.source = name;
            AssetDatabase.CreateAsset(store, storePath);
            return new Ctx { store = store, name = name };
        }
        static void Save(GameObject go, Ctx ctx, string prefabName)
        {
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{prefabName}.prefab");
            Object.DestroyImmediate(go);
            Debug.Log($"[SOUL] {prefabName}: {ctx.meshCount} meshes, {ctx.mats.Count} materials");
        }

        static void Flatten(JToken t, string prefix, SoulModel model, Ctx ctx)
        {
            if (t is JObject o)
            {
                if (o["$node"] != null)
                {
                    int id = o.Value<int>("$node");
                    if (ctx.byId.TryGetValue(id, out var tr)) { model.keys.Add(prefix.TrimEnd('/')); model.nodes.Add(tr); }
                    return;
                }
                foreach (var kv in o) Flatten(kv.Value, prefix + kv.Key + "/", model, ctx);
            }
            else if (t is JArray a) { for (int i = 0; i < a.Count; i++) Flatten(a[i], prefix + i + "/", model, ctx); }
            else if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) { model.numKeys.Add(prefix.TrimEnd('/')); model.nums.Add(t.Value<float>()); }
            else if (t.Type == JTokenType.Boolean) { model.numKeys.Add(prefix.TrimEnd('/')); model.nums.Add(t.Value<bool>() ? 1 : 0); }
        }

        static float[] F(JToken t) => t?.Select(x => x.Value<float>()).ToArray();

        static GameObject BuildNode(JObject n, Transform parent, Ctx ctx)
        {
            string type = (string)n["type"], name = (string)n["name"];
            var go = new GameObject(string.IsNullOrEmpty(name) ? type : name);
            var tr = go.transform; if (parent) tr.SetParent(parent, false);
            var p = F(n["p"]); var q = F(n["q"]); var s = F(n["s"]);
            tr.localPosition = Sim.ToUnity(new Vector3(p[0], p[1], p[2]));
            tr.localRotation = Sim.ToUnity(new Quaternion(q[0], q[1], q[2], q[3]));
            tr.localScale = new Vector3(s[0], s[1], s[2]);
            ctx.byId[(int)n["id"]] = tr;
            if (n["geometry"] != null && (type == "Mesh" || type == "SkinnedMesh")) AddMesh(go, n, ctx);
            if (n["light"] != null) AddLight(go, (JObject)n["light"]);
            foreach (JObject c in n["children"]) BuildNode(c, tr, ctx);
            if (n["visible"] != null && !(bool)n["visible"]) go.SetActive(false);
            return go;
        }

        static void AddLight(GameObject go, JObject l)
        {
            var kind = (string)l["kind"];
            if (kind != "PointLight" && kind != "SpotLight") return;      // hemisphere / sun are set up by the scene
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            var c = F(l["color"]); light.color = new Color(c[0], c[1], c[2]).gamma;
            light.intensity = (float)l["intensity"] * SoulLights.PointScale;
            float d = (float)l["distance"]; light.range = d > 0 ? d : 10;
            light.shadows = LightShadows.None;
        }

        static void AddMesh(GameObject go, JObject n, Ctx ctx)
        {
            var g = (JObject)n["geometry"]; var mj = (JObject)n["material"];
            var pos = F(g["position"]); int vc = pos.Length / 3;
            if (g["drawCount"] != null) vc = Mathf.Min(vc, (int)g["drawCount"]);
            if (vc == 0) return;
            var verts = new Vector3[vc]; for (int i = 0; i < vc; i++) verts[i] = new Vector3(-pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]);
            var mesh = new Mesh { name = go.name };
            if (vc > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            var nor = F(g["normal"]);
            if (nor != null && nor.Length >= vc * 3) { var nn = new Vector3[vc]; for (int i = 0; i < vc; i++) nn[i] = new Vector3(-nor[i * 3], nor[i * 3 + 1], nor[i * 3 + 2]); mesh.normals = nn; }
            var col = F(g["color"]);
            if (col != null && col.Length >= vc * 3) { var cc = new Color[vc]; for (int i = 0; i < vc; i++) cc[i] = new Color(col[i * 3], col[i * 3 + 1], col[i * 3 + 2], 1); mesh.colors = cc; }
            var uv = F(g["uv"]);
            if (uv != null && uv.Length >= vc * 2) { var uu = new Vector2[vc]; for (int i = 0; i < vc; i++) uu[i] = new Vector2(uv[i * 2], uv[i * 2 + 1]); mesh.uv = uu; }
            int[] tris;
            if (g["index"] != null) tris = g["index"].Select(x => (int)x).ToArray();
            else { tris = new int[vc - vc % 3]; for (int i = 0; i < tris.Length; i++) tris[i] = i; }
            for (int i = 0; i + 2 < tris.Length; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }   // flip winding
            mesh.triangles = tris;
            if (nor == null) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, ctx.store); ctx.meshCount++;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(mj, n, ctx, col != null);
            mr.shadowCastingMode = (bool?)n["castShadow"] == true ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = (bool?)n["receiveShadow"] ?? true;
            // three's castShadow is off on most character meshes (shadows came from the sun only
            // on flagged meshes); opaque lit parts cast here for a grounded look.
            if (mj != null && ((string)mj["type"]).StartsWith("MeshStandard")) mr.shadowCastingMode = ShadowCastingMode.On;
        }

        static Material MaterialFor(JObject m, JObject node, Ctx ctx, bool hasColors)
        {
            if (m == null) return Mats.Unlit(Color.magenta, Blend.Opaque);
            string type = (string)m["type"], side = (string)m["side"];
            var cull = side == "double" ? CullMode.Off : side == "back" ? CullMode.Front : CullMode.Back;
            var c = F(m["color"]) ?? new float[] { 1, 1, 1 };
            float opacity = m.Value<float?>("opacity") ?? 1;
            bool vcol = (bool)m["vertexColors"] && hasColors;
            int renderOrder = node.Value<int?>("renderOrder") ?? 0;
            string key = m.ToString(Newtonsoft.Json.Formatting.None) + "|" + vcol + "|" + renderOrder;
            if (ctx.mats.TryGetValue(key, out var cached)) return cached;
            Material mat;
            if (type.StartsWith("MeshStandard") || type.StartsWith("MeshPhysical"))
            {
                var e = F(m["emissive"]) ?? new float[] { 0, 0, 0 }; float ei = m.Value<float?>("emissiveIntensity") ?? 0;
                mat = Mats.Lit(new Color(c[0], c[1], c[2], 1), new Color(e[0] * ei, e[1] * ei, e[2] * ei, 1), m.Value<float>("metalness"), 1 - m.Value<float>("roughness"), vcol, cull);
            }
            else
            {
                bool add = (string)m["blending"] == "add", transparent = (bool)m["transparent"];
                var blend = add ? Blend.Add : transparent ? Blend.Alpha : Blend.Opaque;
                mat = Mats.Unlit(new Color(c[0], c[1], c[2], opacity), blend, vcol, cull, (bool)m["depthWrite"], (bool)m["fog"]);
                if (renderOrder < 0) mat.renderQueue = 1000 + renderOrder;        // sky dome first
            }
            mat.name = (string)node["name"] + "-" + type;
            AssetDatabase.AddObjectToAsset(mat, ctx.store);
            ctx.mats[key] = mat;
            return mat;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // Attached by the importer to every imported model prefab. Holds the named part references
    // the gameplay code drives (enemy hips/knees, character sword/bladeTip, ...), flattened to
    // path keys such as "legs/0/hip", plus numeric fields ("legs/0/side") and model metrics.
    public class SoulModel : MonoBehaviour
    {
        public string kind;
        public float radius, height;
        public float hp;
        public List<string> keys = new List<string>();
        public List<Transform> nodes = new List<Transform>();
        public List<string> numKeys = new List<string>();
        public List<float> nums = new List<float>();

        public Transform Part(string key) { int i = keys.IndexOf(key); return i >= 0 ? nodes[i] : null; }
        public float Num(string key, float fallback = 0) { int i = numKeys.IndexOf(key); return i >= 0 ? nums[i] : fallback; }
        public int Count(string prefix)
        {
            int n = 0; while (keys.Exists(k => k.StartsWith(prefix + "/" + n + "/")) || numKeys.Exists(k => k.StartsWith(prefix + "/" + n + "/"))) n++;
            return n;
        }
        public Transform FindDeep(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
    }
}

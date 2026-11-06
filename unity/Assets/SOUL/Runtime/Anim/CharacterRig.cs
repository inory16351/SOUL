using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // The wanderer: instantiates Resources/Prefabs/Character and mirrors its rig as sim-space
    // TNodes (joints + sword + bladeBase/bladeTip + hoodTail). Animation writes the TNodes,
    // Sync() pushes them to the Unity transforms (the mesh parts are children and follow).
    public class CharacterRig
    {
        public static readonly string[] JointNames = { "pelvis", "spine", "chest", "neck", "head",
            "Lclavicle", "LupperArm", "Lforearm", "Lhand", "Lthigh", "Lshin", "Lfoot", "Ltoe",
            "Rclavicle", "RupperArm", "Rforearm", "Rhand", "Rthigh", "Rshin", "Rfoot", "Rtoe" };

        public readonly GameObject go;
        public readonly SoulModel model;
        public readonly TNode root;
        public readonly Dictionary<string, TNode> joints = new Dictionary<string, TNode>();
        public readonly TNode sword, bladeBase, bladeTip, hoodTail;
        public BodyColliders body;
        public float yaw;   // three: root.rotation.y

        public CharacterRig(Transform parent = null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Character");
            go = Object.Instantiate(prefab, parent); go.name = "Wanderer";
            model = go.GetComponent<SoulModel>();
            var wanted = new HashSet<Transform>();
            foreach (var n in JointNames) wanted.Add(model.Part("joint/" + n));
            foreach (var k in new[] { "sword", "bladeBase", "bladeTip", "hoodTail" }) wanted.Add(model.Part(k));
            wanted.Remove(null);
            // include intermediate groups between rig nodes (e.g. hood-tail sits under a head group)
            foreach (var t in new List<Transform>(wanted)) for (var p = t.parent; p && p != go.transform; p = p.parent) wanted.Add(p);
            root = new TNode("root") { bound = go.transform };
            joints["root"] = root;
            Collect(go.transform, root, wanted);
            sword = Node("sword"); bladeBase = Node("bladeBase"); bladeTip = Node("bladeTip"); hoodTail = Node("hoodTail");
            body = new BodyColliders(joints);
            root.UpdateWorld(); body.Update();
        }
        TNode Node(string key) { var t = model.Part(key); return t ? Find(t) : null; }
        readonly Dictionary<Transform, TNode> map = new Dictionary<Transform, TNode>();
        public TNode Find(Transform t) => map.TryGetValue(t, out var n) ? n : null;
        void Collect(Transform t, TNode parentNode, HashSet<Transform> wanted)
        {
            foreach (Transform c in t)
            {
                var pn = parentNode;
                if (wanted.Contains(c))
                {
                    if (c.parent != parentNode.bound) Debug.LogWarning("[SOUL] rig node with non-rig parent: " + c.name);
                    pn = TNode.FromTransform(c, parentNode); map[c] = pn;
                    if (System.Array.IndexOf(JointNames, c.name) >= 0) joints[c.name] = pn;
                }
                Collect(c, pn, wanted);
            }
        }
        public TNode this[string joint] => joints[joint];
        public Vector3 Position { get => root.position; set => root.position = value; }
        public void ApplyYaw() => root.quaternion = Sim.AxisAngle(Vector3.up, yaw);
        public void UpdateWorld() { ApplyYaw(); root.UpdateWorld(); }
        public void Sync() { ApplyYaw(); root.Sync(); }
        public Vector3 Forward => Sim.Forward(yaw);
        /// root.localToWorld for a sim-space local point
        public Vector3 LocalToWorld(Vector3 p) => root.position + Sim.AxisAngle(Vector3.up, yaw) * p;
    }
}

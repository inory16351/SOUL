using System.Collections.Generic;
using UnityEngine;

namespace Soul
{
    // A sim-space transform node (mirrors THREE.Object3D: position, quaternion, scale, parent).
    // The animation code reads/writes these; Sync() pushes local transforms to the bound Unity
    // Transform with the sim -> Unity conversion. World matrices are recomputed on demand.
    public class TNode
    {
        public string name;
        public Vector3 position;
        public Quaternion quaternion = Quaternion.identity;
        public Vector3 scale = Vector3.one;
        public TNode parent;
        public readonly List<TNode> children = new List<TNode>();
        public Transform bound;
        public Matrix4x4 matrixWorld = Matrix4x4.identity;

        public TNode(string name, TNode parent = null) { this.name = name; parent?.Add(this); }
        public void Add(TNode c) { c.parent?.children.Remove(c); c.parent = this; children.Add(c); }

        public static TNode FromTransform(Transform t, TNode parent)
        {
            var n = new TNode(t.name, parent) { bound = t, position = Sim.FromUnity(t.localPosition), quaternion = Sim.FromUnity(t.localRotation), scale = t.localScale };
            return n;
        }
        public Matrix4x4 LocalMatrix => Matrix4x4.TRS(position, quaternion, scale);
        /// THREE.updateMatrixWorld(true) from this node down (parent assumed current)
        public void UpdateWorld()
        {
            matrixWorld = parent != null ? parent.matrixWorld * LocalMatrix : LocalMatrix;
            for (int i = 0; i < children.Count; i++) children[i].UpdateWorld();
        }
        /// updateWorldMatrix(updateParents: true, updateChildren)
        public void UpdateWorldFromRoot(bool children = true)
        {
            var chain = new List<TNode>(); for (var n = this; n != null; n = n.parent) chain.Add(n);
            for (int i = chain.Count - 1; i >= 1; i--) { var n = chain[i]; n.matrixWorld = n.parent != null ? n.parent.matrixWorld * n.LocalMatrix : n.LocalMatrix; }
            if (children) UpdateWorld(); else matrixWorld = parent != null ? parent.matrixWorld * LocalMatrix : LocalMatrix;
        }
        public Vector3 WorldPosition => new Vector3(matrixWorld.m03, matrixWorld.m13, matrixWorld.m23);
        public Quaternion WorldQuaternion { get { var q = quaternion; for (var p = parent; p != null; p = p.parent) q = p.quaternion * q; return q; } }
        public Vector3 LocalToWorld(Vector3 p) => matrixWorld.MultiplyPoint3x4(p);
        public Vector3 WorldToLocal(Vector3 p) => matrixWorld.inverse.MultiplyPoint3x4(p);
        public Vector3 TransformDirection(Vector3 d) => Sim.Norm(matrixWorld.MultiplyVector(d));
        public TNode Find(string n) { if (name == n) return this; foreach (var c in children) { var r = c.Find(n); if (r != null) return r; } return null; }

        public void Sync()
        {
            if (bound != null) { bound.localPosition = Sim.ToUnity(position); bound.localRotation = Sim.ToUnity(quaternion); bound.localScale = scale; }
            for (int i = 0; i < children.Count; i++) children[i].Sync();
        }
    }
}

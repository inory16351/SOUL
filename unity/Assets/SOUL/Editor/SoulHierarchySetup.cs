using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soul.EditorTools
{
    public static class SoulHierarchySetup
    {
        [MenuItem("SOUL/Build Scene Hierarchy")]
        public static void BuildHierarchy()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<SoulGame>(FindObjectsInactive.Include);
            if (!game) throw new InvalidOperationException("The active scene has no SoulGame object.");

            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build SOUL Scene Hierarchy");

            var binding = game.GetComponent<SoulScene>();
            if (!binding) binding = Undo.AddComponent<SoulScene>(game.gameObject);

            var world = Child(game.transform, "World");
            var actors = Child(world, "Actors (Prefab Instances)");
            var breakables = Child(world, "Breakables (Prefab Instances)");
            var pickups = Child(world, "Pickups (Runtime Slots)");
            var openWorld = Child(world, "Open World (Streamed Chunks)");
            var cloth = Child(world, "Character Cloth");
            var fx = Child(world, "FX");
            var persistentFx = Child(fx, "Persistent");
            var transientFx = Child(fx, "Transient (Pooled and One Shot)");
            var gameplay = Child(world, "Gameplay");
            var zone = Child(world, "Zone Objects");

            binding.world = world;
            binding.actors = actors;
            binding.breakables = breakables;
            binding.pickups = pickups;
            binding.cloth = cloth;
            binding.openWorld = openWorld;
            binding.transientFx = transientFx;
            binding.soulWisps = Child(persistentFx, "Soul Wisps (Pooled Slots)");
            binding.zoneObjects = zone;

            var legacy = GameObject.Find("Map") ?? GameObject.Find("Legacy Central Arena (Map Prefab)");
            if (!legacy) throw new InvalidOperationException("The scene is missing the legacy Map prefab instance.");
            Undo.SetTransformParent(legacy.transform, world, "Move legacy arena into World");
            Undo.RecordObject(legacy, "Rename legacy arena"); legacy.name = "Legacy Central Arena (Map Prefab)";
            binding.legacyArena = legacy;
            binding.dustSky = FindRequired(legacy.transform, "dust sky");
            binding.crateredAshSoil = FindRequired(legacy.transform, "cratered ash soil").GetComponent<MeshRenderer>();
            binding.ruinsSkyline = FindRequired(legacy.transform, "ruins skyline").gameObject;
            binding.bladeTrail = RenderSlot(persistentFx, "Blade Trail");
            binding.driftingAsh = RenderSlot(persistentFx, "Drifting Ash");
            binding.fauna = RenderSlot(persistentFx, "Fauna");
            binding.ropeCable = ComponentSlot<LineRenderer>(gameplay, "Rope Cable");
            binding.ropeHook = RenderSlot(gameplay, "Rope Hook");
            binding.fogGate = RenderSlot(zone, "Fog Gate");
            binding.fogGate.SetActive(false);

            foreach (var name in new[] { "front-left-robe-panel", "back-left-robe-panel", "torn-front-right-strip-a", "torn-front-right-strip-b", "torn-back-right-strip-a", "torn-back-right-strip-b", "long-frayed-face-scarf" })
                RenderSlot(cloth, name);

            var pods = Child(zone, "Pods");
            binding.pods = new GameObject[ZoneAreas.Pods.Count];
            binding.podGlows = new GameObject[ZoneAreas.Pods.Count];
            for (int i = 0; i < ZoneAreas.Pods.Count; i++)
            {
                var group = Child(pods, $"{i:00} - {ZoneAreas.Pods[i].name}");
                binding.pods[i] = RenderSlot(group, "Body"); binding.podGlows[i] = RenderSlot(group, "Glow");
            }

            var arenas = Child(zone, "Arenas");
            binding.arenas = new GameObject[ZoneAreas.Arenas.Count];
            binding.arenaRopeLedges = new GameObject[ZoneAreas.Arenas.Count];
            binding.arenaRopeGlows = new GameObject[ZoneAreas.Arenas.Count];
            for (int i = 0; i < ZoneAreas.Arenas.Count; i++)
            {
                var group = Child(arenas, $"{i:00} - {ZoneAreas.Arenas[i].name}");
                binding.arenas[i] = RenderSlot(group, "Wall");
                binding.arenaRopeLedges[i] = RenderSlot(group, "Rope Ledge");
                binding.arenaRopeGlows[i] = RenderSlot(group, "Rope Glow");
            }

            var chasms = Child(zone, "Chasms");
            binding.chasms = new GameObject[ZoneAreas.Chasms.Count];
            binding.chasmGlows = new GameObject[ZoneAreas.Chasms.Count];
            for (int i = 0; i < ZoneAreas.Chasms.Count; i++)
            {
                var group = Child(chasms, $"{i:00} - Rope Chasm");
                binding.chasms[i] = RenderSlot(group, "Body"); binding.chasmGlows[i] = RenderSlot(group, "Glow");
            }

            var shrines = Child(zone, "Shrines");
            binding.shrines = new GameObject[ZoneAreas.Shrines.Count];
            binding.shrineGlows = new GameObject[ZoneAreas.Shrines.Count];
            for (int i = 0; i < ZoneAreas.Shrines.Count; i++)
            {
                var group = Child(shrines, $"{i:00} - {ZoneAreas.Shrines[i].name}");
                binding.shrines[i] = RenderSlot(group, "Body"); binding.shrineGlows[i] = RenderSlot(group, "Glow");
            }

            game.scene = binding;
            EditorUtility.SetDirty(binding); EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Undo.CollapseUndoOperations(undo);
            Selection.activeGameObject = world.gameObject;
            Debug.Log("[SOUL] Scene hierarchy built and serialized. Prefab instances and pooled/repeated slots remain runtime-owned.");
        }

        static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) return child;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false); return go.transform;
        }

        static Transform FindRequired(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            throw new InvalidOperationException($"Missing '{name}' below {parent.name}.");
        }

        static GameObject RenderSlot(Transform parent, string name)
        {
            var go = Child(parent, name).gameObject;
            if (!go.TryGetComponent<MeshFilter>(out _)) Undo.AddComponent<MeshFilter>(go);
            if (!go.TryGetComponent<MeshRenderer>(out _)) Undo.AddComponent<MeshRenderer>(go);
            return go;
        }

        static GameObject ComponentSlot<T>(Transform parent, string name) where T : Component
        {
            var go = Child(parent, name).gameObject;
            if (!go.TryGetComponent<T>(out _)) Undo.AddComponent<T>(go);
            return go;
        }
    }
}

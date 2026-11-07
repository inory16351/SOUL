using UnityEngine;

namespace Soul
{
    // Scene-owned objects that must remain visible and editable in the Hierarchy.
    // Repeated gameplay instances (prefabs, particles, pickups and streamed chunks) are intentionally excluded.
    [DisallowMultipleComponent]
    public sealed class SoulScene : MonoBehaviour
    {
        [Header("Roots")]
        public Transform world;
        public Transform actors;
        public Transform breakables;
        public Transform pickups;
        public Transform cloth;
        public Transform openWorld;
        public Transform transientFx;
        public Transform soulWisps;
        public Transform zoneObjects;

        [Header("Legacy central arena")]
        public GameObject legacyArena;
        public Transform dustSky;
        public MeshRenderer crateredAshSoil;
        public GameObject ruinsSkyline;

        [Header("Persistent runtime renderers")]
        public GameObject bladeTrail;
        public GameObject driftingAsh;
        public GameObject fauna;
        public GameObject ropeCable;
        public GameObject ropeHook;
        public GameObject fogGate;

        [Header("Zone objects")]
        public GameObject[] pods;
        public GameObject[] podGlows;
        public GameObject[] arenas;
        public GameObject[] arenaRopeLedges;
        public GameObject[] arenaRopeGlows;
        public GameObject[] chasms;
        public GameObject[] chasmGlows;
        public GameObject[] shrines;
        public GameObject[] shrineGlows;

        public bool Complete => world && actors && breakables && pickups && cloth && openWorld && transientFx && soulWisps && zoneObjects
            && legacyArena && dustSky && crateredAshSoil && ruinsSkyline
            && bladeTrail && driftingAsh && fauna && ropeCable && ropeHook && fogGate
            && Count(pods) == ZoneAreas.Pods.Count && Count(podGlows) == ZoneAreas.Pods.Count
            && Count(arenas) == ZoneAreas.Arenas.Count && Count(arenaRopeLedges) == ZoneAreas.Arenas.Count && Count(arenaRopeGlows) == ZoneAreas.Arenas.Count
            && Count(chasms) == ZoneAreas.Chasms.Count && Count(chasmGlows) == ZoneAreas.Chasms.Count
            && Count(shrines) == ZoneAreas.Shrines.Count && Count(shrineGlows) == ZoneAreas.Shrines.Count;

        static int Count(GameObject[] objects)
        {
            if (objects == null) return -1;
            foreach (var go in objects) if (!go) return -1;
            return objects.Length;
        }
    }
}

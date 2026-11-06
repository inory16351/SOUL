using UnityEngine;

namespace Soul
{
    // GROUND DETAIL (Codex-owned file). Flat detail meshes that sit on the ground of each 32 m chunk so open
    // ground has character: cracks and broken kerbs (ruins), faded lane paint and manhole-free asphalt seams
    // (city), glass shards (skyscraper grave), rusted bolts and cable runs (gorge / foundry), leaf / twig
    // litter and roots (woods), puddle sheen and reeds (mire), floor markings and drains (lab).
    // Rules: y between 0.005 and 0.15 (no collision is made; nothing may look like an obstacle),
    // no round / ring shapes, deterministic per chunk (seed from x0, z0), skip where
    // ZoneWorld.RidgeHeight(x, z) > 0.2 and inside the original arena (|x|, |z| < Regions.ARENA_HALF),
    // theme by ZoneWorld.At(x, z), lighter on trails (ZoneRoutes.Dist < ZoneRoutes.EDGE: wheel ruts / foot paths).
    // Budget ~1,500 triangles per chunk, ~1 ms. Colours sRGB via MeshBuilder; rendered lit with vertex colour.
    // GroundLook.cs (Claude) already paints per-zone colour patterns into the ground itself.
    public static class GroundDetail
    {
        /// placeholder: none yet (Codex fills this in)
        public static Mesh Decals(float x0, float z0, float size) => null;
    }
}
